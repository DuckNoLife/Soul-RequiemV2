using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 05/11: SƯƠNG MÙ BẢN ĐỒ theo KHU (dữ liệu thuần — RequiemHUD vẽ). Không phải MonoBehaviour: MapEncounterDirector tạo sau khi nạp cờ.
    ///
    /// Vùng sương = các khu của lớp `zones` trong Tiled, GỘP theo tên hiển thị (khu phục kích chồng lên phòng, vd `spec_breakout`
    /// "Specimen Cells", là cùng một vùng với phòng). Hành lang / ô ngoài mọi khu thuộc về khu (không bí mật) GẦN NHẤT -> cả map chia hết
    /// thành vùng, mở khu là mở luôn lối đi cạnh nó.
    /// Thuộc tính Tiled trên object khu: `fog` = (trống) bình thường · `secret` = khu bí mật: phủ sương KHÔNG có "??", không nhận hành lang,
    /// chỉ mở khi Ren bước hẳn vào trong khung khu · `off` = luôn hiện rõ.
    /// Mở vùng: Ren đứng trên một ô của vùng -> cờ `map_seen_&lt;id khu đầu&gt;` (tự vào bản lưu như mọi cờ). Bản đồ nhặt được: ghi chú / đồ
    /// có `sets = map_seen_r6,map_seen_spec` là mở trước các khu đó. Bản lưu cũ: khu đã có `&lt;id&gt;_started` / `_cleared` tính là đã mở.
    /// Toạ độ theo Ô Tiled (x phải, y xuống) như QuestTracker / minimap.
    /// </summary>
    public sealed class MapFog
    {
        public const string SeenPrefix = "map_seen_";

        public sealed class Region
        {
            public string key, title;
            public bool secret, revealed;
            public Vector2 center;                       // tâm khu lớn nhất của vùng (đặt "??" / tên trên bản đồ lớn)
            internal readonly List<EncounterZone> zones = new List<EncounterZone>();
            internal readonly List<RectInt> rects = new List<RectInt>();
            /// <summary>Khung các khu của vùng (toạ độ Ô) — UI dùng cho hiệu ứng mây tan.</summary>
            public IReadOnlyList<RectInt> Areas => rects;
            internal float bestArea;
        }

        public int Width { get; }
        public int Height { get; }
        /// <summary>Tăng mỗi khi có vùng đổi trạng thái — UI vẽ lại lớp sương khi số này đổi.</summary>
        public int Version { get; private set; }
        public IReadOnlyList<Region> Regions => regions;

        private readonly List<Region> regions = new List<Region>();
        private readonly short[] owner, owner2;          // vùng của ô (owner2 = vùng thứ hai khi 2 khu khác tên chồng nhau)
        private readonly bool[] inZone, alwaysClear;
        private readonly WorldFlags flags;
        private readonly Transform mapRoot;
        private readonly float tileSize;

        internal MapFog(MapLayout layout, Transform mapRoot, float tileSize, int width, int height, WorldFlags flags)
        {
            this.mapRoot = mapRoot; this.tileSize = tileSize; this.flags = flags;
            Width = Mathf.Max(1, width); Height = Mathf.Max(1, height);
            int n = Width * Height;
            owner = new short[n]; owner2 = new short[n];
            inZone = new bool[n]; alwaysClear = new bool[n];
            var area = new float[n];
            for (int i = 0; i < n; i++) { owner[i] = owner2[i] = -1; area[i] = float.MaxValue; }

            var byTitle = new Dictionary<string, Region>();
            foreach (var id in layout.ZoneOrder)
            {
                if (!layout.Zones.TryGetValue(id, out var z) || z.rect.width <= 0f || z.rect.height <= 0f) continue;
                var r = ToTiles(z.rect);
                string mode = (z.fog ?? "").Trim().ToLowerInvariant();
                if (mode == "off") { Fill(r, i => alwaysClear[i] = true); continue; }
                string key = (z.title ?? id).Trim().ToLowerInvariant();
                if (!byTitle.TryGetValue(key, out var reg))
                {
                    reg = new Region { key = id, title = z.title };
                    byTitle[key] = reg;
                    regions.Add(reg);
                }
                reg.zones.Add(z);
                reg.rects.Add(r);
                if (mode == "secret") reg.secret = true;
                float a = r.width * r.height;
                if (a > reg.bestArea) { reg.bestArea = a; reg.center = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f); }
            }

            // ô trong khu: khu nhỏ nhất làm chủ, khu khác tên chồng lên -> owner2
            for (short ri = 0; ri < regions.Count; ri++)
                foreach (var r in regions[ri].rects)
                {
                    float a = r.width * r.height;
                    short idx = ri;
                    Fill(r, i =>
                    {
                        inZone[i] = true;
                        if (owner[i] == idx || owner2[i] == idx) return;
                        if (a < area[i]) { owner2[i] = owner[i]; owner[i] = idx; area[i] = a; }
                        else if (owner2[i] < 0) owner2[i] = idx;
                    });
                }

            // hành lang / ô ngoài khu -> vùng KHÔNG bí mật gần nhất (khoảng cách tới khung khu)
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (inZone[i] || alwaysClear[i]) continue;
                    float best = float.MaxValue; short bi = -1;
                    Vector2 c = new Vector2(x + 0.5f, y + 0.5f);
                    for (short ri = 0; ri < regions.Count; ri++)
                    {
                        if (regions[ri].secret) continue;
                        foreach (var r in regions[ri].rects)
                        {
                            float dx = Mathf.Max(Mathf.Max(r.xMin - c.x, c.x - r.xMax), 0f), dy = Mathf.Max(Mathf.Max(r.yMin - c.y, c.y - r.yMax), 0f);
                            float d = dx * dx + dy * dy;
                            if (d < best) { best = d; bi = ri; }
                        }
                    }
                    owner[i] = bi;
                }

            flags.Changed += _ => Refresh();
            Refresh();
        }

        /// <summary>Mỗi khung (director): mở vùng dưới chân Ren.</summary>
        public void Tick(Vector3 playerWorld)
        {
            Vector2 t = WorldToTile(playerWorld);
            int x = Mathf.FloorToInt(t.x), y = Mathf.FloorToInt(t.y);
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            Reveal(owner[i], i);
            Reveal(owner2[i], i);
        }

        private void Reveal(short ri, int i)
        {
            if (ri < 0) return;
            var r = regions[ri];
            if (r.revealed || (r.secret && !inZone[i])) return;
            flags.Set(SeenPrefix + r.key);                 // Changed -> Refresh -> Version++
        }

        /// <summary>Ô (theo toạ độ Ô) đã hiện rõ chưa: 1 = rõ, 0 = sương.</summary>
        public float ClearAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return 0f;
            int i = y * Width + x;
            if (alwaysClear[i]) return 1f;
            if (owner[i] >= 0 && regions[owner[i]].revealed) return 1f;
            if (owner2[i] >= 0 && regions[owner2[i]].revealed) return 1f;
            return owner[i] < 0 && owner2[i] < 0 ? 1f : 0f;
        }

        /// <summary>Vùng làm chủ ô (chỉ số trong Regions), -1 = không thuộc vùng nào.</summary>
        public int RegionAt(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? -1 : owner[y * Width + x];

        public bool IsClear(Vector2 tile) => ClearAt(Mathf.FloorToInt(tile.x), Mathf.FloorToInt(tile.y)) > 0.5f;

        public Vector2 WorldToTile(Vector3 w)
        {
            Vector3 l = mapRoot != null ? mapRoot.InverseTransformPoint(w) : w;
            return new Vector2(l.x / tileSize, -l.y / tileSize);
        }

        private void Refresh()
        {
            bool changed = false;
            foreach (var r in regions)
            {
                bool rev = false;
                foreach (var z in r.zones)
                    if (flags.Has(SeenPrefix + z.id) || flags.Has(z.id + "_started") || flags.Has(z.id + "_cleared")) { rev = true; break; }
                if (rev != r.revealed) { r.revealed = rev; changed = true; }
            }
            if (changed) Version++;
        }

        private RectInt ToTiles(Rect world)
        {
            Vector2 a = WorldToTile(new Vector3(world.xMin, world.yMax, 0f));
            Vector2 b = WorldToTile(new Vector3(world.xMax, world.yMin, 0f));
            int x0 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(a.x, b.x)), 0, Width), x1 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(a.x, b.x)), 0, Width);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(a.y, b.y)), 0, Height), y1 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(a.y, b.y)), 0, Height);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        private void Fill(RectInt r, System.Action<int> f)
        {
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    f(y * Width + x);
        }
    }
}
