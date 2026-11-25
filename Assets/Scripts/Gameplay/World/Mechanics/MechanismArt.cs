using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Image of a mechanism, placed in Tiled as a TILE OBJECT (dragged from the SR-mech-art.tsx tileset) instead of an empty rectangle
    /// with a `sprite` property - so the user can SEE and drag the object right inside Tiled.
    ///
    /// Two ways to use it (see ARCHITECTURE 2.5 "Mechanism images"):
    ///   1. The mechanism itself is a tile object (sprite_prop, note, passage): tile object frame = image frame; mechanism area (Rect) = that frame.
    ///   2. The mechanism has its own logic area (breakable: hit area larger than the image, several pieces): area = rectangle in `mechanics`,
    ///      each image piece = a tile object with property `art_for` = mechanism name.
    /// The runtime image still comes from Resources by property `sprite` (set on the tileset tile, inherited by the object) to keep
    /// the right pivot / PPU / state images (sprite_lit, sprite_damaged...). No `sprite` -> the tile's image is used.
    /// The image SuperTiled2Unity builds for the tile object is turned off - the mechanism draws its own.
    /// </summary>
    public sealed class MechanismArt
    {
        /// <summary>Image frame (world) - exactly the tile object frame in Tiled.</summary>
        public Rect Box { get; private set; }
        /// <summary>Tile object properties (merged with the tile's default properties).</summary>
        public SuperCustomProperties Props { get; private set; }
        /// <summary>The tile's image in the tileset (fallback when there is no `sprite`).</summary>
        public Sprite TileSprite { get; private set; }
        /// <summary>Flipped horizontally in Tiled.</summary>
        public bool FlipX { get; private set; }
        public string Name { get; private set; }

        /// <summary>Read the tile object (null if the object is not a tile object) and turn off the image SuperTiled2Unity pre-built.</summary>
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

        /// <summary>Resources image from property `key` (default `sprite`); empty / not found -> the tile image.</summary>
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
                Debug.LogWarning("[MechanismArt] " + Name + ": image not found '" + path + "', using the tile image");
            }
            return TileSprite;
        }

        /// <summary>Scale so image `s` is exactly as wide as the frame (frame drawn at the original size -> 1).</summary>
        public float ScaleFor(Sprite s) => s == null ? 1f : Box.width / Mathf.Max(0.0001f, s.bounds.size.x);

        /// <summary>Pivot point of image `s` when drawn to fit the frame - where the transform goes (the object feet if the pivot is at the feet).</summary>
        public Vector2 PivotFor(Sprite s)
        {
            if (s == null) return new Vector2(Box.center.x, Box.yMin);
            float px = s.pivot.x / Mathf.Max(1f, s.rect.width), py = s.pivot.y / Mathf.Max(1f, s.rect.height);
            if (FlipX) px = 1f - px;
            float h = s.bounds.size.y * ScaleFor(s);
            return new Vector2(Box.xMin + px * Box.width, Box.yMin + py * h);
        }

        /// <summary>Set up the renderer: image, pivot position, scale, flip - matching exactly where it is drawn in Tiled.</summary>
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
