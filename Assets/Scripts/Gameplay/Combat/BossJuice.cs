using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SHARED "IMPACT JUICE" for every boss.
    ///   - Light / Flash : real Light2D lights (on the floor + Ren) — flash on bursts, gathering light on wind-ups, lights following bullets.
    ///   - Shake         : 3-tier screen shake (1 normal hit, 2 big hit, 3 signature skill).
    ///   - Stop          : hit-stop (frame freeze) — only for heavy hits LANDING on Ren / signature skills.
    ///   - Impact        : white flash core (bfx_flash tinted by school) + light flash + shake — use at every burst point.
    ///   - Cast / Fire   : SKILL NAME on the HUD (cast bar on the boss HP bar: name in school colour, filling bar, flash on release).
    /// All static, safe to call right after the boss died (no MonoBehaviour caller needed).
    /// Used by: every boss skills file, AbsalomHallMechanism, VesselDomainMechanism. Read by: RequiemHUD.Boss (cast bar).
    /// </summary>
    public static class BossJuice
    {
        // ------------------------------------------------------------------ cast bar (read by the HUD)
        public static string CastName = "";
        public static Color CastColor = Color.white;
        public static float CastStart = -99f, CastEnd = -99f, CastFiredAt = -99f;
        public static Health CastOwner;

        /// <summary>The boss starts winding up skill `name` for `windup` seconds (0 = instant, only flashes the name).</summary>
        public static void Cast(Health owner, string name, Color c, float windup)
        {
            CastOwner = owner; CastName = name ?? ""; CastColor = c;
            CastStart = Time.time; CastEnd = Time.time + Mathf.Max(0f, windup);
            CastFiredAt = windup <= 0.01f ? Time.time : -99f;
        }

        /// <summary>The skill was just RELEASED (wind-up over): the HUD flashes the name then fades.</summary>
        public static void Fire(Health owner)
        {
            if (owner != null && owner != CastOwner) return;
            CastFiredAt = Time.time;
            CastEnd = Mathf.Min(CastEnd, Time.time);
        }

        /// <summary>Cancel the cast (boss interrupted / dead): the HUD clears at once.</summary>
        public static void Clear(Health owner)
        {
            if (owner != null && owner != CastOwner) return;
            CastOwner = null; CastName = ""; CastEnd = CastFiredAt = -99f;
        }

        // ------------------------------------------------------------------ lights
        /// <summary>Light2D at `at`. seconds > 0: shines at `intensity` then fades (squared) over `seconds`; seconds <= 0: stays until destroyed.
        /// follow != null: the light follows an object (bullet, boss hand).</summary>
        public static Light2D Light(Vector2 at, Color c, float radius, float intensity, float seconds, Transform follow = null)
        {
            var go = new GameObject("BossJuice Light");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            if (follow != null) go.transform.SetParent(follow, true);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(c.r, c.g, c.b, 1f);
            l.pointLightOuterRadius = Mathf.Max(0.05f, radius);
            l.pointLightInnerRadius = radius * 0.15f;
            l.intensity = intensity;
            if (seconds > 0f) go.AddComponent<JuiceFade>().Begin(l, intensity, seconds);
            return l;
        }

        /// <summary>Flash light (burst then fade) — on explosions / skill release.</summary>
        public static void Flash(Vector2 at, Color c, float radius, float intensity, float seconds)
            => Light(at, c, radius, intensity, seconds);

        // ------------------------------------------------------------------ shake + freeze
        public static void Shake(int tier) => Shake(tier, Vector2.down);

        public static void Shake(int tier, Vector2 dir)
        {
            if (tier <= 0) return;
            float amp = tier >= 3 ? 0.15f : tier == 2 ? 0.085f : 0.045f;
            float dur = tier >= 3 ? 0.42f : tier == 2 ? 0.28f : 0.16f;
            CameraFollow.KickMain(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down, amp, dur);
        }

        private static bool stopping;
        private static float stopAt;

        /// <summary>Hit-stop: time nearly stops for `seconds` of real time. Skipped while already slowed / paused (menu, cutscene, ongoing freeze).</summary>
        public static void Stop(float seconds)
        {
            if (stopping && Time.unscaledTime - stopAt > 1f) stopping = false;
            if (stopping || Time.timeScale < 0.99f || Time.timeScale > 1.01f) return;
            Runner.StartCoroutine(StopRoutine(seconds));
        }

        private static IEnumerator StopRoutine(float seconds)
        {
            stopping = true; stopAt = Time.unscaledTime;
            Time.timeScale = 0.06f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, 0.06f)) Time.timeScale = 1f;
            stopping = false;
        }

        // ------------------------------------------------------------------ effects
        /// <summary>Tinted effect sprite (white tint = original colours).</summary>
        public static SpriteVfx Burst(string sheet, Vector2 at, float scale, Color tint, int layer, int order, float fps = 16f)
        {
            var v = SpriteVfx.Spawn(sheet, at, scale, layer, order, false, fps);
            if (v != null && tint != Color.white) v.Tint(tint);
            return v;
        }

        /// <summary>
        /// Standard "BURST" beat of an impact point: bfx_flash core (`size` units) tinted by school, light flash of ~1.6x radius, shake by tier.
        /// Tier 3 adds a short hit-stop. Deals no damage.
        /// </summary>
        public static void Impact(Vector2 at, Color c, float size, int tier, int layer, int order)
        {
            var core = new Color(Mathf.Lerp(c.r, 1f, 0.55f), Mathf.Lerp(c.g, 1f, 0.55f), Mathf.Lerp(c.b, 1f, 0.55f), 1f);
            Burst("bfx_flash", at, size / 0.62f, core, layer, order, 20f);
            Flash(at, c, size * 1.6f, tier >= 3 ? 1.9f : tier == 2 ? 1.4f : 1f, tier >= 3 ? 0.5f : 0.32f);
            Shake(tier);
            if (tier >= 3) Stop(0.06f);
        }

        // ------------------------------------------------------------------ run coroutines without needing the boss alive
        private static JuiceRunner runner;
        private static JuiceRunner Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("BossJuice Runner") { hideFlags = HideFlags.HideInHierarchy };
                    runner = go.AddComponent<JuiceRunner>();
                }
                return runner;
            }
        }

        public static Coroutine Run(IEnumerator r) => Runner.StartCoroutine(r);

        private sealed class JuiceRunner : MonoBehaviour
        {
            private void OnDestroy()
            {
                if (stopping && Mathf.Approximately(Time.timeScale, 0.06f)) Time.timeScale = 1f;   // scene changed during the freeze
                stopping = false;
            }
        }
    }

    /// <summary>Fade the light then self-destroy (scaled time: a hit-stop also freezes the light — it shines slightly longer, as intended).</summary>
    public sealed class JuiceFade : MonoBehaviour
    {
        private Light2D l;
        private float i0, dur, t;

        public void Begin(Light2D light, float intensity, float seconds) { l = light; i0 = intensity; dur = Mathf.Max(0.02f, seconds); t = 0f; }

        private void Update()
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            if (l != null) l.intensity = i0 * k * k;
            if (t >= dur) Destroy(gameObject);
        }
    }
    /// <summary>
    /// Standard BEAM WARNING line: a faint band exactly as WIDE as the hit area + 2 bright edges,
    /// a bright core running from the origin to the beam end over the wind-up (reaching the end = fire), fast blinking in the last 25%.
    /// Aim(a, b) to follow a target; Release() to remove.
    /// </summary>
    public sealed class BeamWarn : MonoBehaviour
    {
        private SpriteRenderer band, edgeA, edgeB, core;
        private Vector2 a, b;
        private float halfW, t0, dur;
        private Color col;
        private static Sprite px;

        public static BeamWarn Spawn(Vector2 from, Vector2 to, float halfWidth, Color c, float seconds, int layer, int order)
        {
            var go = new GameObject("BeamWarn");
            var w = go.AddComponent<BeamWarn>();
            w.halfW = Mathf.Max(0.02f, halfWidth); w.col = c; w.t0 = Time.time; w.dur = Mathf.Max(0.05f, seconds);
            w.band = w.Part("Band", layer, order);
            w.edgeA = w.Part("EdgeA", layer, order + 1);
            w.edgeB = w.Part("EdgeB", layer, order + 1);
            w.core = w.Part("Core", layer, order + 2);
            w.Aim(from, to);
            return w;
        }

        private SpriteRenderer Part(string n, int layer, int order)
        {
            var g = new GameObject(n);
            g.transform.SetParent(transform, false);
            var r = g.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sprite = Px;
            r.sortingLayerID = layer; r.sortingOrder = order;
            return r;
        }

        public void Aim(Vector2 from, Vector2 to) { a = from; b = to; Layout(); }

        public void Release() { if (this != null) Destroy(gameObject); }

        private void Update()
        {
            if (Time.time - t0 > dur + 0.6f) { Destroy(gameObject); return; }   // in case the caller forgets Release
            Layout();
        }

        private void Layout()
        {
            Vector2 d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            transform.position = new Vector3(a.x, a.y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
            float k = Mathf.Clamp01((Time.time - t0) / dur);
            float t = Time.time - t0;
            float blink = k < 0.75f ? 0.85f + 0.15f * Mathf.Sin(t * 12f) : 0.6f + 0.4f * Mathf.Sin(t * 40f);
            Place(band, 0f, len, 0f, halfW * 2f, new Color(col.r, col.g, col.b, Mathf.Lerp(0.12f, 0.3f, k)));
            Place(edgeA, 0f, len, halfW, 0.025f, new Color(col.r, col.g, col.b, 0.9f * blink));
            Place(edgeB, 0f, len, -halfW, 0.025f, new Color(col.r, col.g, col.b, 0.9f * blink));
            Color hot = Color.Lerp(col, Color.white, 0.6f);
            Place(core, 0f, len * k, 0f, Mathf.Lerp(0.03f, halfW * 0.9f, k * k), new Color(hot.r, hot.g, hot.b, 0.55f + 0.45f * k));
        }

        private static void Place(SpriteRenderer r, float x0, float len, float y, float thick, Color c)
        {
            r.transform.localPosition = new Vector3(x0 + len * 0.5f, y, 0f);
            r.transform.localScale = new Vector3(Mathf.Max(0.001f, len), Mathf.Max(0.001f, thick), 1f);
            r.color = c;
        }

        private static Sprite Px
        {
            get
            {
                if (px != null) return px;
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white); tex.Apply();
                px = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                return px;
            }
        }
    }
}

