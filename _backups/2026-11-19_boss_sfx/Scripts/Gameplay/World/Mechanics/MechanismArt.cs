using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 06/11: hình của một cơ quan, đặt trong Tiled bằng TILE OBJECT (kéo từ tileset SR-mech-art.tsx) thay vì hình chữ nhật trống
    /// mang thuộc tính `sprite` — để người dùng THẤY và kéo thả được vật ngay trong Tiled.
    ///
    /// Hai cách dùng (xem ARCHITECTURE 2.5 "Hình của cơ quan"):
    ///   1. Chính cơ quan là tile object (sprite_prop, note, passage): khung tile object = khung hình; vùng cơ quan (Rect) = khung đó.
    ///   2. Cơ quan có vùng logic riêng (breakable: vùng chém to hơn hình, nhiều mảnh): vùng = hình chữ nhật trong `mechanics`,
    ///      từng mảnh hình = tile object có thuộc tính `art_for` = tên cơ quan.
    /// Ảnh dùng lúc chạy vẫn lấy từ Resources theo thuộc tính `sprite` (tile trong tileset ghi sẵn, object kế thừa) để giữ
    /// đúng pivot / PPU / ảnh trạng thái (sprite_lit, sprite_damaged...). Không có `sprite` thì dùng ảnh của tile.
    /// Ảnh SuperTiled2Unity tự dựng cho tile object bị tắt — cơ quan tự vẽ hình của nó.
    /// </summary>
    public sealed class MechanismArt
    {
        /// <summary>Khung hình (thế giới) — đúng khung tile object trong Tiled.</summary>
        public Rect Box { get; private set; }
        /// <summary>Thuộc tính của tile object (đã gộp thuộc tính mặc định của tile).</summary>
        public SuperCustomProperties Props { get; private set; }
        /// <summary>Ảnh của tile trong tileset (dự phòng khi không có `sprite`).</summary>
        public Sprite TileSprite { get; private set; }
        /// <summary>Lật ngang trong Tiled.</summary>
        public bool FlipX { get; private set; }
        public string Name { get; private set; }

        /// <summary>Đọc tile object (null nếu object không phải tile object) và tắt ảnh SuperTiled2Unity dựng sẵn.</summary>
        public static MechanismArt From(SuperObject so)
        {
            if (so == null || so.m_SuperTile == null) return null;
            var sr = so.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) return null;
            var b = sr.bounds;
            var art = new MechanismArt
            {
                Box = new Rect(b.min.x, b.min.y, b.size.x, b.size.y),
                Props = so.GetComponent<SuperCustomProperties>(),
                TileSprite = sr.sprite,
                FlipX = sr.transform.lossyScale.x < 0f,
                Name = so.m_TiledName,
            };
            foreach (var r in so.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
            return art;
        }

        /// <summary>Ảnh Resources theo thuộc tính `key` (mặc định `sprite`); trống / không thấy -> ảnh tile.</summary>
        public Sprite Resolve(string key = "sprite")
        {
            string path = Props != null ? Props.GetString(key, "") : "";
            int comma = path.IndexOf(',');
            if (comma >= 0) path = path.Substring(0, comma);
            path = path.Trim();
            if (path.Length > 0)
            {
                var s = Resources.Load<Sprite>(path);
                if (s != null) return s;
                Debug.LogWarning("[MechanismArt] " + Name + ": không thấy ảnh '" + path + "', dùng ảnh tile");
            }
            return TileSprite;
        }

        /// <summary>Tỉ lệ để ảnh `s` rộng đúng bằng khung (khung vẽ đúng cỡ gốc -> 1).</summary>
        public float ScaleFor(Sprite s) => s == null ? 1f : Box.width / Mathf.Max(0.0001f, s.bounds.size.x);

        /// <summary>Điểm pivot của ảnh `s` khi vẽ khít khung — vị trí đặt transform (chân vật nếu pivot ở chân).</summary>
        public Vector2 PivotFor(Sprite s)
        {
            if (s == null) return new Vector2(Box.center.x, Box.yMin);
            float px = s.pivot.x / Mathf.Max(1f, s.rect.width), py = s.pivot.y / Mathf.Max(1f, s.rect.height);
            if (FlipX) px = 1f - px;
            float h = s.bounds.size.y * ScaleFor(s);
            return new Vector2(Box.xMin + px * Box.width, Box.yMin + py * h);
        }

        /// <summary>Đặt renderer: ảnh, vị trí pivot, tỉ lệ, lật — khớp đúng chỗ vẽ trong Tiled.</summary>
        public void Apply(SpriteRenderer r, Sprite s)
        {
            if (r == null) return;
            r.sprite = s;
            r.flipX = FlipX;
            float k = ScaleFor(s);
            var p = PivotFor(s);
            r.transform.position = new Vector3(p.x, p.y, 0f);
            r.transform.localScale = new Vector3(k, k, 1f);
        }
    }
}
