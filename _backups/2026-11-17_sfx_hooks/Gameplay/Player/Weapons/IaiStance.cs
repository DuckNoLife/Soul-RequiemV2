using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§271, §273): HIỂN THỊ ĐÒN GIỮ IAIJUTSU (Oathbreaker). Không vẽ hoạt ảnh mới — Ren dùng khung vung kiếm của nhát 1 (PlayerMelee.IaiSlash
    /// dừng clip ở khung đó). Thành phần này chỉ vẽ:
    ///   - THANH NỘ dọc cạnh người Ren khi đang giữ (PlayerCombat.IaiHolding / IaiCharge): vạch vàng ngang = MỐC có tác dụng (`window`, 0,5 s).
    ///     Dưới mốc ruột xám (thả ra = nhát thường); qua mốc loé sao nhỏ, ruột xanh thép sáng dần (càng giữ càng mạnh); ĐẦY (`hold`) loé sao lớn,
    ///     trắng vàng nhấp nháy = mạnh tối đa, giữ thêm không tăng.
    ///   - Lúc thả đòn đã đầy (<see cref="Release"/>): vệt sáng thẳng dọc hướng chém + rung nhẹ.
    /// Hình vẽ bằng code (không cần ảnh). Gắn tự động từ PlayerCombat.Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IaiStance : MonoBehaviour
    {
        // 16/11 (§273): thanh vẽ bằng các khối 1 px kéo dãn theo ĐƠN VỊ THẾ GIỚI (không gắn làm con của Ren -> không lệch tỉ lệ; bản §271 thanh không đầy hẳn)
        private static readonly Color Back = new Color(0.07f, 0.06f, 0.09f, 0.9f);
        private static readonly Color Rim = new Color(0.78f, 0.66f, 0.38f, 1f);
        private static readonly Color Dim = new Color(0.42f, 0.42f, 0.46f, 1f);       // dưới mốc: chưa có tác dụng
        private static readonly Color Steel = new Color(0.55f, 0.68f, 0.86f, 1f);
        private static readonly Color Full = new Color(1f, 0.93f, 0.7f, 1f);

        private PlayerCombat combat;
        private SpriteRenderer body;
        private SpriteRenderer bg, fill, mark, rimL, rimR, rimT, rimB;
        private float alpha, shown;
        private bool wasFull, wasMark;

        private static Sprite pixelSprite, starSprite, lineSprite;

        public static IaiStance Ensure(PlayerCombat owner)
        {
            var s = owner.GetComponent<IaiStance>();
            if (s == null) s = owner.gameObject.AddComponent<IaiStance>();
            return s;
        }

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            body = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnDestroy()
        {
            foreach (var r in new[] { bg, fill, mark, rimL, rimR, rimT, rimB }) if (r != null) Destroy(r.gameObject);
        }

        private void LateUpdate()
        {
            if (combat == null || body == null) return;
            bool holding = combat.IaiHolding;
            alpha = Mathf.MoveTowards(alpha, holding ? 1f : 0f, Time.deltaTime * (holding ? 10f : 6f));
            if (alpha <= 0.001f) { Show(false); wasFull = wasMark = false; return; }
            Build();
            Show(true);

            float c = holding ? Mathf.Clamp01(combat.IaiCharge) : shown;
            shown = c;
            float m = WeaponTraits.IaiMark(combat);

            // kích thước theo chiều cao thân Ren; đặt cạnh phải, ngang ngực
            var b = body.bounds;
            float h = b.size.y * 0.42f, w = Mathf.Max(0.035f, h * 0.16f), px = w / 5f;
            Vector3 o = new Vector3(b.max.x - b.size.x * 0.16f, b.min.y + b.size.y * 0.28f, 0f);   // chân thanh, giữa bề ngang
            Place(bg, o + new Vector3(0f, h * 0.5f, 0f), w, h);
            Place(rimL, o + new Vector3(-w * 0.5f, h * 0.5f, 0f), px, h + px);
            Place(rimR, o + new Vector3(w * 0.5f, h * 0.5f, 0f), px, h + px);
            Place(rimB, o, w + px, px);
            Place(rimT, o + new Vector3(0f, h, 0f), w + px, px);
            float inner = h - px * 2f;
            Place(fill, o + new Vector3(0f, px + inner * c * 0.5f, 0f), w - px * 2f, Mathf.Max(0.0001f, inner * c));
            Place(mark, o + new Vector3(0f, px + inner * m, 0f), w + px * 3f, px);

            bool full = c >= 0.999f, past = c >= m;
            if (holding && full && !wasFull) StartCoroutine(Star(o + new Vector3(0f, h, 0f), 1f));
            else if (holding && past && !wasMark) StartCoroutine(Star(o + new Vector3(0f, px + inner * m, 0f), 0.55f));
            wasFull = full; wasMark = past;

            Color col;
            if (full) col = Color.Lerp(Full, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
            else if (past) col = Color.Lerp(Steel * 0.8f, Color.Lerp(Steel, Full, 0.4f), (c - m) / Mathf.Max(0.01f, 1f - m));
            else col = Dim;
            col.a = alpha; fill.color = col;
            var back = Back; back.a *= alpha; bg.color = back;
            var rim = Rim; rim.a = alpha;
            rimL.color = rimR.color = rimT.color = rimB.color = rim;
            var mk = past ? Full : Rim; mk.a = alpha; mark.color = mk;

            int baseOrder = body.sortingOrder + 3;
            bg.sortingOrder = baseOrder; fill.sortingOrder = baseOrder + 1;
            rimL.sortingOrder = rimR.sortingOrder = rimT.sortingOrder = rimB.sortingOrder = mark.sortingOrder = baseOrder + 2;
        }

        private void Build()
        {
            if (bg != null) return;
            bg = MakeFx("Iai meter bg", PixelSprite(), 3);
            fill = MakeFx("Iai meter fill", PixelSprite(), 4);
            rimL = MakeFx("Iai meter rim", PixelSprite(), 5); rimR = MakeFx("Iai meter rim", PixelSprite(), 5);
            rimT = MakeFx("Iai meter rim", PixelSprite(), 5); rimB = MakeFx("Iai meter rim", PixelSprite(), 5);
            mark = MakeFx("Iai meter mark", PixelSprite(), 5);
        }

        private void Show(bool on)
        {
            foreach (var r in new[] { bg, fill, mark, rimL, rimR, rimT, rimB }) if (r != null) r.enabled = on;
        }

        private static void Place(SpriteRenderer r, Vector3 center, float w, float h)
        {
            r.transform.position = center;
            r.transform.localScale = new Vector3(w, h, 1f);                 // sprite 1 px = 1 đơn vị -> scale = kích thước thế giới
        }

        /// <summary>Thả đòn đã qua mốc: vệt sáng rút kiếm dọc hướng chém, sáng / dài theo độ mạnh (0..1).</summary>
        public void Release(Vector2 dir, float power)
        {
            alpha = 0f; shown = 0f;
            Show(false);
            StartCoroutine(DrawFlash(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right, Mathf.Clamp01(power)));
        }

        // ------------------------------------------------------------------ hiệu ứng
        private SpriteRenderer MakeFx(string name, Sprite sp, int orderOffset)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder + orderOffset;
            FxLighting.MakeUnlit(sr);
            return sr;
        }

        private IEnumerator Star(Vector3 at, float size)
        {
            var sr = MakeFx("Iai ready", StarSprite(), 6);
            for (float t = 0f; t < 0.3f && sr != null; t += Time.deltaTime)
            {
                float u = t / 0.3f;
                sr.transform.position = at;
                sr.transform.localScale = Vector3.one * size * (0.4f + 0.7f * Mathf.Sin(u * Mathf.PI));
                sr.transform.rotation = Quaternion.Euler(0f, 0f, u * 45f);
                sr.color = new Color(1f, 1f, 1f, 1f - u * u);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        private IEnumerator DrawFlash(Vector2 d, float power)
        {
            var b = body.bounds;
            Vector3 from = new Vector3(b.center.x, b.min.y + b.size.y * 0.38f, 0f);
            var sr = MakeFx("Iai cut", LineSprite(), 5);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            CameraFollow.KickMain(d, 0.025f + 0.03f * power, 0.12f);
            float len = 0.9f + 0.7f * power, life = 0.18f + 0.06f * power;
            for (float t = 0f; t < life && sr != null; t += Time.deltaTime)
            {
                float u = t / life;
                float grow = Mathf.Clamp01(u * 5f);                         // vươn ra rất nhanh rồi mỏng dần
                sr.transform.position = from + (Vector3)(d * len * 0.5f * grow);
                sr.transform.localScale = new Vector3(len * grow / 0.64f, Mathf.Lerp(1.2f, 0.15f, u), 1f);
                sr.color = Color.Lerp(Color.white, new Color(Steel.r, Steel.g, Steel.b, 0f), u);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        // ---- hình vẽ bằng code
        /// <summary>1 px trắng, tâm giữa, 1 px = 1 đơn vị (scale = kích thước thế giới).</summary>
        private static Sprite PixelSprite()
        {
            if (pixelSprite != null) return pixelSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white); tex.Apply();
            return pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Sprite StarSprite()
        {
            if (starSprite != null) return starSprite;
            const int n = 15; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[n * n]; int c = n / 2;
            for (int i = 0; i < n; i++)
            {
                byte a = (byte)Mathf.Clamp(255 - Mathf.Abs(i - c) * 34, 0, 255);
                px[c * n + i] = new Color32(255, 255, 255, a); px[i * n + c] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px); tex.Apply();
            return starSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        }

        private static Sprite LineSprite()
        {
            if (lineSprite != null) return lineSprite;
            const int w = 64, h = 5; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    float edge = 1f - Mathf.Abs(x - w * 0.5f) / (w * 0.5f);        // nhọn hai đầu
                    bool core = y == 2, mid = y == 1 || y == 3;
                    byte a = (byte)(255 * Mathf.Clamp01(edge * (core ? 1.6f : mid ? 0.9f : 0.35f)));
                    px[y * w + x] = core ? new Color32(255, 255, 255, a) : new Color32(190, 214, 240, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return lineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
