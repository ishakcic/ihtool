using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SistemYonetici
{
    public static class Data
    {
        public static string FilePath = @"C:\";
        static readonly HashSet<string> Frozen = new();

        [DllImport("ntdll.dll")] static extern int NtSuspendProcess(IntPtr h);
        [DllImport("ntdll.dll")] static extern int NtResumeProcess(IntPtr h);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int a, bool i, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

        static string G(Dictionary<string, string> m, string k) => m.TryGetValue(k, out var v) ? v : "";

        static List<Dictionary<string, string>> Json(string j)
        {
            var res = new List<Dictionary<string, string>>();
            j = (j ?? "").Trim();
            if (j.Length == 0) return res;
            using var d = JsonDocument.Parse(j);
            var els = d.RootElement.ValueKind == JsonValueKind.Array
                ? d.RootElement.EnumerateArray().ToList() : new List<JsonElement> { d.RootElement };
            foreach (var e in els)
            {
                var m = new Dictionary<string, string>();
                foreach (var p in e.EnumerateObject())
                    m[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? "" : p.Value.ToString();
                res.Add(m);
            }
            return res;
        }

        // ---------- Açıklama sözlükleri (risk: g güvenli, w dikkat, r riskli) ----------
        static readonly Dictionary<string, (string risk, string desc)> SvcInfo = new()
        {
            ["DiagTrack"] = ("g", "Telemetri: kullanım verisini Microsoft’a gönderir. Kapatmak güvenlidir."),
            ["dmwappushservice"] = ("g", "Telemetri için mesaj aktarır. Kapatılabilir."),
            ["WerSvc"] = ("g", "Çöken programların hata raporunu gönderir."),
            ["RemoteRegistry"] = ("g", "Ağdan kayıt defterine erişime izin verir. Kapalı olması daha güvenli."),
            ["Fax"] = ("g", "Faks hizmeti. Çoğu kişi kullanmaz."),
            ["XblAuthManager"] = ("g", "Xbox Live girişi. Xbox oyunu oynamıyorsan kapatılabilir."),
            ["XblGameSave"] = ("g", "Xbox oyun kayıtlarını buluta eşitler."),
            ["XboxNetApiSvc"] = ("g", "Xbox ağ hizmeti."),
            ["MapsBroker"] = ("g", "İndirilen çevrimdışı haritaları yönetir."),
            ["lfsvc"] = ("g", "Konum hizmeti. Kapalıyken uygulamalar konumunu bulamaz."),
            ["RetailDemo"] = ("g", "Mağaza demo modu. Evde gereksizdir."),
            ["WMPNetworkSvc"] = ("g", "Medya Oynatıcı ağ paylaşımı."),
            ["SysMain"] = ("w", "Sık kullanılan uygulamaları önceden belleğe alır (Superfetch). SSD’de gereksiz sayılır."),
            ["WSearch"] = ("w", "Dosya aramasını hızlandırır. Kapanırsa arama yavaşlar."),
            ["Spooler"] = ("w", "Yazıcı kuyruğu. Yazıcı kullanıyorsan gerekir."),
            ["bthserv"] = ("w", "Bluetooth desteği. Kapanırsa kulaklık, fare vb. bağlanmaz."),
            ["TermService"] = ("w", "Uzak Masaüstü. Başka bilgisayardan bağlanmaya izin verir."),
            ["BITS"] = ("w", "Güncellemeleri arka planda indirir."),
            ["WlanSvc"] = ("w", "Kablosuz ağ (Wi-Fi). Kapanırsa Wi-Fi çalışmaz."),
            ["Audiosrv"] = ("w", "Windows ses hizmeti. Kapanırsa ses gider."),
            ["LanmanWorkstation"] = ("w", "Ağ paylaşımlarına bağlanmayı sağlar."),
            ["Themes"] = ("w", "Görsel temalar."),
            ["wuauserv"] = ("r", "Windows Update. Kapatırsan güvenlik yamaları gelmez."),
            ["WinDefend"] = ("r", "Microsoft Defender virüs koruması."),
            ["Dhcp"] = ("r", "İnternet adresini otomatik alır. Kapanırsa internet gidebilir."),
            ["Dnscache"] = ("r", "Alan adı çözümleme. Kapanırsa siteler açılmayabilir."),
            ["RpcSs"] = ("r", "Windows çekirdek hizmeti. Kapanırsa sistem çöker."),
            ["EventLog"] = ("r", "Windows olay günlüğü."),
            ["PlugPlay"] = ("r", "Takılan donanımları tanır."),
            ["Winmgmt"] = ("r", "Windows yönetim altyapısı (WMI). Birçok araç buna bağlıdır."),
            ["BFE"] = ("r", "Güvenlik duvarı altyapısı."),
            ["mpssvc"] = ("r", "Windows güvenlik duvarı."),
            ["CryptSvc"] = ("r", "Şifreleme ve sertifika hizmeti. Güncelleme buna bağlıdır."),
            ["TrustedInstaller"] = ("r", "Windows modül yükleyici. Güncelleme ve onarım buna bağlıdır."),
        };

        static readonly Dictionary<string, (string risk, string desc)> AppxInfo = new()
        {
            ["Microsoft.XboxGamingOverlay"] = ("g", "Xbox Game Bar: oyun sırasında kayıt ve performans paneli."),
            ["Microsoft.BingNews"] = ("g", "Haberler uygulaması."),
            ["Microsoft.BingWeather"] = ("g", "Hava durumu uygulaması."),
            ["Microsoft.GetHelp"] = ("g", "Yardım Al uygulaması."),
            ["Microsoft.Getstarted"] = ("g", "İpuçları uygulaması."),
            ["Microsoft.MicrosoftOfficeHub"] = ("g", "Office tanıtım uygulaması."),
            ["Microsoft.MicrosoftSolitaireCollection"] = ("g", "Solitaire oyunları."),
            ["Microsoft.People"] = ("g", "Kişiler uygulaması."),
            ["Microsoft.WindowsFeedbackHub"] = ("g", "Geri Bildirim Merkezi."),
            ["Microsoft.YourPhone"] = ("g", "Telefon Bağlantısı: telefonunu bilgisayara bağlar."),
            ["Microsoft.ZuneMusic"] = ("g", "Groove Müzik / Medya Oynatıcı."),
            ["Microsoft.ZuneVideo"] = ("g", "Filmler ve TV uygulaması."),
            ["Microsoft.SkypeApp"] = ("g", "Skype (eski)."),
            ["Microsoft.Windows.Cortana"] = ("g", "Cortana sesli asistan (artık desteklenmiyor)."),
            ["Microsoft.549981C3F5F10"] = ("g", "Cortana."),
            ["Microsoft.MixedReality.Portal"] = ("g", "Karma Gerçeklik Portalı (VR)."),
            ["Microsoft.WindowsCalculator"] = ("g", "Hesap Makinesi."),
            ["Microsoft.Windows.Photos"] = ("w", "Fotoğraflar: varsayılan görsel görüntüleyici."),
            ["Microsoft.WindowsStore"] = ("w", "Microsoft Store. Kaldırırsan mağaza uygulamaları güncellenemez."),
            ["Microsoft.MicrosoftEdge.Stable"] = ("w", "Microsoft Edge tarayıcısı."),
            ["Microsoft.SecHealthUI"] = ("r", "Windows Güvenlik arayüzü."),
            ["Microsoft.Windows.ShellExperienceHost"] = ("r", "Görev çubuğu ve bildirimler. Kaldırırsan arayüz bozulur."),
            ["Microsoft.Windows.StartMenuExperienceHost"] = ("r", "Başlat menüsü."),
        };

        // ---------- Kullanıcı uygulamaları ----------
        static List<Item> UserApps()
        {
            var list = new List<Item>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var srcs = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            };
            foreach (var (root, sub) in srcs)
            {
                using var k = root.OpenSubKey(sub);
                if (k == null) continue;
                foreach (var n in k.GetSubKeyNames())
                {
                    using var s = k.OpenSubKey(n);
                    var name = s?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) continue;
                    if (s.GetValue("SystemComponent") is int sc && sc == 1) continue;
                    if (s.GetValue("ParentKeyName") != null) continue;
                    string pub = s.GetValue("Publisher") as string ?? "Bilinmeyen yayıncı";
                    string ver = s.GetValue("DisplayVersion") as string ?? "";
                    string un = (s.GetValue("QuietUninstallString") as string) ?? (s.GetValue("UninstallString") as string) ?? "";
                    string loc = (s.GetValue("InstallLocation") as string ?? "").Trim('"').Trim();
                    string risk = "g";
                    string note = "";
                    if (Regex.IsMatch(name, @"Redistributable|Runtime|\.NET|Driver|Sürücü|NVIDIA|Intel|Realtek|AMD|WebView2", RegexOptions.IgnoreCase))
                    { risk = "w"; note = " Başka programlar veya donanım buna bağlı olabilir."; }
                    list.Add(new Item
                    {
                        Id = name, Name = name, Risk = risk, Path = loc, Extra = un,
                        Status = Frozen.Contains(name) ? "Donduruldu" : "Yüklü",
                        Desc = pub + (ver != "" ? " · v" + ver : "") + "." + note
                    });
                }
            }
            return list.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static string UninstallUser(Item i)
        {
            if (string.IsNullOrWhiteSpace(i.Extra)) return "Kaldırma komutu bulunamadı.";
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + i.Extra + "\"") { UseShellExecute = false });
            return null;
        }

        static string Freeze(Item it, bool suspend)
        {
            if (string.IsNullOrWhiteSpace(it.Path)) return "Kurulum klasörü bilinmiyor, dondurulamadı.";
            int n = 0;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var f = p.MainModule?.FileName;
                    if (f == null || !f.StartsWith(it.Path, StringComparison.OrdinalIgnoreCase)) continue;
                    IntPtr h = OpenProcess(0x0800, false, p.Id);
                    if (h == IntPtr.Zero) continue;
                    if (suspend) NtSuspendProcess(h); else NtResumeProcess(h);
                    CloseHandle(h);
                    n++;
                }
                catch { }
            }
            if (n == 0) return suspend ? "Çalışan işlem bulunamadı." : "Donmuş işlem bulunamadı.";
            if (suspend) Frozen.Add(it.Id); else Frozen.Remove(it.Id);
            return null;
        }

        // ---------- Sistem uygulamaları (Microsoft Store/UWP) ----------
        static List<Item> SysApps()
        {
            var (c, o) = Sys.Ps("Get-AppxPackage -AllUsers | Select-Object Name,PackageFullName,InstallLocation,Publisher,NonRemovable | ConvertTo-Json -Compress");
            var list = new List<Item>();
            var seen = new HashSet<string>();
            foreach (var m in Json(o))
            {
                if (!G(m, "Publisher").Contains("CN=Microsoft")) continue;
                string name = G(m, "Name");
                if (!seen.Add(name)) continue;
                string risk = "w", desc = "Microsoft uygulaması. Ne işe yaradığı bilinmiyorsa dokunma.";
                if (AppxInfo.TryGetValue(name, out var d)) { risk = d.risk; desc = d.desc; }
                else if (name.StartsWith("Microsoft.Windows.") || name.Contains("ExperienceHost") || name.Contains("VCLibs") ||
                         name.Contains("NET.Native") || name.Contains("UI.Xaml") || name.Contains("WindowsStore"))
                { risk = "r"; desc = "Windows’un çalışması için gereken bileşen."; }
                bool nonrem = G(m, "NonRemovable") == "True";
                list.Add(new Item
                {
                    Id = G(m, "PackageFullName"), Name = name, Risk = risk, Path = G(m, "InstallLocation"),
                    Desc = desc + (nonrem ? " (Sistem tarafından korunuyor, kaldırılamaz.)" : ""),
                    Status = nonrem ? "Korumalı" : "Yüklü", Extra = nonrem ? "nonrem" : ""
                });
            }
            return list.OrderBy(i => i.Name).ToList();
        }

        static string RemoveAppx(Item i)
        {
            if (i.Extra == "nonrem") return "Korumalı paket, kaldırılamaz.";
            return Sys.Err(Sys.Ps("Remove-AppxPackage -AllUsers -Package '" + i.Id + "' -ErrorAction Stop", true));
        }

        // ---------- Hizmetler ----------
        static List<Item> Services()
        {
            var list = new List<Item>();
            foreach (var s in ServiceController.GetServices())
            {
                SvcInfo.TryGetValue(s.ServiceName, out var d);
                string st = s.Status == ServiceControllerStatus.Running ? "Çalışıyor" : "Durdu";
                try { if (s.StartType == ServiceStartMode.Disabled) st = "Devre dışı"; } catch { }
                list.Add(new Item
                {
                    Id = s.ServiceName, Name = s.DisplayName + " (" + s.ServiceName + ")",
                    Risk = d.risk ?? "w", Desc = d.desc ?? "Açıklama yok. Ne işe yaradığını bilmiyorsan dokunma.", Status = st
                });
            }
            return list.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        static string SvcOff(Item i)
        {
            Sys.Run("sc.exe", "stop \"" + i.Id + "\"", true);
            return Sys.Err(Sys.Run("sc.exe", "config \"" + i.Id + "\" start= disabled", true));
        }
        static string SvcOn(Item i)
        {
            var e = Sys.Err(Sys.Run("sc.exe", "config \"" + i.Id + "\" start= demand", true));
            if (e != null) return e;
            Sys.Run("sc.exe", "start \"" + i.Id + "\"", true);
            return null;
        }
        static string SvcDelete(Item i)
        {
            Sys.Run("sc.exe", "stop \"" + i.Id + "\"", true);
            return Sys.Err(Sys.Run("sc.exe", "delete \"" + i.Id + "\"", true));
        }

        // ---------- Başlangıç ----------
        const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string BakPath = @"Software\SistemYonetici\DisabledStartup\";
        static RegistryKey Root(string tag) => tag == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;

        static List<Item> Startup()
        {
            var list = new List<Item>();
            foreach (var tag in new[] { "HKCU", "HKLM" })
            {
                using (var k = Root(tag).OpenSubKey(RunPath))
                    if (k != null)
                        foreach (var n in k.GetValueNames())
                            list.Add(Mk(tag, n, k.GetValue(n)?.ToString() ?? "", "Açık"));
                using (var b = Registry.CurrentUser.OpenSubKey(BakPath + tag))
                    if (b != null)
                        foreach (var n in b.GetValueNames())
                            list.Add(Mk(tag, n, b.GetValue(n)?.ToString() ?? "", "Donduruldu"));
            }
            return list.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        static Item Mk(string tag, string name, string cmd, string st) => new Item
        {
            Id = tag + "|" + name, Name = name, Status = st,
            Risk = name.Contains("SecurityHealth", StringComparison.OrdinalIgnoreCase) ? "r" : "g",
            Desc = (name.Contains("SecurityHealth", StringComparison.OrdinalIgnoreCase) ? "Windows Güvenlik bildirimi. Kapatma. " : "Açılışta başlar. ") + cmd
        };
        static string StartMove(Item i, bool disable)
        {
            var parts = i.Id.Split('|'); string tag = parts[0], name = parts[1];
            string from = disable ? RunPath : BakPath + tag;
            RegistryKey fromRoot = disable ? Root(tag) : Registry.CurrentUser;
            using var src = fromRoot.OpenSubKey(from, true);
            var v = src?.GetValue(name);
            if (v == null) return "Kayıt bulunamadı.";
            if (disable) { using var b = Registry.CurrentUser.CreateSubKey(BakPath + tag); b.SetValue(name, v); }
            else { using var r = Root(tag).CreateSubKey(RunPath); r.SetValue(name, v); }
            src.DeleteValue(name, false);
            return null;
        }
        static string StartDelete(Item i)
        {
            var parts = i.Id.Split('|');
            foreach (var (root, path) in new[] { (Root(parts[0]), RunPath), (Registry.CurrentUser, BakPath + parts[0]) })
            { using var k = root.OpenSubKey(path, true); k?.DeleteValue(parts[1], false); }
            return null;
        }

        // ---------- Gizlilik, telemetri, reklam ----------
        static Tweak R(string id, string name, string desc, string risk, params (string k, string n, int off)[] v) => new Tweak
        {
            Id = id, Name = name, Desc = desc, Risk = risk,
            Applied = () => v.All(x => Sys.GetD(x.k, x.n) == x.off),
            Apply = () => { foreach (var x in v) Sys.SetD(x.k, x.n, x.off); },
            Revert = () => { foreach (var x in v) Sys.Del(x.k, x.n); }
        };

        const string HostsBegin = "# SistemYonetici BEGIN", HostsEnd = "# SistemYonetici END";
        static readonly string[] BlockHosts =
        {
            "vortex.data.microsoft.com","vortex-win.data.microsoft.com","telecommand.telemetry.microsoft.com",
            "telemetry.microsoft.com","watson.telemetry.microsoft.com","oca.telemetry.microsoft.com",
            "sqm.telemetry.microsoft.com","watson.microsoft.com","settings-win.data.microsoft.com",
            "df.telemetry.microsoft.com","reports.wes.df.telemetry.microsoft.com","telemetry.urs.microsoft.com",
            "choice.microsoft.com","redir.metaservices.microsoft.com","ads.msn.com","a.ads1.msn.com",
            "doubleclick.net","googleadservices.com","pagead2.googlesyndication.com","adservice.google.com",
            "ads.youtube.com","adnxs.com","taboola.com","outbrain.com"
        };
        static string HostsFile => Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");
        static string StripBlock(string t) =>
            Regex.Replace(t, @"\r?\n?" + HostsBegin + @".*?" + HostsEnd + @"\r?\n?", "\r\n", RegexOptions.Singleline);

        static readonly Tweak[] Privacy =
        {
            R("tele","Telemetriyi en aza indir","Windows’un Microsoft’a gönderdiği tanılama verisini kapatır.","g",
              (@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection","AllowTelemetry",0)),
            R("adid","Reklam kimliği","Uygulamaların seni reklam için takip etmesini engeller.","g",
              (@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo","Enabled",0)),
            R("sugg","Başlat menüsü ve kilit ekranı reklamları","Önerilen uygulama ve reklamları kapatır.","g",
              (@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager","SystemPaneSuggestionsEnabled",0),
              (@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager","SilentInstalledAppsEnabled",0),
              (@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager","SubscribedContent-338388Enabled",0)),
            R("act","Etkinlik geçmişi","Açtığın uygulama ve dosyaların kaydını tutmayı kapatır.","g",
              (@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System","EnableActivityFeed",0),
              (@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System","PublishUserActivities",0)),
            R("wer","Hata raporlama","Çöken programların raporunu Microsoft’a göndermeyi kapatır.","g",
              (@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting","Disabled",1)),
            R("cort","Cortana ve web aramasını kapat","Başlat aramasında internet sonuçlarını ve Cortana’yı kapatır.","g",
              (@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search","AllowCortana",0),
              (@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search","BingSearchEnabled",0)),
            R("loc","Konum hizmetleri","Konum erişimini kapatır. Harita ve hava durumu konumunu bulamaz.","w",
              (@"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors","DisableLocation",1)),
            new Tweak
            {
                Id = "hosts", Name = "Reklam ve telemetri sunucularını engelle (hosts)", Risk = "g",
                Desc = "hosts dosyasına bilinen reklam/telemetri alan adlarını ekler. Defender bu değişikliği uyarı olarak gösterebilir.",
                Applied = () => File.Exists(HostsFile) && File.ReadAllText(HostsFile).Contains(HostsBegin),
                Apply = () =>
                {
                    var t = StripBlock(File.ReadAllText(HostsFile)).TrimEnd();
                    var sb = new System.Text.StringBuilder(t + "\r\n" + HostsBegin + "\r\n");
                    foreach (var h in BlockHosts) sb.Append("0.0.0.0 " + h + "\r\n");
                    sb.Append(HostsEnd + "\r\n");
                    File.SetAttributes(HostsFile, FileAttributes.Normal);
                    File.WriteAllText(HostsFile, sb.ToString());
                },
                Revert = () => File.WriteAllText(HostsFile, StripBlock(File.ReadAllText(HostsFile)).TrimEnd() + "\r\n")
            },
        };

        // ---------- Defender ----------
        static Tweak MP(string id, string name, string desc, string prop, string offText, string setOff, string setOn) => new Tweak
        {
            Id = id, Name = name, Desc = desc, Risk = "r",
            Applied = () => Sys.Ps("(Get-MpPreference)." + prop).o.Trim() == offText,
            Apply = () => { var e = Sys.Err(Sys.Ps("Set-MpPreference " + setOff, true)); if (e != null) throw new Exception(e); },
            Revert = () => { var e = Sys.Err(Sys.Ps("Set-MpPreference " + setOn, true)); if (e != null) throw new Exception(e); }
        };
        static readonly Tweak[] Defender =
        {
            MP("rt","Gerçek zamanlı korumayı kapat","Dosyaları anlık tarayan korumayı kapatır. Virüslere açık kalırsın.","DisableRealtimeMonitoring","True","-DisableRealtimeMonitoring $true","-DisableRealtimeMonitoring $false"),
            MP("bm","Davranış izlemeyi kapat","Şüpheli program davranışlarını izlemeyi kapatır.","DisableBehaviorMonitoring","True","-DisableBehaviorMonitoring $true","-DisableBehaviorMonitoring $false"),
            MP("cloud","Bulut korumasını kapat","Yeni tehdit bilgilerini buluttan almayı kapatır.","MAPSReporting","0","-MAPSReporting 0","-MAPSReporting 2"),
            MP("smp","Otomatik örnek gönderimini kapat","Şüpheli dosyaları Microsoft’a göndermeyi kapatır.","SubmitSamplesConsent","2","-SubmitSamplesConsent 2","-SubmitSamplesConsent 1"),
            new Tweak
            {
                Id = "fw", Name = "Güvenlik duvarını kapat", Risk = "r",
                Desc = "Gelen ve giden ağ trafiği filtrelenmez.",
                Applied = () => Sys.Ps("(Get-NetFirewallProfile | Where-Object Enabled -eq 'True' | Measure-Object).Count").o.Trim() == "0",
                Apply = () => { var e = Sys.Err(Sys.Ps("Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled False", true)); if (e != null) throw new Exception(e); },
                Revert = () => { var e = Sys.Err(Sys.Ps("Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True", true)); if (e != null) throw new Exception(e); }
            },
            R("pol","Defender’ı politika ile tamamen devre dışı bırak","DisableAntiSpyware politikası. Kurcalama Koruması açıksa Windows Güvenlik’ten önce onu kapat.","r",
              (@"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender","DisableAntiSpyware",1)),
        };

        static string TA(Tweak[] set, Item i, bool apply)
        {
            var t = set.First(x => x.Id == i.Id);
            if (apply)
            {
                t.Apply();
                if (!t.Applied()) return "Değişiklik uygulanmadı (Kurcalama Koruması açık olabilir).";
            }
            else t.Revert();
            return null;
        }
        static Task<List<Item>> LT(Tweak[] set) => Task.Run(() => set.Select(t => new Item
        {
            Id = t.Id, Name = t.Name, Desc = t.Desc, Risk = t.Risk, Status = t.Applied() ? "Uygulandı" : "Uygulanmadı"
        }).ToList());

        // ---------- Windows özellikleri ----------
        static readonly Dictionary<string, (string risk, string desc)> FeatInfo = new()
        {
            ["SMB1Protocol"] = ("g", "Eski ve açıklı ağ dosya protokolü. Kapalı olması daha güvenli."),
            ["MicrosoftWindowsPowerShellV2Root"] = ("g", "Eski PowerShell 2.0. Saldırganlar kötüye kullanabilir, kapatılabilir."),
            ["MicrosoftWindowsPowerShellV2"] = ("g", "Eski PowerShell 2.0 motoru."),
            ["TelnetClient"] = ("g", "Şifresiz uzak bağlantı istemcisi."),
            ["WindowsMediaPlayer"] = ("g", "Eski Windows Media Player."),
            ["Printing-XPSServices-Features"] = ("g", "XPS sanal yazıcı."),
            ["FaxServicesClientPackage"] = ("g", "Faks ve tarama."),
            ["WorkFolders-Client"] = ("g", "Çalışma Klasörleri (kurumsal eşitleme)."),
            ["Microsoft-Hyper-V-All"] = ("w", "Sanal makineler. Kapatırsan WSL2 ve emülatörler çalışmaz."),
            ["NetFx3"] = ("w", ".NET Framework 3.5: eski programlar için gerekli."),
            ["Microsoft-Windows-Subsystem-Linux"] = ("w", "Linux için Windows Alt Sistemi."),
            ["VirtualMachinePlatform"] = ("w", "Sanal makine platformu (WSL2, Android emülatörleri)."),
        };
        static List<Item> Features()
        {
            var (c, o) = Sys.Ps("Get-WindowsOptionalFeature -Online | Select-Object FeatureName,@{n='State';e={[string]$_.State}} | ConvertTo-Json -Compress");
            var list = new List<Item>();
            foreach (var m in Json(o))
            {
                string n = G(m, "FeatureName");
                string risk = "w", desc = "Windows isteğe bağlı özelliği. Bilmiyorsan dokunma.";
                if (FeatInfo.TryGetValue(n, out var d)) { risk = d.risk; desc = d.desc; }
                list.Add(new Item { Id = n, Name = n, Risk = risk, Desc = desc, Status = G(m, "State") == "Enabled" ? "Açık" : "Kapalı" });
            }
            return list.OrderBy(i => i.Name).ToList();
        }

        // ---------- Dosya yöneticisi ----------
        static List<Item> Files()
        {
            var list = new List<Item>();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string RiskOf(string p) =>
                p.StartsWith(win, StringComparison.OrdinalIgnoreCase) ? "r" :
                (p.StartsWith(pf, StringComparison.OrdinalIgnoreCase) || p.StartsWith(pfx, StringComparison.OrdinalIgnoreCase)) ? "w" : "g";
            if (string.IsNullOrEmpty(FilePath))
            {
                foreach (var d in DriveInfo.GetDrives())
                    list.Add(new Item { Id = d.Name, Name = d.Name, Kind = "dir", Risk = "r", Status = "Sürücü", Desc = "Sürücü kökü." });
                return list;
            }
            try
            {
                foreach (var d in Directory.GetDirectories(FilePath).Take(2000))
                    list.Add(new Item { Id = d, Name = Path.GetFileName(d), Kind = "dir", Risk = RiskOf(d), Status = "Klasör", Desc = d });
                foreach (var f in Directory.GetFiles(FilePath).Take(2000))
                {
                    long sz = 0; try { sz = new FileInfo(f).Length; } catch { }
                    list.Add(new Item { Id = f, Name = Path.GetFileName(f), Kind = "file", Risk = RiskOf(f), Status = "Dosya", Desc = (sz / 1024) + " KB" });
                }
            }
            catch (Exception e) { list.Add(new Item { Name = "(açılamadı)", Desc = e.Message, Risk = "w" }); }
            return list;
        }
        static string ForceDelete(Item it)
        {
            string p = it.Id;
            if (string.IsNullOrEmpty(p)) return "Geçersiz öğe.";
            if (Path.GetPathRoot(p) == p) return "Sürücü kökü silinemez.";
            bool dir = it.Kind == "dir";
            Sys.Run("takeown.exe", "/f \"" + p + "\" " + (dir ? "/r /d y" : ""), true);
            Sys.Run("icacls.exe", "\"" + p + "\" /grant *S-1-5-32-544:F " + (dir ? "/t /c /q" : "/c /q"), true);
            var r = dir ? Sys.Run("cmd.exe", "/c rd /s /q \"" + p + "\"", true) : Sys.Run("cmd.exe", "/c del /f /q \"" + p + "\"", true);
            bool still = dir ? Directory.Exists(p) : File.Exists(p);
            return still ? "Silinemedi (kullanımda olabilir). " + r.o.Trim() : null;
        }

        // ---------- Kategoriler ----------
        public static readonly Cat[] All =
        {
            new Cat { Title = "Kullanıcı uygulamaları", A = "Dondur", B = "Çöz", C = "Kaldır",
                Load = () => Task.Run(UserApps), DoA = i => Freeze(i, true), DoB = i => Freeze(i, false), DoC = UninstallUser },
            new Cat { Title = "Sistem uygulamaları", A = "Dondur", B = "Çöz", C = "Kaldır",
                Load = () => Task.Run(SysApps), DoA = i => Freeze(i, true), DoB = i => Freeze(i, false), DoC = RemoveAppx },
            new Cat { Title = "Hizmetler", A = "Devre dışı bırak", B = "Etkinleştir", C = "Sil",
                Load = () => Task.Run(Services), DoA = SvcOff, DoB = SvcOn, DoC = SvcDelete },
            new Cat { Title = "Başlangıç", A = "Dondur", B = "Çöz", C = "Sil",
                Load = () => Task.Run(Startup), DoA = i => StartMove(i, true), DoB = i => StartMove(i, false), DoC = StartDelete },
            new Cat { Title = "Gizlilik ve reklam", A = "Uygula", B = "Geri al",
                Load = () => LT(Privacy), DoA = i => TA(Privacy, i, true), DoB = i => TA(Privacy, i, false) },
            new Cat { Title = "Windows özellikleri", A = "Kapat", B = "Aç",
                Load = () => Task.Run(Features),
                DoA = i => Sys.Err(Sys.Ps("Disable-WindowsOptionalFeature -Online -FeatureName '" + i.Id + "' -NoRestart -ErrorAction Stop", true)),
                DoB = i => Sys.Err(Sys.Ps("Enable-WindowsOptionalFeature -Online -FeatureName '" + i.Id + "' -All -NoRestart -ErrorAction Stop", true)) },
            new Cat { Title = "Defender", A = "Devre dışı bırak", B = "Geri aç",
                Load = () => LT(Defender), DoA = i => TA(Defender, i, true), DoB = i => TA(Defender, i, false) },
            new Cat { Title = "Dosya yöneticisi", C = "Zorla sil", Files = true,
                Load = () => Task.Run(Files), DoC = ForceDelete },
        };
    }
}
