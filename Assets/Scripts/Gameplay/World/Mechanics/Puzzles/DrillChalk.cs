using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Code-drawn pixel text for station 3 FINAL EXAM — chalk on the blackboard, numbers painted on trapdoors, the mortar aim ring.
    /// 3x5 font (digits, + - x / = ?, tick V, cross X) scaled `scale` times, each font dot = 1 image pixel (PPU 100) to match the board / door pixel density.
    /// </summary>
    internal static class DrillChalk
    {
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { '0', new[] { "###", "#.#", "#.#", "#.#", "###" } },
            { '1', new[] { ".#.", "##.", ".#.", ".#.", "###" } },
            { '2', new[] { "###", "..#", "###", "#..", "###" } },
            { '3', new[] { "###", "..#", ".##", "..#", "###" } },
            { '4', new[] { "#.#", "#.#", "###", "..#", "..#" } },
            { '5', new[] { "###", "#..", "###", "..#", "###" } },
            { '6', new[] { "###", "#..", "###", "#.#", "###" } },
            { '7', new[] { "###", "..#", ".#.", ".#.", ".#." } },
            { '8', new[] { "###", "#.#", "###", "#.#", "###" } },
            { '9', new[] { "###", "#.#", "###", "..#", "###" } },
            { '+', new[] { "...", ".#.", "###", ".#.", "..." } },
            { '-', new[] { "...", "...", "###", "...", "..." } },
            { 'x', new[] { "...", "#.#", ".#.", "#.#", "..." } },
            { '/', new[] { ".#.", "...", "###", "...", ".#." } },          // division sign ÷
            { '=', new[] { "...", "###", "...", "###", "..." } },
            { '?', new[] { "##.", "..#", ".#.", "...", ".#." } },
            { ' ', new[] { ".", ".", ".", ".", "." } },
            { 'V', new[] { "....#", "...#.", "#.#..", ".#...", "....." } },   // tick mark
            { 'X', new[] { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" } },
        };

        private static readonly Dictionary<int, Sprite> numbers = new Dictionary<int, Sprite>();
        private static Sprite reticle, shadow;

        /// <summary>Width (font dots, before scaling) of a string, 1 dot between characters.</summary>
        private static int Width(string s)
        {
            int w = 0;
            foreach (char c in s) { if (Glyphs.TryGetValue(c, out var g)) w += g[0].Length + 1; }
            return Mathf.Max(1, w - 1);
        }

        /// <summary>
        /// Text image: `ink` = text colour, `outline` (alpha 0 = none) 1 px outline around strokes, chalky = chalk (speckled, uneven strokes).
        /// Pivot at the centre, PPU 100. Returns a NEW Sprite (the caller destroys the texture when replacing it).
        /// </summary>
        public static Sprite Text(string s, int scale, Color32 ink, Color32 outline, bool chalky, int seed = 0)
        {
            int pad = outline.a > 0 ? 1 : 0;
            int w = Width(s) * scale + pad * 2, h = 5 * scale + pad * 2;
            var px = new Color32[w * h];
            var mask = new bool[w * h];
            int x0 = pad;
            foreach (char c in s)
            {
                if (!Glyphs.TryGetValue(c, out var g)) continue;
                int gw = g[0].Length;
                for (int gy = 0; gy < 5; gy++)
                    for (int gx = 0; gx < gw; gx++)
                    {
                        if (g[gy][gx] != '#') continue;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int x = x0 + gx * scale + sx, y = pad + (4 - gy) * scale + sy;   // font row 0 = top
                                mask[y * w + x] = true;
                            }
                    }
                x0 += (gw + 1) * scale;
            }
            var rng = new System.Random(seed + s.GetHashCode());
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (mask[i])
                    {
                        var c = ink;
                        if (chalky)
                        {
                            double r = rng.NextDouble();
                            if (r < 0.10) { px[i] = new Color32(0, 0, 0, 0); continue; }        // worn chalk
                            byte a = (byte)(r < 0.35 ? ink.a * 0.7f : ink.a);
                            c = new Color32(ink.r, ink.g, ink.b, a);
                        }
                        px[i] = c;
                    }
                    else if (pad > 0)
                    {
                        bool edge = false;
                        for (int dy = -1; dy <= 1 && !edge; dy++)
                            for (int dx = -1; dx <= 1 && !edge; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx >= 0 && yy >= 0 && xx < w && yy < h && mask[yy * w + xx]) edge = true;
                            }
                        if (edge) px[i] = outline;
                    }
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "drill_chalk" };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Number painted on a trapdoor (cream, dark brown outline) — shared, cached.</summary>
        public static Sprite Number(int v)
        {
            if (numbers.TryGetValue(v, out var s) && s != null) return s;
            s = Text(v.ToString(), 3, new Color32(232, 222, 190, 255), new Color32(14, 16, 30, 255), false);   // size 3 (doors are no longer scaled), dark teal outline like the pack's stone
            numbers[v] = s;
            return s;
        }

        /// <summary>Red mortar aim ring (circle + 4 ticks), 40 px.</summary>
        public static Sprite Reticle()
        {
            if (reticle != null) return reticle;
            const int N = 40;
            var px = new Color32[N * N];
            var red = new Color32(255, 70, 50, 230); var dark = new Color32(90, 10, 10, 170);
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    bool ring = d > 15.5f && d < 18.5f;
                    bool tick = (Mathf.Abs(x - c) < 1.1f && (d < 9f && d > 4f)) || (Mathf.Abs(y - c) < 1.1f && (d < 9f && d > 4f));
                    if (ring || tick) px[y * N + x] = red;
                    else if (d >= 18.5f && d < 19.6f) px[y * N + x] = dark;
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "drill_reticle" };
            tex.SetPixels32(px); tex.Apply(false, true);
            reticle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return reticle;
        }

        /// <summary>Faint oval shadow under a shell in flight.</summary>
        public static Sprite Shadow()
        {
            if (shadow != null) return shadow;
            const int W = 28, H = 12;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x - (W - 1) * 0.5f) / (W * 0.5f), v = (y - (H - 1) * 0.5f) / (H * 0.5f);
                    float d = u * u + v * v;
                    if (d < 1f) px[y * W + x] = new Color32(0, 0, 0, (byte)(150 * (1f - d * 0.6f)));
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "drill_shadow" };
            tex.SetPixels32(px); tex.Apply(false, true);
            shadow = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return shadow;
        }
    }
}
