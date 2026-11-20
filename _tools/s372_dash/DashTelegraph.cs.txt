using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§372): VÙNG BÁO TRƯỚC CHIÊU LƯỚT / LAO dùng chung cho mọi boss (thay vạch m3_ray_warn 4 px — mảnh 0,06–0,09 đv, khó thấy, không cho biết
    /// lúc nào lao, độ dài vạch còn lệch quãng lao thật).
    /// Hình (vẽ bằng code, điểm ảnh 1:1 PPU 100, không cần ảnh ngoài):
    ///   • LÀN rộng đúng bề ngang vùng trúng đòn (thân boss + thân Ren) — biết phải bước sang ngang bao xa;
    ///   • nền làn mờ + 2 MÉP sáng rõ;
    ///   • VỆT NẠP chạy từ gốc tới cuối làn trong đúng thời gian gồng — đầy vệt = lao (đọc được nhịp);
    ///   • MŨI TÊN ">" trôi theo hướng lao; DẤU ĐÍCH (vòng + chữ X) ở chỗ boss sẽ dừng;
    ///   • 25% cuối: khoá hướng (nếu chiêu có bám theo) -> nhấp nháy nhanh + sáng hơn = sắp lao.
    /// Dùng: var tg = DashTelegraph.Spawn(...); (đang gồng: tg.Aim(...) nếu đổi hướng) -> lúc lao: tg.Release().
    /// Quên Release (boss gục giữa chừng) -> tự huỷ sau thời gian gồng + 0,6 s.
    /// DashTelegraph.Reach: quãng lao thật (dừng trước tường / cột, không tính trigger / chính boss / Ren / quái khác).
    /// </summary>
    public sealed class DashTelegraph : MonoBehaviour
    {
        private SpriteRenderer fill, charge, edgeA, edgeB, endMark;
        private readonly List<SpriteRenderer> chevrons = new List<SpriteRenderer>();
        private Color color;
        private float length, width, seconds, born, releasedAt = -1f;
        private const float Spacing = 0.34f, Edge = 0.03f;
        private static Sprite pixel, chevron, target;

        // ------------------------------------------------------------------ tạo / điều khiển
        /// <param name="from">gốc làn (chân boss)</param>
        /// <param name="dir">hướng lao</param>
        /// <param name="length">quãng lao thật (nên lấy từ Reach)</param>
        /// <param name="width">bề ngang làn = vùng trúng đòn (xem WidthFor)</param>
        /// <param name="seconds">thời gian gồng — vệt nạp đầy đúng lúc lao</param>
        public static DashTelegraph Spawn(Vector2 from, Vector2 dir, float length, float width, Color color, float seconds, int sortingLayer, int sortingOrder)
        {
            Build();
            var go = new GameObject("DashTelegraph");
            var t = go.AddComponent<DashTelegraph>();
            t.color = color; t.seconds = Mathf.Max(0.05f, seconds); t.born = Time.time; t.width = Mathf.Max(0.2f, width);
            t.fill = t.Part("fill", pixel, sortingLayer, sortingOrder);
            t.charge = t.Part("charge", pixel, sortingLayer, sortingOrder);
            t.edgeA = t.Part("edge", pixel, sortingLayer, sortingOrder + 1);
            t.edgeB = t.Part("edge", pixel, sortingLayer, sortingOrder + 1);
            t.endMark = t.Part("end", target, sortingLayer, sortingOrder + 1);
            t.sortLayer = sortingLayer; t.sortOrder = sortingOrder + 1;
            t.Aim(from, dir, length);
            t.LateUpdate();
            return t;
        }

        private int sortLayer, sortOrder;

        /// <summary>Đổi gốc / hướng / độ dài (chiêu bám theo Ren trong lúc gồng).</summary>
        public void Aim(Vector2 from, Vector2 dir, float len)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            length = Mathf.Max(0.3f, len);
            transform.position = from;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>Bắt đầu lao: làn loé sáng rồi tắt nhanh (0,15 s) và tự huỷ.</summary>
        public void Release()
        {
            if (releasedAt >= 0f) return;
            releasedAt = Time.time;
        }

        // ------------------------------------------------------------------ vẽ mỗi khung
        private void LateUpdate()
        {
            float age = Time.time - born;
            if (releasedAt < 0f && age > seconds + 0.6f) releasedAt = Time.time;   // quên Release
            float fade = 1f;
            if (releasedAt >= 0f)
            {
                float k = (Time.time - releasedAt) / 0.15f;
                if (k >= 1f) { Destroy(gameObject); return; }
                fade = 1f - k;
            }
            float u = Mathf.Clamp01(age / seconds);
            bool late = u > 0.75f;
            float blink = late ? (Mathf.Repeat(Time.time * 14f, 1f) < 0.5f ? 1f : 0.55f) : 0.85f + 0.15f * Mathf.Sin(Time.time * 9f);
            if (releasedAt >= 0f) blink = 1.3f;                                     // loé lúc lao
            float half = width * 0.5f;
            // nền làn + vệt nạp (gốc bên trái, kéo theo trục x cục bộ)
            Place(fill, 0f, 0f, length, width, Alpha(0.16f * blink * fade));
            Place(charge, 0f, 0f, length * u, width, Alpha((late ? 0.34f : 0.24f) * fade));
            Place(edgeA, 0f, half - Edge * 0.5f, length, Edge, Alpha(0.9f * blink * fade));
            Place(edgeB, 0f, -half + Edge * 0.5f, length, Edge, Alpha(0.9f * blink * fade));
            // dấu đích
            endMark.transform.localPosition = new Vector3(length, 0f, 0f);
            endMark.transform.localRotation = Quaternion.Euler(0f, 0f, -transform.eulerAngles.z);   // vòng luôn thẳng
            float es = Mathf.Max(0.6f, width / 0.36f) * (late ? 1f + 0.08f * Mathf.Sin(Time.time * 30f) : 1f);
            endMark.transform.localScale = new Vector3(es, es * 0.75f, 1f);
            endMark.color = Alpha(0.95f * blink * fade);
            // mũi tên trôi theo hướng lao (nhanh dần khi sắp lao)
            int n = Mathf.Max(1, Mathf.FloorToInt((length - 0.25f) / Spacing));
            while (chevrons.Count < n) chevrons.Add(Part("chev", chevron, sortLayer, sortOrder));
            float shift = Mathf.Repeat(age * (late ? 2.4f : 1.2f), Spacing);
            float cs = Mathf.Clamp(width / 0.5f, 0.7f, 1.6f);
            for (int i = 0; i < chevrons.Count; i++)
            {
                var c = chevrons[i];
                float x = 0.15f + i * Spacing + shift;
                bool on = i < n && x < length - 0.15f;
                c.enabled = on;
                if (!on) continue;
                c.transform.localPosition = new Vector3(x, 0f, 0f);
                c.transform.localScale = new Vector3(cs, cs, 1f);
                float edgeFade = Mathf.Clamp01(Mathf.Min(x - 0.1f, length - x) / 0.25f);
                c.color = Alpha(0.85f * edgeFade * blink * fade);
            }
        }

        private Color Alpha(float a) => new Color(color.r, color.g, color.b, Mathf.Clamp01(a) * color.a);

        private static void Place(SpriteRenderer r, float x, float y, float sx, float sy, Color c)
        {
            r.enabled = sx > 0.001f;
            r.transform.localPosition = new Vector3(x, y, 0f);
            r.transform.localScale = new Vector3(Mathf.Max(0.0001f, sx), sy, 1f);
            r.color = c;
        }

        private SpriteRenderer Part(string name, Sprite s, int layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sortingLayerID = layer;
            r.sortingOrder = order;
            FxLighting.MakeUnlit(r);
            return r;
        }

        // ------------------------------------------------------------------ tiện ích
        /// <summary>Bề ngang làn từ khung va chạm của boss: bề ngang thân + ~thân Ren (tối thiểu `min`).</summary>
        public static float WidthFor(Collider2D body, float min = 0.6f)
        {
            if (body == null) return min;
            return Mathf.Max(min, body.bounds.size.x + 0.3f);
        }

        /// <summary>Quãng lao thật theo `dir` tối đa `want`: dừng trước vật cản đặc đầu tiên (tường, cột, đồ vật) — không tính trigger,
        /// chính boss, Ren, quái khác. `radius` = nửa bề ngang thân boss (quét bằng hình tròn).</summary>
        public static float Reach(Vector2 from, Vector2 dir, float want, float radius, Collider2D self, Transform player)
        {
            if (dir.sqrMagnitude < 0.0001f) return want;
            float best = want;
            foreach (var h in Physics2D.CircleCastAll(from, Mathf.Max(0.05f, radius), dir.normalized, want))
            {
                if (h.collider == null || h.collider.isTrigger || h.collider == self) continue;
                if (player != null && h.collider.transform.IsChildOf(player)) continue;
                if (h.collider.GetComponentInParent<EnemyAI>() != null) continue;
                if (h.distance <= 0.01f) continue;                                     // đang chạm sẵn (đứng sát vách) — bỏ qua
                best = Mathf.Min(best, h.distance);
            }
            return Mathf.Max(0.3f, best);
        }

        /// <summary>Quãng lao "tới chỗ Ren rồi trượt quá" — không bao giờ quá ngắn / quá xa.</summary>
        public static float WantReach(Vector2 from, Vector2 playerPos, float overshoot, float min, float max)
            => Mathf.Clamp((playerPos - from).magnitude + overshoot, min, max);

        // ------------------------------------------------------------------ ảnh vẽ bằng code
        private static void Build()
        {
            if (pixel != null) return;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "dash_px" };
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px); t.Apply(false, true);
            pixel = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);    // 1 đv, gốc mép trái

            // mũi tên ">" 12 x 14 px, nét 3 px + viền tối 1 px
            const int W = 12, H = 14;
            var c = new Color32[W * H];
            var ink = new Color32(255, 255, 255, 255); var dark = new Color32(30, 10, 10, 200);
            var mask = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float cy = (H - 1) * 0.5f, dy = Mathf.Abs(y - cy);
                    float xLine = 2f + (cy - dy) * 0.95f;                                     // đỉnh mũi tên bên phải, giữa chiều cao
                    if (x >= xLine - 1.5f && x <= xLine + 1.5f && dy <= cy - 0.5f) mask[y * W + x] = true;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (mask[i]) { c[i] = ink; continue; }
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                        for (int dx = -1; dx <= 1 && !edge; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < W && yy < H && mask[yy * W + xx]) edge = true;
                        }
                    if (edge) c[i] = dark;
                }
            var ct = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "dash_chevron" };
            ct.SetPixels32(c); ct.Apply(false, true);
            chevron = Sprite.Create(ct, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);

            // dấu đích: vòng tròn 2 px + chữ X giữa, 36 px
            const int N = 36;
            var e = new Color32[N * N];
            float m = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - m) * (x - m) + (y - m) * (y - m));
                    bool ring = d > 14.5f && d < 17f;
                    bool cross = (Mathf.Abs((x - m) - (y - m)) < 1.6f || Mathf.Abs((x - m) + (y - m)) < 1.6f) && d < 8.5f;
                    if (ring || cross) e[y * N + x] = ink;
                    else if (d >= 17f && d < 18f) e[y * N + x] = dark;
                }
            var et = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "dash_target" };
            et.SetPixels32(e); et.Apply(false, true);
            target = Sprite.Create(et, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
