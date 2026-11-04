using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace SistemYonetici
{
    public class Item
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public string Risk { get; set; } = "g";
        public string Desc { get; set; } = "";
        public string Status { get; set; } = "";
        public string Path { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Extra { get; set; } = "";
        public string RiskText => Risk == "g" ? "✓ Güvenli" : Risk == "w" ? "⚠ Dikkat" : "! Riskli";
    }
    public class Tweak
    {
        public string Id, Name, Desc, Risk;
        public Func<bool> Applied;
        public Action Apply, Revert;
    }
    public class Cat
    {
        public string Title, A, B, C;
        public bool Files;
        public bool Log;
        public Func<Task<List<Item>>> Load;
        public Func<Item, string> DoA, DoB, DoC;
        // Verilen öğe için A işleminin etkisini (gerçek bağımlılıklar dahil) açıklayan metin döndürür; yoksa null.
        public Func<Item, string> Impact;
    }
}
