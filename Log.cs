using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SistemYonetici
{
    public class LogEntry
    {
        public DateTime Time { get; set; }
        public string Category { get; set; }
        public string ItemName { get; set; }
        public string ItemId { get; set; }
        public string Action { get; set; } // "A" | "B" | "C"
        public string Label { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public bool Preview { get; set; }
    }

    public static class Log
    {
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemYonetici");
        static readonly string FilePath = Path.Combine(Dir, "log.json");
        static List<LogEntry> items;

        public static List<LogEntry> Items
        {
            get
            {
                if (items == null) Load();
                return items;
            }
        }

        public static void Load()
        {
            try
            {
                items = File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<List<LogEntry>>(File.ReadAllText(FilePath)) ?? new List<LogEntry>()
                    : new List<LogEntry>();
            }
            catch { items = new List<LogEntry>(); }
        }

        public static void Add(LogEntry e)
        {
            Items.Insert(0, e);
            if (Items.Count > 500) Items.RemoveRange(500, Items.Count - 500);
            Save();
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Items));
            }
            catch { }
        }

        // Günlükten "tersini uygula" — sadece anahtar/değer (açma-kapama) tipi işlemler için mümkün.
        public static string Revert(int index)
        {
            if (index < 0 || index >= Items.Count) return "Kayıt bulunamadı.";
            var e = Items[index];
            if (!e.Success) return "Bu işlem zaten başarısızdı, geri alınacak bir şey yok.";
            var cat = Data.All.FirstOrDefault(c => c.Title == e.Category);
            if (cat == null) return "Kategori artık mevcut değil.";
            // A <-> B birbirinin tersidir (Dondur/Çöz, Devre dışı/Etkinleştir, Uygula/Geri al, Kapat/Aç).
            Func<Item, string> inverse =
                e.Action == "A" ? cat.DoB :
                e.Action == "B" ? cat.DoA : null;
            if (inverse == null) return "Bu işlem türü (" + e.Label + ") geri alınamaz — kalıcı bir silme/kaldırma işlemiydi.";
            var it = new Item { Id = e.ItemId, Name = e.ItemName };
            try
            {
                var err = inverse(it);
                Add(new LogEntry { Time = DateTime.Now, Category = e.Category, ItemName = e.ItemName, ItemId = e.ItemId,
                    Action = e.Action == "A" ? "B" : "A", Label = "Günlükten geri alma", Success = err == null, Error = err });
                return err;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
