using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// EYE / MOUTH moves (all of them LUNGE the head down first — VesselRig.Lunge). Current move tables:
    ///   P2 CINDER  — mouth: Magma Breath · eyes: Ember Wisps
    ///   P3 RIME    — mouth: Frost Breath, Whiteout · eyes: Glacial Cross (.Rework.cs)
    ///   P4 TEMPEST — mouth: Thunderstorm, BALL LIGHTNING (below) · eyes: Thunder Gaze (.Rework.cs)
    ///   P5 UMBRA   — mouth: Umbral Orbs, Devour · eyes: Sweeping Gaze, Shadow Watch (cone from the eye sockets)
    /// FROST GAZE (kept, not in any move table since Glacial Cross replaced it) — both eyes blaze ice blue, 2 eye beams cross the arena in an X through Ren
    ///               (2 warning lines 0.85 s) -> beams sweep 1 s, hit = 10 + frostbite; ice thorns rise behind the beams for 1.6 s (touch = 6 + frostbite).
    /// BALL LIGHTNING — opens its mouth and releases 3–4 lightning balls (PixelLab vc_ball_lightning) drifting after Ren for 6 s; every 0.9 s a ball close to Ren
    ///               (≤ 1.8 u) LOCKS a spot (thin flash 0.35 s) then strikes THAT SPOT (0.45 u) = 8 -> stepping away dodges it. Near a lightning rod (≤ 1.5 u) -> the ball
    ///               discharges into the rod and dies ("GROUNDED"). A sword hit breaks a ball.
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private float lungeGoal;

        // ------------------------------------------------------------------ P3: FROST GAZE
        private IEnumerator FrostGaze(float pressure)
        {
            DrillBoard.Say("FROST GAZE", IceCol);
            DrillBoard.Yell("Hold still. Let me look at you.", 1.6f);
            lungeGoal = 1f;
            yield return Wait(0.6f);
            Vector2[] seat = { EyeSocket(-1), EyeSocket(1) };
            var glow = new Light2D[2];
            for (int i = 0; i < 2; i++) glow[i] = BossJuice.Light(seat[i], IceCol, 1f, 1.8f, 0f);
            Vector2 c = ClampFloor(Feet + PlayerVel * 0.3f, 0.5f);
            float span = Float("gaze_span", 2.4f);
            // 2 beams crossing through Ren: top-left -> bottom-right, top-right -> bottom-left
            Vector2[] a = { ClampFloor(c + new Vector2(-span, 0.9f), 0.3f), ClampFloor(c + new Vector2(span, 0.9f), 0.3f) };
            Vector2[] b = { ClampFloor(c + new Vector2(span, -0.9f), 0.3f), ClampFloor(c + new Vector2(-span, -0.9f), 0.3f) };
            float warnT = Mathf.Lerp(0.85f, 0.7f, pressure * 0.5f);
            var warns = new List<BeamWarn>();
            for (int i = 0; i < 2; i++) { var w = BeamWarn.Spawn(a[i], b[i], 0.3f, IceWarn, warnT, layer, -13); if (w != null) warns.Add(w); }
            Sfx.Play("eye_beam_charge", seat[0], 0.8f);
            for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime) { SyncEyes(seat, glow); yield return null; }
            foreach (var w in warns) if (w != null) w.Release();
            if (aborted) { foreach (var g in glow) if (g != null) Object.Destroy(g.gameObject); lungeGoal = 0f; yield break; }
            var lines = new SpriteRenderer[6];
            for (int i = 0; i < 6; i++) lines[i] = NewLine("frost gaze", order + 3 + i % 3);
            var spikes = new List<(Vector2 p, float born)>();
            var last = new[] { a[0], a[1] };
            Sfx.Play("eye_beam", chest, 1f);
            Sfx.Play("trap_ice", chest, 0.7f);
            float dur = Mathf.Lerp(1.05f, 0.85f, pressure * 0.5f), nextHit = 0f, nextSpike = 0f;
            bool hit = false;
            for (float t = 0f; t < dur + 1.6f && !aborted; t += Time.deltaTime)
            {
                SyncEyes(seat, glow);
                if (t < dur)
                {
                    float u = t / dur, e = u * u * (3f - 2f * u);
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 p = Vector2.Lerp(a[i], b[i], e);
                        float wob = 1f + 0.12f * Mathf.Sin(Time.time * 37f + i);
                        StretchLine(lines[i * 3], seat[i], p, 0.26f * wob); lines[i * 3].color = new Color(0.55f, 0.85f, 1f, 0.32f);
                        StretchLine(lines[i * 3 + 1], seat[i], p, 0.1f * wob); lines[i * 3 + 1].color = new Color(0.75f, 0.95f, 1f, 0.95f);
                        StretchLine(lines[i * 3 + 2], seat[i], p, 0.035f); lines[i * 3 + 2].color = Color.white;
                        if ((p - last[i]).magnitude > 0.22f)                                // ice thorns rise behind the beam
                        {
                            SpriteVfx.Spawn("abs_ice_spikes", p, 1f, layer, p.y < Feet.y ? order + 1 : order - 1, Random.value < 0.5f, 16f, false);
                            spikes.Add((p, Time.time));
                            last[i] = p;
                        }
                        if (!hit && (Feet - p).magnitude < 0.4f && Hurt(10f, p, 0.7f)) { hit = true; Chill(0.6f, 1.6f, "Frozen by its gaze!"); }
                    }
                }
                else foreach (var l in lines) if (l != null) l.color = new Color(0f, 0f, 0f, 0f);
                if (Time.time >= nextSpike)
                {
                    nextSpike = Time.time + 0.12f;
                    spikes.RemoveAll(sp => Time.time - sp.born > 1.6f);
                }
                if (Time.time >= nextHit)
                    foreach (var sp in spikes)
                        if ((Feet - sp.p).magnitude < 0.3f && Hurt(6f, sp.p, 0.5f)) { nextHit = Time.time + 0.6f; Chill(0.7f, 1f, "Ice spikes!"); break; }
                yield return null;
            }
            foreach (var l in lines) DestroyObj(l);
            foreach (var g in glow) if (g != null) Object.Destroy(g.gameObject);
            lungeGoal = 0f;
            yield return Wait(0.3f);
        }

        // ------------------------------------------------------------------ P4: BALL LIGHTNING
        private sealed class Ball { public SpriteVfx fx; public Light2D l; public Vector2 p; public float born, nextZap; public bool dead, cut; }
        private readonly List<Ball> balls = new List<Ball>();
        private Vector2 BallCore(Ball b) => b.p + Vector2.up * 0.55f;

        private IEnumerator BallLightning(float pressure)
        {
            DrillBoard.Say("BALL LIGHTNING", StormCol);
            DrillBoard.Yell("Catch.", 1f);
            jawHold = 1f;                                                               // open mouth -> lunge down
            Sfx.Play("eye_beam_charge", rig.MouthWorld(), 0.7f);
            yield return Wait(0.75f);
            int n = pressure >= 1f ? 4 : 3;
            for (int k = 0; k < n && !aborted; k++)
            {
                Vector2 m = rig.MouthWorld();
                float ang = Mathf.Lerp(-150f, -30f, n > 1 ? k / (float)(n - 1) : 0.5f) * Mathf.Deg2Rad;
                Vector2 land = ClampFloor(new Vector2(m.x, Floor.yMax) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f) * 1.6f, 0.4f);
                var fx = SpriteVfx.Spawn("vc_ball_lightning", land, 0.9f, layer, order + 2, false, 12f, false);
                if (fx != null) fx.Looping(20f);
                var b = new Ball { fx = fx, l = BossJuice.Light(land, StormCol, 1.3f, 1f, 0f), p = land, born = Time.time, nextZap = Time.time + 1.2f + 0.3f * k };
                balls.Add(b);
                SpriteVfx.Spawn("vc_volt_impact", m, 0.6f, layer, order + 3, false, 22f, false);
                Sfx.Play("bolt_strike", m, 0.4f);
                StartR(BallRun(b));
                yield return Wait(0.25f);
            }
            jawHold = -1f;
            yield return Wait(1.2f);
        }

        private IEnumerator BallRun(Ball b)
        {
            float life = Float("ball_seconds", 6f);
            while (!aborted && !b.dead && !b.cut && Time.time - b.born < life)
            {
                float now = Time.time;
                b.p = Vector2.MoveTowards(b.p, ClampFloor(Feet, 0.3f), Float("ball_speed", 1.1f) * Time.deltaTime);
                if (b.fx != null) b.fx.transform.position = b.p;
                if (b.l != null) { b.l.transform.position = BallCore(b); b.l.intensity = 0.9f + 0.3f * Mathf.Sin(now * 17f); }
                // near a lightning rod -> grounded
                Rod rod = null;
                foreach (var r in rods) if (!r.dead && (r.p - b.p).magnitude < 1.5f) { rod = r; break; }
                if (rod != null)
                {
                    StartR(FlashArc(BallCore(b), RodTip(rod), 0.35f));
                    SpriteVfx.Spawn("vc_volt_impact", RodTip(rod), 0.7f, layer, order + 3, false, 22f, false);
                    DrillBoard.Say("GROUNDED", StormCol);
                    break;
                }
                if (now >= b.nextZap)
                {
                    b.nextZap = now + Float("ball_zap_every", 0.9f) + Random.Range(0f, 0.3f);
                    if ((Feet - b.p).magnitude < 1.8f) StartR(BallZap(b, Feet));
                }
                yield return null;
            }
            if (b.fx != null) b.fx.Stop();
            if (b.l != null) Object.Destroy(b.l.gameObject);
            SpriteVfx.Spawn("vc_volt_impact", BallCore(b), b.cut ? 0.9f : 0.6f, layer, order + 3, false, 22f, false);
            if (b.cut) { Sfx.Play("glass_break", b.p, 0.4f); DrillBoard.Say("POPPED", StormCol); }
            b.dead = true;
            balls.Remove(b);
        }

        /// <summary>Lock the spot where Ren stands -> thin flash 0.35 s -> lightning strikes that spot.</summary>
        private IEnumerator BallZap(Ball b, Vector2 at)
        {
            var ring = NewRing(at, 0.45f);
            var thin = MakeArc(BallCore(b), at, 6, false);
            thin.Jag = 0.05f;
            for (float t = 0f; t < 0.35f && !aborted && !b.dead && !b.cut; t += Time.deltaTime)
            {
                thin.A = BallCore(b); thin.B = at;
                thin.Alpha = Mathf.Sin(Time.time * 45f) > 0f ? 0.35f : 0.08f;
                JitterArc(thin, false);
                float rk = 0.45f / (Ring().bounds.size.x * 0.5f);
                ring.transform.localScale = new Vector3(rk, rk * 0.6f, 1f);
                ring.color = new Color(StormWarn.r, StormWarn.g, StormWarn.b, 0.5f + 0.4f * Mathf.Sin(t * 40f));
                yield return null;
            }
            KillArc(thin); DestroyObj(ring);
            if (aborted || b.dead || b.cut) yield break;
            StartR(FlashArc(BallCore(b), at, 0.25f));
            SpriteVfx.Spawn("vc_volt_impact", at, 0.6f, layer, order + 3, false, 22f, false);
            if ((Feet - at).magnitude < 0.45f) Shock(8f, at, 0.6f, 0f);
        }

        /// <summary>Sword hit on a lightning ball -> it breaks.</summary>
        private void StrikeBalls(Vector2 c, float r)
        {
            foreach (var b in balls) if (!b.dead && (BallCore(b) - c).magnitude < r + 0.45f) b.cut = true;
        }

        private void KillBalls()
        {
            foreach (var b in balls) { b.dead = true; if (b.fx != null) b.fx.Stop(); if (b.l != null) Object.Destroy(b.l.gameObject); }
            balls.Clear();
        }
    }
}
