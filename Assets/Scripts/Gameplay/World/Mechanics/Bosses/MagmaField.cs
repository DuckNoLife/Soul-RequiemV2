using UnityEngine;
using Unity.Collections;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SOFT CURVED LAVA SURFACE of Magma Breath, drawn into a texture built in code.
    /// - Lava shape: a grid of lava cells / safe path (bool[cols, rows]) SMOOTHLY INTERPOLATED (bilinear between cell centres) + 2-octave Perlin noise -> wavy edges,
    ///   the path becomes a curved stream, no square corners. Edges are quantised to 2-pixel blocks to match pixel art.
    /// - Lava FLOODS from the foot of the fall (pour point on the north edge) outward along an ellipse + noise (bumpy, bright yellow front), not cell by cell.
    /// - Lava pixels come from vc_lava (16 frames, Volcano pack), drifting slowly south (sense of flow); 3 px dark crust rim + bright rim.
    /// - Area about to flood: a pulsing dark red layer (same shape) as the warning.
    /// Burn checks use EXACTLY the drawing shape (`Burning`) -> wherever lava is visible, it burns. Not a MonoBehaviour (VesselDomainMechanism.Fire owns it and calls Tick).
    /// </summary>
    internal sealed class MagmaField
    {
        private const float Upt = 0.01f;                                                // 1 texture pixel = 1 source pixel (PPU 100)
        private readonly Rect area;
        private readonly bool[,] lava;
        private readonly int cols, rows, tw, th;
        private readonly float cw, ch, seed;
        private readonly Vector2 pour;
        private readonly float[] shape, reach;                                         // shape > 0: inside the lava area (depth from the edge); reach: flow distance
        private readonly Texture2D tex, warnTex;
        private readonly SpriteRenderer sr, warnSr;
        private readonly Color32[][] frames;
        private readonly int fw, fh;
        private int parity;
        public readonly GameObject Root;
        public float Reach { get; private set; }                                        // radius the lava has flowed to (units)
        public float MaxReach { get; private set; }
        /// <summary>LIGHT POOLS (centre x, y, semi-axes x, y) — lava under them COOLS into a rock crust (drawn + no burn). Must be updated every frame before Tick.
        /// Currently nothing fills this list (the tower lights became watchers), so the lava never cools.</summary>
        public readonly System.Collections.Generic.List<Vector4> Cool = new System.Collections.Generic.List<Vector4>();
        private float[] coolK;                                                          // per-pixel cooling (smooth, never toggles)

        public MagmaField(Rect area, bool[,] lavaCells, Vector2 pour, Sprite[] lavaFrames, int layer, int order, Transform parent)
        {
            this.area = area; lava = lavaCells; this.pour = pour;
            cols = lavaCells.GetLength(0); rows = lavaCells.GetLength(1);
            cw = area.width / cols; ch = area.height / rows;
            seed = Random.Range(0f, 500f);
            tw = Mathf.CeilToInt(area.width / Upt); th = Mathf.CeilToInt(area.height / Upt);
            shape = new float[tw * th]; reach = new float[tw * th];

            // lava pixels (needs vc_lava.png.meta isReadable = 1 — _tools/s421/make_s421.py)
            int n = lavaFrames != null ? lavaFrames.Length : 0;
            frames = new Color32[Mathf.Max(1, n)][];
            fw = 32; fh = 32;
            try
            {
                if (n > 0)
                {
                    var src = lavaFrames[0].texture.GetPixels32();
                    int stride = lavaFrames[0].texture.width;
                    fw = (int)lavaFrames[0].rect.width; fh = (int)lavaFrames[0].rect.height;
                    for (int i = 0; i < n; i++)
                    {
                        var r = lavaFrames[i].rect;
                        var f = new Color32[fw * fh];
                        for (int y = 0; y < fh; y++) for (int x = 0; x < fw; x++) f[y * fw + x] = src[((int)r.y + y) * stride + (int)r.x + x];
                        frames[i] = f;
                    }
                }
            }
            catch (UnityException) { n = 0; Debug.LogWarning("[MagmaField] vc_lava has Read/Write disabled -> flat-colour lava"); }
            if (n == 0)
            {
                var f = new Color32[fw * fh];
                for (int i = 0; i < f.Length; i++) f[i] = (i * 7 % 5 == 0) ? new Color32(255, 196, 60, 255) : new Color32(240, 110, 20, 255);
                frames = new[] { f };
            }

            // lava shape + flow distance, computed per 2x2 pixel block
            MaxReach = 0f;
            for (int by = 0; by < th; by += 2)
                for (int bx = 0; bx < tw; bx += 2)
                {
                    Vector2 p = new Vector2(area.xMin + (bx + 1) * Upt, area.yMin + (by + 1) * Upt);
                    float s = Shape(p), d = Dist(p);
                    if (s > 0f) MaxReach = Mathf.Max(MaxReach, d);
                    for (int y = by; y < Mathf.Min(th, by + 2); y++)
                        for (int x = bx; x < Mathf.Min(tw, bx + 2); x++) { shape[y * tw + x] = s; reach[y * tw + x] = d; }
                }
            MaxReach += 0.3f;

            Root = new GameObject("magma field");
            Root.transform.SetParent(parent, false);
            Root.transform.position = new Vector3(area.xMin, area.yMin, 0f);
            tex = NewTex("magma"); warnTex = NewTex("magma warn");
            var wd = warnTex.GetRawTextureData<Color32>();
            for (int i = 0; i < wd.Length; i++) wd[i] = shape[i] > 0f ? new Color32(150, 18, 0, 255) : new Color32(0, 0, 0, 0);
            warnTex.Apply(false);
            var cl = tex.GetRawTextureData<Color32>();
            for (int i = 0; i < cl.Length; i++) cl[i] = new Color32(0, 0, 0, 0);
            tex.Apply(false);
            warnSr = NewRenderer("warn", warnTex, layer, -16);
            sr = NewRenderer("lava", tex, layer, -15);
            warnSr.color = new Color(1f, 1f, 1f, 0f);
        }

        private Texture2D NewTex(string name) =>
            new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };

        private SpriteRenderer NewRenderer(string name, Texture2D t, int layer, int ord)
        {
            var g = new GameObject(name);
            g.transform.SetParent(Root.transform, false);
            var r = g.AddComponent<SpriteRenderer>();
            r.sprite = Sprite.Create(t, new Rect(0, 0, tw, th), Vector2.zero, 1f / Upt, 0, SpriteMeshType.FullRect);
            FxLighting.MakeUnlit(r);
            r.sortingLayerID = layer; r.sortingOrder = ord;
            return r;
        }

        // ---------------------------------------------------------------- shape (shared by drawing + burn checks)
        private float Cell(int c, int r) => lava[Mathf.Clamp(c, 0, cols - 1), Mathf.Clamp(r, 0, rows - 1)] ? 1f : 0f;

        /// <summary>&gt; 0 = inside lava (value ~ depth from the edge, 0.1 ≈ 0.1 cell).</summary>
        public float Shape(Vector2 p)
        {
            float gx = (p.x - area.xMin) / cw - 0.5f, gy = (p.y - area.yMin) / ch - 0.5f;
            int c0 = Mathf.FloorToInt(gx), r0 = Mathf.FloorToInt(gy);
            float fx = gx - c0, fy = gy - r0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);                // smooth interpolation (smoothstep) -> curved edges, no kinks
            float v = Mathf.Lerp(Mathf.Lerp(Cell(c0, r0), Cell(c0 + 1, r0), fx), Mathf.Lerp(Cell(c0, r0 + 1), Cell(c0 + 1, r0 + 1), fx), fy);
            float nz = (Mathf.PerlinNoise(seed + p.x * 1.3f, seed * 0.7f + p.y * 1.3f) - 0.5f) * 0.34f
                     + (Mathf.PerlinNoise(seed * 1.9f + p.x * 4.2f, p.y * 4.2f + 11f) - 0.5f) * 0.12f;
            return v + nz - 0.55f;
        }

        /// <summary>"Flow" distance from the pour point (ellipse: farther vertically) + noise -> bumpy front edge.</summary>
        public float Dist(Vector2 p)
        {
            Vector2 d = p - pour;
            float e = Mathf.Sqrt(d.x * d.x * 0.42f + d.y * d.y);
            return e + (Mathf.PerlinNoise(seed * 0.3f + p.x * 2.1f, p.y * 2.1f + 37f) - 0.5f) * 0.7f;
        }

        /// <summary>Hot lava at p (already reached, inside the area, slightly deeper than the crust rim).</summary>
        public bool Burning(Vector2 p) => area.Contains(p) && Shape(p) > 0.04f && Dist(p) < Reach - 0.05f && CoolAt(p) < 0.5f;

        /// <summary>Cooling at p (0 = hot lava, 1 = rock crust under a light pool; 0.25 u blend at the pool edge).</summary>
        public float CoolAt(Vector2 p)
        {
            float k = 0f;
            foreach (var e in Cool)
            {
                float dx = (p.x - e.x) / Mathf.Max(0.01f, e.z), dy = (p.y - e.y) / Mathf.Max(0.01f, e.w);
                float d = Mathf.Sqrt(dx * dx + dy * dy);                                 // 1 = pool edge
                k = Mathf.Max(k, Mathf.Clamp01((1f - d) * e.z / 0.25f));
            }
            return k;
        }

        // ---------------------------------------------------------------- drawing
        /// <param name="reach">radius flowed so far</param><param name="warn">warning layer strength 0..1</param>
        /// <param name="heat">1 = hot, 0 = cooled dark</param><param name="alpha">fade while disappearing</param>
        public void Tick(float now, float reachNow, float warn, float heat, float alpha)
        {
            Reach = reachNow;
            warnSr.color = new Color(1f, 1f, 1f, warn);
            sr.color = new Color(1f, 1f, 1f, alpha);
            var px = tex.GetRawTextureData<Color32>();
            int fi = (int)(now * 10f) % frames.Length;
            var f = frames[fi];
            int scroll = (int)(now * 9f);                                               // lava texture drifts south
            parity ^= 1;
            Color32 crust = new Color32(52, 14, 8, 255), cold = new Color32(70, 30, 24, 255);
            for (int y = 0; y < th; y++)
            {
                // even / odd rows alternate each frame (saves CPU); rows near the front edge are always drawn
                int row = y * tw;
                for (int x = 0; x < tw; x++)
                {
                    int i = row + x;
                    float s = shape[i];
                    if (s <= 0f) continue;
                    float ahead = reachNow - reach[i];                                 // > 0: lava has arrived
                    if (ahead <= 0f) { if (px[i].a != 0) px[i] = new Color32(0, 0, 0, 0); continue; }
                    if (((y ^ parity) & 1) == 1 && ahead > 0.12f && px[i].a != 0) continue;
                    Color32 c;
                    if (s < 0.03f) c = crust;                                          // dark crust rim
                    else
                    {
                        c = f[((y + scroll) % fh) * fw + (x % fw)];
                        if (s < 0.06f || ahead < 0.06f)                                  // bright rim + flowing front edge
                            c = new Color32((byte)Mathf.Min(255, c.r + 40), (byte)Mathf.Min(255, c.g + 55), (byte)Mathf.Min(255, c.b + 25), 255);
                        else if (ahead < 0.12f) c = new Color32((byte)(c.r * 0.8f), (byte)(c.g * 0.6f), (byte)(c.b * 0.5f), 255);   // thin crust behind the edge
                    }
                    if (heat < 1f) c = Color32.Lerp(cold, c, heat * heat);
                    px[i] = c;
                }
            }
            ApplyCool(px, now);
            tex.Apply(false);
        }

        /// <summary>Paint a COOLED ROCK CRUST over the lava inside light pools (warm dark rock, cracks still glowing red). Cooling changes smoothly over ~0.25 s.</summary>
        private void ApplyCool(Unity.Collections.NativeArray<Color32> px, float now)
        {
            if (coolK == null) coolK = new float[tw * th];
            float dt = Mathf.Clamp(now - lastCool, 0f, 0.1f); lastCool = now;
            float rise = dt / 0.25f;
            // only scan the bounding box of the pools + areas still cooling (coolBox)
            int x0 = tw, y0 = th, x1 = -1, y1 = -1;
            foreach (var e in Cool)
            {
                x0 = Mathf.Min(x0, Mathf.FloorToInt((e.x - e.z - area.xMin) / Upt)); x1 = Mathf.Max(x1, Mathf.CeilToInt((e.x + e.z - area.xMin) / Upt));
                y0 = Mathf.Min(y0, Mathf.FloorToInt((e.y - e.w - area.yMin) / Upt)); y1 = Mathf.Max(y1, Mathf.CeilToInt((e.y + e.w - area.yMin) / Upt));
            }
            if (hasBox) { x0 = Mathf.Min(x0, bx0); y0 = Mathf.Min(y0, by0); x1 = Mathf.Max(x1, bx1); y1 = Mathf.Max(y1, by1); }
            x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0); x1 = Mathf.Min(tw - 1, x1); y1 = Mathf.Min(th - 1, y1);
            if (x1 < x0 || y1 < y0) { hasBox = false; return; }
            bool any = false; int nx0 = tw, ny0 = th, nx1 = -1, ny1 = -1;
            Color32 rock = new Color32(46, 34, 30, 255), rockHi = new Color32(74, 56, 48, 255), ember = new Color32(196, 70, 24, 255);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * tw + x;
                    float goal = 0f;
                    if (Cool.Count > 0 && shape[i] > 0f) goal = CoolAt(new Vector2(area.xMin + (x + 0.5f) * Upt, area.yMin + (y + 0.5f) * Upt));
                    float k = coolK[i];
                    k = goal > k ? Mathf.Min(goal, k + rise) : Mathf.Max(goal, k - rise);
                    coolK[i] = k;
                    if (k <= 0.001f || px[i].a == 0) continue;
                    any = true;
                    nx0 = Mathf.Min(nx0, x); ny0 = Mathf.Min(ny0, y); nx1 = Mathf.Max(nx1, x); ny1 = Mathf.Max(ny1, y);
                    // rock crust: 3x3 pixel blocks (crisp), faint bright veins + red smouldering cracks on an offset grid
                    int cx = x / 3, cy = y / 3;
                    uint hsh = (uint)(cx * 73856093) ^ (uint)(cy * 19349663);
                    Color32 r = (hsh & 7) == 0 ? rockHi : rock;
                    bool crack = Mathf.Abs(Mathf.Sin(x * 0.11f + Mathf.Sin(y * 0.07f) * 2.3f) * Mathf.Sin(y * 0.09f + x * 0.02f)) < 0.05f;
                    if (crack) r = Color32.Lerp(rock, ember, 0.55f + 0.25f * Mathf.Sin(now * 3f + x * 0.05f));
                    float kk = k * k * (3f - 2f * k);
                    px[i] = Color32.Lerp(px[i], r, kk);
                }
            hasBox = any; bx0 = nx0; by0 = ny0; bx1 = nx1; by1 = ny1;
        }
        private float lastCool;
        private bool hasBox;
        private int bx0, by0, bx1, by1;

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            if (tex != null) Object.Destroy(tex);
            if (warnTex != null) Object.Destroy(warnTex);
        }
    }
}
