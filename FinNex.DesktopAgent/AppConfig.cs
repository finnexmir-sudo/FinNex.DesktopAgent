using System;
using System.IO;
using System.Text.Json;

namespace FinNex.DesktopAgent
{
    public class AppConfig
    {
        public string BaseUrl { get; set; } = "https://localhost:7001";

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
                if (doc.RootElement.TryGetProperty("FinNex", out var finnexSec) &&
                    finnexSec.TryGetProperty("BaseUrl", out var urlProp))
                {
                    return new AppConfig { BaseUrl = urlProp.GetString() ?? "https://localhost:7001" };
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
