using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Art for THE MARGIN fight (The Registrar).
    ///   - PixelLab images in Resources/VFX/props/margin_*.png (ledger maws, bookshelves, ink pots, ink pools...) — <see cref="Prop"/>.
    ///   - Code-drawn shapes (PPU 100, Point filter): violet beam, lift sigil, landing ring, bookshelf arrow, plain pixel to stretch.
    /// Every Sprite is cached, never rebuilt per attack.
    /// </summary>
    internal static class MarginArt
    {
        public static readonly Color Violet = new Color(0.66f, 0.42f, 1f);
        public static readonly Color VioletDim = new Color(0.42f, 0.26f, 0.7f);
        public static readonly Color Ink = new Color(0.16f, 0.09f, 0.24f);

        private static readonly Dictionary<string, Sprite> props = new Dictionary<string, Sprite>();
        private static Sprite pixel, beam, sigil, ring, chevron, quillLine;

        /// <summary>Prop image Resources/VFX/props/&lt;name&gt; (first frame). Missing -> null.</summary>
        public static Sprite Prop(string name)
        {
            if (props.TryGetValue(name, out var s) && s != null) return s;
            var all = Resources.LoadAll<Sprite>("VFX/props/" + name);
            s = all.Length > 0 ? all[0] : null;
            if (s == null) Debug.LogWarning("[Margin] Missing image Resources/VFX/props/" + name + ".png (run Build Map 4 Enemies to slice the art)");
            props[name] = s;
            return s;
        }

        private static Sprite Make(int w, int h, Color32[] px, Vector2 pivot, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 100f);
        }

        /// <summary>1 px white square, centred pivot — stretch into strips, lines, dark overlays (scale = size * 100).</summary>
        public static Sprite Pixel()
        {
            if (pixel != null) return pixel;
            return pixel = Make(1, 1, new[] { new Color32(255, 255, 255, 255) }, new Vector2(0.5f, 0.5f), "margin_px");
        }

        /// <summary>Violet beam body: 4 x 24 px, pivot at the LEFT middle. White-violet core, violet edge fading out. Length = scale.x * 0.04, thickness = scale.y * 0.24.</summary>
        public static Sprite Beam()
        {
            if (beam != null) return beam;
            const int W = 4, H = 24;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float d = Mathf.Abs(y - (H - 1) * 0.5f) / (H * 0.5f);      // 0 centre -> 1 edge
                Color c = d < 0.22f ? new Color(0.97f, 0.9f, 1f, 1f)
                        : d < 0.5f ? Color.Lerp(new Color(0.86f, 0.62f, 1f, 1f), new Color(0.62f, 0.34f, 0.98f, 1f), (d - 0.22f) / 0.28f)
                        : new Color(0.5f, 0.24f, 0.9f, Mathf.Clamp01(1f - (d - 0.5f) / 0.5f) * 0.8f);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
            return beam = Make(W, H, px, new Vector2(0f, 0.5f), "margin_beam");
        }

        /// <summary>Lift sigil (64 px): double ring + 12 rune-like ticks, violet.</summary>
        public static Sprite Sigil()
        {
            if (sigil != null) return sigil;
            const int N = 64;
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            var main = new Color32(186, 120, 255, 230); var dim = new Color32(110, 60, 190, 170);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
                    bool outer = d > 28.5f && d < 31f;
                    bool inner = d > 21.5f && d < 23f;
                    bool rune = d >= 23.5f && d <= 28f && (Mathf.Repeat(a, 30f) < 4f || (Mathf.Repeat(a + 9f, 30f) < 2f && d < 25.5f));
                    bool core = d < 3f;
                    if (outer || rune) px[y * N + x] = main;
                    else if (inner || core) px[y * N + x] = dim;
                }
            return sigil = Make(N, N, px, new Vector2(0.5f, 0.5f), "margin_sigil");
        }

        /// <summary>Thin ring (40 px) marking a landing spot / where an ink pot breaks.</summary>
        public static Sprite Ring()
        {
            if (ring != null) return ring;
            const int N = 40;
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    if (d > 16.5f && d < 19f) px[y * N + x] = new Color32(255, 255, 255, 235);
                    else if (Mathf.Abs(x - c) < 1f && d < 6f || Mathf.Abs(y - c) < 1f && d < 6f) px[y * N + x] = new Color32(255, 255, 255, 180);
                }
            return ring = Make(N, N, px, new Vector2(0.5f, 0.5f), "margin_ring");
        }

        /// <summary>Warning arrow for a charging bookshelf (16 px, points RIGHT) — rotate to the direction.</summary>
        public static Sprite Chevron()
        {
            if (chevron != null) return chevron;
            const int N = 16;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int dy = Mathf.Abs(y - 7);
                    bool on = (x - 3 - dy >= 0 && x - 3 - dy < 4) && dy < 7;
                    if (on) px[y * N + x] = new Color32(255, 255, 255, 255);
                }
            return chevron = Make(N, N, px, new Vector2(0.5f, 0.5f), "margin_chevron");
        }

        private static readonly Dictionary<long, Sprite> torn = new Dictionary<long, Sprite>();

        /// <summary>TORN PAPER EDGE — strip `lenPx` x 14 px, pivot at the MIDDLE OF THE TOP EDGE: paper surface on top, ragged fibres hanging down,
        /// abyss shadow at the bottom. Placed on the tear line, rotated so the bottom of the image points into the void. Uses a lit material.</summary>
        public static Sprite TornEdge(int lenPx, int seed)
        {
            lenPx = Mathf.Clamp(lenPx, 4, 4096);
            long key = ((long)lenPx << 20) ^ seed;
            if (torn.TryGetValue(key, out var cached) && cached != null) return cached;
            const int H = 14;
            var px = new Color32[lenPx * H];
            var rng = new System.Random(seed);
            float depth = 4f;
            var paper = new Color32(104, 90, 76, 255);
            var fiber = new Color32(150, 134, 112, 255);
            var crease = new Color32(80, 68, 58, 255);
            for (int x = 0; x < lenPx; x++)
            {
                depth = Mathf.Clamp(depth + (float)(rng.NextDouble() * 2.2 - 1.1), 2f, 8f);
                int d = Mathf.RoundToInt(depth + (rng.NextDouble() < 0.12 ? 2 : 0));       // an occasional long fibre
                for (int k = 0; k < H; k++)                                                   // k = 0 is the top row
                {
                    int y = H - 1 - k;
                    Color32 c;
                    if (k == 0) c = crease;
                    else if (k < d) c = rng.NextDouble() < 0.18 ? fiber : paper;
                    else if (k == d) c = fiber;                                               // light fibre edge
                    else if (k <= d + 3) { byte a = (byte)(200 - (k - d - 1) * 55); c = new Color32(8, 5, 14, a); }   // abyss shadow
                    else c = new Color32(0, 0, 0, 0);
                    px[y * lenPx + x] = c;
                }
            }
            var s = Make(lenPx, H, px, new Vector2(0.5f, 1f), "margin_torn");
            torn[key] = s;
            return s;
        }

        /// <summary>Ink stroke of the striking pen: 8 x 6 px, pivot left middle, ragged like wet ink.</summary>
        public static Sprite InkStroke()
        {
            if (quillLine != null) return quillLine;
            const int W = 8, H = 6;
            var px = new Color32[W * H];
            var rng = new System.Random(373);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool edge = y == 0 || y == H - 1;
                    if (edge && rng.NextDouble() < 0.45) continue;
                    px[y * W + x] = edge ? new Color32(70, 30, 110, 200) : new Color32(36, 14, 58, 255);
                }
            return quillLine = Make(W, H, px, new Vector2(0f, 0.5f), "margin_stroke");
        }
    }
}
