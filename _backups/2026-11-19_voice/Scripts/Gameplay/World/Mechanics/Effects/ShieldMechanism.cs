using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// shield — bong bóng năng lượng bọc quanh tâm (bán kính `radius` ô, màu `color`), phập phồng, chặn người chơi.
    /// `veil` = tên object nằm trong bong bóng: nhìn qua lớp Ichor (mờ, ám xanh, gợn). Vỡ khi có cờ `off_when`.
    /// </summary>
    public sealed class ShieldMechanism : FxMechanism
    {
        protected override bool PopsWhenOff => true;
        private Color acid;

        public override void Init()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var c = MechanismContext.ParseColor(Str("color", "#7dff4a"), 1f);
            acid = c;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (r <= 1f)
                    {
                        a = 0.18f + 0.40f * Mathf.Pow(r, 6f);                       // lõi mờ, viền đậm
                        if (r > 0.93f) a = 0.75f;                                    // vỏ
                        float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.45f, 0.5f)) / 0.28f);
                        a = Mathf.Max(a, hl * 0.55f);                                // vệt sáng phản chiếu
                    }
                    a = Mathf.Round(a * 6f) / 6f;                                    // lượng tử hoá cho hợp pixel art
                    px[y * N + x] = new Color(Mathf.Lerp(c.r, 1f, a > 0.5f ? 0.35f : 0f), Mathf.Lerp(c.g, 1f, a > 0.5f ? 0.2f : 0f), c.b, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float diameter = Float("radius", 5f) * 2f * Ctx.Director.tileSize;
            // điểm sắp xếp (pivot) đặt ở 3/4 bán kính phía nam tâm: người chơi đứng phía nam -> vẽ trước bong bóng,
            // phía bắc / hai bên -> vẽ sau (như một quả cầu thật); Con Mắt (chân ở nam tâm ~3 ô) vẫn nằm trong bong bóng.
            const float sortDrop = 0.75f;
            var sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f - sortDrop * 0.5f), N / diameter);
            var go = new GameObject("shield_" + Name);
            go.transform.SetParent(Ctx.Host.transform, false);
            go.transform.position = (Vector3)Rect.center - new Vector3(0f, diameter * 0.5f * sortDrop, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder; }
            else sr.sortingOrder = 500;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            // vỏ năng lượng chặn người chơi (không cho bước vào hố khi Eye còn được nuôi)
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = diameter * 0.5f * 0.94f;
            col.offset = new Vector2(0f, diameter * 0.5f * sortDrop);
            var lightGo = new GameObject("shield_light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.position = Rect.center;
            var l = lightGo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = diameter * 0.7f;
            l.pointLightInnerRadius = diameter * 0.2f;
            l.intensity = 0.7f;
            l.color = c;
            fx = go.transform;
            fxSr = sr;
            fxScale = Vector3.one;
            foreach (var n in Str("veil", "").Split(','))
                if (!string.IsNullOrWhiteSpace(n) && Ctx.Named.TryGetValue(n.Trim(), out var vl))
                    foreach (var g in vl)
                        if (g != null)
                            foreach (var vsr in g.GetComponentsInChildren<SpriteRenderer>(true))
                                veil.Add((vsr, vsr.color, vsr.transform.localPosition));
        }

        public override void Animate()
        {
            if (fx == null || Done) return;
            float s = 1f + Mathf.Sin(Time.time * 2.2f) * 0.025f;
            fx.localScale = new Vector3(s, s, 1f);
            // vật bên trong: ám xanh Ichor + trong mờ, gợn ngang nhẹ như nhìn qua chất lỏng
            float a = 0.5f + 0.06f * Mathf.Sin(Time.time * 3.1f);
            for (int i = 0; i < veil.Count; i++)
            {
                var (vsr, col, pos) = veil[i];
                if (vsr == null) continue;
                var c = Color.Lerp(col, acid * 0.8f, 0.55f);
                c.a = col.a * a;
                vsr.color = c;
                vsr.transform.localPosition = pos + new Vector3(Mathf.Sin(Time.time * 2.6f + i) * 0.02f, Mathf.Sin(Time.time * 1.7f) * 0.012f, 0f);
            }
        }
    }
}
