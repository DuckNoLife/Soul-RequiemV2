using System.Collections.Generic;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 07/11 (§183): danh mục NỘI TẠI (passive) — sách đã học (cờ book_*, mang theo qua map bằng GameSession.Books). Luôn có hiệu lực,
    /// không chiếm ô; bảng nghỉ (Mantras) hiện thành mục "Passives". Thêm sách mới: thêm một dòng ở đây + icon Resources/UI/Requiem/&lt;icon&gt;.
    /// </summary>
    public static class PassiveCatalog
    {
        public struct Entry
        {
            public string flag, name, description, icon;
            public Entry(string flag, string name, string description, string icon) { this.flag = flag; this.name = name; this.description = description; this.icon = icon; }
        }

        public static readonly Entry[] All =
        {
            new Entry(TurnedBlade.Flag, "The Turned Blade",
                "Strike an enemy projectile just before it hits you and it flies back at its owner - faster, and twice as deadly.",
                "ic_passive_turned_blade"),
            new Entry("book_purifier", "The Purifier's Rite",
                "Earthshatter now also scatters any poison cloud it touches.",
                "ic_passive_purifier"),
        };

        /// Các nội tại đã học (theo cờ của map đang chơi; cờ sách đã được chép từ GameSession.Books khi vào map).
        public static List<Entry> Learned(WorldFlags flags)
        {
            var list = new List<Entry>();
            foreach (var e in All)
                if ((flags != null && flags.Has(e.flag)) || GameSession.Books.Contains(e.flag)) list.Add(e);
            return list;
        }
    }
}
