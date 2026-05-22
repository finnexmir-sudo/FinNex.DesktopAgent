using System;
using System.IO;
using System.Text.Json;

namespace FinNex.DesktopAgent
{
    public class AppConfig
    {
        public string BaseUrl { get; set; } = "https://localhost:7001";

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
                    return new AppConfig { BaseUrl = urlProp.GetString() };
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