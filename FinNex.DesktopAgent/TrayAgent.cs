using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinNex.DesktopAgent
{
    public class TrayAgent : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private HubConnection _hubConnection;
        private readonly AppConfig _config;
        private readonly string _token;
        private readonly string _isciAd;

        public TrayAgent(AppConfig config, string token, string isciAd)
        {
            _config = config;
            _token = token;
            _isciAd = isciAd;

            InitializeTray();
            InitializeSignalR();
        }

        private void InitializeTray()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information, // Standart göy məlumat ikonu
                Text = $"FinNex Agent - {_isciAd}",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Çıxış", null, (s, e) => Exit());
            _notifyIcon.ContextMenuStrip = contextMenu;

            // İlk qoşulma uğurlu olanda istifadəçiyə xoş gəldin bildirişi ataq
            _notifyIcon.ShowBalloonTip(3000, "FinNex Sistem qoruyucusu", $"Xoş gəldiniz, {_isciAd}. Bildirişlər aktivdir.", ToolTipIcon.Info);
        }

        private async void InitializeSignalR()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_config.BaseUrl}/notificationHub", options =>
                {
                    // JWT tokeni SignalR qoşulma sorğusuna query string olaraq əlavə edirik
                    options.AccessTokenProvider = () => Task.FromResult(_token);
                })
                .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            // SERVERDƏN GƏLƏN CANLI BİLDİRİŞ EVENTİ BURADA TUTULUR!
            _hubConnection.On<string, string>("ReceiveDesktopNotification", (bashliq, metn) =>
            {
                _notifyIcon.ShowBalloonTip(8000, bashliq, metn, ToolTipIcon.Info);
            });

            try
            {
                await _hubConnection.StartAsync();
            }
            catch
            {
                // Arxa fonda WithAutomaticReconnect özü avtomatik yenidən qoşulmağa cəhd edəcək
            }
        }

        private void Exit()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            if (_hubConnection != null)
            {
                _ = _hubConnection.DisposeAsync();
            }
            Application.Exit();
        }
    }
}