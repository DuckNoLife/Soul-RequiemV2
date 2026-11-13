using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// beam — tia năng lượng trang trí từ `src` tới `dst` (ô Tiled), dùng ảnh động mẫu 'beampal' kéo dài + nhuộm `color`,
    /// rộng `width` ô; tắt dần khi có cờ `off_when`.
    /// </summary>
    public sealed class BeamMechanism : FxMechanism
    {
        protected override bool PopsWhenOff => false;

        public override void Init()
        {
            if (Ctx.BeamTemplate == null && Ctx.Named.TryGetValue("beampal", out var tpl) && tpl.Count > 0)
                Ctx.BeamTemplate = tpl[0].GetComponentInChildren<SpriteRenderer>(true);
            var template = Ctx.BeamTemplate;
            if (template == null) { Debug.LogWarning("[MapMechanics] Thiếu mẫu tia 'beampal'"); return; }
            template.enabled = false;                       // mẫu nằm ở góc map: ẩn hình, ảnh động vẫn chạy để tia lấy khung
            Vector3 a = Ctx.TilePoint(Str("src", ""), Rect.center), b = Ctx.TilePoint(Str("dst", ""), Rect.center);
            var go = new GameObject("beam_" + Name);
            go.transform.SetParent(Ctx.Host.transform, false);
            // sprite nằm ở con, lùi đúng bằng tâm bounds -> tia luôn căn giữa a..b dù pivot của ô Tiled ở góc dưới-trái
            var body = new GameObject("beam_sprite");
            body.transform.SetParent(go.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = template.sprite;
            if (sr.sprite != null) body.transform.localPosition = -(Vector3)(Vector2)sr.sprite.bounds.center;
            sr.sortingLayerID = template.sortingLayerID;
            sr.sortingOrder = template.sortingOrder + 5;
            sr.color = MechanismContext.ParseColor(Str("color", "#8dff5a"), 0.85f);
            Vector3 d = b - a;
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one * 0.32f;
            float width = Float("width", 0.8f) * Ctx.Director.tileSize;
            fxScale = new Vector3(d.magnitude / Mathf.Max(0.01f, size.x), width / Mathf.Max(0.01f, size.y), 1f);
            go.transform.localScale = fxScale;
            fx = go.transform;
            fxSr = sr;
            // đèn dọc tia
            int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 1.2f));
            for (int i = 0; i <= n; i++)
            {
                var lg = new GameObject("beam_light");
                lg.transform.position = Vector3.Lerp(a, b, i / (float)n);
                lg.transform.SetParent(go.transform, true);
                var l = lg.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightOuterRadius = 0.7f;
                l.pointLightInnerRadius = 0.05f;
                l.intensity = 0.5f;
                l.color = sr.color;
            }
        }

        public override void Animate()
        {
            if (fx == null || Done || Ctx.BeamTemplate == null || fxSr == null) return;
            fxSr.sprite = Ctx.BeamTemplate.sprite;                         // chạy theo khung của ảnh động mẫu
            fx.localScale = new Vector3(fxScale.x, fxScale.y * (1f + Mathf.Sin(Time.time * 9f) * 0.12f), 1f);
        }
    }
}
