using Microsoft.Win32;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    /// <summary>
    /// Windows Startup qeydiyyatını (HKCU Run açarı) idarə edir.
    /// Administrator icazəsi tələb etmir — cari istifadəçi üçün işləyir.
    /// </summary>
    internal static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName    = "FinNexDesktopAgent";

        /// <summary>Proqram Windows ilə birlikdə başlayırmı?</summary>
        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }

        /// <summary>Windows Startup-ı aktiv et.</summary>
        public static void Enable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            // Yol dırnaq içindədir ki boşluqlu path-larda da işləsin
            key?.SetValue(AppName, $"\"{ Application.ExecutablePath}\"");
        }

        /// <summary>Windows Startup-ı ləğv et.</summary>
        public static void Disable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }

        /// <summary>Vəziyyəti əks istiqamətə dəyiş.</summary>
        public static void Toggle()
        {
            if (IsEnabled()) Disable(); else Enable();
        }
    }
}
