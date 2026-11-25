using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Shared effects of Burlap's Proving Grounds —
    ///   Warn()    WARNING LINE: a row of red arrows (drill/fx_arrow) along a segment, blinking faster, gone exactly when the hit arrives. Every attack in the
    ///             minigame is telegraphed (getting hit is the player's fault, never "cheap").
    ///   Impact()  HIT feel: straw sparks + hit star (drill/fx_hit), 0.06 s hit-stop, screen shake along the hit direction.
    ///   HitStop() freeze (like PlayerStrikes: timeScale 0.05 then restored, never overrides the pause menu).
    /// </summary>
    internal static class DrillFx
    {
        private static bool stopping;
        private static float stopAt;

        /// <summary>Arrow row from `from` to `to` (pointing along the travel direction), lives `seconds`. `gapFrom..gapTo` (0..1 along the segment) = safe gap
        /// (those arrows are bright and small) — leave empty for an all-red row.</summary>
        public static void Warn(MonoBehaviour host, Vector2 from, Vector2 to, float seconds, Vector2 pointDir, Transform player,
                                float spacing = 0.34f, float gapFrom = -1f, float gapTo = -1f)
        {
            var frames = SpriteVfx.Load("drill/fx_arrow");
            if (frames.Length == 0 || host == null) return;
            var safe = SpriteVfx.Load("drill/fx_arrow_safe");                         // separate GREEN arrow for the safe gap
            var list = new List<SpriteRenderer>();
            float len = (to - from).magnitude;
            // +0.001 — len / spacing = 13.9999 dropped one arrow, misaligning the arrow row with the dummy row and shrinking / shifting the green gap
            int n = Mathf.Max(1, Mathf.FloorToInt(len / spacing + 0.001f) + 1);
            float ang = Mathf.Atan2(pointDir.y, pointDir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0.5f : i / (float)(n - 1);
                bool gap = gapFrom >= 0f && u >= gapFrom && u <= gapTo;
                var sr = TrapArt.MakeRenderer(host.transform, gap ? GapName : "DrillWarn", Vector2.Lerp(from, to, u), player, -5);
                FxLighting.MakeUnlit(sr);
                // a green-tinted RED arrow came out dark brown and was scaled 0.55 -> nearly invisible. Now a dedicated green image at 0.85.
                bool green = gap && safe.Length > 0;
                sr.sprite = green ? safe[0] : frames[0];
                sr.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                sr.transform.localScale = gap ? new Vector3(0.85f, 0.85f, 1f) : Vector3.one;
                sr.color = gap && !green ? new Color(0.55f, 1f, 0.6f, 0.0f) : new Color(1f, 1f, 1f, 0f);
                list.Add(sr);
            }
            host.StartCoroutine(Blink(list, frames, safe, seconds));
        }

        private const string GapName = "DrillWarnGap";

        private static IEnumerator Blink(List<SpriteRenderer> list, Sprite[] frames, Sprite[] safe, float seconds)
        {
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                float u = (Time.time - t0) / seconds;
                float hz = Mathf.Lerp(3f, 11f, u);                                      // blink faster and faster
                bool on = Mathf.Repeat((Time.time - t0) * hz, 1f) < 0.6f;
                float a = Mathf.Clamp01(u * 6f);                                         // fade in over the first 0.15 s
                for (int i = 0; i < list.Count; i++)
                {
                    var sr = list[i];
                    if (sr == null) continue;
                    bool isGap = sr.name == GapName;
                    var c = sr.color; c.a = (on ? 0.95f : isGap ? 0.7f : 0.45f) * a; sr.color = c;   // green gap blinks less, always easy to see
                    var f = isGap && safe.Length > 0 ? safe : frames;
                    if (f.Length > 1) sr.sprite = f[on ? 1 : 0];
                    // a wave runs along the arrow row (shows direction) — red arrows only
                    float k = 1f + 0.12f * Mathf.Sin(Time.time * 14f - i * 0.9f);
                    if (!isGap) sr.transform.localScale = new Vector3(k, k, 1f);
                }
                yield return null;
            }
            foreach (var sr in list) if (sr != null) Object.Destroy(sr.gameObject);
        }

        /// <summary>Hit on the player: straw sparks + hit-stop + screen shake (direction = from the attacker to the player).</summary>
        public static void Impact(MonoBehaviour host, Vector2 at, Vector2 dir, int layer, int order, float shake = 0.09f)
        {
            SpriteVfx.Spawn("drill/fx_hit", at + Vector2.up * 0.15f, 1f, layer, order + 2, dir.x < 0f, 22f, false);
            CameraFollow.KickMain(dir, shake, 0.22f);
            HitStop(host, 0.07f);
        }

        public static void HitStop(MonoBehaviour host, float seconds)
        {
            if (stopping && Time.unscaledTime - stopAt > 1f) stopping = false;                 // host destroyed midway (scene change)
            if (host == null || stopping || Time.timeScale < 0.99f) return;
            host.StartCoroutine(Stop(seconds));
        }

        private static IEnumerator Stop(float seconds)
        {
            stopping = true; stopAt = Time.unscaledTime;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
            stopping = false;
        }
    }
}
