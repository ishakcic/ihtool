using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace SistemYonetici
{
    public static class Updater
    {
        // Kendi deponla değiştir: "kullaniciadi/SistemYonetici"
        public const string Repo = "KULLANICI_ADINI_BURAYA_YAZ/SistemYonetici";

        public static string CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

        public static async Task<(bool hasUpdate, string latestTag, string assetUrl, string error)> CheckAsync()
        {
            if (Repo.Contains("KULLANICI_ADINI_BURAYA_YAZ"))
                return (false, null, null, "Güncelleme kontrolü için Updater.cs içindeki Repo alanını kendi GitHub kullanıcı adın/depo adınla değiştirmen gerekiyor.");
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SistemYonetici-App");
                var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
                using var doc = JsonDocument.Parse(json);
                string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
                string assetUrl = null;
                if (doc.RootElement.TryGetProperty("assets", out var assets))
                    foreach (var a in assets.EnumerateArray())
                        if ((a.GetProperty("name").GetString() ?? "").EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        { assetUrl = a.GetProperty("browser_download_url").GetString(); break; }
                string tagVer = tag.TrimStart('v', 'V');
                bool newer = Version.TryParse(tagVer, out var tv) && Version.TryParse(CurrentVersion, out var cv) && tv > cv;
                return (newer && assetUrl != null, tag, assetUrl, null);
            }
            catch (Exception ex) { return (false, null, null, ex.Message); }
        }

        public static async Task<string> DownloadAndApplyAsync(string assetUrl)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string dir = Path.GetDirectoryName(exePath);
                string newPath = Path.Combine(dir, "SistemYonetici.new.exe");
                using (var http = new HttpClient())
                using (var s = await http.GetStreamAsync(assetUrl))
                using (var f = File.Create(newPath))
                    await s.CopyToAsync(f);

                string bat = Path.Combine(Path.GetTempPath(), "su_update.bat");
                File.WriteAllText(bat,
                    "@echo off\r\n" +
                    "timeout /t 2 /nobreak >nul\r\n" +
                    "move /y \"" + newPath + "\" \"" + exePath + "\" >nul\r\n" +
                    "start \"\" \"" + exePath + "\"\r\n" +
                    "del \"%~f0\"\r\n");
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"") { WindowStyle = ProcessWindowStyle.Hidden });
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
