using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemYonetici
{
    public partial class MainWindow : Window
    {
        Cat cur;
        List<Item> all = new();
        bool restored;

        public MainWindow()
        {
            InitializeComponent();
            Nav.ItemsSource = Data.All.Select(c => c.Title).ToList();
            Nav.SelectedIndex = 0;
        }

        void Status(string s) => StatusTxt.Text = s;

        void Nav_Changed(object s, SelectionChangedEventArgs e)
        {
            if (Nav.SelectedIndex < 0) return;
            cur = Data.All[Nav.SelectedIndex];
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
            }
            Reload();
            if (errs.Count > 0)
                MessageBox.Show(string.Join("\n\n", errs.Take(8)), ok + " başarılı, " + errs.Count + " hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            else Status(ok + " işlem tamamlandı.");
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
