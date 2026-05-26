using System;
using System.IO;
using System.Text.Json;

namespace FinNex.DesktopAgent
{
    public class AppConfig
    {
        public string BaseUrl { get; set; } = "https://localhost:7001";

        /// <summary>
        /// Self-signed SSL sertifikatlara icazə verilsin?
        /// Yalnız daxili test serverlər üçün true edilsin.
        /// İşçilərin kompüterlərindəki production deploymentdə false olmalıdır.
        /// </summary>
        public bool AllowSelfSignedCert { get; set; } = false;

        /// <summary>
        /// BaseUrl-in sonundaki "/" işarəsini silib token endpointini qaytarır.
        /// Məs: "http://192.168.0.94:4370/" → "http://192.168.0.94:4370/api/desktop/token"
        /// </summary>
        public string FullTokenUrl => BaseUrl.TrimEnd('/') + "/api/desktop/token";

        /// <summary>
        /// BaseUrl-in sonundaki "/" işarəsini silib SignalR hub URL-ini qaytarır.
        /// </summary>
        public string FullHubUrl => BaseUrl.TrimEnd('/') + "/notificationHub";

        public static AppConfig Load()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(path)) return new AppConfig();

                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("FinNex", out var finnexSec))
                {
                    var cfg = new AppConfig();
                    if (finnexSec.TryGetProperty("BaseUrl", out var urlProp))
                        cfg.BaseUrl = urlProp.GetString() ?? "https://localhost:7001";
                    if (finnexSec.TryGetProperty("AllowSelfSignedCert", out var sslProp) &&
                        sslProp.ValueKind == JsonValueKind.True)
                        cfg.AllowSelfSignedCert = true;
                    return cfg;
                }
            }
            catch
            {
                // Hər hansı xəta olarsa default dəyərlə davam etsin
            }
            return new AppConfig();
        }
    }
}
