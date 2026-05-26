using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinNex.DesktopAgent
{
    public class TrayAgent : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly string _token;
        private readonly int _isciId;
        private readonly string _isciAd;

        private readonly NotifyIcon        _notifyIcon;
        private readonly Control           _uiMarshal;
        private readonly ToolStripMenuItem _startupMenuItem;
        private HubConnection? _hubConnection;
        private volatile bool _disposed;
        private volatile bool _connected;
        private volatile bool _reconnectLoopActive;
        private int   _unreadCount;
        private Icon? _badgeIcon;

        // Kəsilmə başlayan vaxt — uzun kəsilmə üçün bildiriş məntiqi
        private DateTime? _disconnectTime;

        public TrayAgent(AppConfig config, string token, int isciId, string isciAd)
        {
            _config = config;
            _token  = token;
            _isciId = isciId;
            _isciAd = isciAd;

            _uiMarshal = new Control();
            _ = _uiMarshal.Handle;

            _notifyIcon = new NotifyIcon
            {
                Icon    = SystemIcons.Information,
                Text    = $"FinNex Agent - {_isciAd}",
                Visible = true
            };

            _startupMenuItem = new ToolStripMenuItem(
                StartupLabel(), null, OnStartupToggle);

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(_startupMenuItem);
            contextMenu.Items.Add("Yenidən Giriş Et", null, OnReLogin);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Çıxış", null, (_, _) => Exit());
            _notifyIcon.ContextMenuStrip = contextMenu;

            NotificationPopup.UnreadChanged = OnUnreadChanged;

            _ = Task.Run(ConnectAsync);
        }

        private static string StartupLabel() =>
            (StartupHelper.IsEnabled() ? "✓  " : "    ") + "Windows ilə başla";

        private void OnStartupToggle(object? sender, EventArgs e)
        {
            StartupHelper.Toggle();
            _startupMenuItem.Text = StartupLabel();
        }

        private void OnReLogin(object? sender, EventArgs e)
        {
            TokenCache.Clear();
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath)
                {
                    UseShellExecute = false
                });
            }
            catch { }
            Exit();
        }

        private async Task ConnectAsync()
        {
            var hubUrl = $"{_config.FullHubUrl}?access_token={_token}&isciId={_isciId}";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    // SSL bypass yalnız konfiqurasiyada AllowSelfSignedCert=true olduqda aktivdir.
                    // İşçilərin kompüterlərindəki appsettings.json-da false olmalıdır.
                    if (_config.AllowSelfSignedCert)
                        options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                        };
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(60)
                })
                .Build();

            // VM yük altında serverin cavab vaxtı uzana bilir.
            _hubConnection.ServerTimeout     = TimeSpan.FromSeconds(120);
            _hubConnection.KeepAliveInterval = TimeSpan.FromSeconds(30);

            _hubConnection.On<JsonElement>("ReceiveDesktopNotification", payload =>
            {
                var bashliq = payload.TryGetProperty("bashliq", out var b)
                              ? b.GetString() ?? "Bildiris" : "Bildiris";
                var metn    = payload.TryGetProperty("metn",    out var m)
                              ? m.GetString() ?? ""         : "";
                var tarix   = payload.TryGetProperty("tarix",   out var t)
                              ? t.GetString() ?? ""         : "";
                var url     = payload.TryGetProperty("url",     out var u)
                              && u.ValueKind == JsonValueKind.String
                              ? u.GetString() : null;
                var nov     = payload.TryGetProperty("nov",     out var n)
                              && n.ValueKind == JsonValueKind.Number
                              ? n.GetInt32() : 0;

                ShowPopup(
                    bashliq,
                    string.IsNullOrEmpty(tarix) ? metn : $"{metn}\n{tarix}",
                    url, nov);
            });

            // Qısa kəsilmə (2-60 saniyə): SƏSSIZ — yalnız tray ikonu dəyişir.
            // Uzun kəsilmə (Closed event): balloon göstərilir.
            _hubConnection.Reconnecting += _ =>
            {
                _connected      = false;
                _disconnectTime = DateTime.UtcNow;
                RefreshIconSafe();
                // Balloon YOX — qısa reconnect-lər istifadəçini narahat etməsin
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += _ =>
            {
                _connected = true;
                var wasLong = _disconnectTime.HasValue &&
                              (DateTime.UtcNow - _disconnectTime.Value).TotalSeconds > 30;
                _disconnectTime = null;
                RefreshIconSafe();
                // Yalnız 30 saniyədən çox kəsilmişdisə bildiriş göstər
                if (wasLong)
                    ShowBalloon("Serverə yenidən qoşuldu.", ToolTipIcon.Info);
                return Task.CompletedTask;
            };

            _hubConnection.Closed += error =>
            {
                if (!_disposed)
                {
                    _connected      = false;
                    _disconnectTime = DateTime.UtcNow;
                    RefreshIconSafe();
                    // Closed = bütün cəhdlər tükəndi — bu həqiqi uzun kəsilmədir
                    ShowBalloon("Bağlantı kəsildi — yenidən qoşulmağa çalışılır...",
                        ToolTipIcon.Warning);
                    _ = ReconnectLoopAsync();
                }
                return Task.CompletedTask;
            };

            try
            {
                await _hubConnection.StartAsync();
                _connected = true;
                RefreshIconSafe();
            }
            catch
            {
                _connected = false;
                RefreshIconSafe();
                _ = ReconnectLoopAsync();
            }
        }

        private async Task ReconnectLoopAsync()
        {
            if (_reconnectLoopActive) return;
            _reconnectLoopActive = true;
            try
            {
                while (!_disposed && !_connected)
                {
                    await Task.Delay(10000);
                    if (_disposed || _connected) break;
                    try
                    {
                        await _hubConnection!.StartAsync();
                        _connected      = true;
                        _disconnectTime = null;
                        RefreshIconSafe();
                        ShowBalloon("Serverə yenidən qoşuldu.", ToolTipIcon.Info);
                    }
                    catch
                    {
                        // Server hələ əlçatan deyil — 10 saniyə sonra yenidən
                    }
                }
            }
            finally { _reconnectLoopActive = false; }
        }

        private void ShowBalloon(string metn, ToolTipIcon icon)
        {
            if (_disposed || _uiMarshal.IsDisposed || !_uiMarshal.IsHandleCreated)
                return;
            try
            {
                _uiMarshal.BeginInvoke(new Action(() =>
                {
                    if (!_disposed)
                        _notifyIcon.ShowBalloonTip(5000, "FinNex Agent", metn, icon);
                }));
            }
            catch { }
        }

        private void ShowPopup(string bashliq, string metn, string? url,
                               int nov, int autoCloseMs = 0)
        {
            if (_disposed || _uiMarshal.IsDisposed || !_uiMarshal.IsHandleCreated)
                return;

            try
            {
                _uiMarshal.BeginInvoke(new Action(() =>
                {
                    if (_disposed) return;
                    NotificationPopup.ShowOrUpdate(
                        bashliq, metn, url, _config.BaseUrl, nov, autoCloseMs);
                }));
            }
            catch { }
        }

        private void OnUnreadChanged(int total)
        {
            _unreadCount = total;
            RefreshIconSafe();
        }

        private void RefreshIconSafe()
        {
            if (_disposed || _uiMarshal.IsDisposed || !_uiMarshal.IsHandleCreated)
                return;
            try { _uiMarshal.BeginInvoke(new Action(RefreshIcon)); }
            catch { }
        }

        private void RefreshIcon()
        {
            if (_disposed) return;
            var old = _badgeIcon;

            if (!_connected)
            {
                _badgeIcon       = null;
                _notifyIcon.Icon = SystemIcons.Warning;
                _notifyIcon.Text = "FinNex Agent — Bağlantı yoxdur";
            }
            else if (_unreadCount > 0)
            {
                _badgeIcon       = BuildBadgeIcon(_unreadCount);
                _notifyIcon.Icon = _badgeIcon;
                _notifyIcon.Text = $"FinNex Agent - {_isciAd}";
            }
            else
            {
                _badgeIcon       = null;
                _notifyIcon.Icon = SystemIcons.Information;
                _notifyIcon.Text = $"FinNex Agent - {_isciAd}";
            }
            old?.Dispose();
        }

        private static Icon BuildBadgeIcon(int count)
        {
            var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawIcon(SystemIcons.Information, new Rectangle(0, 0, 32, 32));
            var text  = count > 99 ? "99+" : count.ToString();
            var badge = new RectangleF(17, 0, 14, 14);
            g.FillEllipse(Brushes.Red, badge);
            using var font = new Font("Arial", count > 9 ? 5.5f : 7f, FontStyle.Bold);
            g.DrawString(text, font, Brushes.White, badge,
                new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });
            var icon = Icon.FromHandle(bmp.GetHicon());
            bmp.Dispose();
            return icon;
        }

        private void Exit()
        {
            _disposed = true;
            NotificationPopup.UnreadChanged = null;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _badgeIcon?.Dispose();
            _uiMarshal.Dispose();
            if (_hubConnection != null)
                _ = _hubConnection.DisposeAsync();
            Application.Exit();
        }
    }
}
