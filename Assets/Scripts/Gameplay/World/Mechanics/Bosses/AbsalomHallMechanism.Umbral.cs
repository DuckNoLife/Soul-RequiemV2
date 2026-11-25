using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// UMBRAL school (violet) — FORCES POSITION: pulling, chasing, delayed bursts.
    ///   Umbral Orbs  — 3 dark orbs (power 1: 2) fly slowly, home lightly (55°/s, power 1: 40°/s), spawn 0.6 s apart; they burst into a ring of 6 shards
    ///                  when they reach Ren (fuseRadius), hit a wall or expire. The ring's GAP always faces Ren (stand still / roll sideways to escape).
    ///                  Dodge: drag them away and stand outside when they burst.
    ///   Abyssal Well — 0.8 s warning ring, then a BLACK HOLE opens between Ren and the hall centre for 3.2 s: PULLS Ren toward its centre (walking against
    ///                  it still escapes), the centre burns per tick, 3 pulses × 4 slow dark shards (pulse 1.3 s; power 1: 1.6 s); closing = a ring of 8 shards
    ///                  with a 2-shard GAP toward Ren. Dodge: walk against the pull, avoid the shard spirals.
    /// Visuals: Umbral Orbs = 103 px VOID ORBS (black core, bright violet rim, tail + sparkle), self-lit violet; burst = abs_void_burst (violet flash,
    ///   dark smoke, rays) + light + shake, shards = abs_void_shard. Abyssal Well = VOID VORTEX on the floor (abs_well2 104 px, colour rings flowing INTO
    ///   the centre, rising particles) + breathing violet light, VIOLET PARTICLES SUCKED in from afar (shows the pull strength and direction); closing = big burst + shake 2.
    /// Called by: ElementSkill / PressureSkill (.Lamps.cs) and the Ctrl+F7 showcase. Uses: Shot, Hazard, PullRen (.Skills.cs).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private IEnumerator UmbralOrbs(int power)
        {
            yield return Cast(School.Umbral, "UMBRAL ORBS", 0.5f, "teleport_dark");
            if (aborted) yield break;
            Vector2 aim = (PlayerFeet - Hand).normalized;
            int n = power >= 2 ? 3 : 2;
            Fx("abs_void_burst", Hand, 0.9f, Order + 4, 18f, Color.white);
            FlashLight(Hand, Umbral, 2f, 1.4f, 0.3f);
            for (int i = 0; i < n; i++)
            {
                // orbs home + burst when within 0.55 u of Ren (fuseRadius); hitting a wall / Ren also bursts them into shards.
                // Tuning: turn rate 55°/s (power 1: 40°/s), acceleration 0.15, life 5.5 s, spawn interval 0.6 s
                var b = BigShot("abs_void_orb", Hand, Rot(aim, (i - (n - 1) * 0.5f) * 38f) * 1.35f, Umbral, 1f, 10f, 0.18f, 5.5f,
                                "none", 1f, Color.white);
                b.homing = power >= 2 ? 55f : 40f;
                b.accel = 0.15f;
                b.fuseRadius = 0.55f;
                b.expireOnPop = true;
                b.onExpire = OrbBurst;
                yield return Wait(0.6f);                                                // orbs come one by one, spaced enough to drag each one away
            }
            yield return Wait(0.3f);
            abs.Play("idle", 5f);
        }

        private void OrbBurst(HallBullet b)
        {
            if (aborted || b == null) return;
            Vector2 p = b.transform.position;
            // 6 slow shards; the ring's GAP faces Ren -> a close-range burst can still be dodged (stand still / roll sideways)
            RingWithGap(p, 6, 1.55f, 3f);
            Sfx.Play("teleport_dark", p, 0.45f);
            Fx("abs_void_burst", p, 1.4f, Order + 3, 16f, Color.white);
            FlashLight(p, Umbral, 2.4f, 1.8f, 0.4f);
            Shake(1);
        }

        /// <summary>Ring of `n` void shards around `p`, arranged so the direction to Ren lies BETWEEN two shards (escape gap); `skip` = drop that many extra
        /// shards on each side of the gap (wider gap). Used by every Umbral ring burst.</summary>
        private void RingWithGap(Vector2 p, int n, float speed, float life, int skip = 0)
        {
            float step = 360f / n;
            Vector2 toRen = PlayerFeet - p;
            float face = toRen.sqrMagnitude > 0.0001f ? Mathf.Atan2(toRen.y, toRen.x) * Mathf.Rad2Deg : Random.Range(0f, 360f);
            float a0 = face + step * 0.5f;                                            // shard 0 and shard n-1 straddle the direction to Ren
            for (int k = 0; k < n; k++)
            {
                if (skip > 0 && (k < skip || k >= n - skip)) continue;                // drop the shards next to the gap
                VoidShard(p, Rot(Vector2.right, a0 + k * step) * speed, 8f, life);
            }
        }

        /// <summary>Void shard (small 56 px tailed bullet, breaks into a small violet flash).</summary>
        private HallBullet VoidShard(Vector2 at, Vector2 vel, float dmg, float life)
        {
            var s = Shot("abs_void_shard", at, vel, Color.white, 1f, dmg, 0.1f, life);
            if (s != null) { s.popFx = "abs_void_burst"; s.popScale = 0.45f; }
            return s;
        }

        private IEnumerator AbyssalWell(int power)
        {
            yield return Cast(School.Umbral, "ABYSSAL WELL", 0.45f, "sz_inhale");
            if (aborted) yield break;
            Vector2 at = InArena(Vector2.Lerp(PlayerFeet, A.center, 0.45f) + Random.insideUnitCircle * 0.3f, 0.8f);
            var warn = RingMark(at, 0.5f, 0.5f, new Color(Umbral.r, Umbral.g, Umbral.b, 0.45f), 0.8f);
            var warnRim = RingMark(at, 0.62f, 0.05f, new Color(Umbral.r, Umbral.g, Umbral.b, 0.9f), 0.8f);
            yield return Wait(0.8f);
            if (aborted) yield break;
            float dur = power >= 2 ? 3.2f : 2.6f;
            var h = AddHazard(HazardShape.Circle, at, at, 0.32f, 0f, 10f, 0f, dur, 0.5f);
            HazardFx(h, "abs_well2", at, 1.25f, Order - 2, 12f, Color.white);
            var glowGo = GlowAt(at, Umbral, 2.4f, 1.2f, 0f);
            h.Objs.Add(glowGo);
            Fx("abs_void_burst", at, 1.8f, Order + 3, 16f, Color.white);
            Shake(2);
            FlashLight(at, Umbral, 3.4f, 2f, 0.45f);
            Sfx.Play("life_drain", at, 0.8f);
            FirstTimeHints.Show("tut_abyssal_well", "The well pulls you in. Walk against the pull - stay out of its heart.", 4.5f);
            wellAt = at; wellSpeed = power >= 2 ? 0.9f : 0.72f; wellPull = true;                       // gentle pull strength
            float t0 = Time.time, nextSpit = Time.time + 0.8f, nextMote = Time.time;
            float spin = Random.Range(0f, 360f);
            var gl = glowGo != null ? glowGo.GetComponent<UnityEngine.Rendering.Universal.Light2D>() : null;
            while (!aborted && Time.time - t0 < dur)
            {
                if (TimeStop.Active) { t0 += Time.deltaTime; nextSpit += Time.deltaTime; yield return null; continue; }
                if (gl != null) gl.intensity = 1.1f + 0.4f * Mathf.Sin(Time.time * 6f);
                // violet particles SUCKED in from afar (harmless) — shows how strong the pull is and where it points
                if (Time.time >= nextMote)
                {
                    nextMote = Time.time + 0.06f;
                    float ang = Random.Range(0f, 360f), rr = Random.Range(1.6f, 2.8f);
                    Vector2 from = at + Rot(Vector2.right, ang) * rr;
                    Vector2 v = Rot((at - from).normalized, 28f) * Random.Range(2.4f, 3.2f);
                    var m = Shot("abs_void_shard", from, v, new Color(1f, 1f, 1f, 0.8f), 0.8f, 0f, 0f, rr / v.magnitude * 0.95f);
                    m.harmless = true; m.stopOnWalls = false; m.popFx = "none";
                }
                if (Time.time >= nextSpit)
                {
                    nextSpit = Time.time + (power >= 2 ? 1.3f : 1.6f);                        // pulse interval
                    for (int k = 0; k < 4; k++)                                               // 4 shards per pulse
                    {
                        Vector2 d = Rot(Vector2.right, spin + k * 90f);
                        var b = VoidShard(at + d * 0.3f, Rot(d, 25f) * 1.15f, 8f, 3.5f);
                        if (b != null) b.homing = 0f;
                    }
                    spin += 37f;
                    Fx("abs_void_burst", at, 0.8f, Order + 3, 18f, Color.white);
                    Sfx.Play("hymn_pop", at, 0.35f);
                }
                yield return null;
            }
            wellPull = false;
            h.Dead = true;
            if (aborted) yield break;
            Fx("abs_void_burst", at, 2.2f, Order + 3, 14f, Color.white);
            FlashLight(at, Umbral, 4f, 2.4f, 0.6f);
            Shake(2);
            Sfx.Play("teleport_dark", at, 0.8f);
            RingWithGap(at, 10, 1.6f, 3f, 1);                                           // 8 shards + wide gap toward Ren
            abs.Play("idle", 5f);
        }
    }
}
