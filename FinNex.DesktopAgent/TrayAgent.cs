using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Threading;
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

        private readonly NotifyIcon _notifyIcon;
        private readonly SynchronizationContext _uiContext;
        private HubConnection _hubConnection;
        private volatile bool _disposed;
        private int _unreadCount;
        private Icon _badgeIcon;

        public TrayAgent(AppConfig config, string token, int isciId, string isciAd)
        {
            _config    = config;
            _token     = token;
            _isciId    = isciId;
            _isciAd    = isciAd;
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

            _notifyIcon = new NotifyIcon
            {
                Icon    = SystemIcons.Information,
                Text    = $"FinNex Agent - {_isciAd}",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Çıxış", null, (s, e) => Exit());
            _notifyIcon.ContextMenuStrip = contextMenu;

            _ = ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            var hubUrl = $"{_config.BaseUrl}/notificationHub?access_token={_token}&isciId={_isciId}";

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
                var bashliq = payload.TryGetProperty("bashliq", out var b) ? b.GetString() ?? "Bildiriş" : "Bildiriş";
                var metn    = payload.TryGetProperty("metn",    out var m) ? m.GetString() ?? ""         : "";
                var tarix   = payload.TryGetProperty("tarix",   out var t) ? t.GetString() ?? ""         : "";
                var url     = payload.TryGetProperty("url",     out var u) && u.ValueKind == JsonValueKind.String
                              ? u.GetString() : null;
                ShowPopup(bashliq, string.IsNullOrEmpty(tarix) ? metn : $"{metn}\n{tarix}", url);
            });

            _hubConnection.Reconnected += _ =>
            {
                ShowPopup("FinNex", "Bağlantı bərpa olundu.", null);
                return Task.CompletedTask;
            };

            try
            {
                await _hubConnection.StartAsync();
                ShowPopup("FinNex Sistem qoruyucusu",
                    $"Xoş gəldiniz, {_isciAd}. Bildirişlər aktivdir.", null);
            }
            catch { }
        }

        private void ShowPopup(string bashliq, string metn, string? url)
        {
            _uiContext.Post(_ =>
            {
                if (_disposed) return;

                if (url != null) { _unreadCount++; RefreshIcon(); }

                var popup = new NotificationPopup(bashliq, metn, url, _config.BaseUrl);
                popup.FormClosed += (__, _e) =>
                {
                    if (url != null)
                    {
                        _uiContext.Post(___ =>
                        {
                            if (_unreadCount > 0) _unreadCount--;
                            RefreshIcon();
                        }, null);
                    }
                };
                popup.Show();
                popup.Refresh();
            }, null);
        }

        private void RefreshIcon()
        {
            if (_disposed) return;
            var old = _badgeIcon;
            if (_unreadCount > 0)
            {
                _badgeIcon = BuildBadgeIcon(_unreadCount);
                _notifyIcon.Icon = _badgeIcon;
            }
            else
            {
                _badgeIcon = null;
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
                new StringFormat { Alignment = StringAlignment.Center,
                                   LineAlignment = StringAlignment.Center });
            var icon = Icon.FromHandle(bmp.GetHicon());
            bmp.Dispose();
            return icon;
        }

        private void Exit()
        {
            _disposed = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _badgeIcon?.Dispose();
            if (_hubConnection != null)
                _ = _hubConnection.DisposeAsync();
            Application.Exit();
        }
    }
}
