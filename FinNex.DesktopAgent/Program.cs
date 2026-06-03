using System;
using System.Threading;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal static class Program
    {
        private static readonly Mutex _mutex =
            new(true, "{FinNex_Desktop_Notification_Agent_2026_Mutex}");

        [STAThread]
        static void Main()
        {
            if (!_mutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show(
                    "FinNex Desktop Agent artıq arxa fonda işləyir!",
                    "Məlumat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();
            var config = AppConfig.Load();

            // Önce DPAPI ilə şifrəli cache-dən token yüklə.
            // Token tapılıbsa və vaxtı keçməyibsə login formu göstərilmir.
            var cached = TokenCache.Load();
            if (cached != null)
            {
                StartupHelper.Enable();
                Application.Run(
                    new TrayAgent(config, cached.Token, cached.IsciId, cached.IsciAd));
            }
            else
            {
                // Cache yoxdur və ya token vaxtı keçib → Login formu göstər
                using var loginForm = new LoginForm(config);
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    TokenCache.Save(loginForm.Token, loginForm.IsciId, loginForm.IsciAd);
                    StartupHelper.Enable();
                    Application.Run(
                        new TrayAgent(config, loginForm.Token, loginForm.IsciId, loginForm.IsciAd));
                }
            }

            GC.KeepAlive(_mutex);
        }
    }
}
