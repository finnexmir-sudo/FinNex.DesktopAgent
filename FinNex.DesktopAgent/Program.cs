using System;
using System.Threading;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal static class Program
    {
        // Proqramın kompüterdə yalnız 1 nüsxədə işləməsini təmin edən unikal Mutex adı
        private static Mutex mutex = new Mutex(true, "{FinNex_Desktop_Notification_Agent_2026_Mutex}");

        [STAThread]
        static void Main()
        {
            // Əgər proqram artıq arxa fonda işləyirsə, ikincisini açmağa icazə vermə
            if (!mutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show("FinNex Desktop Agent artıq arxa fonda işləyir!", "Məlumat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();

            // 1. Konfiqurasiyanı (BaseUrl) yüklə
            var config = AppConfig.Load();

            // 2. Giriş pəncərəsini (LoginForm) başlat
            using (var loginForm = new LoginForm(config))
            {
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    // 3. Giriş uğurludursa, token və işçi adını götür, Tray Agent-i işə sal
                    string token = loginForm.Token;
                    string isciAd = loginForm.IsciAd;

                    Application.Run(new TrayAgent(config, token, isciAd));
                }
            }

            // Proqram tamamilə bağlananda mutex-i sərbəst burax
            GC.KeepAlive(mutex);
        }
    }
}