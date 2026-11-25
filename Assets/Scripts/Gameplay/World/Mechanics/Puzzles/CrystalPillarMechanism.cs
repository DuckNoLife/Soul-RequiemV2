using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// crystal_pillar - crystal source of The Reliquary Lists (Map 4). Property `crystal`:
    /// crimson (fire) | azure (ice) | verdant (life / poison) | gilded (holy light).
    ///   Images are named crystal_shelf_* (not crystal_pillar_*, because Tiled caches images by file name).
    ///   Look = teal STONE SHELF with a brass frame (PixelLab, _tools/m4duo_1119/s353/make_shelves.py): a large crystal on the top tier, small shards below,
    ///   an ENGRAVED DISC at the shelf base carrying the element symbol (flame / snowflake / leaf / sun).
    ///   3 LIGHT layers: (1) self-lit layer crystal_shelf_<c>_lit drawn Unlit over the crystal + symbol (bright in the dark);
    ///   (2) a soft Unlit glow in the crystal colour around the crystal; (3) a crystal-coloured Light2D cast on the floor. All three breathe with the state.
    /// Three states:
    ///   Dormant : soft light, slow breathing.
    ///   Called  : the Hollow Champion CALLS this shelf (bright flare, fast pulse, element aura ring at its base) -> RACE: Ren gets there first and presses T ("Attune")
    ///             -> Ren absorbs the charm (RelicBoons) and the Champion arrives empty-handed -> dismayed; the Champion gets there first -> DRAINS it for 1.1 s (always succeeds).
    ///   Spent   : crystal drained (grey, symbol off, light off) - the shelves re-forge once all four are drained.
    /// Solid collider at the shelf base. Fight reset (ZoneReset) -> Dormant, Ren's charm is lost.
    /// </summary>
    public sealed class CrystalPillarMechanism : Mechanism
    {
        public enum State { Dormant, Called, Spent }
        public State Now { get; private set; } = State.Dormant;
        public string Crystal => Str("crystal", "crimson");
        public Vector2 Stand { get; private set; }
        public bool ClaimedByRen { get; private set; }
        public Color Tint => ColorOf(Crystal);
        /// <summary>Centre of the large crystal on the shelf (where the drain beam starts) - 74% of the shelf image height.</summary>
        public Vector2 Tip => new Vector2(Rect.center.x, Rect.yMin + 0.02f + Height * 0.74f);

        private SpriteRenderer sr, lit, halo;
        private Sprite full, spent;
        private Light2D glow;
        private SpriteVfx aura;
        private float Scale => Float("scale", 1f);   // 1:1 scale like the tiles (no mixels)
        private float Height => (full != null ? full.bounds.size.y : 0.78f) * Scale;
        private static Sprite haloSprite;

        public static Color ColorOf(string c) =>
            c == "crimson" ? new Color(1f, 0.35f, 0.25f) : c == "azure" ? new Color(0.4f, 0.78f, 1f) :
            c == "verdant" ? new Color(0.45f, 1f, 0.4f) : c == "gilded" ? new Color(1f, 0.85f, 0.35f) : new Color(0.72f, 0.42f, 1f);

        public override string DefaultVerb => "Attune";
        public override bool Usable => Now == State.Called;

        public override void Init()
        {
            full = WardenStatueMechanism.Load("VFX/props/crystal_shelf_" + Crystal);
            spent = WardenStatueMechanism.Load("VFX/props/crystal_shelf_" + Crystal + "_spent");
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Crystal Shelf " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = full;
            sr.transform.localScale = new Vector3(Scale, Scale, 1f);
            // (1) self-lit layer: crystal + symbol, same origin, drawn right above the shelf
            var lg0 = new GameObject("CrystalLit " + Name);
            lg0.transform.SetParent(sr.transform, false);
            lit = lg0.AddComponent<SpriteRenderer>();
            lit.sprite = WardenStatueMechanism.Load("VFX/props/crystal_shelf_" + Crystal + "_lit");
            lit.spriteSortPoint = SpriteSortPoint.Pivot;
            lit.sortingLayerID = sr.sortingLayerID; lit.sortingOrder = sr.sortingOrder + 1;
            FxLighting.MakeUnlit(lit);
            // (2) soft glow around the crystal
            var hg = new GameObject("CrystalHalo " + Name);
            hg.transform.SetParent(sr.transform, false);
            hg.transform.position = Tip;
            halo = hg.AddComponent<SpriteRenderer>();
            halo.sprite = HaloSprite;
            halo.sortingLayerID = sr.sortingLayerID; halo.sortingOrder = sr.sortingOrder + 2;
            FxLighting.MakeUnlit(halo);
            // (3) crystal-coloured light cast on the floor
            var lg = new GameObject("CrystalLight " + Name);
            lg.transform.SetParent(sr.transform, false);
            lg.transform.position = Tip;
            glow = lg.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = Tint;
            glow.pointLightInnerRadius = 0.15f;
            glow.pointLightOuterRadius = 2.2f;
            glow.falloffIntensity = 0.6f;
            var go = new GameObject("CrystalCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            go.AddComponent<BoxCollider2D>().size = Rect.size;
            var arena = MapMechanics.Instance != null ? MapMechanics.Instance.MechRect("duo_arena") : null;
            bool south = arena.HasValue && arena.Value.center.y > Rect.center.y;
            Stand = new Vector2(Rect.center.x, south ? Rect.yMax + 0.2f : Rect.yMin - 0.2f);
            MapEncounterDirector.ZoneReset += OnZoneReset;
            SetDormant();
        }

        /// <summary>Soft round glow (drawn in code, 48 px) - tinted with the crystal colour.</summary>
        private static Sprite HaloSprite
        {
            get
            {
                if (haloSprite != null) return haloSprite;
                const int n = 48;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        a = Mathf.Round(a * a * 5f) / 5f;                  // 5 steps - keeps the pixel look, no smooth gradient
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                haloSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return haloSprite;
            }
        }

        public override void Animate()
        {
            if (glow == null) return;
            float t = Time.time;
            if (Now == State.Called)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                glow.intensity = 1.5f + 0.8f * k;
                SetHalo(0.42f + 0.18f * k, 1.25f + 0.15f * k);
                if (sr != null) sr.color = Color.Lerp(Color.white, Color.Lerp(Color.white, Tint, 0.35f), k);   // the shelf blushes with the crystal colour in rhythm
            }
            else if (Now == State.Dormant)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 2f + Rect.x);
                glow.intensity = 0.85f + 0.2f * k;
                SetHalo(0.2f + 0.08f * k, 0.95f);
            }
        }

        private void SetHalo(float alpha, float size)
        {
            if (halo == null) return;
            halo.enabled = alpha > 0.01f;
            halo.color = new Color(Tint.r, Tint.g, Tint.b, alpha);
            halo.transform.localScale = new Vector3(size, size, 1f);
        }

        private void SetDormant()
        {
            Now = State.Dormant; ClaimedByRen = false;
            if (sr != null) { sr.sprite = full; sr.color = Color.white; }
            if (lit != null) { lit.enabled = true; lit.color = Color.white; }
            if (glow != null) { glow.enabled = true; glow.pointLightOuterRadius = 2.2f; }
            StopAura();
        }

        private void StopAura() { if (aura != null) { aura.Stop(); aura = null; } }

        /// <summary>The Champion calls the crystal: the shelf flares, element aura ring at its base, the race begins.</summary>
        public void Call()
        {
            if (Now != State.Dormant) return;
            Now = State.Called; ClaimedByRen = false;
            if (glow != null) glow.pointLightOuterRadius = 3.2f;
            Sfx.Play("rack_unseal", Rect.center);
            StopAura();
            aura = SpriteVfx.Spawn("duo_aura_" + Crystal, new Vector2(Rect.center.x, Rect.yMin - 0.04f), 1.3f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) - 1, false, 12f, false);
            if (aura != null) aura.Looping(9999f);
        }

        /// <summary>Ren presses T: absorbs the crystal.</summary>
        public override void Use()
        {
            if (Now != State.Called) return;
            ClaimedByRen = true;
            Drain();
            Sfx.Play("relic_claim", Rect.center);
            var bv = SpriteVfx.Spawn("m3_break", Tip, 0.9f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 3, false, 14f, false);
            if (bv != null) bv.Tint(Tint);
            RelicBoons.Grant(Crystal, Tip);
        }

        /// <summary>The Champion got there first: starts draining (the crystal is spent). true if allowed.</summary>
        public bool TakeByChampion()
        {
            if (Now != State.Called) return false;
            Drain();
            return true;
        }

        private void Drain()
        {
            Now = State.Spent;
            if (sr != null) { sr.sprite = spent; sr.color = Color.white; }
            if (lit != null) lit.enabled = false;
            if (glow != null) glow.enabled = false;
            SetHalo(0f, 1f);
            StopAura();
        }

        /// <summary>Re-forge (once all four shelves are drained).</summary>
        public void Rearm() { if (Now == State.Spent) SetDormant(); }

        /// <summary>The Champion falls: remaining crystals open for Ren to absorb.</summary>
        public void Release() { if (Now == State.Dormant) Call(); }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (zone.Contains(Rect.center)) { SetDormant(); RelicBoons.Clear(); }
        }

        internal override void DebugSolve() { }
    }
}
