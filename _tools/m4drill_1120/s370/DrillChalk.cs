using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§368): chữ pixel vẽ bằng code cho trạm 3 FINAL EXAM — phấn trên bảng đen, số sơn trên cửa sập, vòng ngắm của pháo cối.
    /// Phông 3x5 (chữ số, + - x / = ?, dấu tích V, dấu X) phóng `scale` lần, mỗi điểm ảnh = 1 px ảnh (PPU 100) để hợp mật độ điểm ảnh của bảng / cửa.
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
            { '/', new[] { ".#.", "...", "###", "...", ".#." } },          // dấu chia ÷
            { '=', new[] { "...", "###", "...", "###", "..." } },
            { '?', new[] { "##.", "..#", ".#.", "...", ".#." } },
            { ' ', new[] { ".", ".", ".", ".", "." } },
            { 'V', new[] { "....#", "...#.", "#.#..", ".#...", "....." } },   // dấu tích
            { 'X', new[] { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" } },
        };

        private static readonly Dictionary<int, Sprite> numbers = new Dictionary<int, Sprite>();
        private static Sprite reticle, shadow;

        /// <summary>Độ rộng (điểm phông, chưa phóng) của chuỗi, cách chữ 1 điểm.</summary>
        private static int Width(string s)
        {
            int w = 0;
            foreach (char c in s) { if (Glyphs.TryGetValue(c, out var g)) w += g[0].Length + 1; }
            return Mathf.Max(1, w - 1);
        }

        /// <summary>
        /// Ảnh chữ: `ink` = màu chữ, `outline` (alpha 0 = không viền) viền 1 px quanh nét, chalky = phấn (lấm tấm, nét không đều).
        /// Pivot giữa ảnh, PPU 100. Trả về Sprite MỚI (người gọi tự huỷ texture khi thay).
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
                                int x = x0 + gx * scale + sx, y = pad + (4 - gy) * scale + sy;   // hàng 0 của phông = trên cùng
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
                            if (r < 0.10) { px[i] = new Color32(0, 0, 0, 0); continue; }        // phấn bị mòn
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

        /// <summary>Số sơn trên cửa sập (kem, viền nâu sẫm) — dùng chung, có bộ nhớ đệm.</summary>
        public static Sprite Number(int v)
        {
            if (numbers.TryGetValue(v, out var s) && s != null) return s;
            s = Text(v.ToString(), 3, new Color32(232, 222, 190, 255), new Color32(14, 16, 30, 255), false);   // §369: cỡ 3 (cửa không còn phóng), viền xanh đen như đá gói
            numbers[v] = s;
            return s;
        }

        /// <summary>Vòng ngắm đỏ của pháo cối (vòng tròn + 4 vạch), 40 px.</summary>
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

        /// <summary>Bóng bầu dục mờ dưới đạn rơm đang bay.</summary>
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
