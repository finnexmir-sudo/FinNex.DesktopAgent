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

        private readonly NotifyIcon       _notifyIcon;
        private readonly Control          _uiMarshal;
        private readonly ToolStripMenuItem _startupMenuItem;
        private HubConnection? _hubConnection;
        private volatile bool _disposed;
        private int   _unreadCount;
        private Icon? _badgeIcon;

        public TrayAgent(AppConfig config, string token, int isciId, string isciAd)
        {
            _config = config;
            _token  = token;
            _isciId = isciId;
            _isciAd = isciAd;

            // NotifyIcon Component-dir və pəncərə handle-i yaratmır, ona görə
            // WinForms SynchronizationContext avtomatik quraşdırılmır. SignalR
            // thread-indən UI thread-ə düzgün keçid (BeginInvoke) üçün UI
            // thread-də handle-i olan görünməz Control yaradırıq.
            _uiMarshal = new Control();
            _ = _uiMarshal.Handle;

            _notifyIcon = new NotifyIcon
            {
                Icon    = SystemIcons.Information,
                Text    = $"FinNex Agent - {_isciAd}",
                Visible = true
            };

            // ── Context menyu ──────────────────────────────────────────────
            _startupMenuItem = new ToolStripMenuItem(
                StartupLabel(), null, OnStartupToggle);

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(_startupMenuItem);
            contextMenu.Items.Add("Yenidən Giriş Et", null, OnReLogin);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Çıxış", null, (_, _) => Exit());
            _notifyIcon.ContextMenuStrip = contextMenu;

            _ = Task.Run(ConnectAsync);
        }

        // ── Startup toggle ─────────────────────────────────────────────────

        private static string StartupLabel() =>
            (StartupHelper.IsEnabled() ? "✓  " : "    ") + "Windows ilə başla";

        private void OnStartupToggle(object? sender, EventArgs e)
        {
            StartupHelper.Toggle();
            _startupMenuItem.Text = StartupLabel();
        }

        // ── Yenidən Giriş Et ───────────────────────────────────────────────

        private void OnReLogin(object? sender, EventArgs e)
        {
            // Token cache-i sil ki yeni açılışda login sorulsun
            TokenCache.Clear();
            // Proqramı yenidən başlat
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath)
                {
                    UseShellExecute = true
                });
            }
            catch { }
            Exit();
        }

        // ── SignalR bağlantısı ─────────────────────────────────────────────

        private async Task ConnectAsync()
        {
            var hubUrl = $"{_config.BaseUrl}/notificationHub" +
                         $"?access_token={_token}&isciId={_isciId}";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                    };
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

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

            // Bağlantı yenidən qurulmağa çalışır
            _hubConnection.Reconnecting += _ =>
            {
                ShowBalloon("Serverlə bağlantı kəsildi, yenidən qoşulunur...",
                    ToolTipIcon.Warning);
                return Task.CompletedTask;
            };

            // Bağlantı uğurla bərpa edildi
            _hubConnection.Reconnected += _ =>
            {
                ShowBalloon("Serverə yenidən qoşuldu.", ToolTipIcon.Info);
                return Task.CompletedTask;
            };

            // Bütün yeniden-qoşulma cəhdləri uğursuz oldu
            _hubConnection.Closed += _ =>
            {
                if (!_disposed)
                {
                    // Token vaxtı keçmiş ola bilər — növbəti açılışda login sorulsun
                    TokenCache.Clear();
                    ShowBalloon(
                        "Serverlə bağlantı kəsildi və bərpa edilə bilmədi.\n" +
                        "Sağ klik → \"Yenidən Giriş Et\" basın.",
                        ToolTipIcon.Error);
                }
                return Task.CompletedTask;
            };

            try
            {
                // Task.Run içində çağrılır (SynchronizationContext = null).
                // SignalR daxili loopları üçün UI context-i tutmur — UI donmaz.
                await _hubConnection.StartAsync();
            }
            catch { }
        }

        // ── Balloon bildirişi ─────────────────────────────────────────────

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

        // ── Popup bildirişi ───────────────────────────────────────────────

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

                    if (url != null) { _unreadCount++; RefreshIcon(); }

                    var popup = new NotificationPopup(
                        bashliq, metn, url, _config.BaseUrl, nov, autoCloseMs);
                    popup.FormClosed += (_, _) =>
                    {
                        if (url != null && _unreadCount > 0)
                        {
                            _unreadCount--;
                            RefreshIcon();
                        }
                    };
                    popup.Show();
                }));
            }
            catch { }
        }

        // ── Tray icon badge ───────────────────────────────────────────────

        private void RefreshIcon()
        {
            if (_disposed) return;
            var old = _badgeIcon;
            if (_unreadCount > 0)
            {
                _badgeIcon       = BuildBadgeIcon(_unreadCount);
                _notifyIcon.Icon = _badgeIcon;
            }
            else
            {
                _badgeIcon       = null;
                _notifyIcon.Icon = SystemIcons.Information;
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

        // ── Çıxış ────────────────────────────────────────────────────────

        private void Exit()
        {
            _disposed = true;
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
