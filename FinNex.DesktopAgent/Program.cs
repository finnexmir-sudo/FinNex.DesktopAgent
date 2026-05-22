using System;
using System.Threading;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal static class Program
    {
        private static Mutex mutex = new Mutex(true, "{FinNex_Desktop_Notification_Agent_2026_Mutex}");

        [STAThread]
        static void Main()
        {
            if (!mutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show("FinNex Desktop Agent artıq arxa fonda işləyir!", "Məlumat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();

            var config = AppConfig.Load();

            using (var loginForm = new LoginForm(config))
            {
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    Application.Run(new TrayAgent(config, loginForm.Token, loginForm.IsciId, loginForm.IsciAd));
                }
            }

            GC.KeepAlive(mutex);
        }
    }
}
