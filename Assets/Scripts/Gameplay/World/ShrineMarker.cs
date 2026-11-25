using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Rest point = a highlighted CAMPFIRE (no extra rune circle): a rest point's fire is brighter than a normal campfire,
    /// with a warm pulsing glow around it and rising sparks - the player learns "bright fire = rest spot".
    /// First step inside (Kindle): flares up, the fire turns into a blue soul flame and stays that way (lit).
    /// MapTriggerHandler creates one for every checkpoint trigger.
    /// </summary>
    public class ShrineMarker : MonoBehaviour
    {
        private static readonly Color Warm = new Color(1f, 0.72f, 0.38f), Spirit = new Color(0.45f, 0.85f, 1f);
        private Light2D glow;
        private SpriteRenderer[] flames = new SpriteRenderer[0];
        private Color[] flameBase = new Color[0];
        private bool lit;
        private float k, nextSpark;
        private int layer, order;

        /// <summary>Every rest campfire in the map (the HUD draws icons on the minimap / large map: orange = unlit, blue = lit).</summary>
        public static readonly System.Collections.Generic.List<ShrineMarker> All = new System.Collections.Generic.List<ShrineMarker>();
        /// <summary>Explanation line for the first lighting - the HUD merges it into the [T] prompt (one prompt only, no second hint).</summary>
        public static string Notice = "";
        /// <summary>Rest prompt word for the rest point being stood at (Tiled property `rest_noun`, default "shrine"; Map 1 = "campfire").</summary>
        public static string RestLabel = "Rest at the shrine";
        /// <summary>Rest point name of the current map ("shrine" / "campfire") - shared by the death screen, rest menu and hints.</summary>
        public static string Noun = "shrine";
        public static float NoticeUntil;
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        public static ShrineMarker Create(Rect area, Transform mapRoot, int sortingLayerId)
        {
            // the rest point's flame = animated tile object near the rest area (within 1.6 units)
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            if (mapRoot != null)
                foreach (var a in mapRoot.GetComponentsInChildren<TileObjectAnimator>(true))
                {
                    var r = a.GetComponent<SpriteRenderer>();
                    if (r == null) continue;
                    Vector2 p = r.bounds.center;
                    float dx = Mathf.Max(area.xMin - p.x, 0f, p.x - area.xMax), dy = Mathf.Max(area.yMin - p.y, 0f, p.y - area.yMax);
                    if (dx * dx + dy * dy < 0.4f * 0.4f) list.Add(r);     // the rest area surrounds the campfire -> only take the fire / smoke inside it
                }
            Vector2 at = area.center;                                       // rest area centred on the campfire

            var go = new GameObject("Shrine Marker");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var m = go.AddComponent<ShrineMarker>();
            m.flames = list.ToArray();
            m.flameBase = new Color[m.flames.Length];
            for (int i = 0; i < m.flames.Length; i++) m.flameBase[i] = m.flames[i].color;
            m.layer = sortingLayerId;
            m.order = list.Count > 0 ? list[0].sortingOrder + 1 : 3;
            m.glow = go.AddComponent<Light2D>();
            m.glow.lightType = Light2D.LightType.Point;
            m.glow.pointLightOuterRadius = 1.7f;
            m.glow.pointLightInnerRadius = 0.2f;
            m.Apply();
            return m;
        }

        public bool Lit => lit;

        /// <summary>Bounding box of the rest point's flames / candles (puts the glow ring right under the candle cluster). false = none.</summary>
        public bool TryFlameBounds(out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var f in flames)
            {
                if (f == null) continue;
                if (!any) { b = f.bounds; any = true; } else b.Encapsulate(f.bounds);
            }
            return any;
        }

        /// <summary>Already lit in the save - blue straight away, no burst VFX.</summary>
        public void SetLit() => lit = true;

        /// <summary>Light the rest point (first step inside): flare, the fire turns blue.</summary>
        public void Kindle()
        {
            if (lit) return;
            lit = true;
            SoulRequiem.Core.Sfx.Play("checkpoint", transform.position);   // sound
            var fx = SpriteVfx.Spawn("m3_heal_lantern", transform.position + new Vector3(0f, -0.1f, 0f), 1.1f, layer, 40, false, 14f, false);   // the "checkpoint" sound already played above
            if (fx != null) fx.Tint(new Color(0.6f, 0.9f, 1f, 1f));
        }

        private void Update()
        {
            k = Mathf.MoveTowards(k, lit ? 1f : 0f, Time.deltaTime * 1.5f);
            Apply();
            // rising sparks (small, sparse) - something normal campfires do not have
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + Random.Range(0.35f, 0.7f);
                var s = SpriteVfx.Spawn("m3_hymn_pop", transform.position + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(0.05f, 0.25f), 0f),
                                        0.25f, layer, order, false, 12f, false);   // decorative sparks - SILENT (a "hymn_pop" 2-3 times per second was too noisy)
                if (s != null) s.Tint(lit ? new Color(0.6f, 0.95f, 1f, 0.9f) : new Color(1f, 0.8f, 0.45f, 0.9f));
            }
        }

        private void Apply()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
            if (glow != null)
            {
                glow.color = Color.Lerp(Warm, Spirit, k);
                glow.intensity = Mathf.Lerp(0.75f, 1.05f, k) + pulse * 0.25f;
            }
            for (int i = 0; i < flames.Length; i++)
                if (flames[i] != null)
                {
                    // unlit: fire brighter than normal fires; lit: blue soul flame
                    Color bright = new Color(1f, 0.95f, 0.8f, flameBase[i].a);
                    Color spirit = new Color(Spirit.r, Spirit.g, Spirit.b, flameBase[i].a);
                    flames[i].color = Color.Lerp(bright, spirit, k);
                }
        }
    }
}
