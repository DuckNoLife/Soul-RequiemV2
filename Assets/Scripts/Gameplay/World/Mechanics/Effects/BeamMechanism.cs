using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// beam - decorative energy beam from `src` to `dst` (Tiled tiles), using the animated template 'beampal' stretched + tinted `color`,
    /// `width` tiles wide; fades out once flag `off_when` is set.
    /// </summary>
    public sealed class BeamMechanism : FxMechanism
    {
        protected override bool PopsWhenOff => false;

        public override void Init()
        {
            if (Ctx.BeamTemplate == null && Ctx.Named.TryGetValue("beampal", out var tpl) && tpl.Count > 0)
                Ctx.BeamTemplate = tpl[0].GetComponentInChildren<SpriteRenderer>(true);
            var template = Ctx.BeamTemplate;
            if (template == null) { Debug.LogWarning("[MapMechanics] Missing beam template 'beampal'"); return; }
            template.enabled = false;                       // the template sits in a map corner: hide its image, the animation keeps running so beams can copy its frames
            Vector3 a = Ctx.TilePoint(Str("src", ""), Rect.center), b = Ctx.TilePoint(Str("dst", ""), Rect.center);
            var go = new GameObject("beam_" + Name);
            go.transform.SetParent(Ctx.Host.transform, false);
            // the sprite is a child offset by exactly the bounds centre -> the beam is always centred on a..b even though the Tiled tile pivot is bottom-left
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
            // lights along the beam
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
            fxSr.sprite = Ctx.BeamTemplate.sprite;                         // follow the template's animation frames
            fx.localScale = new Vector3(fxScale.x, fxScale.y * (1f + Mathf.Sin(Time.time * 9f) * 0.12f), 1f);
        }
    }
}
