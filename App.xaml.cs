using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace SistemYonetici
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll")] static extern bool AllocConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Length > 0)
            {
                RunCli(e.Args).GetAwaiter().GetResult();
                Environment.Exit(_exitCode);
                return;
            }
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var w = new MainWindow();
            w.Show();
        }

        static int _exitCode;

        static async Task RunCli(string[] a)
        {
            AllocConsole();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

            if (a[0] == "--help" || a[0] == "-h" || a[0] == "/?")
            {
                Console.WriteLine("Sistem Yöneticisi — komut satırı modu");
                Console.WriteLine();
                Console.WriteLine("  --help                                   bu yardımı gösterir");
                Console.WriteLine("  --categories                             kategori listesini gösterir");
                Console.WriteLine("  --list \"<Kategori>\"                      kategorideki öğeleri (id, ad) listeler");
                Console.WriteLine("  --run \"<Kategori>\" <A|B|C> \"<itemId>\"    bir işlemi uygular");
                Console.WriteLine();
                Console.WriteLine("  Dosya yöneticisi ve Değişiklik günlüğü kategorileri CLI'dan desteklenmez.");
                Console.WriteLine("  Riskli (r) işlemler için uygulamayı yönetici olarak çalıştırman gerekir.");
                return;
            }
            if (a[0] == "--categories")
            {
                foreach (var c in Data.All)
                    if (!c.Files && !c.Log)
                        Console.WriteLine(c.Title + "   [" + c.A + (c.B != null ? " | " + c.B : "") + (c.C != null ? " | " + c.C : "") + "]");
                return;
            }
            if (a[0] == "--list" && a.Length >= 2)
            {
                var cat = Find(a[1]);
                if (cat == null) return;
                var items = await cat.Load();
                foreach (var it in items)
                    Console.WriteLine(it.Id + "\t" + it.Name + "\t[" + it.Risk + "]\t" + it.Status);
                return;
            }
            if (a[0] == "--run" && a.Length >= 4)
            {
                var cat = Find(a[1]);
                if (cat == null) return;
                string action = a[2].ToUpperInvariant();
                string id = a[3];
                var items = await cat.Load();
                var item = items.FirstOrDefault(x => x.Id == id);
                if (item == null) { Console.Error.WriteLine("Öğe bulunamadı: " + id); _exitCode = 1; return; }
                var f = action == "A" ? cat.DoA : action == "B" ? cat.DoB : action == "C" ? cat.DoC : null;
                string label = action == "A" ? cat.A : action == "B" ? cat.B : cat.C;
                if (f == null) { Console.Error.WriteLine("Bu kategoride " + action + " işlemi yok."); _exitCode = 1; return; }
                Console.WriteLine(label + ": " + item.Name + " ...");
                string err = null;
                try { err = f(item); } catch (Exception ex) { err = ex.Message; }
                Log.Add(new LogEntry { Time = DateTime.Now, Category = cat.Title, ItemName = item.Name, ItemId = item.Id, Action = action, Label = label, Success = err == null, Error = err });
                if (err == null) { Console.WriteLine("Tamam."); }
                else { Console.Error.WriteLine("Hata: " + err); _exitCode = 1; }
                return;
            }
            Console.Error.WriteLine("Geçersiz komut. --help ile kullanım bilgisini gör.");
            _exitCode = 1;
        }

        static Cat Find(string title) =>
            Data.All.FirstOrDefault(c => !c.Files && !c.Log && string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));
    }
}
