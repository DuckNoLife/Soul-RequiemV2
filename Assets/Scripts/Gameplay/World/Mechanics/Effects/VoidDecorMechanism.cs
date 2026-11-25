using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// void_decor - multi-layer VOID BACKGROUND for The Unwritten domain (one rectangle covering the whole map, mechanics layer).
    /// From far to near:
    ///   1. purple nebulae (Resources/VFX/void/nebula_0..2) - large, very faint colour patches drifting extremely slowly, strongest parallax
    ///   2. tiny twinkling stars (2 sizes), a few "breathing" slowly
    ///   3. a GIANT SIGIL RING turning slowly behind the island (drawn in code: 2 circles + 32 tick marks + 8 diamond glyphs) - peeking out around the island edge
    ///   4. void drafts: thin light streaks flying straight up from below (as if the island were being pulled up)
    /// Properties: nebula (6), stars (80), streaks (16), ring_at "x,y" (Tiled tile, ring centre; empty = area centre), ring_radius (tiles, 17), ring_alpha (0.1),
    ///   avoid "x,y,w,h" (Tiled tiles) - streaks do not fly over the island floor.
    /// Everything is unlit, drawn below the tile layers (order -60..-41), no collision.
    /// </summary>
    public sealed class VoidDecorMechanism : Mechanism
    {
        private class Item { public SpriteRenderer r; public Vector2 at, vel; public float depth, phase, freq, baseA, life, born; public Sprite orig, gray; }

        private Transform root, ring;
        private SpriteRenderer ringR;
        private readonly List<Item> nebulae = new List<Item>(), stars = new List<Item>(), streaks = new List<Item>();
        private Rect area, avoid;
        private bool hasAvoid;
        private Vector3 camStart; private bool hasCam;
        private float ringAlpha;
        private static Sprite px1, px2, streakS;

        public override void Init()
        {
            Tint = Color.white;
            DomainTone.Reset();                                                        // new scene -> original images
            area = Rect;
            root = new GameObject("Void Decor " + Name).transform;
            root.SetParent(Ctx.Host.transform, false);
            if (px1 == null) { px1 = MakeRect(1, 1, "void_px1"); px2 = MakeRect(2, 2, "void_px2"); streakS = MakeStreak(); }

            var neb = new List<Sprite>();
            for (int i = 0; i < 3; i++) { var s = Resources.Load<Sprite>("VFX/void/nebula_" + i); if (s != null) neb.Add(s); }
            int nn = neb.Count > 0 ? Int("nebula", 6) : 0;
            for (int i = 0; i < nn; i++)
            {
                var it = NewItem("nebula", neb[i % neb.Count], -60);
                it.orig = neb[i % neb.Count]; it.gray = DomainTone.Gray("VFX/void/nebula_" + (i % neb.Count));
                it.at = RandIn(area); it.depth = Random.Range(0.75f, 1f);
                it.vel = new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.01f, 0.01f));
                float sc = Random.Range(3.2f, 5.2f);
                it.r.transform.localScale = new Vector3(sc * (Random.value < 0.5f ? -1f : 1f), sc, 1f);
                it.baseA = Random.Range(0.16f, 0.3f); it.phase = Random.Range(0f, 10f); it.freq = Random.Range(0.03f, 0.06f);
                nebulae.Add(it);
            }
            int ns = Int("stars", 80);
            for (int i = 0; i < ns; i++)
            {
                var it = NewItem("star", Random.value < 0.18f ? px2 : px1, -58);
                it.at = RandIn(area); it.depth = Random.Range(0.85f, 1f);
                it.baseA = Random.Range(0.25f, 0.85f); it.phase = Random.Range(0f, 10f);
                it.freq = Random.value < 0.15f ? Random.Range(0.15f, 0.35f) : Random.Range(0.8f, 2.4f);
                float tint = Random.value;
                it.r.color = tint < 0.6f ? new Color(0.85f, 0.75f, 1f) : tint < 0.85f ? new Color(0.6f, 0.75f, 1f) : new Color(1f, 0.7f, 0.95f);
                stars.Add(it);
            }

            ringAlpha = Float("ring_alpha", 0.1f);
            float rr = Float("ring_radius", 17f) * 0.32f;
            Vector2 rc = Ctx.TilePoint(Str("ring_at", ""), area.center);
            ring = new GameObject("rune ring").transform;
            ring.SetParent(root, false);
            ring.position = new Vector3(rc.x, rc.y, 0f);
            ringR = ring.gameObject.AddComponent<SpriteRenderer>();
            ringR.sprite = MakeRing(Mathf.RoundToInt(rr * 100f));
            FxLighting.MakeUnlit(ringR);
            ringR.sortingOrder = -50;
            ring.localScale = new Vector3(1f, 0.62f, 1f);                                  // tilted to match the top-down view angle

            string av = Str("avoid", "");
            var v = av.Split(',');
            if (v.Length == 4 && float.TryParse(v[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ax) &&
                float.TryParse(v[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ay) &&
                float.TryParse(v[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float aw) &&
                float.TryParse(v[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ah))
            {
                Vector3 a = Ctx.Director.TileToWorld(ax, ay), b = Ctx.Director.TileToWorld(ax + aw, ay + ah);
                avoid = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                hasAvoid = true;
            }
            int nk = Int("streaks", 16);
            for (int i = 0; i < nk; i++) { var it = NewItem("streak", streakS, -41); ResetStreak(it, true); streaks.Add(it); }
        }

        private Item NewItem(string n, Sprite s, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(root, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s; FxLighting.MakeUnlit(r); r.sortingOrder = order;
            return new Item { r = r };
        }

        private static Vector2 RandIn(Rect r) => new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));

        private void ResetStreak(Item it, bool anyAge)
        {
            for (int k = 0; k < 20; k++)
            {
                it.at = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.center.y));
                if (!hasAvoid || !avoid.Contains(it.at)) break;
            }
            it.vel = new Vector2(0f, Random.Range(0.5f, 1.2f));
            it.life = Random.Range(2.5f, 5f);
            it.born = Time.time - (anyAge ? Random.Range(0f, it.life) : 0f);
            it.depth = Random.Range(0.3f, 0.8f);
            it.baseA = Random.Range(0.12f, 0.3f);
            float l = Random.Range(0.6f, 1.4f);
            it.r.transform.localScale = new Vector3(1f, l, 1f);
        }

        /// <summary>Tint nebulae / streaks / sigil ring by the Vessel's phase (set by the domain; Init resets to white).</summary>
        public static Color Tint = Color.white;

        public override void Animate()
        {
            if (root == null) return;
            float now = Time.time, dt = Time.deltaTime;
            var cam = Camera.main;
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            if (!hasCam && cam != null) { camStart = cp; hasCam = true; }
            Vector2 cd = hasCam ? (Vector2)(cp - camStart) : Vector2.zero;

            foreach (var it in nebulae)
            {
                it.at += it.vel * dt;
                Vector2 p = it.at + cd * Mathf.Lerp(0.5f, 0.8f, it.depth);
                it.r.transform.position = new Vector3(p.x, p.y, 0f);
                float a = it.baseA * (0.75f + 0.25f * Mathf.Sin((now + it.phase) * it.freq * Mathf.PI * 2f));
                bool g = DomainTone.Active && it.gray != null;                         // grey nebula x phase colour
                var want = g ? it.gray : it.orig;
                if (want != null && it.r.sprite != want) it.r.sprite = want;
                it.r.color = g ? new Color(DomainTone.Tint.r, DomainTone.Tint.g, DomainTone.Tint.b, a) : new Color(Tint.r, Tint.g, Tint.b, a);
            }
            foreach (var it in stars)
            {
                Vector2 p = it.at + cd * Mathf.Lerp(0.6f, 0.85f, it.depth);
                it.r.transform.position = new Vector3(p.x, p.y, 0f);
                var c = it.r.color;
                float tw = 0.5f + 0.5f * Mathf.Sin((now + it.phase) * it.freq * Mathf.PI * 2f);
                c.a = it.baseA * (0.35f + 0.65f * tw * tw);
                it.r.color = c;
            }
            foreach (var it in streaks)
            {
                float age = now - it.born;
                if (age > it.life || (hasAvoid && avoid.Contains(it.at))) { ResetStreak(it, false); continue; }
                it.at += it.vel * dt;
                Vector2 p = it.at + cd * Mathf.Lerp(0.1f, 0.4f, it.depth);
                it.r.transform.position = new Vector3(p.x, p.y, 0f);
                float u = age / it.life;
                Color sc = DomainTone.Active ? DomainTone.Glow : new Color(0.75f, 0.55f, 1f);
                it.r.color = new Color(sc.r, sc.g, sc.b, it.baseA * Mathf.Sin(u * Mathf.PI));
            }
            RotateRing(now);
        }

        // rotating sigil ring: swapping sprites per angle is costly - rotate the transform of a child inside a squashed parent instead
        private Transform spin;
        private void RotateRing(float now)
        {
            if (ringR == null) return;
            if (spin == null)
            {
                spin = new GameObject("spin").transform;
                spin.SetParent(ring, false);
                var r2 = spin.gameObject.AddComponent<SpriteRenderer>();
                r2.sprite = ringR.sprite; FxLighting.MakeUnlit(r2); r2.sortingOrder = ringR.sortingOrder;
                ringR.enabled = false;
                ringR = r2;
            }
            spin.localRotation = Quaternion.Euler(0f, 0f, now * 2.2f);                     // ~2.7 minutes per turn
            float a = ringAlpha * (0.8f + 0.2f * Mathf.Sin(now * 0.6f));
            Color rc = DomainTone.Active ? DomainTone.Glow : new Color(0.72f, 0.48f, 1f);
            ringR.color = new Color(rc.r, rc.g, rc.b, a);
        }

        // ------------------------------------------------------------------ code-drawn images
        private static Sprite MakeRect(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = name };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Draft streak: 1 x 12 px, bright at the top, fading toward the tail.</summary>
        private static Sprite MakeStreak()
        {
            const int H = 12;
            var tex = new Texture2D(1, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "void_streak" };
            var px = new Color32[H];
            for (int y = 0; y < H; y++) px[y] = new Color32(255, 255, 255, (byte)(255 * (y + 1) / H));
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 1f), 100f);
        }

        /// <summary>Sigil ring: 2 concentric circles, 32 tick marks, 8 diamonds - 1 px white strokes (tinted when drawn).</summary>
        private static Sprite MakeRing(int r)
        {
            int size = r * 2 + 8, c = size / 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "void_ring" };
            var px = new Color32[size * size];
            void Dot(int x, int y, byte a) { if (x >= 0 && y >= 0 && x < size && y < size && px[y * size + x].a < a) px[y * size + x] = new Color32(255, 255, 255, a); }
            void Circle(float rad, byte a) { int n = Mathf.CeilToInt(rad * 6.3f); for (int i = 0; i < n; i++) { float t = i * Mathf.PI * 2f / n; Dot(c + Mathf.RoundToInt(Mathf.Cos(t) * rad), c + Mathf.RoundToInt(Mathf.Sin(t) * rad), a); } }
            Circle(r, 255); Circle(r - 3, 160); Circle(r * 0.86f, 200);
            for (int k = 0; k < 32; k++)
            {
                float t = k * Mathf.PI * 2f / 32f; int len = k % 4 == 0 ? 10 : 5;
                for (int d = 0; d < len; d++) Dot(c + Mathf.RoundToInt(Mathf.Cos(t) * (r * 0.86f + 2 + d)), c + Mathf.RoundToInt(Mathf.Sin(t) * (r * 0.86f + 2 + d)), 220);
            }
            for (int k = 0; k < 8; k++)
            {
                float t = (k + 0.5f) * Mathf.PI * 2f / 8f; float rm = r * 0.93f;
                int cx = c + Mathf.RoundToInt(Mathf.Cos(t) * rm), cy = c + Mathf.RoundToInt(Mathf.Sin(t) * rm);
                for (int d = -4; d <= 4; d++) { int w = 4 - Mathf.Abs(d); Dot(cx - w, cy + d, 255); Dot(cx + w, cy + d, 255); }
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
