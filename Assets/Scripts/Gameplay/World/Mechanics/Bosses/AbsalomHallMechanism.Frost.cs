using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// FROST school (blue) — CHASE / SNIPE: fast attacks, short but clear warnings, punishes running in a straight line.
    ///   Rime Thorns    — 3 rows of ice thorns erupt one after another from Absalom's feet, crawling to where Ren stood at cast time
    ///                    (each thorn has a 0.45 s warning ring). Dodge: step sideways off the path (do not run along it).
    ///   Glacial Volley — 4–5 ice lances, each with a 0.45 s aim line locked on Ren's CURRENT spot, then fired very fast. Dodge: change direction after each line.
    /// Rime Thorns = big ICE THORN CLUSTERS (abs_rime 95x85: thorns rise from the floor, cold mist, sparkle, sink back) every 0.45 u, radius 0.28;
    ///   every 3rd cluster flashes a blue light, the end of the path bursts in an ice ring + shake. Glacial Volley = 115 px ICE LANCE (ice rim, mist tail);
    ///   the lance "freezes into being" over his shoulder (fades in, light grows) while aiming, glows, and shatters into an ice-shard ring (abs_ice_shatter)
    ///   on wall / Ren.
    /// Called by: ElementSkill / PressureSkill (.Lamps.cs) and the Ctrl+F7 showcase. Uses: Shot, RingSprite, FlashLight (.Skills.cs).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private IEnumerator RimeThorns(int power)
        {
            yield return Cast(School.Frost, "RIME THORNS", 0.5f, "trap_ice");
            if (aborted) yield break;
            var spike = SpriteVfx.Load("abs_rime");
            if (spike.Length == 0) spike = SpriteVfx.Load("abs_ice_spikes");
            Vector2 from = abs.Pos + Vector2.down * 0.2f;
            Vector2 target = PlayerFeet;
            int lines = power >= 2 ? 3 : 2;
            float spread = 24f;
            var paths = new List<(Vector2 d, float len)>();
            for (int i = 0; i < lines; i++)
            {
                Vector2 d = Rot((target - from).normalized, (i - (lines - 1) * 0.5f) * spread);
                Vector2 end = ClipToArena(from, d, (target - from).magnitude + 1.8f);
                paths.Add((d, (end - from).magnitude));
            }
            const float Step = 0.45f;
            int steps = 0;
            foreach (var p in paths) steps = Mathf.Max(steps, Mathf.FloorToInt(p.len / Step));
            Sfx.Play("root_spike", from, 0.6f);
            for (int k = 1; k <= steps && !aborted; k++)
            {
                foreach (var p in paths)
                {
                    if (k * Step > p.len) continue;
                    Vector2 at = from + p.d * (k * Step) + Rot(p.d, 90f) * Random.Range(-0.05f, 0.05f);
                    var g = GroundStrike.Spawn(at, 0.45f, 0.28f, 9f, spike, 2, 16f, Order + 1, Frost);
                    if (g != null) { g.impactSfx = k % 3 == 0 ? "ice_shatter" : "none"; g.bodyScale = Random.Range(0.72f, 0.85f); }
                    bool tip = (k + 1) * Step > p.len;                                 // last cluster of the thorn path
                    if (tip || k % 3 == 0) Ctx.Run(FrostPulse(at, 0.45f + 2f / 16f, tip));
                }
                yield return Wait(0.08f);
            }
            abs.Play("idle", 5f);
        }

        /// <summary>When a thorn erupts — blue light flash (+ ice ring burst + shake at the end of the path).</summary>
        private IEnumerator FrostPulse(Vector2 at, float delay, bool tip)
        {
            yield return Wait(delay);
            if (aborted || state != State.Fight) yield break;
            FlashLight(at, Frost, tip ? 2.2f : 1.5f, tip ? 1.8f : 1.1f, 0.35f);
            if (tip) { Fx("abs_ice_shatter", at + Vector2.up * 0.25f, 1.4f, Order + 3, 18f, Color.white); Shake(1); }
        }

        private IEnumerator GlacialVolley(int power)
        {
            yield return Cast(School.Frost, "GLACIAL VOLLEY", 0.4f, "st_chill");
            if (aborted) yield break;
            int n = power >= 2 ? 5 : 3;
            for (int i = 0; i < n && !aborted; i++)
            {
                Vector2 from = Hand + Rot(Vector2.right, 90f + (i % 2 == 0 ? 35f : -35f)) * 0.4f;     // lances appear alternately over each shoulder
                Vector2 aim = (PlayerFeet + Vector2.up * 0.18f - from).normalized;
                var tel = DashTelegraph.Spawn(from, aim, 9f, 0.16f, Frost, 0.45f, Layer, Order - 2);
                // lance "freezes into being": fades from transparent to white, blue light grows, ice particles gather
                var ghost = SpriteVfx.Spawn("abs_ice_lance2", from, 1f, Layer, Order + 3, false, 12f, false);
                if (ghost != null) { ghost.Looping(0.6f); ghost.Tint(new Color(1f, 1f, 1f, 0.1f)); ghost.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg); }
                Fx("abs_charge2", from, 0.6f, Order + 4, 12f / 0.45f, Frost);
                var gl = GlowAt(from, Frost, 1.2f, 0f, 0.5f);
                for (float t = 0f; t < 0.45f && !aborted; t += Time.deltaTime)
                {
                    float u = t / 0.45f;
                    if (ghost != null) ghost.Tint(new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 0.95f, u)));
                    var l = gl != null ? gl.GetComponent<UnityEngine.Rendering.Universal.Light2D>() : null;
                    if (l != null) l.intensity = 1.4f * u;
                    yield return null;
                }
                if (tel != null) tel.Release();
                if (ghost != null) ghost.Stop();
                if (aborted) yield break;
                BigShot("abs_ice_lance2", from, aim * 9.5f, Frost, 1f, 10f, 0.12f, 1.4f, "abs_ice_shatter", 1f, Color.white);
                Fx("abs_ice_shatter", from, 0.8f, Order + 4, 22f, Color.white);
                FlashLight(from, Frost, 2f, 1.5f, 0.22f);
                Shake(1);
                Sfx.Play("ice_shatter", from, 0.45f);
                yield return Wait(power >= 2 ? 0.18f : 0.3f);
            }
            abs.Play("idle", 5f);
        }
    }
}
