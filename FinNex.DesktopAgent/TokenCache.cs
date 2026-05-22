using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FinNex.DesktopAgent
{
    /// <summary>
    /// JWT tokeni Windows DPAPI ilə şifrələyib lokal fayla saxlayır.
    /// Token yalnız eyni Windows istifadəçi hesabından oxuna bilər.
    /// </summary>
    internal static class TokenCache
    {
        private static readonly string CacheDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinNex");
        private static readonly string CacheFile = Path.Combine(CacheDir, "token.dat");

        internal sealed record CachedSession(string Token, int IsciId, string IsciAd);

        /// <summary>Tokeni DPAPI ilə şifrələyib diske saxla.</summary>
        public static void Save(string token, int isciId, string isciAd)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                var json   = JsonSerializer.Serialize(new { token, isciId, isciAd });
                var plain  = Encoding.UTF8.GetBytes(json);
                var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CacheFile, cipher);
            }
            catch { /* Saxlaya bilmədik — növbəti açılışda login sorulacaq */ }
        }

        /// <summary>
        /// Diskdən tokeni yüklə. Fayl tapılmadıqda, şifrə açılmadıqda
        /// və ya JWT vaxtı keçdikdə null qaytar.
        /// </summary>
        public static CachedSession? Load()
        {
            try
            {
                if (!File.Exists(CacheFile)) return null;

                var cipher = File.ReadAllBytes(CacheFile);
                var plain  = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                var json   = Encoding.UTF8.GetString(plain);

                using var doc = JsonDocument.Parse(json);
                var root   = doc.RootElement;
                var token  = root.GetProperty("token").GetString()  ?? "";
                var isciId = root.GetProperty("isciId").GetInt32();
                var isciAd = root.GetProperty("isciAd").GetString() ?? "";

                if (!IsTokenValid(token)) return null;

                return new CachedSession(token, isciId, isciAd);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Cache faylını sil (logout / "Yenidən Giriş Et" zamanı).</summary>
        public static void Clear()
        {
            try { File.Delete(CacheFile); } catch { }
        }

        // JWT payload-ı decode edib exp claim-ini yoxla.
        private static bool IsTokenValid(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2) return false;

                // Base64url → standart Base64
                var payload = parts[1]
                    .Replace('-', '+')
                    .Replace('_', '/');

                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "=";  break;
                }

                var bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);

                if (!doc.RootElement.TryGetProperty("exp", out var expProp))
                    return false;

                var expiry = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64()).UtcDateTime;
                // 5 dəqiqə buffer — tokenin sonuna çox yaxın istifadə etmə
                return DateTime.UtcNow < expiry.AddMinutes(-5);
            }
            catch
            {
                return false;
            }
        }
    }
}
