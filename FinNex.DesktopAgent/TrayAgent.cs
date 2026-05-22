using System;
using System.Drawing;
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
        // SignalR callback-ləri thread pool-da işləyir;
        // ShowBalloonTip düzgün göstərilmək üçün UI message loop thread-inə ehtiyac duyur.
        private readonly SynchronizationContext _uiContext;
        private HubConnection _hubConnection;

        public TrayAgent(AppConfig config, string token, int isciId, string isciAd)
        {
            _config = config;
            _token = token;
            _isciId = isciId;
            _isciAd = isciAd;
            // Constructor [STAThread] UI thread-ində işləyir — WindowsFormsSynchronizationContext-i yadda saxla
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Text = $"FinNex Agent - {_isciAd}",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Çıxış", null, (s, e) => Exit());
            _notifyIcon.ContextMenuStrip = contextMenu;

            _ = ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            // access_token: JWT Bearer auth üçün
            // isciId: server tərəf OnConnectedAsync-də qrup adını YALNIZ query-dən oxuyur
            var hubUrl = $"{_config.BaseUrl}/notificationHub?access_token={_token}&isciId={_isciId}";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    // İnkişaf mühitlərindəki self-signed sertifikatları qəbul et
                    options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                    };
                })
                .WithAutomaticReconnect(new[] {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            // Server { bashliq, metn, tarix } object göndərir — JsonElement ilə al
            _hubConnection.On<JsonElement>("ReceiveDesktopNotification", payload =>
            {
                var bashliq = payload.TryGetProperty("bashliq", out var b) ? b.GetString() ?? "Bildiriş" : "Bildiriş";
                var metn    = payload.TryGetProperty("metn",    out var m) ? m.GetString() ?? ""         : "";
                var tarix   = payload.TryGetProperty("tarix",   out var t) ? t.GetString() ?? ""         : "";
                ShowBalloon(bashliq, string.IsNullOrEmpty(tarix) ? metn : $"{metn}\n{tarix}");
            });

            _hubConnection.Reconnected += _ =>
            {
                ShowBalloon("FinNex", "Bağlantı bərpa olundu.");
                return Task.CompletedTask;
            };

            try
            {
                await _hubConnection.StartAsync();
                ShowBalloon("FinNex Sistem qoruyucusu", $"Xoş gəldiniz, {_isciAd}. Bildirişlər aktivdir.");
            }
            catch
            {
                // WithAutomaticReconnect arxa fonda yenidən qoşulmaqı davam etdirəcək
            }
        }

        private void ShowBalloon(string bashliq, string metn)
        {
            _uiContext.Post(_ =>
            {
                if (_notifyIcon.IsDisposed) return;
                _notifyIcon.ShowBalloonTip(8000, bashliq, metn, ToolTipIcon.Info);
            }, null);
        }

        private void Exit()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            if (_hubConnection != null)
                _ = _hubConnection.DisposeAsync();
            Application.Exit();
        }
    }
}
