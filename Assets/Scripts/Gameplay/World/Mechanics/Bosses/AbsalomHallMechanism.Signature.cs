using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SIGNATURE SKILLS — each one has its own dodge, big VFX + lights + shake:
    ///   Judgement Ray (Light)      — 0.9 s charge, warning lines at the start + end of the sweep, then a GIANT LIGHT BEAM (3 layers: glow / body / white core,
    ///                                0.75 u thick) SWEEPS ~110° across the hall in 1.5–1.8 s, scorching the floor, sparks at the tip, constant shake.
    ///                                Dodge: roll through the beam (invulnerable) or run AHEAD of the sweep / behind him.
    ///   Arcane Blaster (any school) — 3–4 VOLLEYS x 5 rapid energy shots (abs_blaster 71 px tinted by school, muzzle flash), later volleys LEAD the target
    ///                                (predict Ren's movement). Dodge: change direction between volleys.
    ///   Void Cannon (Umbral)       — a violet aim line TRACKS Ren for 0.9 s -> locks for 0.3 s (white flash) -> straight VOID CANNON 0.55 u thick; 2–3 shots.
    ///                                Dodge: sidestep the moment the line locks.
    ///   Eclipse (Umbral, the dark mage signature) — he flies high, a VOID RIFT (abs_rift 177x253) opens overhead and drinks the light, then for ~3 s
    ///                                it SPIRALS 2 arms of void shards + 1–2 dark orbs that HOME lightly; closing = a ring of 12 shards with a gap toward Ren (~55 bullets).
    ///                                Dodge: move with the spiral, keep distance, drag the homing orbs away then roll.
    ///   Grasp of the Damned (Umbral) — 5–7 dark pools chase Ren's position one after another (0.45 s warning rings), then SHADOW TENTACLES burst up and grab
    ///                                the legs (abs_grasp): hit = damage + ROOT 0.5 s. Dodge: keep running, change direction.
    /// Called by: PressureSkill (.Lamps.cs) and the Ctrl+F7 showcase. Uses: MakeBeam (shared 3-layer beam, below), Shot, Hazard, RingSprite (.Skills.cs).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        // ------------------------------------------------------------------ shared 3-layer beam
        private sealed class Beam
        {
            public SpriteRenderer Halo, Mid, Core;
            public readonly List<GameObject> Lights = new List<GameObject>();
        }

        private Beam MakeBeam(Color col)
        {
            var b = new Beam { Halo = MakeLine("Beam Halo"), Mid = MakeLine("Beam"), Core = MakeLine("Beam Core") };
            b.Halo.sortingOrder = Order + 2; b.Mid.sortingOrder = Order + 3; b.Core.sortingOrder = Order + 4;
            b.Halo.color = new Color(col.r, col.g, col.b, 0.35f); b.Mid.color = new Color(col.r, col.g, col.b, 0.95f); b.Core.color = Color.white;
            b.Halo.enabled = b.Mid.enabled = b.Core.enabled = true;
            for (int i = 0; i < 4; i++) b.Lights.Add(GlowAt(Hand, col, 1.4f, 1.3f, 0f));
            return b;
        }

        private static void PlaceBeam(Beam b, Vector2 from, Vector2 to, float width)
        {
            float f = 1f + 0.12f * Mathf.Sin(Time.time * 47f);                          // beam thickness "vibrates"
            Stretch(b.Halo, from, to, width * f);
            Stretch(b.Mid, from, to, width * 0.5f * f);
            Stretch(b.Core, from, to, width * 0.2f);
            for (int i = 0; i < b.Lights.Count; i++)
                if (b.Lights[i] != null) b.Lights[i].transform.position = Vector2.Lerp(from, to, (i + 0.5f) / b.Lights.Count);
        }

        private static void KillBeam(Beam b)
        {
            if (b == null) return;
            if (b.Halo != null) Object.Destroy(b.Halo.gameObject);
            if (b.Mid != null) Object.Destroy(b.Mid.gameObject);
            if (b.Core != null) Object.Destroy(b.Core.gameObject);
            foreach (var l in b.Lights) if (l != null) Object.Destroy(l);
        }

        private readonly List<Beam> beams = new List<Beam>();

        private void ClearBeams()
        {
            foreach (var b in beams) KillBeam(b);
            beams.Clear();
        }

        private SpriteVfx Muzzle(Vector2 at, Vector2 dir, Color col, float scale = 1.2f)
        {
            var fx = Fx("abs_muzzle", at, scale, Order + 4, 18f, col);
            if (fx != null) fx.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            return fx;
        }

        // ------------------------------------------------------------------ JUDGEMENT RAY
        private IEnumerator JudgementRay(int power)
        {
            liftBoost = 0.35f;
            Fx("abs_charge2", Hand, 1.5f, Order + 4, 12f / 0.9f, Sun);
            yield return Cast(School.Light, "JUDGEMENT RAY", 0.9f, "eye_beam_charge");
            if (aborted) { liftBoost = 0f; yield break; }
            Vector2 from = Hand;
            float aim = Vector2.SignedAngle(Vector2.right, PlayerFeet - from);
            float span = power >= 2 ? 115f : 95f, side = Random.value < 0.5f ? 1f : -1f;
            float a0 = aim - side * span * 0.5f, a1 = aim + side * span * 0.5f;
            var t0 = DashTelegraph.Spawn(from, Rot(Vector2.right, a0), 10f, 0.34f, Sun, 0.55f, Layer, Order - 2);
            var t1 = DashTelegraph.Spawn(from, Rot(Vector2.right, a1), 10f, 0.16f, new Color(Sun.r, Sun.g, Sun.b, 0.55f), 0.55f, Layer, Order - 2);
            yield return Wait(0.55f);
            if (t0 != null) t0.Release();
            if (t1 != null) t1.Release();
            if (aborted) { liftBoost = 0f; yield break; }
            var beam = MakeBeam(Sun);
            beams.Add(beam);
            Sfx.Play("mantra_eyeray", from, 1f);
            Sfx.Play("bolt_strike", from, 0.6f);
            Shake(2);
            FlashLight(from, Sun, 4f, 2.2f, 0.5f);
            float dur = power >= 2 ? 1.5f : 1.8f, nextHit = 0f, nextFx = 0f, nextShake = 0f;
            for (float t = 0f; t < dur && !aborted; t += Time.deltaTime)
            {
                if (TimeStop.Active) { t -= Time.deltaTime; yield return null; continue; }
                float u = Mathf.SmoothStep(0f, 1f, t / dur);
                Vector2 d = Rot(Vector2.right, Mathf.Lerp(a0, a1, u));
                Vector2 end = ClipToArena(from, d, 12f);
                PlaceBeam(beam, from, end, 0.75f);
                if (Time.time >= nextHit && SegDist(PlayerFeet + Vector2.up * 0.15f, from, end) <= 0.3f && HurtRen(12f, from, 1.4f)) nextHit = Time.time + 0.4f;
                if (Time.time >= nextFx)
                {
                    nextFx = Time.time + 0.06f;
                    Fx("abs_arcane_hit", end, 0.9f, Order + 3, 20f, new Color(1f, 0.9f, 0.55f));
                    var sc = SpriteVfx.Spawn("abs_scorch", Vector2.Lerp(from, end, Random.Range(0.35f, 1f)), 1.6f, 0, -5, Random.value < 0.5f, 10f, false);
                    if (sc != null) sc.Looping(1.8f);
                }
                if (Time.time >= nextShake) { nextShake = Time.time + 0.18f; Shake(1); }
                yield return null;
            }
            KillBeam(beam); beams.Remove(beam);
            liftBoost = 0f;
            abs.Play("idle", 5f);
        }

        // ------------------------------------------------------------------ ARCANE BLASTER
        private Vector2 lastFeet, renVel;

        /// <summary>Estimate Ren's velocity (for lead shots) — called every frame during the fight.</summary>
        private void TrackRenVelocity(float dt)
        {
            Vector2 f = PlayerFeet;
            if (dt > 0f) renVel = Vector2.Lerp(renVel, (f - lastFeet) / dt, Mathf.Clamp01(dt * 8f));
            lastFeet = f;
        }

        private IEnumerator ArcaneBlaster(School s, int power)
        {
            Color c = SchoolColor(s);
            yield return Cast(s, "ARCANE BLASTER", 0.45f, "lg_charge");
            if (aborted) yield break;
            int bursts = power >= 2 ? 4 : 3;
            for (int k = 0; k < bursts && !aborted; k++)
            {
                Vector2 from = Hand + Rot(Vector2.right, k % 2 == 0 ? 20f : 160f) * 0.3f;
                float lead = k == 0 ? 0f : 0.35f;                                        // volley 2+ leads to where Ren is going
                Vector2 target = PlayerFeet + Vector2.up * 0.18f + renVel * lead;
                Vector2 aim = (target - from).normalized;
                var tel = DashTelegraph.Spawn(from, aim, 9f, 0.12f, c, 0.28f, Layer, Order - 2);
                yield return Wait(0.28f);
                if (tel != null) tel.Release();
                if (aborted) yield break;
                for (int i = 0; i < 5 && !aborted; i++)
                {
                    Vector2 d = Rot(aim, Random.Range(-5f, 5f));
                    var b = Shot("abs_blaster", from, d * 8f, c, 1f, 7f, 0.11f, 1.6f);
                    if (b != null) { b.popFx = "abs_arcane_hit"; b.popScale = 0.5f; b.popTint = c; if (i % 2 == 0) b.Glow(c, 0.8f, 0.9f); }
                    Muzzle(from, d, c);
                    FlashLight(from, c, 1.6f, 1.2f, 0.12f);
                    Sfx.Play("mantra_eyeray", from, 0.25f);
                    yield return Wait(0.07f);
                }
                Shake(1);
                yield return Wait(power >= 2 ? 0.3f : 0.42f);
            }
            abs.Play("idle", 5f);
        }

        // ------------------------------------------------------------------ VOID CANNON
        private IEnumerator VoidCannon(int power)
        {
            yield return Cast(School.Umbral, "VOID CANNON", 0.5f, "sz_inhale");
            if (aborted) yield break;
            int shots = power >= 2 ? 3 : 2;
            for (int k = 0; k < shots && !aborted; k++)
            {
                // aim: thin violet line TRACKS Ren, then locks (white flash)
                var sight = MakeLine("Void Sight");
                sight.sortingOrder = Order + 2; sight.enabled = true;
                var keep = AddHazard(HazardShape.Circle, Hand, Hand, 0f, 0f, 0f, 0f, 3f);   // fight reset midway -> ClearHazards removes the aim line
                keep.Objs.Add(sight.gameObject);
                Vector2 from = Hand, aim = (PlayerFeet - from).normalized;
                for (float t = 0f; t < 0.9f && !aborted; t += Time.deltaTime)
                {
                    aim = Vector3.Slerp(aim, (PlayerFeet + Vector2.up * 0.15f - from).normalized, Time.deltaTime * 9f);
                    Stretch(sight, from, ClipToArena(from, aim, 12f), 0.03f);
                    sight.color = new Color(Umbral.r, Umbral.g, Umbral.b, 0.45f + 0.3f * Mathf.Sin(t * 30f));
                    yield return null;
                }
                Sfx.Play("dial_turn", from, 0.9f);
                for (float t = 0f; t < 0.3f && !aborted; t += Time.deltaTime)
                {
                    Stretch(sight, from, ClipToArena(from, aim, 12f), 0.06f);
                    sight.color = Mathf.Repeat(t * 16f, 1f) < 0.5f ? Color.white : Umbral;
                    yield return null;
                }
                keep.Dead = true;
                if (aborted) yield break;
                // fire: straight void cannon
                Vector2 end = ClipToArena(from, aim, 12f);
                var beam = MakeBeam(Umbral);
                beams.Add(beam);
                Sfx.Play("teleport_dark", from, 1f);
                Sfx.Play("keg_blast", end, 0.6f);
                Fx("abs_void_burst", from, 1.4f, Order + 4, 18f, Color.white);
                Fx("abs_void_burst", end, 1.8f, Order + 3, 16f, Color.white);
                Shake(2);
                FlashLight(from, Umbral, 3.5f, 2.2f, 0.4f);
                FlashLight(end, Umbral, 2.5f, 2f, 0.4f);
                bool hit = false;
                for (float t = 0f; t < 0.45f && !aborted; t += Time.deltaTime)
                {
                    float w = Mathf.Lerp(1.1f, 0.4f, t / 0.45f);
                    PlaceBeam(beam, from, end, w);
                    beam.Core.color = Color.white; beam.Mid.color = new Color(Umbral.r, Umbral.g, Umbral.b, 0.95f);
                    if (!hit && SegDist(PlayerFeet + Vector2.up * 0.15f, from, end) <= w * 0.42f) hit = HurtRen(14f, from, 2f);
                    yield return null;
                }
                KillBeam(beam); beams.Remove(beam);
                // aftermath: void shards drift down along the cannon path
                for (float d = 0.6f; d < (end - from).magnitude; d += 0.7f)
                    Fx("abs_void_burst", Vector2.Lerp(from, end, d / (end - from).magnitude), 0.5f, Order + 2, 16f, Color.white);
                yield return Wait(0.25f);
            }
            abs.Play("idle", 5f);
        }

        // ------------------------------------------------------------------ ECLIPSE
        private IEnumerator Eclipse(int power)
        {
            liftBoost = 0.6f;
            yield return Cast(School.Umbral, "ECLIPSE", 0.7f, "teleport_dark");
            if (aborted) { liftBoost = 0f; yield break; }
            Vector2 at = abs.Body + Vector2.up * 1.1f;
            var rift = SpriteVfx.Spawn("abs_rift", at, 0.1f, Layer, Order + 5, false, 12f, false);
            if (rift != null) rift.Looping(99f);
            var glowGo = GlowAt(at, Umbral, 4f, 0f, 0f);
            var gl = glowGo.GetComponent<Light2D>();
            var keep = AddHazard(HazardShape.Circle, at, at, 0f, 0f, 0f, 0f, 8f);            // fight reset midway -> remove the rift + lights
            keep.Objs.Add(glowGo);
            if (rift != null) keep.Objs.Add(rift.gameObject);
            Sfx.Play("sz_inhale", at, 1f);
            // open: rift expands + draws particles in
            for (float t = 0f; t < 0.6f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.6f;
                if (rift != null) rift.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 1.1f, 1f - (1f - u) * (1f - u));
                if (gl != null) gl.intensity = 2.2f * u;
                if (Random.value < 0.6f)
                {
                    Vector2 p = at + Rot(Vector2.right, Random.Range(0f, 360f)) * Random.Range(1.4f, 2.4f);
                    var m = Shot("abs_void_shard", p, (at - p) * 2.2f, new Color(1f, 1f, 1f, 0.8f), 0.8f, 0f, 0f, 0.42f);
                    m.harmless = true; m.stopOnWalls = false; m.popFx = "none";
                }
                yield return null;
            }
            Shake(2);
            FlashLight(at, Umbral, 5f, 2.6f, 0.5f);
            Fx("abs_void_burst", at, 2.2f, Order + 6, 14f, Color.white);
            // spiral of 2 arms + homing orbs.
            // Tuning: 2 arms every 0.2 s (power 1: 0.24 s) for 3 s, shard speed 1.8, 2 orbs (power 1: 1) homing lightly, closing ring of 12 with a gap toward Ren ≈ 55 bullets.
            //   Arms rotate 17° per tick -> there is always a wide corridor between two spiral lines to walk around with the spin.
            float dur = power >= 2 ? 3f : 2.6f, spin = Random.Range(0f, 360f), dir = Random.value < 0.5f ? 1f : -1f;
            float nextShot = 0f, nextOrb = Time.time + 0.9f;
            int orbsLeft = power >= 2 ? 2 : 1;
            for (float t = 0f; t < dur && !aborted; t += Time.deltaTime)
            {
                if (TimeStop.Active) { t -= Time.deltaTime; yield return null; continue; }
                if (gl != null) gl.intensity = 2f + 0.6f * Mathf.Sin(Time.time * 9f);
                if (rift != null) rift.transform.localScale = Vector3.one * (1.1f + 0.05f * Mathf.Sin(Time.time * 7f));
                if (Time.time >= nextShot)
                {
                    nextShot = Time.time + (power >= 2 ? 0.2f : 0.24f);
                    for (int arm = 0; arm < 2; arm++)
                    {
                        Vector2 d = Rot(Vector2.right, spin + arm * 180f);
                        var s = VoidShard(at + d * 0.45f, d * 1.8f, 8f, 4f);
                        if (s != null) s.stopOnWalls = true;
                    }
                    spin += 17f * dir;
                    if (Random.value < 0.35f) Sfx.Play("hymn_pop", at, 0.25f);
                }
                if (orbsLeft > 0 && Time.time >= nextOrb)
                {
                    orbsLeft--;
                    nextOrb = Time.time + 1.3f;
                    var o = BigShot("abs_void_orb", at, (PlayerFeet - at).normalized * 1.2f, Umbral, 1f, 10f, 0.18f, 4.5f, "none", 1f, Color.white);
                    if (o != null) { o.homing = 45f; o.accel = 0.12f; o.fuseRadius = 0.5f; o.expireOnPop = true; o.onExpire = OrbBurst; }
                    Fx("abs_void_burst", at, 1f, Order + 6, 18f, Color.white);
                }
                yield return null;
            }
            // closing: ring of 12 shards, gap toward Ren
            for (float t = 0f; t < 0.35f && !aborted && rift != null; t += Time.deltaTime)
            {
                rift.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.05f, t / 0.35f);
                yield return null;
            }
            keep.Dead = true;
            liftBoost = 0f;
            if (aborted) yield break;
            Fx("abs_void_burst", at, 2.6f, Order + 6, 14f, Color.white);
            FlashLight(at, Umbral, 5f, 3f, 0.6f);
            Shake(3);
            Sfx.Play("teleport_dark", at, 1f);
            RingWithGap(at, 12, 1.9f, 3.5f, 1);
            abs.Play("idle", 5f);
        }

        // ------------------------------------------------------------------ GRASP OF THE DAMNED
        private IEnumerator GraspOfTheDamned(int power)
        {
            yield return Cast(School.Umbral, "GRASP OF THE DAMNED", 0.5f, "v_chant_cast");
            if (aborted) yield break;
            var frames = SpriteVfx.Load("abs_grasp");
            int n = power >= 2 ? 7 : 5;
            for (int i = 0; i < n && !aborted; i++)
            {
                Vector2 at = InArena(PlayerFeet + renVel * 0.25f, 0.35f);
                var g = GroundStrike.Spawn(at, 0.45f, 0.42f, 9f, frames, 4, 14f, Order + 1, Umbral);
                if (g != null) { g.stunSeconds = 0.5f; g.impactSfx = "root_spike"; g.bodyScale = 1f; }
                Ctx.Run(GraspFlash(at, 0.45f + 4f / 14f));
                yield return Wait(power >= 2 ? 0.34f : 0.42f);
            }
            abs.Play("idle", 5f);
        }

        private IEnumerator GraspFlash(Vector2 at, float delay)
        {
            yield return Wait(delay);
            if (aborted || state != State.Fight) yield break;
            FlashLight(at, Umbral, 1.8f, 1.6f, 0.4f);
            Shake(1);
        }
    }
}
