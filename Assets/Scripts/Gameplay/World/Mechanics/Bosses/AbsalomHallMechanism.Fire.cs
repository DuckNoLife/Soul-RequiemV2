using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// FIRE school (orange) — CLAIMS THE FLOOR: fire stays on the floor for a few seconds and shrinks the safe area (dodge = pick where to stand, not roll).
    ///   Pyre Lines  — 3 red lines fan out from Absalom toward Ren (0.9 s), then fire runs along them into 3 SOLID FIRE WALLS on ember trenches for 3.5 s
    ///                 (standing on them = burn per tick).
    ///   Cinder Rain — 5–6 METEORS fall from the sky around Ren (warning rings), explode and leave a FIRE POOL for 3 s.
    /// Meteor = abs_meteor 170x194 (molten rock with fire trail + smoke; impact = flash + burst + wave + cracks + smoke); the warning ring stays
    /// until impact; fire pool = lava ring abs_cinder_pool (92 px), radius 0.38 + breathing orange light.
    /// Called by: ElementSkill / PressureSkill (.Lamps.cs) and the Ctrl+F7 showcase. Uses: Hazard, RingSprite, FlashLight, Shake (.Skills.cs).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        /// <summary>PYRE LINES: warning lines -> fire RUNS out from Absalom along each line (small bursts per segment) -> SOLID FIRE WALL (abs_fire_wall
        /// segments overlapping 45%, sorted by feet) on a glowing CRACKED EMBER TRENCH (3 bands: red glow / orange / white-gold core) + orange lights along
        /// the wall + shake -> 3.5 s -> fades into scorch marks (abs_scorch) for 2.5 s.</summary>
        private IEnumerator PyreLines(int power)
        {
            yield return Cast(School.Fire, "PYRE LINES", 0.5f, "trap_fire");
            if (aborted) yield break;
            Vector2 from = abs.Pos + Vector2.down * 0.05f;
            Vector2 aim = (PlayerFeet - from).normalized;
            int n = power >= 2 ? 3 : 2;
            float spread = power >= 2 ? 34f : 42f;
            const float Len = 8f, Half = 0.22f, Burn = 3.5f, Warn = 0.9f, Step = 0.36f;
            var ends = new List<Vector2>();
            var tels = new List<DashTelegraph>();
            for (int i = 0; i < n; i++)
            {
                float o = (i - (n - 1) * 0.5f) * spread + Random.Range(-6f, 6f);
                Vector2 d = Rot(aim, o);
                Vector2 b = ClipToArena(from, d, Len);
                ends.Add(b);
                tels.Add(DashTelegraph.Spawn(from, d, (b - from).magnitude, Half * 2f, Fire, Warn, Layer, Order - 2));
            }
            yield return Wait(Warn);
            foreach (var t in tels) if (t != null) t.Release();
            if (aborted) yield break;
            Sfx.Play("keg_blast", abs.Pos, 0.8f);
            Sfx.Play("trap_fire", abs.Pos, 1f);
            Shake(2);
            FlashLight(from, Fire, 3f, 1.6f, 0.5f);
            // each line: burn zone + ember trench + running fire
            var walls = new List<(Hazard h, Vector2 b, SpriteRenderer[] crack)>();
            foreach (var b in ends)
            {
                var h = AddHazard(HazardShape.Segment, from, b, 0f, Half, 7f, 0.12f, Burn, 0.45f);
                var crack = new[] { MakeLine("Pyre Crack Glow"), MakeLine("Pyre Crack"), MakeLine("Pyre Crack Core") };
                Color[] cc = { new Color(0.85f, 0.2f, 0.05f, 0.45f), new Color(1f, 0.5f, 0.12f, 0.9f), new Color(1f, 0.92f, 0.6f, 1f) };
                for (int k = 0; k < 3; k++) { crack[k].color = cc[k]; crack[k].sortingLayerID = 0; crack[k].sortingOrder = -4 + k; crack[k].enabled = true; h.Objs.Add(crack[k].gameObject); }
                walls.Add((h, b, crack));
            }
            float maxLen = 0f;
            foreach (var w in walls) maxLen = Mathf.Max(maxLen, (w.b - from).magnitude);
            int steps = Mathf.CeilToInt(maxLen / Step);
            for (int j = 0; j <= steps && !aborted; j++)
            {
                foreach (var w in walls)
                {
                    float len = (w.b - from).magnitude, at = Mathf.Min(j * Step, len);
                    Vector2 dir = (w.b - from) / Mathf.Max(0.001f, len);
                    Vector2 tip = from + dir * at;
                    float[] widths = { 0.2f, 0.09f, 0.035f };
                    for (int k = 0; k < 3; k++) Stretch(w.crack[k], from, tip + dir * 0.001f, widths[k]);
                    if (j * Step > len) continue;
                    var fx = Grounded("abs_fire_wall", tip + new Vector2(Random.Range(-0.02f, 0.02f), 0f), Random.Range(1.15f, 1.3f), Random.Range(11f, 14f), true, 999f);
                    if (fx != null) w.h.Fx.Add(fx);
                    if (j % 2 == 0) SpriteVfx.Spawn("abs_fire_blast", tip, 0.9f, Layer, Order + 1, false, 20f, false);
                    if (j % 4 == 1) w.h.Objs.Add(GlowAt(tip, Fire, 1.4f, 0.9f, 0f));
                }
                yield return Wait(0.025f);
            }
            abs.Play("idle", 5f);
            // wall burns; fades out in the last 0.6 s
            float endAt = Time.time + Burn - steps * 0.025f;
            while (!aborted && Time.time < endAt - 0.6f) { FlickerCracks(walls, 1f); yield return null; }
            for (float t = 0f; t < 0.6f && !aborted; t += Time.deltaTime)
            {
                float k = 1f - t / 0.6f;
                FlickerCracks(walls, k);
                foreach (var w in walls) foreach (var f in w.h.Fx) if (f != null) f.Tint(new Color(1f, 1f, 1f, k));
                yield return null;
            }
            if (aborted) yield break;
            // aftermath: scorch marks
            foreach (var w in walls)
            {
                w.h.Dead = true;
                float len = (w.b - from).magnitude;
                for (float d = 0.2f; d < len; d += 0.42f)
                {
                    var sc = SpriteVfx.Spawn("abs_scorch", Vector2.Lerp(from, w.b, d / len) + Random.insideUnitCircle * 0.04f, 1.5f, 0, -5, Random.value < 0.5f, 10f, false);
                    if (sc != null) sc.Looping(2.5f);
                }
            }
        }

        private static void FlickerCracks(List<(Hazard h, Vector2 b, SpriteRenderer[] crack)> walls, float k)
        {
            foreach (var w in walls)
                for (int i = 0; i < w.crack.Length; i++)
                {
                    var c = w.crack[i];
                    if (c == null) continue;
                    var col = c.color;
                    float baseA = i == 0 ? 0.45f : i == 1 ? 0.9f : 1f;
                    col.a = baseA * k * (0.8f + 0.2f * Mathf.Sin(Time.time * (14f + i * 5f) + i));
                    c.color = col;
                }
        }

        /// <summary>Clip the ray (from, d, len) at the hall edge.</summary>
        private Vector2 ClipToArena(Vector2 from, Vector2 d, float len)
        {
            var r = new Rect(A.xMin + 0.15f, A.yMin + 0.1f, A.width - 0.3f, A.height - 0.25f);
            float t = len;
            if (d.x > 0.0001f) t = Mathf.Min(t, (r.xMax - from.x) / d.x);
            if (d.x < -0.0001f) t = Mathf.Min(t, (r.xMin - from.x) / d.x);
            if (d.y > 0.0001f) t = Mathf.Min(t, (r.yMax - from.y) / d.y);
            if (d.y < -0.0001f) t = Mathf.Min(t, (r.yMin - from.y) / d.y);
            return from + d * Mathf.Max(0.5f, t);
        }

        private IEnumerator CinderRain(int power)
        {
            yield return Cast(School.Fire, "CINDER RAIN", 0.5f, "drill_shell_whistle");
            if (aborted) yield break;
            int n = power >= 2 ? 6 : 4;
            Vector2 c = PlayerFeet;
            float a0 = Random.Range(0f, 360f);
            const float Warn = 1.05f, Fall = 7.5f / 18f;                               // abs_meteor: 18 fps, the rock hits the floor around frame 7.5
            for (int i = 0; i < n && !aborted; i++)
            {
                Vector2 at = i == 0 ? c : c + Rot(Vector2.right, a0 + i * 360f / (n - 1)) * Random.Range(0.8f, 1.45f);
                at = InArena(at, 0.4f);
                var g = GroundStrike.Spawn(at, Warn, 0.46f, 10f, null, 0, 16f, Order + 1, Fire);   // warning ring + damage only; the visual is the separate meteor
                if (g != null)
                {
                    g.impactSfx = "keg_blast";
                    Ctx.Run(MeteorAt(at, Warn - Fall));
                    Ctx.Run(PoolAfter(at, Warn + 0.12f));
                }
                yield return Wait(0.16f);
            }
            abs.Play("idle", 5f);
        }

        /// <summary>Meteor lands at `at` after `delay` s; on impact: orange light flash + shake.</summary>
        private IEnumerator MeteorAt(Vector2 at, float delay)
        {
            yield return Wait(Mathf.Max(0f, delay));
            if (aborted || state != State.Fight) yield break;
            var fx = SpriteVfx.Spawn("abs_meteor", at, 1f, Layer, Order + 2, Random.value < 0.5f, 18f, false);
            Sfx.Play("meteor_fall", at, 0.55f);                                    // sound
            if (fx != null) { var sr = fx.GetComponent<SpriteRenderer>(); if (sr != null) sr.spriteSortPoint = SpriteSortPoint.Pivot; }
            yield return Wait(7.5f / 18f);
            if (aborted) yield break;
            FlashLight(at, Fire, 2.8f, 2.1f, 0.5f);
            Shake(2);
            Sfx.Play("meteor_impact", at, 0.85f);                                  // sound
        }

        /// <summary>After the meteor bursts: fire pool for 3 s (burn per tick), big lava ring + breathing orange light.</summary>
        private IEnumerator PoolAfter(Vector2 at, float delay)
        {
            yield return Wait(delay);
            if (aborted || state != State.Fight) yield break;
            var h = AddHazard(HazardShape.Circle, at, at, 0.38f, 0f, 6f, 0f, 3f, 0.5f);
            HazardFx(h, "abs_cinder_pool", at, 1f, Order - 2, 10f, Color.white);
            h.Objs.Add(GlowAt(at, Fire, 1.5f, 1.1f, 0f));
        }
    }
}
