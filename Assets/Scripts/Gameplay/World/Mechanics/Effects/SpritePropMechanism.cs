using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// sprite_prop - a decoration with its own image (Resources), placed from Tiled, with a blocking base and an optional "lit" state.
    /// Used for the seal pillars in the Containment Vault (hiding behind them from Specimen Zero's wail), reusable for other maps.
    /// Tiled properties (object in the mechanics layer; object feet = middle of the object area's bottom edge):
    ///   sprite        normal image (e.g. VFX/specimen/ward_stone0). Required. Image pivot = object feet.
    ///   sprite_lit    lit image (empty = use `sprite`)
    ///   group         group name so code can find it again (e.g. sz_ward) - <see cref="Group"/>
    ///   collider      "width,height" (world units) of the blocking box at the feet; "0" = no blocking. Default 0.34,0.12
    ///   collider_shape "box" (default, square-based objects) | "round" (round-based objects: barrels, pots, round pillars, rock piles...)
    ///                 round = horizontal CapsuleCollider2D (width = height gives a circle) -> the player slides around the edge without snagging corners.
    ///   light_color   light colour when lit (#rrggbb, default candle yellow), light_radius (default 1.6)
    ///   decal         image drawn on the floor (e.g. VFX/specimen/ward_sigil) - seal ring; decal_at "x,y" (Tiled tile, ring centre);
    ///   decal_size    diameter (units, default 0.8), decal_squash (vertical squash, default 0.6), decal_alpha (normal state, 0.14)
    ///   icon          icon image of the object (Resources) for code use - e.g. each seal pillar's own rune shown under the boss
    ///   flat          true = the object lies flat on the floor (bones, anchor rings...) -> always drawn below characters, not sorted by feet
    /// DEFAULT PLACEMENT = tile objects in the `mechanics_art` layer, dragged from the SR-mech-art.tsx tileset (visible in Tiled).
    ///   Tiles already carry class sprite_prop + `sprite` / `sprite_lit` / `collider` / `flat`; objects only set what differs from the default.
    ///   The image fits the tile object frame (scaling / flipping in Tiled = scaling / flipping in game; collider + glow ring scale with it).
    ///   The old way (rectangle + `sprite`, feet = middle of the bottom edge) still works for maps not yet converted.
    /// No saved state - the light is turned on / off by code calling SetLit (e.g. a boss).
    /// </summary>
    public sealed class SpritePropMechanism : Mechanism
    {
        private static readonly List<SpritePropMechanism> all = new List<SpritePropMechanism>();

        /// <summary>Objects of the group on the current map (skipping ones destroyed by a map change).</summary>
        public static List<SpritePropMechanism> Group(string group)
        {
            all.RemoveAll(p => p == null || p.Body == null);
            var list = new List<SpritePropMechanism>();
            foreach (var p in all) if (p.group == group) list.Add(p);
            return list;
        }

        public SpriteRenderer Body { get; private set; }
        public SpriteRenderer Decal { get; private set; }
        public Light2D Glow { get; private set; }
        /// <summary>Object feet (world).</summary>
        public Vector2 Foot { get; private set; }
        /// <summary>Centre of the floor seal ring (world); no decal = object feet.</summary>
        public Vector2 DecalAt { get; private set; }
        public float DecalAlpha { get; private set; }
        public bool Lit { get; private set; }
        /// <summary>Icon image (`icon`), null if none.</summary>
        public Sprite Icon { get; private set; }

        private string group;
        private Sprite normal, lit;

        public override void Init()
        {
            group = Str("group", "");
            string icon = Str("icon", "");
            Icon = icon.Length > 0 ? Resources.Load<Sprite>(icon) : null;
            var art = Art.Count > 0 ? Art[0] : null;
            normal = art != null ? art.Resolve("sprite") : Resources.Load<Sprite>(Str("sprite", ""));
            string litPath = Str("sprite_lit", "");
            lit = litPath.Length > 0 ? Resources.Load<Sprite>(litPath) : null;
            if (normal == null) { Debug.LogWarning("[SpriteProp] " + Name + ": image not found '" + Str("sprite", "") + "'"); return; }

            Foot = art != null ? art.PivotFor(normal) : new Vector2(Rect.center.x, Rect.yMin);
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;

            var go = new GameObject("Prop " + Name);
            go.transform.SetParent(Ctx.Host.transform, false);
            go.transform.position = new Vector3(Foot.x, Foot.y, 0f);
            Body = go.AddComponent<SpriteRenderer>();
            Body.sprite = normal;
            if (art != null) art.Apply(Body, normal);                          // match the drawing in Tiled (position, scale, flip)
            Body.spriteSortPoint = SpriteSortPoint.Pivot;                      // same layer as characters: Ren standing north of a pillar is hidden behind it
            if (psr != null) { Body.sortingLayerID = psr.sortingLayerID; Body.sortingOrder = psr.sortingOrder; Body.sharedMaterial = psr.sharedMaterial; }
            if (Props.GetBool("flat", false)) Body.sortingOrder -= 6;           // lying flat on the floor: below characters' feet
            // `scale` (rectangle placement, no tile art) - scales the image; `flip` = flip horizontally
            float sc = Float("scale", 1f);
            if (art == null && Mathf.Abs(sc - 1f) > 0.001f) go.transform.localScale = new Vector3(sc, sc, 1f);
            if (Props.GetBool("flip", false)) Body.flipX = true;

            string col = Str("collider", "0.34,0.12");
            var wh = col.Split(',');
            if (wh.Length == 2 && float.TryParse(wh[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w) &&
                float.TryParse(wh[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h) && w > 0f && h > 0f)
            {
                if (Str("collider_shape", "box").ToLowerInvariant() == "round")
                {
                    var cap = go.AddComponent<CapsuleCollider2D>();            // round base - no square corners to snag on
                    cap.direction = w >= h ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                    cap.size = new Vector2(w, h);
                    cap.offset = new Vector2(0f, h * 0.5f);
                }
                else
                {
                    var box = go.AddComponent<BoxCollider2D>();                // no Rigidbody -> static obstacle
                    box.size = new Vector2(w, h);
                    box.offset = new Vector2(0f, h * 0.5f);
                }
            }

            DecalAt = Foot;
            string decal = Str("decal", "");
            var ds = decal.Length > 0 ? Resources.Load<Sprite>(decal) : null;
            if (ds != null)
            {
                DecalAt = Ctx.TilePoint(Str("decal_at", ""), Foot);
                DecalAlpha = Float("decal_alpha", 0.14f);
                var dg = new GameObject("Decal " + Name);
                dg.transform.SetParent(go.transform, true);
                dg.transform.position = new Vector3(DecalAt.x, DecalAt.y, 0f);
                float size = Float("decal_size", 0.8f), squash = Float("decal_squash", 0.6f);
                float k = size / Mathf.Max(0.01f, ds.bounds.size.x);
                float parentK = Mathf.Max(0.0001f, go.transform.localScale.x);   // the seal ring keeps its `decal_size` even when the object is scaled up
                dg.transform.localScale = new Vector3(k / parentK, k * squash / parentK, 1f);
                Decal = dg.AddComponent<SpriteRenderer>();
                Decal.sprite = ds;
                FxLighting.MakeUnlit(Decal);                                   // self-lit gold strokes, not swallowed by the vault's darkness
                Decal.sortingLayerID = Body.sortingLayerID;
                Decal.sortingOrder = -4;                                       // above floor decals, below characters
                Decal.color = new Color(1f, 1f, 1f, DecalAlpha);
            }

            var lg = new GameObject("Glow " + Name);
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            Glow = lg.AddComponent<Light2D>();
            Glow.lightType = Light2D.LightType.Point;
            Glow.color = MechanismContext.ParseColor(Str("light_color", "#ffd38a"), 1f);
            Glow.pointLightOuterRadius = Float("light_radius", 1.6f);
            Glow.pointLightInnerRadius = 0.05f;
            Glow.intensity = 0f;
            Glow.enabled = false;
            all.Add(this);
            // `lit` = lit from the start (decorations with lights: soul braziers, crystal clusters) - `light_intensity` (default 0.8)
            if (Props.GetBool("lit", false)) { SetLit(true); Glow.intensity = Float("light_intensity", 0.8f); }
        }

        /// <summary>Turn the lit state on / off (swaps the image). The light and seal ring brightness are driven by the caller through Glow / Decal.</summary>
        public void SetLit(bool on)
        {
            if (Body == null) return;
            Lit = on;
            Body.sprite = on && lit != null ? lit : normal;
            Glow.enabled = on;
            if (!on)
            {
                Glow.intensity = 0f;
                if (Decal != null) { Decal.color = new Color(1f, 1f, 1f, DecalAlpha); Decal.transform.localRotation = Quaternion.identity; }
            }
        }
    }
}
