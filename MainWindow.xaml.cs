using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace SistemYonetici
{
    public partial class MainWindow : Window
    {
        Cat cur;
        List<Item> all = new();
        bool restored;
        Forms.NotifyIcon tray;
        bool exiting;

        public MainWindow()
        {
            InitializeComponent();
            SetupTray();
            CheckAdmin();
            for (int i = 0; i < Data.All.Length; i++)
            {
                var rb = new RadioButton
                {
                    Content = Data.All[i].Title, GroupName = "nav",
                    Style = (Style)FindResource("NavBtn"), Tag = i, IsChecked = i == 0
                };
                rb.Checked += (s, e) => SelectCat((int)((RadioButton)s).Tag);
                NavPanel.Children.Add(rb);
            }
            SelectCat(0);
        }

        void Status(string s) => StatusTxt.Text = s;

        void CheckAdmin()
        {
            bool admin;
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                admin = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { admin = false; }
            if (!admin)
                MessageBox.Show(
                    "Uygulama yönetici yetkisiyle çalışmıyor.\n\nGüvenli (✓) işlemler yine de çalışır, ama Dikkat (⚠) ve Riskli (!) işaretli işlemler (hizmet kapatma, Defender, Windows özellikleri, zorla silme vb.) başarısız olur.\n\nEXE'yi sağ tıklayıp \"Yönetici olarak çalıştır\" ile açman önerilir.",
                    "Yönetici yetkisi yok", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        void SetupTray()
        {
            tray = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Shield,
                Text = "Sistem Yöneticisi",
                Visible = true
            };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Aç", null, (s, e) => ShowFromTray());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Çıkış", null, (s, e) => { exiting = true; tray.Visible = false; tray.Dispose(); Close(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => ShowFromTray();

            StateChanged += (s, e) => { if (WindowState == WindowState.Minimized) { Hide(); tray.ShowBalloonTip(1500, "Sistem Yöneticisi", "Arka planda çalışmaya devam ediyor. Tepsi simgesinden açabilirsin.", Forms.ToolTipIcon.Info); } };
            Closing += (s, e) => { if (!exiting) { e.Cancel = true; WindowState = WindowState.Minimized; } else { tray.Visible = false; tray.Dispose(); } };
        }
        void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }

        void SelectCat(int i)
        {
            cur = Data.All[i];
            TitleTxt.Text = cur.Title;
            Vis(BtnA, cur.A); Vis(BtnB, cur.B); Vis(BtnC, cur.C);
            BtnUp.Visibility = cur.Files ? Visibility.Visible : Visibility.Collapsed;
            Filter.Text = "";
            Reload();
        }
        void Vis(Button b, string label)
        {
            b.Content = label;
            b.Visibility = label == null ? Visibility.Collapsed : Visibility.Visible;
        }

        async void Reload()
        {
            var c = cur;
            Status("Yükleniyor…");
            Grid.ItemsSource = null;
            try { all = await c.Load(); }
            catch (Exception ex) { all = new List<Item>(); Status("Hata: " + ex.Message); return; }
            if (c != cur) return;
            ApplyFilter();
        }
        void ApplyFilter()
        {
            string f = Filter.Text.Trim();
            var v = all.Where(i => f == "" || i.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                                   i.Desc.Contains(f, StringComparison.OrdinalIgnoreCase)).ToList();
            Grid.ItemsSource = v;
            Status(v.Count + " öğe" + (cur.Files ? "  ·  " + (string.IsNullOrEmpty(Data.FilePath) ? "Sürücüler" : Data.FilePath) : ""));
        }
        void Filter_Changed(object s, TextChangedEventArgs e) { if (cur != null) ApplyFilter(); }
        void Refresh_Click(object s, RoutedEventArgs e) => Reload();

        void Adv_Changed(object s, RoutedEventArgs e)
        {
            Sys.Advanced = ChkAdv.IsChecked == true;
            if (Sys.Advanced && !Sys.HasNSudo)
                MessageBox.Show("NSudoLC.exe uygulamanın yanında bulunamadı.\nRiskli işlemler yönetici yetkisiyle yapılır; kilitli (TrustedInstaller) öğeler için NSudoLC.exe dosyasını SistemYonetici.exe ile aynı klasöre koy.",
                    "Gelişmiş mod", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        async void Restore_Click(object s, RoutedEventArgs e)
        {
            Status("Geri yükleme noktası oluşturuluyor…");
            var err = await Task.Run(Sys.CreateRestorePoint);
            if (err == null) restored = true;
            Status(err == null ? "Geri yükleme noktası oluşturuldu." : "Oluşturulamadı: " + err);
        }

        void BtnA_Click(object s, RoutedEventArgs e) => Act("A");
        void BtnB_Click(object s, RoutedEventArgs e) => Act("B");
        void BtnC_Click(object s, RoutedEventArgs e) => Act("C");

        async void Act(string which)
        {
            var sel = Grid.SelectedItems.Cast<Item>().ToList();
            if (sel.Count == 0) { Status("Önce bir öğe seç."); return; }
            var f = which == "A" ? cur.DoA : which == "B" ? cur.DoB : cur.DoC;
            string label = which == "A" ? cur.A : which == "B" ? cur.B : cur.C;
            var risky = sel.Where(i => i.Risk != "g").ToList();

            // Önizleme modu: hiçbir şey uygulamadan etkisini göster (gerçek bağımlılık varsa onu da).
            if (ChkPreview.IsChecked == true)
            {
                var lines = new List<string>();
                foreach (var it in sel)
                {
                    string impact = cur.Impact != null ? await Task.Run(() => { try { return cur.Impact(it); } catch (Exception ex) { return "Kontrol edilemedi: " + ex.Message; } }) : null;
                    lines.Add("• " + it.Name + (impact != null ? ": " + impact : " için bilinen bir bağımlılık kontrolü yok."));
                    Log.Add(new LogEntry { Time = DateTime.Now, Category = cur.Title, ItemName = it.Name, ItemId = it.Id, Action = which, Label = label, Success = true, Preview = true });
                }
                MessageBox.Show(string.Join("\n\n", lines), "Önizleme — “" + label + "” uygulanırsa", MessageBoxButton.OK, MessageBoxImage.Information);
                if (cur.Log) Reload();
                return;
            }

            if (risky.Any(i => i.Risk == "r") && !Sys.Advanced)
            {
                MessageBox.Show("Seçimde riskli (!) öğe var. Sol alttan Gelişmiş modu (NSudo) açmadan bu işlem yapılamaz.",
                    "Riskli işlem", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string q = sel.Count == 1 ? "“" + sel[0].Name + "” için “" + label + "” uygulansın mı?"
                                      : sel.Count + " öğe için “" + label + "” uygulansın mı?";
            if (risky.Count > 0) q += "\n\n" + risky.Count + " öğe Dikkat/Riskli işaretli. Sistem etkilenebilir.";
            if (which == "C") q += "\n\nBu işlem geri alınamayabilir.";
            if (cur.Impact != null && sel.Count == 1)
            {
                var imp = await Task.Run(() => { try { return cur.Impact(sel[0]); } catch { return null; } });
                if (imp != null) q += "\n\nBağımlılık kontrolü: " + imp;
            }
            if (risky.Count > 0 || which == "C")
                if (MessageBox.Show(q, "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            if (risky.Count > 0 && !restored)
            {
                Status("Geri yükleme noktası oluşturuluyor…");
                var re = await Task.Run(Sys.CreateRestorePoint);
                restored = true;
                if (re != null) Status("Geri yükleme noktası oluşturulamadı: " + re);
            }

            var errs = new List<string>();
            int ok = 0;
            foreach (var it in sel)
            {
                Status(label + ": " + it.Name + "…");
                string err = await Task.Run(() => { try { return f(it); } catch (Exception ex) { return ex.Message; } });
                if (err == null) ok++; else errs.Add(it.Name + ": " + err);
                if (!cur.Log)
                    Log.Add(new LogEntry { Time = DateTime.Now, Category = cur.Title, ItemName = it.Name, ItemId = it.Id, Action = which, Label = label, Success = err == null, Error = err });
            }
            Reload();
            if (errs.Count > 0)
                MessageBox.Show(string.Join("\n\n", errs.Take(8)), ok + " başarılı, " + errs.Count + " hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            else Status(ok + " işlem tamamlandı.");
        }

        async void CheckUpdate_Click(object s, RoutedEventArgs e)
        {
            Status("Güncelleme kontrol ediliyor…");
            var (has, tag, url, err) = await Updater.CheckAsync();
            if (err != null) { Status("Güncelleme kontrolü başarısız: " + err); return; }
            if (!has) { Status("Zaten güncel sürümdesin."); return; }
            if (MessageBox.Show("Yeni sürüm bulundu: " + tag + "\nİndirilip uygulansın mı? Uygulama yeniden başlayacak.",
                "Güncelleme", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            Status("İndiriliyor…");
            var dlErr = await Updater.DownloadAndApplyAsync(url);
            if (dlErr != null) { Status("Güncelleme başarısız: " + dlErr); return; }
            Application.Current.Shutdown();
        }

        void Grid_Dbl(object s, MouseButtonEventArgs e)
        {
            if (cur == null || !cur.Files) return;
            if (Grid.SelectedItem is Item it && it.Kind == "dir") { Data.FilePath = it.Id; Reload(); }
        }
        void BtnUp_Click(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Data.FilePath)) return;
            Data.FilePath = Directory.GetParent(Data.FilePath.TrimEnd('\\'))?.FullName ?? "";
            Reload();
        }
    }
}
