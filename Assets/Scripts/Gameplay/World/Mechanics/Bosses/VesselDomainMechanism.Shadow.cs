using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE 5 UMBRA (shadow, original violet, the map goes fully dark). Same recipe as Phases 3–4: a PHASE RULE + a COUNTER + second-half ESCALATION + subtle VFX.
    /// Eye / mouth moves all LUNGE onto the arena; Shadow Watch shines from the Vessel's eye sockets. The tower lights are watchers here too (.Light.cs);
    /// there is no "light dispels darkness" rule any more.
    ///   Counter       — SKULL ANVIL: after each slam its chain flashes BLUE for a moment — strike the chain to cut it -> the anvil breaks, the chain snaps back
    ///                   and the hand holding it crashes down on that spot and lies for 4 s = the biggest counter window of the phase.
    ///   SWEEPING GAZE — both eyes blaze, a HORIZONTAL BAND on Ren's row is warned for 1 s -> violet lasers sweep along the band from one edge to the other,
    ///                   ploughing a void trench. Dodge: leave the band (up / down). High pressure: a second sweep on a new row, opposite direction.
    ///   UMBRAL ORBS   — the mouth releases 5–7 slowly homing dark orbs (PixelLab void orbs rotating toward their flight direction); near Ren they SWELL 0.3 s
    ///                   then burst (0.85 u, 10). A sword hit breaks an orb harmlessly.
    ///   SHADOW WATCH  — shadow CONES from the Vessel's eye sockets crawl after Ren (slower than Ren). Inside a cone pool for 0.6 s -> INK TETHER
    ///                   (ShadowSnare -> ink tether from .Rework.cs: pinned within `tether_len`, low damage).
    ///   SINGULARITY   — replaces BLACK HOLE (.Rework.cs). BlackHole() is the older version, unused.
    ///   SKULL ANVIL   — a skull anvil (Volcano pack crusher) rises and hops after Ren for ~10 s at 50 % speed; each landing has a PENTAGON RING warning (hit = 14).
    ///   SKULL RAIN    — 10–14 violet skulls (PixelLab) fall diagonally; a skull mark + shrinking ring warns 0.9 s ahead; hit = 12.
    ///   DEVOUR        — lowers the head, opens the mouth and PULLS Ren toward the north edge for 3.2 s (2.4 u/s, Ren walks at 4 u/s -> resistible);
    ///                   touching the mouth = BITE 22. High pressure: skulls fall on the path.
    ///   RAKE / GRAB / SLAM — like Phase 1 (violet).
    ///   ECLIPSE (phase HP &lt; 50 %, about every 25 s) — the arena goes dark for 10 s; SHADOW HANDS rise under Ren's feet every ~1 s (0.6 s warning,
    ///                   bind 1.2 s + 8) and dark orbs crawl in from the arena edges; both watchtowers search. Keep moving.
    /// Assets: _tools/s429/make_umbra.py (vc_black_hole, vc_skull_meteor, vc_skull_anvil, vc_anvil_seal) + existing abs_umbral_orb / abs_umbral_burst / abs_grasp / void_eye.
    /// Called by: PhaseLoop (move table), TickFight (TickUmbra), OnStruckHand (orbs, anvil chain).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static readonly Color UmbraCol = new Color(0.75f, 0.4f, 1f);
        private static readonly Color UmbraDeep = new Color(0.32f, 0.08f, 0.5f);
        private float nextEclipse, eclipseDim;
        private bool eclipseOn, anvilAlive, watchOn, anvilHinted;
        private readonly List<UOrb> uorbs = new List<UOrb>();

        private Move[] UmbraMoves() => new[]
        {
            new Move { name = "SLAM", w = 1.3f, slam = true, run = pr => Slam(Feet.x < chest.x ? -1 : 1, pr) },
            new Move { name = "SWEEPING GAZE", w = 1.3f, run = SweepingGaze },
            new Move { name = "UMBRAL ORBS", w = 1.3f, run = UmbralOrbs },
            new Move { name = "SHADOW WATCH", w = watchOn ? 0.05f : 1.1f, run = ShadowWatch },
            new Move { name = "SINGULARITY", w = 1.2f, run = Singularity },             // reworked black hole
            new Move { name = "RAKE", w = 0.8f, run = Rake },
            new Move { name = "SKULL ANVIL", w = anvilAlive ? 0f : 1.4f, run = SkullAnvil },
            new Move { name = "SKULL RAIN", w = 1.2f, run = SkullRain },
            new Move { name = "GRAB", w = 0.8f, run = Grab },
            new Move { name = "DEVOUR", w = 1.1f, run = Devour },
            new Move { name = "ECLIPSE", w = vhp < 0.5f && Time.time >= nextEclipse ? 2.4f : 0f, run = Eclipse },
        };

        /// <summary>Every frame (TickFight): dark orbs, Eclipse darkness.</summary>
        private void TickUmbra(float dt)
        {
            eclipseDim = Mathf.MoveTowards(eclipseDim, eclipseOn ? 1f : 0f, dt * 1.2f);
            TickOrbs(dt);
        }

        private void KillUmbra()
        {
            foreach (var o in uorbs) o.dead = true;
            uorbs.Clear();
            eclipseOn = false; anvilAlive = false; watchOn = false;
        }

        // ------------------------------------------------------------------ SWEEPING GAZE
        private IEnumerator SweepingGaze(float pressure)
        {
            DrillBoard.Say("SWEEPING GAZE", UmbraCol);
            DrillBoard.Yell("Nothing escapes my sight.", 1.4f);
            int passes = pressure >= 1.2f ? 2 : 1;
            lungeGoal = 1f;                                                             // lunge down onto the arena before the eye sweep
            yield return Wait(0.6f);
            var eyeL = BossJuice.Light(EyeSocket(-1), UmbraCol, 1.2f, 0f, 0f);
            var eyeR = BossJuice.Light(EyeSocket(1), UmbraCol, 1.2f, 0f, 0f);
            int dir = Feet.x < Floor.center.x ? -1 : 1;                                 // sweep FROM the side far from Ren -> time to read it
            for (int pass = 0; pass < passes && !aborted; pass++)
            {
                float y = Mathf.Clamp(Feet.y, Floor.yMin + 0.3f, Floor.yMax - 0.5f);
                float x0 = dir < 0 ? Floor.xMax - 0.1f : Floor.xMin + 0.1f, x1 = dir < 0 ? Floor.xMin + 0.1f : Floor.xMax - 0.1f;
                float warnT = Mathf.Lerp(1.05f, 0.85f, pressure * 0.5f);
                var band = Warn(new Vector2(Floor.xMin, y), new Vector2(Floor.xMax, y), 0.3f, WarnViolet, warnT);
                Sfx.Play("eye_beam_charge", chest, 0.8f);
                for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime)
                {
                    float u = t / warnT;
                    if (eyeL != null) eyeL.intensity = 0.4f + 2.2f * u;
                    if (eyeR != null) eyeR.intensity = 0.4f + 2.2f * u;
                    jawHold = 0.3f;
                    yield return null;
                }
                if (band != null) band.Release();
                if (aborted) break;
                // LASER: 2 "inverted light" beams (pale pink core, violet body, dark violet rim, glow + star flash + particles running along) from the 2 eye sockets meeting at the sweep point;
                // behind them a burning ink trench (bright core fading, ink smoke rising)
                var bl = NewBeam(VoidRim, VoidMid, VoidEdge, 0.2f, order + 4);
                var bR = NewBeam(VoidRim, VoidMid, VoidEdge, 0.2f, order + 4);
                var trench = new List<(FxBeam b, Vector2 a, Vector2 e, float born)>();
                Sfx.Play("eye_beam", chest, 1f);
                BossJuice.Shake(1);
                float dur = Mathf.Lerp(1.45f, 1.1f, pressure * 0.5f), lastX = x0;
                bool hurt = false;
                for (float t = 0f; t < dur && !aborted; t += Time.deltaTime)
                {
                    float u = t / dur, e = u * u * (3f - 2f * u);
                    float x = Mathf.Lerp(x0, x1, e);
                    Vector2 p = new Vector2(x, y);
                    float kk = t < 0.08f ? Mathf.Lerp(0.3f, 1.3f, t / 0.08f) : 1f;
                    SetBeam(bl, EyeSocket(-1), p + Vector2.left * 0.06f, 1f, kk);
                    SetBeam(bR, EyeSocket(1), p + Vector2.right * 0.06f, 1f, kk);
                    if (Mathf.Abs(x - lastX) > 0.22f)
                    {
                        var sb = NewBeam(new Color(0.95f, 0.75f, 1f, 1f), new Color(0.45f, 0.15f, 0.7f, 0.95f), new Color(0.12f, 0.03f, 0.2f, 0.8f), 0.16f, -14, true);
                        trench.Add((sb, new Vector2(lastX, y), p, Time.time));
                        for (int q = 0; q < 3; q++) AddMote(p + Random.insideUnitCircle * 0.1f, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.4f)), new Color(0.2f, 0.06f, 0.3f, 0.9f), 0.7f, 0.05f);
                        lastX = x;
                    }
                    if (!hurt && Mathf.Abs(Feet.y - y) < 0.27f && Mathf.Abs(Feet.x - x) < 0.4f) hurt = Hurt(14f, new Vector2(x, y + 0.5f), 1.1f);
                    InkTrench(trench);
                    yield return null;
                }
                KillBeam(bl); KillBeam(bR);
                for (float t = 0f; t < 1.2f && !aborted; t += Time.deltaTime) { InkTrench(trench); yield return null; }
                foreach (var tr in trench) KillBeam(tr.b);
                dir = -dir;
                if (pass + 1 < passes) yield return Wait(0.3f);
            }
            jawHold = -1f;
            lungeGoal = 0f;
            if (eyeL != null) Object.Destroy(eyeL.gameObject);
            if (eyeR != null) Object.Destroy(eyeR.gameObject);
            yield return Wait(0.3f);
        }

        /// <summary>Ink trench of Sweeping Gaze — bright core fades over 1.6 s, thinning.</summary>
        private void InkTrench(List<(FxBeam b, Vector2 a, Vector2 e, float born)> list)
        {
            foreach (var tr in list)
            {
                float age = Mathf.Clamp01((Time.time - tr.born) / 1.6f);
                SetBeam(tr.b, tr.a, tr.e, 0.9f * (1f - age), Mathf.Lerp(1f, 0.45f, age));
            }
        }

        private static void FadeTrench(List<(SpriteRenderer sr, float born)> list)
        {
            foreach (var (s, born) in list)
            {
                if (s == null) continue;
                float a = Mathf.Clamp01(1f - (Time.time - born) / 1.2f);
                s.color = new Color(0.55f, 0.2f, 0.85f, 0.9f * a);
            }
        }

        // ------------------------------------------------------------------ UMBRAL ORBS (homing dark orbs, burst when close, break on a sword hit)
        private sealed class UOrb
        {
            public SpriteVfx fx;
            public Light2D l;
            public Vector2 p, v;
            public float born, life, armAt = -1f, speed;
            public bool dead, cut;
        }

        private IEnumerator UmbralOrbs(float pressure)
        {
            DrillBoard.Say("UMBRAL ORBS", UmbraCol);
            DrillBoard.Yell("Swallow the dark, little one.", 1.4f);
            jawHold = 1f;
            Sfx.Play("gas_hiss", rig.MouthWorld(), 0.8f);
            yield return Wait(0.7f);                                                     // wait for the Vessel to lunge down (jawHold -> Lunge)
            int n = Mathf.RoundToInt(Mathf.Lerp(5f, 7f, pressure * 0.5f));
            for (int i = 0; i < n && !aborted; i++)
            {
                Vector2 m = rig.MouthWorld();
                float a = Mathf.Lerp(-150f, -30f, n > 1 ? i / (float)(n - 1) : 0.5f) * Mathf.Deg2Rad;
                SpawnOrb(m, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.2f, Float("orb_speed", 2.1f), 4.5f);
                Sfx.Play("trap_fire", m, 0.35f);
                yield return Wait(0.18f);
            }
            jawHold = -1f;
            if (!orbHinted) { orbHinted = true; Hints.Show("Strike an umbral orb to burst it before it reaches you.", 3f); }
            yield return Wait(1.6f);
        }

        private void SpawnOrb(Vector2 at, Vector2 v, float speed, float life)
        {
            // PixelLab void orb (black core, bright rim, tapered tail),
            // pivot at the orb head, ROTATES TO ITS FLIGHT DIRECTION every frame, bigger (1.35)
            var fx = SpriteVfx.Spawn("vc_umbral_orb", at, OrbScale, layer, order + 3, false, 12f, false);
            if (fx == null) fx = SpriteVfx.Spawn("abs_umbral_orb", at, 1.5f, layer, order + 3, false, 14f, false);
            if (fx == null) return;
            fx.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            fx.Looping(life + 2f);
            var l = BossJuice.Light(at, UmbraCol, 0.9f, 0.8f, 0f);
            uorbs.Add(new UOrb { fx = fx, l = l, p = at, v = v, born = Time.time, life = life, speed = speed });
        }

        private void TickOrbs(float dt)
        {
            float now = Time.time;
            Vector2 target = Feet + Vector2.up * 0.3f;
            for (int i = uorbs.Count - 1; i >= 0; i--)
            {
                var o = uorbs[i];
                if (o.dead || o.fx == null) { KillOrb(o, false); uorbs.RemoveAt(i); continue; }
                if (o.cut)                                                               // Ren's sword hit -> breaks harmlessly
                {
                    SpriteVfx.Spawn("vc_umbral_pop", o.p, 0.9f, layer, order + 3, false, 18f, false);
                    Sfx.Play("glass_break", o.p, 0.35f);
                    KillOrb(o, false); uorbs.RemoveAt(i); continue;
                }
                if (o.armAt < 0f)
                {
                    Vector2 want = (target - o.p).normalized * o.speed;
                    o.v = Vector2.MoveTowards(o.v, want, 3.2f * dt);                     // slow turning -> rolling / circling dodges it
                    o.p += o.v * dt;
                    if (o.v.sqrMagnitude > 0.0001f)
                    {
                        float want2 = Mathf.Atan2(o.v.y, o.v.x) * Mathf.Rad2Deg;
                        o.fx.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(o.fx.transform.eulerAngles.z, want2, 720f * dt));
                    }
                    if ((target - o.p).magnitude < 0.55f || now - o.born > o.life) { o.armAt = now; Sfx.Play("eye_beam_charge", o.p, 0.35f); }
                }
                else
                {
                    float u = (now - o.armAt) / 0.3f;
                    o.fx.transform.localScale = Vector3.one * OrbScale * (1f + 0.45f * u);
                    o.fx.Tint(Mathf.Sin(now * 50f) > 0f ? Color.white : new Color(1f, 0.6f, 1f));
                    if (o.l != null) o.l.intensity = 0.8f + 2f * u;
                    if (u >= 1f)
                    {
                        SpriteVfx.Spawn("vc_umbral_pop", o.p, 1.5f, layer, order + 3, false, 16f, false);
                        SpriteVfx.Spawn("abs_umbral_burst", o.p, 1.2f, layer, order + 2, false, 20f, false);
                        Sfx.Play("bolt_strike", o.p, 0.35f);
                        if ((target - o.p).magnitude < 0.85f) Hurt(10f, o.p, 0.9f);
                        KillOrb(o, false); uorbs.RemoveAt(i); continue;
                    }
                }
                o.fx.transform.position = o.p;
                if (o.l != null) o.l.transform.position = o.p;
            }
        }

        private const float OrbScale = 1.35f;
        private bool orbHinted;

        /// <summary>Sword hit on a dark orb -> it breaks.</summary>
        private void StrikeOrbs(Vector2 c, float r)
        {
            foreach (var o in uorbs) if (!o.dead && (o.p - c).magnitude < r + 0.45f) o.cut = true;
        }

        private static void KillOrb(UOrb o, bool fx)
        {
            o.dead = true;
            if (o.fx != null) o.fx.Stop();
            if (o.l != null) Object.Destroy(o.l.gameObject);
        }

        // ------------------------------------------------------------------ SHADOW WATCH (shadow cones from the eyes, ink tether)
        private IEnumerator ShadowWatch(float pressure)
        {
            watchOn = true;
            DrillBoard.Say("SHADOW WATCH", UmbraCol);
            DrillBoard.Yell("Stand still in my shadow.", 1.4f);
            lungeGoal = 1f;                                                             // shadow cones shine from the Vessel's OWN 2 eye sockets (lunged down)
            yield return Wait(0.6f);
            Vector2[] seat = { EyeSocket(-1), EyeSocket(1) };
            var eyes = new SpriteVfx[2];
            var eyeGlow = new Light2D[2];
            var cones = new SpriteRenderer[2];
            var pools = new SpriteRenderer[2];
            var rings = new SpriteRenderer[2];
            var aims = new Vector2[2];
            var expo = new float[2];
            var coneS = ConeSprite();                                                  // soft hand-drawn cone (bright edge, gradient inside)
            for (int i = 0; i < 2; i++)
            {
                eyeGlow[i] = BossJuice.Light(seat[i], UmbraCol, 1f, 1.6f, 0f);
                SpriteVfx.Spawn("vc_umbral_pop", seat[i], 0.7f, layer, order + 5, false, 16f, false);
                if (coneS != null)
                {
                    cones[i] = NewLine("shadow cone", order + 3);
                    cones[i].sprite = coneS;
                }
                pools[i] = NewLine("shadow pool", -14);
                pools[i].sprite = Disc();
                rings[i] = NewRing(Feet, 0.75f);
                aims[i] = ClampFloor(Feet + new Vector2(i == 0 ? -2.2f : 2.2f, 0.6f), 0.6f);
            }
            Sfx.Play("eye_beam_charge", chest, 0.7f);
            float dur = Float("watch_seconds", 6f) + (pressure >= 1.2f ? 1f : 0f), speed = Float("watch_speed", 1.6f);
            const float R = 0.75f, Ry = 0.45f, Bind = 0.6f;
            for (float t = 0f; t < dur && !aborted; t += Time.deltaTime)
            {
                float a = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((dur - t) / 0.4f);
                SyncEyes(seat, eyeGlow);
                for (int i = 0; i < 2; i++)
                {
                    Vector2 e = EyePos(eyes, seat, i);
                    aims[i] = Vector2.MoveTowards(aims[i], ClampFloor(Feet + PlayerVel * 0.2f, 0.4f), speed * Time.deltaTime);
                    if (cones[i] != null)
                    {
                        Vector2 d = aims[i] - e;
                        float L = Mathf.Max(0.1f, d.magnitude);
                        cones[i].transform.position = e;
                        cones[i].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg);
                        cones[i].transform.localScale = new Vector3(R * 2f / coneS.bounds.size.x, L / coneS.bounds.size.y, 1f);
                        cones[i].color = new Color(0.72f, 0.42f, 1f, 0.85f * a);
                        if (Random.value < 0.45f) AddMote(Vector2.Lerp(e, aims[i], Random.Range(0.35f, 1f)) + Random.insideUnitCircle * 0.15f, (aims[i] - e).normalized * 0.35f, new Color(0.9f, 0.7f, 1f, 0.8f * a), 0.8f, 0.03f);   // dust floating in the cone
                    }
                    pools[i].transform.position = aims[i];
                    pools[i].transform.localScale = new Vector3(R * 2f, Ry * 2f, 1f);
                    pools[i].color = new Color(0.4f, 0.18f, 0.6f, 0.3f * a);                     // faint lit pool (seal ring around it)
                    // Ren inside a cone pool -> count up time; enough -> tether
                    Vector2 q = Feet - aims[i];
                    bool inside = (q.x * q.x) / (R * R) + (q.y * q.y) / (Ry * Ry) <= 1f;
                    expo[i] = inside ? expo[i] + Time.deltaTime : Mathf.Max(0f, expo[i] - Time.deltaTime * 2f);
                    float k = expo[i] / Bind;
                    float rk = Mathf.Lerp(R, 0.3f, k) / (Ring().bounds.size.x * 0.5f);
                    rings[i].transform.position = aims[i];
                    rings[i].transform.localScale = new Vector3(rk, rk * (Ry / R), 1f);
                    rings[i].color = new Color(0.85f, 0.45f, 1f, (0.35f + 0.6f * k) * a);
                    if (expo[i] >= Bind)
                    {
                        expo[0] = expo[1] = 0f;
                        ShadowSnare(1.6f, 6f, "SHADOW-BOUND");
                    }
                }
                yield return null;
            }
            for (int i = 0; i < 2; i++)
            {
                if (eyeGlow[i] != null) Object.Destroy(eyeGlow[i].gameObject);
                DestroyObj(cones[i]); DestroyObj(pools[i]); DestroyObj(rings[i]);
            }
            watchOn = false;
            lungeGoal = 0f;
            yield return Wait(0.3f);
        }

        /// <summary>Shadow snare under Ren's feet for `stun` s (3 s immunity afterwards) + `dmg`.</summary>
        private void ShadowSnare(float stun, float dmg, string say)
        {
            // no stun — the INK TETHER pins Ren within `tether_len`, damage reduced to 35 %
            StartTether(Float("tether_seconds", 4f), dmg * 0.35f, say);
        }

        // ------------------------------------------------------------------ BLACK HOLE
        private IEnumerator BlackHole(float pressure)
        {
            DrillBoard.Say("BLACK HOLE", UmbraCol);
            DrillBoard.Yell("Fall into me.", 1.2f);
            Vector2 c = Feet;
            for (int k = 0; k < 8; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                c = ClampFloor(Feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * 2.2f, 1f);
                if ((c - Feet).magnitude > 1.6f) break;
            }
            const float Fps = 7f, BurstAt = 21f / Fps, Pull0 = 0.3f, Pull1 = 2.85f;
            var bh = SpriteVfx.Spawn("vc_black_hole", c, 1.4f, layer, c.y < Feet.y ? order + 1 : order - 1, false, Fps, false);
            var l = BossJuice.Light(c, UmbraCol, 2.4f, 0f, 0f);
            Sfx.Play("teleport_dark", c, 1f);
            var st = RenStatus;
            float nextCore = 0f, a0 = Random.Range(0f, 45f);
            var warns = new List<BeamWarn>();
            bool warned = false;
            for (float t = 0f; t < BurstAt && !aborted; t += Time.deltaTime)
            {
                Vector2 d = c - Feet;
                float dist = d.magnitude;
                if (t > Pull0 && t < Pull1 && st != null && dist > 0.05f)
                {
                    float s = Float("hole_pull", 2.2f) * Mathf.Clamp01(1.2f - dist / 5f);
                    st.Push(d / dist * s, 0.12f);
                }
                if (dist < 0.55f && Time.time >= nextCore && Hurt(6f, c, 0f)) nextCore = Time.time + 0.4f;
                if (l != null) l.intensity = Mathf.Lerp(0.5f, 1.8f, t / BurstAt) * (0.85f + 0.15f * Mathf.Sin(t * 20f));
                if (!warned && t >= BurstAt - 0.6f)                                     // 8 burst rays: warning
                {
                    warned = true;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector2 dir = RayDir(a0 + k * 45f);
                        var w = BeamWarn.Spawn(c, c + dir * 3.2f, 0.22f, WarnViolet, 0.6f, layer, -13);
                        if (w != null) warns.Add(w);
                    }
                }
                yield return null;
            }
            foreach (var w in warns) if (w != null) w.Release();
            if (aborted) { if (l != null) Object.Destroy(l.gameObject); yield break; }
            // BURST: 8 dark rays
            Sfx.Play("bolt_strike", c, 1f);
            Sfx.Play("glass_break", c, 0.6f);
            BossJuice.Shake(2);
            BossJuice.Flash(c, UmbraCol, 3.5f, 3f, 0.3f);
            var rays = new List<(SpriteRenderer a, SpriteRenderer b, Vector2 end)>();
            for (int k = 0; k < 8; k++)
            {
                Vector2 end = c + RayDir(a0 + k * 45f) * 3.2f;
                var ha = NewLine("hole ray", order + 3); var co = NewLine("hole ray core", order + 4);
                StretchLine(ha, c, end, 0.26f); StretchLine(co, c, end, 0.07f);
                rays.Add((ha, co, end));
            }
            bool hit = false;
            for (float t = 0f; t < 0.35f && !aborted; t += Time.deltaTime)
            {
                float a = 1f - t / 0.35f;
                foreach (var (ha, co, end) in rays)
                {
                    if (ha != null) ha.color = new Color(UmbraCol.r, UmbraCol.g, UmbraCol.b, 0.45f * a);
                    if (co != null) co.color = new Color(1f, 0.9f, 1f, a);
                    if (!hit && SegDist(Feet + Vector2.up * 0.2f, c, end) < 0.28f) hit = Hurt(12f, c, 1.2f);
                }
                yield return null;
            }
            foreach (var (ha, co, _) in rays) { DestroyObj(ha); DestroyObj(co); }
            if (l != null) Object.Destroy(l.gameObject);
            yield return Wait(0.5f);
        }

        private static Vector2 RayDir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad) * 0.62f).normalized;

        // ------------------------------------------------------------------ SKULL ANVIL (skull anvil hopping after Ren — cut its chain)
        private IEnumerator SkullAnvil(float pressure)
        {
            int side = FreeSide(Feet.x < chest.x ? -1 : 1);
            if (side == 0) yield break;
            int hi = Idx(side);
            handBusy[hi] = true;
            anvilAlive = true;
            DrillBoard.Say("SKULL ANVIL", UmbraCol);
            DrillBoard.Yell("Kneel beneath it.", 1.2f);
            var frames = SpriteVfx.Load("vc_skull_anvil");
            var seals = SpriteVfx.Load("vc_anvil_seal");
            float scale = Float("anvil_scale", 0.62f), rx = Float("anvil_rx", 0.68f), ry = rx * 0.55f;
            Vector2 pos = ClampFloor(Feet + new Vector2(Feet.x < Floor.center.x ? 1.8f : -1.8f, 0.6f), 0.7f);

            var g = new GameObject("skull anvil");
            fieldObjs.Add(g);
            g.transform.position = pos;
            g.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer;
            if (frames.Length > 0) sr.sprite = frames[0];
            var shadow = NewLine("anvil shadow", -14); shadow.sprite = Disc();
            var tether = NewLine("anvil tether", order + 3); var tetherCore = NewLine("anvil tether core", order + 4);
            var seal = NewLine("anvil seal", -14);
            if (seals.Length > 0) seal.sprite = seals[0];
            float sealK = rx * 2f / Mathf.Max(0.01f, seals.Length > 0 ? seals[0].bounds.size.x * 0.78f : 1f);
            var glow = BossJuice.Light(pos + Vector2.up * 0.8f, UmbraCol, 1.6f, 0.9f, 0f);
            float topH = frames.Length > 0 ? frames[0].bounds.size.y * scale * 0.9f : 1f;      // top of the anvil (where the chain is tied)

            // rise from the ground
            SpriteVfx.Spawn("abs_grasp", pos, 1.3f, layer, order - 1, false, 22f, false);
            Sfx.Play("quake_rumble", pos, 0.8f);
            for (float t = 0f; t < 0.9f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.9f;
                AnvilPose(sr, frames, shadow, g, pos, -0.8f * (1f - u), u, scale);
                rig.SetHand(side, pos + Vector2.up * 2.6f, 6f, "hand_point");
                Tether(tether, tetherCore, side, g.transform.position, topH, u); if (glow != null) glow.transform.position = g.transform.position + Vector3.up * 0.8f;
                yield return null;
            }
            if (!anvilHinted) { anvilHinted = true; Hints.Show("When the anvil lands, its chain glows blue - cut it, and the hand that pulls it falls.", 4f); }

            float end = Time.time + Float("anvil_seconds", 10f);
            bool shattered = false;
            while (!aborted && Time.time < end && !shattered)
            {
                // lift + glide after Ren, pentagon ring marks the landing spot
                Vector2 from = pos, target = ClampFloor(Feet + PlayerVel * 0.3f, 0.5f);
                float lift = Mathf.Lerp(1.2f, 0.9f, pressure * 0.5f);                    // 50 % speed
                Sfx.Play("drill_whoosh", pos, 0.5f);
                for (float t = 0f; t < lift && !aborted; t += Time.deltaTime)
                {
                    float u = t / lift, e = u * u * (3f - 2f * u);
                    if (u < 0.6f) target = Vector2.Lerp(target, ClampFloor(Feet + PlayerVel * 0.25f, 0.5f), 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                    pos = Vector2.Lerp(from, target, e);
                    AnvilPose(sr, frames, shadow, g, pos, Mathf.Sin(u * Mathf.PI) * 1.3f + u * 0.4f, 1f, scale);
                    seal.transform.position = target;
                    seal.transform.localScale = new Vector3(sealK, sealK, 1f);
                    seal.color = new Color(1f, 1f, 1f, 0.35f + 0.4f * u + (u > 0.7f ? 0.2f * Mathf.Sin(t * 40f) : 0f));
                    rig.SetHand(side, pos + Vector2.up * 2.8f, 7f, "hand_point");
                    Tether(tether, tetherCore, side, g.transform.position, topH, 1f); if (glow != null) glow.transform.position = g.transform.position + Vector3.up * 0.8f;
                    yield return null;
                }
                if (aborted) break;
                // SLAM
                pos = target;
                AnvilPose(sr, frames, shadow, g, pos, 0f, 1f, scale);
                seal.color = new Color(1f, 1f, 1f, 0f);
                SpriteVfx.Spawn("vc_anvil_seal", pos, sealK, layer, -13, false, 12f, false);
                SpriteVfx.Spawn("vc_quake_crack", pos, 1.1f, layer, -13, Random.value < 0.5f, 16f, false)?.Tint(new Color(0.85f, 0.6f, 1f));
                Sfx.Play("quake_rumble", pos, 0.8f);
                Sfx.Play("hit_block", pos, 0.7f);
                BossJuice.Shake(2);
                BossJuice.Flash(pos, UmbraCol, 2f, 2f, 0.25f);
                Vector2 q = Feet - pos;
                if ((q.x * q.x) / (rx * rx) + (q.y * q.y) / (ry * ry) <= 1f) HeavyHit(14f, pos);
                // COUNTER: after each slam the chain flashes BLUE (hittable) for a moment — striking it = the chain snaps
                tetherCuttable = true; tetherCut = false;
                float open = Mathf.Lerp(0.8f, 0.56f, pressure * 0.5f);
                for (float t = 0f; t < open && !aborted && !tetherCut; t += Time.deltaTime)
                {
                    Tether(tether, tetherCore, side, g.transform.position, topH, 1f);
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 22f);
                    tether.color = new Color(RelicBlue.r, RelicBlue.g, RelicBlue.b, 0.5f + 0.3f * pulse);
                    tetherCore.color = Color.Lerp(RelicBlue, Color.white, 0.5f + 0.5f * pulse);
                    yield return null;
                }
                tetherCuttable = false;
                if (tetherCut) { shattered = true; break; }
            }
            DestroyObj(shadow); DestroyObj(tether); DestroyObj(tetherCore); DestroyObj(seal);
            if (glow != null) Object.Destroy(glow.gameObject);
            anvilAlive = false;
            if (aborted) { handBusy[hi] = false; yield break; }
            if (shattered)
            {
                // chain snaps -> anvil breaks, the chain whips back -> the hand holding it crashes down on that spot
                for (int k = 0; k < 4; k++) SpriteVfx.Spawn("abs_void_burst", pos + Random.insideUnitCircle * 0.4f + Vector2.up * 0.6f, 1f, layer, order + 3, k % 2 == 0, 20f, false);
                SpriteVfx.Spawn("abs_umbral_burst", pos + Vector2.up * 0.7f, 2f, layer, order + 4, false, 18f, false);
                Sfx.Play("glass_break", pos, 1f);
                Sfx.Play("ward_break", pos, 0.8f);
                DrillBoard.Say("BACKLASH!", new Color(0.92f, 0.88f, 1f));
                DestroyObj(g);
                Vector2 drop = ClampFloor(pos + Vector2.up * 0.25f, 0.5f);
                rig.SetHand(side, drop + Vector2.up * 1.2f, 30f, "hand_open");
                yield return Wait(0.12f);
                rig.SetHand(side, drop, 50f, "hand_open");
                yield return Wait(0.06f);
                SlamFx(drop, 3);
                if (InHandPrint(drop, side, "hand_open")) HeavyHit(10f, drop);
                handBusy[hi] = false;
                yield return LieDown(side, drop, Float("anvil_backlash", 4f), "hand_open");
                yield break;
            }
            // time up: the anvil sinks into the ground
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - t / 0.5f); yield return null; }
            DestroyObj(g);
            rig.RestHand(side, 5f);
            handBusy[hi] = false;
            yield return Wait(0.3f);
        }

        private Vector2 tetherA, tetherB;
        private bool tetherCuttable, tetherCut;

        /// <summary>Ren's sword hits the anvil chain while it flashes blue -> it snaps.</summary>
        private void StrikeTether(Vector2 c, float r)
        {
            if (!tetherCuttable || !anvilAlive) return;
            if (SegDist(c, tetherA, tetherB) < r + 0.25f) tetherCut = true;
        }

        private void AnvilPose(SpriteRenderer sr, Sprite[] frames, SpriteRenderer shadow, GameObject g, Vector2 pos, float h, float alpha, float scale)
        {
            if (g == null) return;
            g.transform.position = pos + Vector2.up * h;
            if (frames.Length > 0) sr.sprite = frames[(int)(Time.time * 10f) % frames.Length];
            sr.sortingOrder = pos.y < Feet.y ? order + 1 : order - 1;
            sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            float s = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(h / 1.5f));
            shadow.transform.position = pos;
            shadow.transform.localScale = new Vector3(1.3f * s, 0.55f * s, 1f);
            shadow.color = new Color(0.05f, 0f, 0.1f, 0.5f * alpha);
        }

        /// <summary>Dark chain from the hand down to the top of the anvil (slight sway).</summary>
        private void Tether(SpriteRenderer a, SpriteRenderer b, int side, Vector2 anvil, float topH, float alpha)
        {
            Vector2 h = rig.HandWorld(side), top = anvil + Vector2.up * topH;
            tetherA = h; tetherB = top;
            Vector2 wob = new Vector2(Mathf.Sin(Time.time * 23f) * 0.03f, 0f);
            StretchLine(a, h, top + wob, 0.09f); a.color = new Color(UmbraCol.r, UmbraCol.g, UmbraCol.b, 0.45f * alpha);
            StretchLine(b, h, top - wob, 0.025f); b.color = new Color(0.95f, 0.85f, 1f, 0.9f * alpha);
        }

        // ------------------------------------------------------------------ SKULL RAIN
        private IEnumerator SkullRain(float pressure)
        {
            DrillBoard.Say("SKULL RAIN", UmbraCol);
            DrillBoard.Yell("Remember them? They remember you.", 1.6f);
            Roar(0.8f);
            int n = Mathf.RoundToInt(Mathf.Lerp(10f, 14f, pressure * 0.5f));
            float dur = Mathf.Lerp(3.4f, 2.8f, pressure * 0.5f);
            for (int i = 0; i < n && !aborted; i++)
            {
                Vector2 at = Random.value < 0.5f
                    ? ClampFloor(Feet + PlayerVel * 0.6f + Random.insideUnitCircle * 0.9f, 0.3f)
                    : new Vector2(Random.Range(Floor.xMin + 0.3f, Floor.xMax - 0.3f), Random.Range(Floor.yMin + 0.3f, Floor.yMax - 0.4f));
                StartR(SkullFall(at, Float("skull_warn", 0.9f), 12f));
                yield return Wait(dur / n);
            }
            yield return Wait(1.2f);
        }

        /// <summary>One falling skull: warning ring shrinks for `warn` s (+ dark disc) -> PixelLab skull dives diagonally, landing exactly as the warning ends.</summary>
        private IEnumerator SkullFall(Vector2 at, float warn, float dmg)
        {
            const float Impact = 7f / 18f;                                               // vc_skull_meteor lands on frame 7 (18 fps)
            var ring = NewRing(at, 0.6f);
            var dot = NewLine("skull mark", -14); dot.sprite = Disc();
            dot.transform.position = at;
            bool spawned = false;
            for (float t = 0f; t < warn && !aborted; t += Time.deltaTime)
            {
                float u = t / warn;
                float rk = Mathf.Lerp(0.62f, 0.32f, u) / (Ring().bounds.size.x * 0.5f);
                ring.transform.localScale = new Vector3(rk, rk * 0.6f, 1f);
                ring.color = new Color(0.85f, 0.45f, 1f, 0.35f + 0.5f * u);
                float dk = Mathf.Lerp(0.25f, 0.95f, u);
                dot.transform.localScale = new Vector3(dk, dk * 0.6f, 1f);
                dot.color = new Color(0.1f, 0f, 0.16f, 0.25f + 0.3f * u);
                if (!spawned && t >= warn - Impact)
                {
                    spawned = true;
                    SpriteVfx.Spawn("vc_skull_meteor", at, 1f, layer, at.y < Feet.y ? order + 2 : order - 1, false, 18f, false);
                }
                yield return null;
            }
            DestroyObj(ring); DestroyObj(dot);
            if (aborted) yield break;
            Sfx.Play("hit_block", at, 0.5f);
            if (!spawned) SpriteVfx.Spawn("abs_umbral_burst", at, 1.2f, layer, order + 2, false, 20f, false);
            if ((Feet - at).magnitude < 0.55f) Hurt(dmg, at, 0.9f);
        }

        // ------------------------------------------------------------------ DEVOUR (pull Ren into the mouth)
        private IEnumerator Devour(float pressure)
        {
            DrillBoard.Say("DEVOUR", UmbraCol);
            DrillBoard.Yell("Come. Be nothing with me.", 1.6f);
            jawHold = 1f; slumpTarget = 0.35f;
            var eyeGlow = BossJuice.Light(EyesMid, UmbraCol, 1.8f, 0f, 0f);
            for (float t = 0f; t < 0.8f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.8f;
                rig.HeadDip = u * u * (3f - 2f * u);
                if (eyeGlow != null) eyeGlow.intensity = 2.5f * u;
                yield return null;
            }
            if (aborted) { DevourEnd(eyeGlow); yield break; }
            Vector2 m = new Vector2(Mathf.Clamp(rig.MouthWorld().x, Floor.xMin + 0.8f, Floor.xMax - 0.8f), Floor.yMax - 0.15f);
            var maw = BossJuice.Light(m, UmbraCol, 2.2f, 1.2f, 0f);
            Sfx.Play("gas_hiss", m, 1f);
            Sfx.Play("quake_rumble", m, 0.6f);
            var st = RenStatus;
            var bite = NewDot("devour maw", -13, new Color(0.08f, 0.02f, 0.14f, 0f));   // bite zone (dark) + seal ring + ripples shrinking into the mouth
            var biteRim = NewRing(m, 1.1f);
            float nextRipple = 0f;
            float dur = Float("devour_seconds", 3.2f), nextMote = 0f, nextSkull = 0.8f;
            bool bitten = false;
            for (float t = 0f; t < dur && !aborted && !bitten; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                Vector2 d = m - Feet;
                float dist = Mathf.Max(0.05f, d.magnitude);
                float s = Float("devour_pull", 2.4f);
                if (st != null) st.Push(d / dist * s, 0.12f);
                // ink / light particles SUCKED in spiralling toward the mouth (streaking), ripple rings shrink in, the bite zone darkens
                if (Time.time >= nextMote)
                {
                    nextMote = Time.time + 0.025f;
                    Vector2 sp = new Vector2(Random.Range(Floor.xMin, Floor.xMax), Random.Range(Floor.yMin, Floor.yMax));
                    AddMote(sp, Vector2.zero, Random.value < 0.5f ? new Color(0.85f, 0.6f, 1f, 0.9f) : new Color(0.3f, 0.1f, 0.45f, 0.9f), 1.6f, Random.Range(0.025f, 0.045f), true, m, 3.2f, Random.Range(-0.8f, 0.8f), order + 2);
                }
                if (Time.time >= nextRipple) { nextRipple = Time.time + 0.35f; StartR(DevourRipple(m)); }
                float ua = Mathf.Clamp01(t / 0.4f);
                bite.transform.position = m; bite.transform.localScale = new Vector3(2.2f, 1.2f, 1f);
                bite.color = new Color(0.08f, 0.02f, 0.14f, 0.55f * ua);
                float brk = 1.1f / (Ring().bounds.size.x * 0.5f);
                biteRim.transform.localScale = new Vector3(brk, brk * 0.545f, 1f);
                biteRim.color = new Color(0.85f, 0.45f, 1f, (0.5f + 0.3f * Mathf.Sin(t * 12f)) * ua);
                if (maw != null) maw.intensity = 1.2f + 0.4f * Mathf.Sin(Time.time * 18f);
                if (pressure >= 1f && t >= nextSkull) { nextSkull = t + 0.9f; StartR(SkullFall(ClampFloor(Feet + PlayerVel * 0.6f, 0.3f), 0.9f, 10f)); }
                Vector2 q = Feet - m;
                if ((q.x * q.x) / (1.1f * 1.1f) + (q.y * q.y) / (0.6f * 0.6f) <= 1f) bitten = true;
                yield return null;
            }
            DestroyObj(bite); DestroyObj(biteRim);
            if (bitten && !aborted)
            {
                rig.JawOpen = 0f;
                Sfx.Play("hit_block", m, 1f);
                Sfx.Play("glass_break", m, 0.5f);
                BossJuice.Shake(3);
                SpriteVfx.Spawn("abs_umbral_burst", Feet + Vector2.up * 0.3f, 1.8f, layer, order + 3, false, 20f, false);
                Hurt(22f, m + Vector2.up, 2f);
                DrillBoard.Say("DEVOURED", UmbraCol);
            }
            if (maw != null) Object.Destroy(maw.gameObject);
            DevourEnd(eyeGlow);
            yield return Wait(0.6f);
        }

        private void DevourEnd(Light2D eyeGlow)
        {
            if (eyeGlow != null) Object.Destroy(eyeGlow.gameObject);
            if (rig != null && rig.HeadDip > 0f) StartR(LiftHead(null));
            else { jawHold = -1f; slumpTarget = 0f; }
        }

        // ------------------------------------------------------------------ ECLIPSE (second half of the phase)
        private IEnumerator Eclipse(float pressure)
        {
            nextEclipse = Time.time + Float("eclipse_every", 25f);
            DrillBoard.Say("ECLIPSE", UmbraCol);
            DrillBoard.Yell("Let the last light go out.", 1.8f);
            Roar(1.2f);
            eclipseOn = true;
            Hints.Show("Eclipse: the dark reaches up under your feet. Keep moving - and both watchtowers are searching.", 3.5f);
            float dur = Float("eclipse_seconds", 10f), t0 = Time.time, nextGrasp = Time.time + 1.4f, nextOrb = Time.time + 1f;
            while (!aborted && Time.time - t0 < dur)
            {
                float now = Time.time;
                if (now >= nextGrasp)
                {
                    nextGrasp = now + Mathf.Lerp(1.15f, 0.9f, pressure * 0.5f);
                    StartR(Grasp(ClampFloor(Feet + PlayerVel * 0.25f, 0.3f)));
                }
                if (now >= nextOrb)
                {
                    nextOrb = now + 0.9f;
                    int e = Random.Range(0, 3);
                    Vector2 p = e == 0 ? new Vector2(Floor.xMin - 0.3f, Random.Range(Floor.yMin, Floor.yMax))
                              : e == 1 ? new Vector2(Floor.xMax + 0.3f, Random.Range(Floor.yMin, Floor.yMax))
                              : new Vector2(Random.Range(Floor.xMin, Floor.xMax), Floor.yMin - 0.3f);
                    SpawnOrb(p, (Feet - p).normalized * 0.8f, 1.5f, 5f);
                }
                yield return null;
            }
            eclipseOn = false;
            yield return Wait(0.6f);
        }

        /// <summary>Shadow hand: a dark pool spreads under the feet for 0.6 s -> rises and binds (if Ren is still standing there).</summary>
        private IEnumerator Grasp(Vector2 at)
        {
            var pool = NewLine("grasp pool", -14); pool.sprite = Disc();
            pool.transform.position = at;
            var ring = NewRing(at, 0.5f);
            for (float t = 0f; t < 0.6f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.6f, k = Mathf.Lerp(0.3f, 1.05f, u);
                pool.transform.localScale = new Vector3(k, k * 0.55f, 1f);
                pool.color = new Color(0.06f, 0f, 0.12f, 0.35f + 0.35f * u);
                float rk = 0.52f / (Ring().bounds.size.x * 0.5f);
                ring.transform.localScale = new Vector3(rk, rk * 0.55f, 1f);
                ring.color = new Color(0.85f, 0.45f, 1f, Mathf.Sin(t * 30f) > 0f ? 0.8f : 0.3f);
                yield return null;
            }
            DestroyObj(pool); DestroyObj(ring);
            if (aborted) yield break;
            SpriteVfx.Spawn("abs_grasp", at, 0.9f, layer, at.y < Feet.y ? order + 1 : order - 1, Random.value < 0.5f, 26f, false);
            Vector2 q = Feet - at;
            if ((q.x * q.x) / 0.3f + (q.y * q.y) / 0.1f <= 1f)
            {
                ShadowSnare(1.2f, 8f, "GRASPED");
            }
        }
    }
}
