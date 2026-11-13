using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 06/11 (người dùng: VFX của Ram nhoè, chưa hợp style): hình hiệu ứng của Ether Ram vẽ bằng code theo kiểu PIXEL ART của chính con Ram —
    /// điểm ảnh to bằng điểm ảnh của Ram (Ram vẽ ×2 -> PPU 50), viền tối, 3 sắc xanh phóng xạ, mờ dần theo bậc (không nhoè).
    ///   Puddle  vũng Ether độc trên sàn (3 dạng, 2 khung bọt sủi)      Mote  hạt phóng xạ bay lên (quá tải)
    ///   Ring    vòng chấm quanh chân khi quá tải (2 khung)             Puff  bụi / hơi nước (3 khung, ảnh xám -> nhuộm khi dùng)
    ///   Drop    giọt nước khi ướt sũng
    /// Chỉ tạo ảnh (tĩnh, dùng chung); RamSkills đặt và điều khiển renderer.
    /// </summary>
    public static class RamVfx
    {
        public const float PPU = 50f;
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Dark = new Color32(18, 58, 30, 235), Mid = new Color32(58, 170, 62, 230),
                                         Light = new Color32(150, 255, 112, 240), Hot = new Color32(222, 255, 170, 255);
        private static Sprite[][] puddles;
        private static Sprite[] ring, puff;
        private static Sprite moteS, moteL, drop;

        public static Sprite[] Puddle(int variant)
        {
            if (puddles == null) { puddles = new Sprite[3][]; for (int v = 0; v < 3; v++) puddles[v] = MakePuddle(v); }
            return puddles[((variant % 3) + 3) % 3];
        }
        public static Sprite Mote(bool big) => big ? (moteL != null ? moteL : (moteL = Solid(2, 2, Hot))) : (moteS != null ? moteS : (moteS = Solid(1, 1, Light)));
        public static Sprite Drop => drop != null ? drop : (drop = Solid(1, 2, new Color32(200, 235, 255, 255)));
        public static Sprite[] Ring => ring != null ? ring : (ring = new[] { MakeRing(0), MakeRing(1) });
        public static Sprite[] Puff => puff != null ? puff : (puff = new[] { MakePuff(2), MakePuff(3), MakePuff(4) });

        // ------------------------------------------------------------------ vẽ
        private static Sprite MakeSprite(Color32[] px, int w, int h, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, PPU, 0, SpriteMeshType.FullRect);
        }

        private static Sprite Solid(int w, int h, Color32 c)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            return MakeSprite(px, w, h, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Vũng 14x7 điểm ảnh, mép răng cưa ngẫu nhiên (cố định theo variant), viền tối, ánh sáng mép trên-trái, 1 bọt sủi.</summary>
        private static Sprite[] MakePuddle(int variant)
        {
            const int W = 14, H = 7;
            var rng = new System.Random(31 + variant * 17);
            var inside = new bool[W, H];
            float[] bump = new float[8];
            for (int i = 0; i < bump.Length; i++) bump[i] = (float)rng.NextDouble() * 0.35f - 0.1f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float nx = (x - (W - 1) * 0.5f) / (W * 0.5f), ny = (y - (H - 1) * 0.5f) / (H * 0.5f);
                    float a = Mathf.Atan2(ny, nx) / (2f * Mathf.PI) + 0.5f;
                    float edge = 1f - bump[Mathf.Clamp((int)(a * bump.Length), 0, bump.Length - 1)];
                    inside[x, y] = nx * nx + ny * ny < edge;
                }
            int bx = 4 + rng.Next(W - 8), by = 2 + rng.Next(H - 4);
            var frames = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (!inside[x, y]) { px[y * W + x] = Clear; continue; }
                        bool rim = x == 0 || y == 0 || x == W - 1 || y == H - 1 || !inside[x - 1, y] || !inside[x + 1, y] || !inside[x, y - 1] || !inside[x, y + 1];
                        bool shine = !rim && y >= H - 3 && x <= W / 2 && (x + y) % 3 == 0;   // ánh sáng mép trên (y lớn = trên)
                        px[y * W + x] = rim ? Dark : shine ? Light : Mid;
                    }
                // bọt sủi: khung 0 = vòng bọt 3x3, khung 1 = vỡ còn 1 chấm sáng
                if (f == 0)
                {
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if ((dx != 0 || dy != 0) && inside[bx + dx, by + dy]) px[(by + dy) * W + bx + dx] = Light;
                }
                else px[by * W + bx] = Hot;
                frames[f] = MakeSprite(px, W, H, new Vector2(0.5f, 0.5f));
            }
            return frames;
        }

        /// <summary>Vòng elip đứt nét 22x8 điểm ảnh quanh chân (quá tải); 2 khung lệch pha nét đứt -> trông như xoay.</summary>
        private static Sprite MakeRing(int phase)
        {
            const int W = 22, H = 8;
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            for (int k = 0; k < 64; k++)
            {
                float a = k / 64f * Mathf.PI * 2f;
                int x = Mathf.RoundToInt((W - 1) * 0.5f + Mathf.Cos(a) * (W - 1) * 0.5f);
                int y = Mathf.RoundToInt((H - 1) * 0.5f + Mathf.Sin(a) * (H - 1) * 0.5f);
                if (((k / 4) + phase) % 2 == 0) px[y * W + x] = k % 4 == 0 ? Hot : Light;
            }
            return MakeSprite(px, W, H, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Cụm bụi / hơi tròn bán kính r điểm ảnh: lõi xám sáng, viền xám tối, lỗ thủng hoà sắc (ảnh xám — nhuộm qua color).</summary>
        private static Sprite MakePuff(int r)
        {
            int n = r * 2 + 1;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                    Color32 c = Clear;
                    if (d <= r - 0.6f) c = (r >= 4 && (x + y) % 2 == 0 && d < r - 1.5f) ? Clear : new Color32(235, 235, 235, 255);   // cụm to: lõi thưa dần
                    else if (d <= r + 0.2f) c = new Color32(150, 150, 150, 255);
                    px[y * n + x] = c;
                }
            return MakeSprite(px, n, n, new Vector2(0.5f, 0.5f));
        }
    }
}
