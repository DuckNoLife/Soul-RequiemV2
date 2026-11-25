using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ELEMENT MOVES added on top of the phase files (full design: `_tools/s435/VESSEL_REWORK_435.md`):
    ///   P1 HOLLOW  — searchlight catches Ren -> CRUSHING GRIP: a hand grabs and LIFTS Ren, squeezing; strike the hand `grip_strikes` (6) times within `grip_seconds` (4.5 s)
    ///                and the hand drops and lies for 3 s (counter window); too slow -> crushed for 30.
    ///   P3 RIME    — searchlight -> FLASH FREEZE: Ren is frozen inside an ice crystal cluster (PixelLab), a giant icicle hangs overhead, its shadow growing;
    ///                MASH ATTACK `freeze_strikes` (6) times to break the ice, then leave the shadow before the icicle falls (2.6 s). Still stuck = 26.
    ///                Eyes -> GLACIAL CROSS: both eyes fire at the 2 top corners and refract into 2 FULL-ARENA DIAGONAL BEAMS (an X); below 50 % the beams rotate;
    ///                the 4 triangles between the beams are safe; the beams leave frost trails for 2.5 s (slow).
    ///   P4 TEMPEST — searchlight -> STATIC CHARGE: Ren is charged for 6 s, lightning strikes his spot every 1.5 s; TOUCH A LIGHTNING ROD to ground it (arc Ren -> rod).
    ///                Eyes -> THUNDER GAZE: real forked lightning (not a laser) from the eye sockets strikes the arena 3–4 times, following Ren.
    ///                LIGHTNING RODS matter: they attract every nearby bolt (≤ 1.3–1.4 u) + store CHARGE (3 levels, brighter each) — when full they discharge into a
    ///                lying hand (OVERLOAD, damages the Vessel) or burst an electric ring around the rod.
    ///   P5 UMBRA   — SINGULARITY (replaces Black Hole): black core + bent light rim + particle disc spiralling in; contracts, then bursts into 8 bright cracks.
    ///                Shadow bind -> INK TETHER: an ink mark pinned under Ren's feet, an elastic tether keeps him within radius 1.4 (no stun, low damage);
    ///                strike the ink mark to cut the tether.
    /// Called by: move tables in .Ice / .Storm / .Shadow, Caught (.Light.cs), TickFight (TickRework), ClearField (KillRework).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static Sprite tombSprite, icicleSprite;
        private readonly HashSet<string> moveBag = new HashSet<string>();   // move bag (PickMove)
        private int bagPhase = -1;
        private bool frozenSolid;
        private static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Every frame (TickFight).</summary>
        private void TickRework(float dt)
        {
            TickMotes(dt);
            TickTether();
        }

        /// <summary>ClearField: reset the state of these moves.</summary>
        private void KillRework()
        {
            EndTether(false);
            KillMotes();
            frozenSolid = false;
            charged = false;
            rodCharge.Clear();
            shadowSigil.Clear();
        }

        // ================================================================== P1: CRUSHING GRIP
        private IEnumerator CrushingGrip()
        {
            const int side = 1;
            for (float w = 0f; w < 1.2f && !HandFree(side) && !aborted; w += Time.deltaTime) yield return null;
            if (aborted) yield break;
            if (!HandFree(side)) { yield return TripleSlam(1.2f); yield break; }
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead) yield break;
            DrillBoard.Say("CRUSHING GRIP", WarnViolet);
            DrillBoard.Yell("There you are.", 1f);
            handBusy[1] = true;
            var shadow = Shadow(side, Feet, "hand_claw");
            Sfx.Play("reg_grip", Feet, 0.5f);
            for (float t = 0f; t < 0.55f && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(side, Feet + Vector2.up * 1.5f, 14f, "hand_claw");
                GrowShadow(shadow, side, Feet, Mathf.Clamp01(t / 0.55f), t > 0.4f, t);
                yield return null;
            }
            DestroyObj(shadow);
            if (aborted) { handBusy[1] = false; yield break; }
            Vector2 at = Feet;
            if (hp.IsDead || hp.Invulnerable)
            {
                rig.SetHand(side, at, 50f, "hand_claw"); SlamFx(at, 1);
                yield return Wait(0.4f);
                rig.RestHand(side, 5f); handBusy[1] = false;
                yield break;
            }
            grabbing = true; grabStrikes = 0;
            HookStruck();
            rig.SetHand(side, at, 60f, "hand_fist");
            Sfx.Play("reg_grip", at, 1f);
            BossJuice.Shake(2);
            int need = Mathf.RoundToInt(Float("grip_strikes", 6f));
            float limit = Float("grip_seconds", 4.5f), nextSq = Time.time + 0.8f;
            int sq = 0;
            DrillBoard.CommandTime = Time.time;
            for (float t = 0f; t < limit && !aborted && grabStrikes < need; t += Time.deltaTime)
            {
                float u = t / limit, lift = Mathf.Clamp01(t / 0.6f) * 1.3f, shake = 0.02f + 0.06f * u;
                rig.SetHand(side, at + new Vector2(Mathf.Sin(t * 31f) * shake, 0.25f + lift), 30f, "hand_fist");
                DrillBoard.Command = "STRIKE THE HAND! " + grabStrikes + "/" + need;
                if (Time.time >= nextSq)
                {
                    nextSq = Time.time + Mathf.Lerp(0.8f, 0.4f, u);
                    sq++;
                    Hurt(2f + sq, rig.HandWorld(side) + Vector2.up, 0f);
                    hp.TintPulse(new Color(1f, 0.35f, 0.35f), 0.25f);
                    Sfx.Play("reg_grip", at, 0.55f);
                    BossJuice.Shake(0);
                    for (int k = 0; k < 4; k++) AddMote(rig.HandWorld(side) + Random.insideUnitCircle * 0.3f, Random.insideUnitCircle * 1.5f, new Color(0.85f, 0.75f, 1f, 0.9f), 0.35f, 0.04f);
                }
                yield return null;
            }
            bool broke = grabStrikes >= need;
            ReleaseGrab(true);
            DrillBoard.Command = "";
            if (aborted) { handBusy[1] = false; yield break; }
            if (broke)
            {
                Sfx.Play("glass_break", at, 0.8f);
                DrillBoard.Say("BROKE FREE", RelicBlue);
                BossJuice.Flash(rig.HandWorld(side), RelicBlue, 2f, 2f, 0.25f);
                Vector2 down = ClampFloor(at + new Vector2(at.x < chest.x ? 0.9f : -0.9f, 0.3f), 0.6f);
                rig.SetHand(side, down, 40f, "hand_open");
                yield return Wait(0.12f);
                SlamFx(down, 1);
                handBusy[1] = false;
                StartR(LieDown(side, down, Float("grip_stun", 3f), "hand_open"));      // reward: the hand drops and lies down = counter window
            }
            else
            {
                Sfx.Play("glass_break", at, 1f);
                BossJuice.Flash(at, new Color(1f, 0.3f, 0.3f), 2.4f, 3f, 0.3f);
                BossJuice.Shake(3);
                HeavyHit(Float("grip_crush", 30f), at + Vector2.up);
                DrillBoard.Say("CRUSHED", WarnViolet);
                handBusy[1] = false;
                rig.RestHand(side, 5f);
            }
            yield return Wait(0.4f);
        }

        // ================================================================== P3: FLASH FREEZE (searchlight)
        private IEnumerator FlashFreeze(Vector2 at)
        {
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead) yield break;
            if (tombSprite == null) tombSprite = Resources.Load<Sprite>("VFX/vc_ice_tomb");
            if (icicleSprite == null) icicleSprite = Resources.Load<Sprite>("VFX/vc_icicle");
            DrillBoard.Yell("Be still. Forever.", 1.4f);
            at = Feet;
            BossJuice.Flash(at, IceCol, 2.6f, 3f, 0.3f);
            BossJuice.Shake(2);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.25f);
            Sfx.Play("trap_ice", at, 1f);
            Sfx.Play("glass_break", at, 0.4f);
            // ice crystal cluster grows around Ren
            var tomb = new GameObject("ice tomb");
            fieldObjs.Add(tomb);
            tomb.transform.position = at + Vector2.down * 0.08f;
            var ts = tomb.AddComponent<SpriteRenderer>();
            ts.sprite = tombSprite != null ? tombSprite : Disc();
            FxLighting.MakeUnlit(ts);
            ts.sortingLayerID = layer; ts.sortingOrder = order + 2;
            float tk = tombSprite != null ? Float("tomb_height", 1.05f) / tombSprite.bounds.size.y : 1f;
            for (int k = 0; k < 14; k++) AddMote(at + Random.insideUnitCircle * 0.5f, Random.insideUnitCircle.normalized * Random.Range(1f, 2.5f), new Color(0.85f, 0.97f, 1f, 1f), Random.Range(0.3f, 0.6f), 0.045f, true);
            // icicle hanging overhead + shadow
            var ic = new GameObject("icicle");
            fieldObjs.Add(ic);
            var isr = ic.AddComponent<SpriteRenderer>();
            isr.sprite = icicleSprite != null ? icicleSprite : Disc();
            FxLighting.MakeUnlit(isr);
            isr.sortingLayerID = layer; isr.sortingOrder = order + 7;
            float ik = icicleSprite != null ? Float("icicle_height", 1.7f) / icicleSprite.bounds.size.y : 1f;
            ic.transform.localScale = new Vector3(ik, ik, 1f);
            var shadowDot = NewDot("icicle shadow", -14, new Color(0.05f, 0.12f, 0.25f, 0f));
            var ring = NewRing(at, 0.75f);
            int need = Mathf.RoundToInt(Float("freeze_strikes", 6f)), hits = 0;
            bool free = false;
            frozenSolid = true;
            float drop = Float("freeze_drop", 2.6f);
            DrillBoard.Say("FLASH FREEZE", IceCol);
            DrillBoard.CommandTime = Time.time;
            for (float t = 0f; t < drop && !aborted; t += Time.deltaTime)
            {
                float u = t / drop;
                if (!free)
                {
                    hp.Stun(0.15f);
                    float grow = Mathf.Clamp01(t / 0.15f);
                    tomb.transform.localScale = new Vector3(tk, tk * Mathf.Lerp(0.2f, 1f, grow), 1f);
                    float crack = (float)hits / need;
                    ts.color = Color.Lerp(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.65f), crack);
                    DrillBoard.Command = "BREAK THE ICE! " + hits + "/" + need;
                    if (GameInput.PressedRaw(GameAction.Attack))
                    {
                        hits++;
                        tomb.transform.position = at + Vector2.down * 0.08f + new Vector2(Random.Range(-0.04f, 0.04f), 0f);
                        Sfx.Play("glass_break", at, 0.35f);
                        for (int k = 0; k < 5; k++) AddMote(at + Vector2.up * Random.Range(0.1f, 0.8f), new Vector2(Random.Range(-2f, 2f), Random.Range(0.5f, 2f)), new Color(0.8f, 0.95f, 1f, 1f), 0.4f, 0.04f);
                        if (hits >= need)
                        {
                            free = true;
                            frozenSolid = false;
                            DrillBoard.Command = "";
                            DrillBoard.Say("SHATTERED", RelicBlue);
                            SpriteVfx.Spawn("abs_ice_shatter", at + Vector2.up * 0.4f, 1f, layer, order + 3, false, 18f, false);
                            SpriteVfx.Spawn("duo_ice_burst", at, 0.9f, layer, order + 3, false, 18f, false);
                            Sfx.Play("glass_break", at, 1f);
                            DestroyObj(tomb);
                        }
                    }
                }
                Vector2 hang = at + Vector2.up * (4.2f - 0.4f * u) + new Vector2(Mathf.Sin(t * 50f) * 0.03f * u, 0f);
                ic.transform.position = hang;
                float sk = Mathf.Lerp(0.25f, 1.05f, u);
                shadowDot.transform.position = at;
                shadowDot.transform.localScale = new Vector3(sk, sk * 0.5f, 1f);
                shadowDot.color = new Color(0.05f, 0.12f, 0.25f, 0.2f + 0.35f * u);
                ring.color = new Color(IceWarn.r, IceWarn.g, IceWarn.b, (0.3f + 0.5f * u) * (0.7f + 0.3f * Mathf.Sin(t * (10f + 20f * u))));
                if (Random.value < 0.25f) AddMote(hang + new Vector2(Random.Range(-0.2f, 0.2f), -1.2f), Vector2.down * 0.6f, new Color(0.85f, 0.95f, 1f, 0.8f), 0.6f, 0.03f);
                yield return null;
            }
            DestroyObj(ring);
            if (aborted) { DestroyObj(tomb); DestroyObj(ic); DestroyObj(shadowDot); frozenSolid = false; yield break; }
            // fall
            Vector2 from = ic.transform.position, to = at + Vector2.up * 0.05f;
            Sfx.Play("arrow_shot", from, 0.6f);
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) { ic.transform.position = Vector2.Lerp(from, to, t / 0.12f); yield return null; }
            DestroyObj(ic); DestroyObj(shadowDot);
            SpriteVfx.Spawn("abs_ice_shatter", at + Vector2.up * 0.3f, 1.4f, layer, order + 4, false, 18f, false);
            SpriteVfx.Spawn("duo_ice_burst", at, 1.3f, layer, order + 3, false, 18f, false);
            SpriteVfx.Spawn("abs_ice_spikes", at, 1.2f, layer, order - 1, Random.value < 0.5f, 16f, false);
            for (int k = 0; k < 18; k++) AddMote(at, Random.insideUnitCircle.normalized * Random.Range(1.5f, 4f), new Color(0.85f, 0.97f, 1f, 1f), Random.Range(0.3f, 0.6f), 0.05f, true);
            BossJuice.Flash(at, IceCol, 2.4f, 3f, 0.25f);
            BossJuice.Shake(3);
            Sfx.Play("glass_break", at, 1f);
            Sfx.Play("trap_ice", at, 0.8f);
            if (!free)
            {
                DestroyObj(tomb);
                frozenSolid = false;
                DrillBoard.Command = "";
                HeavyHit(Float("freeze_crush", 26f), at + Vector2.up);
            }
            else if ((Feet - at).magnitude < 0.75f) Hurt(16f, at, 1.2f);
        }

        // ================================================================== P3: GLACIAL CROSS (eyes)
        private static readonly Color IceCore = new Color(0.95f, 1f, 1f, 1f), IceMid = new Color(0.55f, 0.88f, 1f, 0.95f), IceEdge = new Color(0.3f, 0.55f, 0.95f, 0.6f);

        private IEnumerator GlacialCross(float pressure)
        {
            DrillBoard.Say("GLACIAL CROSS", IceCol);
            DrillBoard.Yell("Nowhere is out of my sight.", 1.6f);
            lungeGoal = 1f;
            yield return Wait(0.6f);
            if (aborted) { lungeGoal = 0f; yield break; }
            Rect f = Floor;
            const float M = 0.1f;
            Vector2 tl = new Vector2(f.xMin + M, f.yMax - M), tr = new Vector2(f.xMax - M, f.yMax - M);
            Vector2 bl = new Vector2(f.xMin + M, f.yMin + M), br = new Vector2(f.xMax - M, f.yMin + M), c = f.center;
            Vector2 d1 = (br - tl).normalized, d2 = (bl - tr).normalized;
            float half = (br - tl).magnitude * 0.5f;
            bool sweep = pressure >= 1.1f || vhp < 0.5f;
            float sweepDeg = sweep ? Float("cross_sweep", 12f) : 0f, sgn = Random.value < 0.5f ? -1f : 1f;
            Light2D[] eyeGlow = { BossJuice.Light(EyeSocket(-1), IceCol, 1.4f, 0f, 0f), BossJuice.Light(EyeSocket(1), IceCol, 1.4f, 0f, 0f) };
            var eyeL = NewBeam(IceCore, IceMid, IceEdge, 0.18f, order + 3);
            var eyeR = NewBeam(IceCore, IceMid, IceEdge, 0.18f, order + 3);
            var m1 = NewBeam(IceCore, IceMid, IceEdge, 0.5f, order + 3);
            var m2 = NewBeam(IceCore, IceMid, IceEdge, 0.5f, order + 3);
            Sfx.Play("eye_beam_charge", EyeSocket(-1), 0.9f);
            // WARN: thin eye beams touch the 2 top corners, 2 frost lines run along the 2 diagonals
            float warnT = Mathf.Lerp(1.15f, 0.9f, pressure * 0.5f);
            Vector2 a1 = c - Rot(d1, -sweepDeg * sgn) * half, b1 = c + Rot(d1, -sweepDeg * sgn) * half;
            Vector2 a2 = c - Rot(d2, sweepDeg * sgn) * half, b2 = c + Rot(d2, sweepDeg * sgn) * half;
            for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime)
            {
                float u = t / warnT;
                for (int i = 0; i < 2; i++) if (eyeGlow[i] != null) { eyeGlow[i].transform.position = EyeSocket(i == 0 ? -1 : 1); eyeGlow[i].intensity = 1.2f + 1.4f * u + 0.4f * Mathf.Sin(t * 40f); }
                SetBeam(eyeL, EyeSocket(-1), a1, 0.35f + 0.4f * u, 0.4f);
                SetBeam(eyeR, EyeSocket(1), a2, 0.35f + 0.4f * u, 0.4f);
                SetBeam(m1, a1, b1, 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(t * 14f)), 0.1f);
                SetBeam(m2, a2, b2, 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(t * 14f)), 0.1f);
                if (Random.value < 0.6f) AddMote(Vector2.Lerp(a1, b1, Random.value), Vector2.up * 0.4f, new Color(0.8f, 0.95f, 1f, 0.9f), 0.5f, 0.03f);
                if (Random.value < 0.6f) AddMote(Vector2.Lerp(a2, b2, Random.value), Vector2.up * 0.4f, new Color(0.8f, 0.95f, 1f, 0.9f), 0.5f, 0.03f);
                yield return null;
            }
            if (aborted) { KillBeam(eyeL); KillBeam(eyeR); KillBeam(m1); KillBeam(m2); foreach (var g in eyeGlow) if (g != null) Object.Destroy(g.gameObject); lungeGoal = 0f; yield break; }
            // FIRE: 2 full-arena diagonal beams (rotating sweep under high pressure)
            Sfx.Play("trap_ice", c, 1f);
            Sfx.Play("gas_hiss", c, 0.8f);
            BossJuice.Shake(2);
            BossJuice.Flash(a1, IceCol, 2f, 2.5f, 0.2f); BossJuice.Flash(a2, IceCol, 2f, 2.5f, 0.2f);
            float fire = Float("cross_seconds", 1.6f), nextHurt = 0f;
            for (float t = 0f; t < fire && !aborted; t += Time.deltaTime)
            {
                float u = t / fire, off = sweep ? Mathf.Lerp(-sweepDeg, sweepDeg, u * u * (3f - 2f * u)) * sgn : 0f;
                Vector2 r1 = Rot(d1, off), r2 = Rot(d2, -off);
                a1 = c - r1 * half; b1 = c + r1 * half; a2 = c - r2 * half; b2 = c + r2 * half;
                float k = t < 0.1f ? Mathf.Lerp(0.3f, 1.25f, t / 0.1f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.1f) / 0.2f));
                SetBeam(eyeL, EyeSocket(-1), a1, 1f, 1f);
                SetBeam(eyeR, EyeSocket(1), a2, 1f, 1f);
                SetBeam(m1, a1, b1, 1f, k);
                SetBeam(m2, a2, b2, 1f, k);
                for (int n = 0; n < 2; n++)
                {
                    Vector2 pa = n == 0 ? a1 : a2, pb = n == 0 ? b1 : b2, dir = (pb - pa).normalized;
                    Vector2 q = Vector2.Lerp(pa, pb, Random.value);
                    AddMote(q, new Vector2(-dir.y, dir.x) * Random.Range(-1.2f, 1.2f) + Vector2.up * 0.3f, new Color(0.85f, 0.97f, 1f, 1f), Random.Range(0.25f, 0.5f), 0.04f, true);
                }
                if (Time.time >= nextHurt)
                {
                    Vector2 fp = Feet + Vector2.up * 0.15f;
                    if (SegDist(fp, a1, b1) < 0.3f || SegDist(fp, a2, b2) < 0.3f)
                        if (Hurt(Float("cross_dmg", 12f), c, 0.8f)) { AddFrost(1); nextHurt = Time.time + 0.5f; }
                }
                yield return null;
            }
            KillBeam(eyeL); KillBeam(eyeR);
            foreach (var g in eyeGlow) if (g != null) Object.Destroy(g.gameObject);
            lungeGoal = 0f;
            // frost trails along the 2 beams: fade over 2.5 s, touch = slow
            StartR(FrostStreak(m1, a1, b1));
            StartR(FrostStreak(m2, a2, b2));
            yield return Wait(0.5f);
        }

        private IEnumerator FrostStreak(FxBeam b, Vector2 a, Vector2 e)
        {
            float life = Float("streak_seconds", 2.5f), nextChill = 0f;
            for (float t = 0f; t < life && !aborted; t += Time.deltaTime)
            {
                float u = t / life;
                SetBeam(b, a, e, 0.45f * (1f - u), Mathf.Lerp(0.55f, 0.35f, u));
                if (Time.time >= nextChill && SegDist(Feet + Vector2.up * 0.15f, a, e) < 0.22f) { Chill(0.7f, 1.2f, null); nextChill = Time.time + 0.8f; }
                yield return null;
            }
            KillBeam(b);
        }

        // ================================================================== P4: CHARGED LIGHTNING RODS
        private readonly Dictionary<Rod, int> rodCharge = new Dictionary<Rod, int>();
        private bool rodChargeHinted;

        /// <summary>A rod absorbed `n` bolts: brightens in 3 levels; full -> OVERLOAD (discharge into a lying hand / electric ring around the rod).</summary>
        private void RodCharge(Rod r, int n)
        {
            if (r == null || r.dead) return;
            rodCharge.TryGetValue(r, out int c);
            c = Mathf.Min(3, c + n);
            rodCharge[r] = c;
            if (r.l != null) { r.l.intensity = 0.8f + 0.7f * c; r.l.pointLightOuterRadius = 1.1f + 0.35f * c; }
            if (r.sr != null) r.sr.color = Color.Lerp(Color.white, new Color(1.4f, 1.3f, 0.8f), c / 3f);
            for (int k = 0; k < 6 * n; k++) AddMote(RodTip(r), Random.insideUnitCircle * 2.5f, new Color(1f, 0.95f, 0.6f, 1f), 0.3f, 0.04f, true);
            DrillBoard.Say("ROD " + c + "/3", StormCol);
            if (!rodChargeHinted) { rodChargeHinted = true; Hints.Show("Storm rods drink lightning. A full rod bursts into a fallen hand.", 3.5f); }
            if (c >= 3) StartR(RodOverload(r));
        }

        private IEnumerator RodOverload(Rod r)
        {
            for (float t = 0f; t < 0.45f && !aborted && !r.dead; t += Time.deltaTime)
            {
                if (r.sr != null) r.sr.color = Mathf.Sin(t * 50f) > 0f ? new Color(2f, 2f, 1.4f) : Color.white;
                yield return null;
            }
            if (aborted || r.dead) yield break;
            Vector2 tip = RodTip(r);
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                if (!HandDown(side)) continue;
                float d = (rig.HandWorld(side) - r.p).magnitude;
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0)
            {
                Vector2 h = rig.HandWorld(best == 0 ? -1 : 1);
                StartR(ForkBolt(tip, h, 0.4f));
                DamageVessel(Float("rod_hits", 2f), h);
                downUntil[best] += 0.8f;
                DrillBoard.Say("OVERLOAD!", StormCol);
            }
            else
            {
                // electric ring around the rod (standing next to a full rod = hit)
                for (int k = 0; k < 6; k++) StartR(ForkBolt(tip, r.p + Rot(Vector2.right, k * 60f + Random.Range(-15f, 15f)) * new Vector2(1.2f, 0.75f), 0.3f));
                if ((Feet - r.p).magnitude < 1.1f) Shock(9f, r.p, 1f, 0f);
                DrillBoard.Say("OVERCHARGE", StormCol);
            }
            BossJuice.Flash(tip, StormCol, 2.4f, 3f, 0.25f);
            BossJuice.Shake(2);
            Sfx.Play("bolt_strike", tip, 1f);
            rodCharge.Remove(r);
            BreakRod(r, true);
        }

        private Rod NearestRod(Vector2 p, float within)
        {
            Rod best = null; float bd = within;
            foreach (var r in rods) if (!r.dead && (r.p - p).magnitude < bd) { bd = (r.p - p).magnitude; best = r; }
            return best;
        }

        // ================================================================== P4: STATIC CHARGE (searchlight)
        private bool charged;

        private IEnumerator StaticCharge()
        {
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead) yield break;
            DrillBoard.Yell("You carry my storm now.", 1.4f);
            // grounding needs rods: if missing, plant more until there are 2, away from Ren
            int alive = 0;
            foreach (var r in rods) if (!r.dead) alive++;
            for (int k = alive; k < 2; k++)
            {
                Vector2 p = Feet;
                for (int tries = 0; tries < 10; tries++)
                {
                    p = new Vector2(Random.Range(Floor.xMin + 0.8f, Floor.xMax - 0.8f), Random.Range(Floor.yMin + 0.6f, Floor.yMax - 0.8f));
                    if ((p - Feet).magnitude > 2.2f && (p - Feet).magnitude < 4f) break;
                }
                var rod = PlantRod(p, Float("rod_life", 12f), 1.6f);
                rods.Add(rod);
                SkyBolt(p, 1f);
            }
            charged = true;
            float until = Time.time + Float("charge_seconds", 6f), nextStrike = Time.time + 1.1f, nextSpark = 0f;
            var glow = BossJuice.Light(Feet, StormCol, 1.2f, 1.2f, 0f);
            Sfx.Play("bolt_strike", Feet, 0.6f);
            hp.TintPulse(StormCol, 0.4f);
            DrillBoard.CommandTime = Time.time;
            while (charged && Time.time < until && !aborted && !hp.IsDead)
            {
                DrillBoard.Command = "CHARGED! TOUCH A STORM ROD (" + Mathf.CeilToInt(until - Time.time) + ")";
                if (glow != null) { glow.transform.position = Feet + Vector2.up * 0.4f; glow.intensity = 1f + 0.5f * Mathf.Sin(Time.time * 30f); }
                if (Time.time >= nextSpark)
                {
                    nextSpark = Time.time + 0.05f;
                    AddMote(Feet + Vector2.up * Random.Range(0.1f, 0.8f) + Vector2.right * Random.Range(-0.25f, 0.25f), Random.insideUnitCircle * 1.6f, new Color(1f, 0.95f, 0.55f, 1f), 0.18f, 0.035f, true);
                }
                var rod = NearestRod(Feet, Float("ground_radius", 0.8f));
                if (rod != null)
                {
                    StartR(ForkBolt(Feet + Vector2.up * 0.4f, RodTip(rod), 0.4f));
                    charged = false;
                    DrillBoard.Say("GROUNDED", RelicBlue);
                    Sfx.Play("bolt_strike", rod.p, 0.8f);
                    RodCharge(rod, 2);
                    break;
                }
                if (Time.time >= nextStrike)
                {
                    nextStrike = Time.time + Float("charge_every", 1.5f);
                    StartR(Strike(ClampFloor(Feet + PlayerVel * 0.3f, 0.3f), 0.65f, 12f));
                }
                yield return null;
            }
            charged = false;
            DrillBoard.Command = "";
            if (glow != null) Object.Destroy(glow.gameObject);
        }

        // ================================================================== P4: THUNDER GAZE (eyes — real lightning instead of a laser)
        private IEnumerator ThunderGaze(float pressure)
        {
            DrillBoard.Say("THUNDER GAZE", StormCol);
            DrillBoard.Yell("Look at me when I strike.", 1.4f);
            lungeGoal = 1f;
            yield return Wait(0.55f);
            Light2D[] eyeGlow = { BossJuice.Light(EyeSocket(-1), StormCol, 1.3f, 1.6f, 0f), BossJuice.Light(EyeSocket(1), StormCol, 1.3f, 1.6f, 0f) };
            Sfx.Play("eye_beam_charge", EyeSocket(1), 0.9f);
            int waves = pressure >= 1.1f || vhp < 0.5f ? 4 : 3;
            float warn = Mathf.Lerp(0.6f, 0.48f, pressure * 0.5f);
            for (int w = 0; w < waves && !aborted; w++)
            {
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    Vector2 target = ClampFloor(Feet + PlayerVel * 0.35f + new Vector2(side * Random.Range(0.25f, 0.7f), Random.Range(-0.4f, 0.4f)), 0.3f);
                    if (w == waves - 1 && i == 0) target = ClampFloor(Feet + PlayerVel * 0.25f, 0.3f);
                    StartR(EyeStrike(side, target, warn));
                }
                for (float t = 0f; t < Mathf.Lerp(0.8f, 0.6f, pressure * 0.5f) && !aborted; t += Time.deltaTime)
                {
                    for (int i = 0; i < 2; i++) if (eyeGlow[i] != null) { eyeGlow[i].transform.position = EyeSocket(i == 0 ? -1 : 1); eyeGlow[i].intensity = 1.4f + 0.6f * Mathf.Sin(Time.time * 45f + i); }
                    yield return null;
                }
            }
            yield return Wait(0.3f);
            foreach (var g in eyeGlow) if (g != null) Object.Destroy(g.gameObject);
            lungeGoal = 0f;
        }

        private IEnumerator EyeStrike(int side, Vector2 target, float warn)
        {
            var ring = NewRing(target, 0.6f);
            for (float t = 0f; t < warn && !aborted; t += Time.deltaTime)
            {
                float u = t / warn, rk = Mathf.Lerp(0.6f, 0.3f, u) / (Ring().bounds.size.x * 0.5f);
                ring.transform.localScale = new Vector3(rk, rk * 0.6f, 1f);
                ring.color = new Color(StormWarn.r, StormWarn.g, StormWarn.b, 0.35f + 0.5f * u);
                if (Random.value < 0.35f) AddMote(target + Random.insideUnitCircle * 0.3f, Vector2.up * Random.Range(0.6f, 1.4f), new Color(1f, 0.95f, 0.6f, 0.9f), 0.3f, 0.03f, true);
                yield return null;
            }
            DestroyObj(ring);
            if (aborted) yield break;
            var catcher = NearestRod(target, 1.4f);
            Vector2 hit = catcher != null ? RodTip(catcher) : target;
            StartR(ForkBolt(EyeSocket(side), hit, 0.3f));
            SpriteVfx.Spawn("vc_volt_impact", hit, 0.8f, layer, order + 3, false, 22f, false);
            BossJuice.Flash(hit, StormCol, 1.6f, 2.2f, 0.15f);
            BossJuice.Shake(1);
            Sfx.Play("bolt_strike", hit, 0.9f);
            if (catcher != null) RodCharge(catcher, 1);
            else if ((Feet - target).magnitude < 0.55f) Shock(11f, target, 0.8f, 0.6f);
        }

        // ================================================================== P5: SINGULARITY (replaces Black Hole)
        private static readonly Color VoidRim = new Color(1f, 0.78f, 1f, 1f), VoidMid = new Color(0.78f, 0.42f, 1f, 0.95f), VoidEdge = new Color(0.35f, 0.12f, 0.6f, 0.6f);

        private IEnumerator Singularity(float pressure)
        {
            DrillBoard.Say("SINGULARITY", UmbraCol);
            DrillBoard.Yell("Fall into me.", 1.2f);
            Vector2 c = Feet;
            for (int k = 0; k < 10; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                c = ClampFloor(Feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * 2.2f, 1f);
                if ((c - Feet).magnitude > 1.6f) break;
            }
            float life = Float("hole_seconds", 3.2f);
            var core = NewDot("singularity core", order + 2, Color.black);
            var rim = NewDot("singularity rim", order + 3, VoidRim); rim.sprite = BandRing();
            var halo = NewDot("singularity halo", order + 1, new Color(0.7f, 0.4f, 1f, 0.25f)); halo.sprite = BandRing();
            var l = BossJuice.Light(c, UmbraCol, 2.4f, 0f, 0f);
            if (l != null) fieldObjs.Add(l.gameObject);
            Sfx.Play("teleport_dark", c, 1f);
            var st = RenStatus;
            float nextCore = 0f, a0 = Random.Range(0f, 45f);
            var warns = new List<WarnBand>();
            bool warned = false;
            for (float t = 0f; t < life && !aborted; t += Time.deltaTime)
            {
                float u = t / life, grow = Mathf.Clamp01(t / 0.45f);
                float coreR = 0.28f * grow * (1f + 0.06f * Mathf.Sin(t * 9f));
                core.transform.position = c; core.transform.localScale = new Vector3(coreR * 2f, coreR * 2f, 1f);
                float rimR = coreR * 1.45f;
                rim.transform.position = c; rim.transform.localScale = new Vector3(rimR, rimR, 1f);
                rim.transform.rotation = Quaternion.Euler(0f, 0f, t * 90f);
                rim.color = new Color(VoidRim.r, VoidRim.g, VoidRim.b, 0.8f + 0.2f * Mathf.Sin(t * 23f));
                float hr = rimR * (1.9f + 0.15f * Mathf.Sin(t * 5f));
                halo.transform.position = c; halo.transform.localScale = new Vector3(hr, hr * 0.5f, 1f);       // disc squashed to the view angle
                halo.color = new Color(0.75f, 0.45f, 1f, 0.3f * grow);
                // accretion disc: particles spiral in from the outer ellipse; debris streaks in from afar
                for (int k = 0; k < 3; k++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f), rr = Random.Range(1.2f, 2.4f);
                    Vector2 p = c + new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr * 0.45f);
                    Color mc = Random.value < 0.25f ? new Color(1f, 0.9f, 1f, 1f) : Random.value < 0.5f ? VoidMid : new Color(0.55f, 0.3f, 0.9f, 0.9f);
                    AddMote(p, Vector2.zero, mc, 1.4f, Random.Range(0.025f, 0.05f), true, c, 1.6f, 1.8f, order + 2);
                }
                if (Random.value < 0.15f)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    AddMote(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f) * 3.2f, Vector2.zero, new Color(0.2f, 0.1f, 0.3f, 0.9f), 1.6f, 0.06f, true, c, 2.4f, 0.6f, order + 1);
                }
                Vector2 d = c - Feet;
                float dist = d.magnitude;
                if (t > 0.4f && t < life - 0.35f && st != null && dist > 0.05f)
                    st.Push(d / dist * Float("hole_pull", 2.2f) * Mathf.Clamp01(1.2f - dist / 5f), 0.12f);
                if (dist < 0.4f && Time.time >= nextCore && Hurt(6f, c, 0f)) nextCore = Time.time + 0.4f;
                if (l != null) l.intensity = Mathf.Lerp(0.4f, 1.6f, u) * (0.85f + 0.15f * Mathf.Sin(t * 20f));
                if (!warned && t >= life - 0.6f)
                {
                    warned = true;
                    for (int k = 0; k < 8; k++) warns.Add(Warn(c, c + RayDir(a0 + k * 45f) * 3.2f, 0.16f, WarnViolet, 0.6f));
                }
                yield return null;
            }
            foreach (var w in warns) if (w != null) w.Release();
            if (aborted) { DestroyObj(core); DestroyObj(rim); DestroyObj(halo); if (l != null) DestroyObj(l.gameObject); yield break; }
            // CONTRACT for one beat
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                float s = Mathf.Lerp(0.56f, 0.08f, t / 0.16f);
                core.transform.localScale = new Vector3(s, s, 1f);
                rim.transform.localScale = new Vector3(s * 0.75f, s * 0.75f, 1f);
                yield return null;
            }
            DestroyObj(core); DestroyObj(halo);
            // BURST: light ring spreads + 8 bright cracks
            Sfx.Play("bolt_strike", c, 1f);
            Sfx.Play("glass_break", c, 0.6f);
            BossJuice.Shake(2);
            BossJuice.Flash(c, VoidRim, 3.5f, 3f, 0.3f);
            var rays = new List<(FxBeam b, Vector2 end)>();
            for (int k = 0; k < 8; k++) rays.Add((NewBeam(VoidRim, VoidMid, VoidEdge, 0.14f, order + 3), c + RayDir(a0 + k * 45f) * 3.2f));
            for (int k = 0; k < 30; k++) AddMote(c, Random.insideUnitCircle.normalized * Random.Range(2f, 5f), Random.value < 0.4f ? VoidRim : VoidMid, Random.Range(0.3f, 0.6f), 0.045f, true);
            bool hit = false;
            for (float t = 0f; t < 0.45f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.45f, reach = Mathf.Clamp01(t / 0.08f);
                float rr = Mathf.Lerp(0.1f, 2.6f, Mathf.Sqrt(u));
                rim.transform.localScale = new Vector3(rr, rr * 0.6f, 1f);
                rim.color = new Color(VoidRim.r, VoidRim.g, VoidRim.b, 1f - u);
                foreach (var (b, end) in rays)
                {
                    Vector2 e = Vector2.Lerp(c, end, reach);
                    SetBeam(b, c, e, 1f - u * u, Mathf.Lerp(1.3f, 0.6f, u));
                    if (!hit && reach >= 1f && SegDist(Feet + Vector2.up * 0.2f, c, end) < 0.22f) hit = Hurt(12f, c, 1.2f);
                }
                yield return null;
            }
            foreach (var (b, _) in rays) KillBeam(b);
            DestroyObj(rim);
            if (l != null) DestroyObj(l.gameObject);
            yield return Wait(0.4f);
        }

        // ================================================================== P5: INK TETHER (replaces shadow bind)
        private bool tetherOn;
        private Vector2 tetherPin;
        private float tetherUntil;
        private SpriteRenderer tetherSigil, tetherCore;
        private readonly List<SpriteRenderer> tetherBeads = new List<SpriteRenderer>();

        private void StartTether(float seconds, float dmg, string say)
        {
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead || tetherOn) return;
            if (dmg > 0f) Hurt(dmg, Feet + Vector2.up * 0.4f, 0f);
            tetherOn = true;
            tetherPin = Feet;
            tetherUntil = Time.time + seconds;
            tetherCore = NewDot("ink pin", -13, new Color(0.08f, 0.02f, 0.14f, 0.9f));
            tetherCore.transform.position = tetherPin; tetherCore.transform.localScale = new Vector3(0.5f, 0.28f, 1f);
            tetherSigil = NewDot("ink sigil", -12, VoidMid); tetherSigil.sprite = BandRing();
            tetherSigil.transform.position = tetherPin;
            for (int i = 0; i < 11; i++) tetherBeads.Add(NewDot("ink bead", order + 1, new Color(0.2f, 0.06f, 0.3f, 1f)));
            SpriteVfx.Spawn("abs_grasp", tetherPin, 0.8f, layer, order + 1, Random.value < 0.5f, 24f, false);
            for (int k = 0; k < 12; k++) AddMote(tetherPin, Random.insideUnitCircle * 2f + Vector2.up, new Color(0.15f, 0.05f, 0.25f, 1f), 0.5f, 0.05f);
            Sfx.Play("reg_grip", Feet, 0.8f);
            hp.TintPulse(UmbraCol, 0.4f);
            DrillBoard.Say(say, UmbraCol);
            DrillBoard.Command = "STRIKE THE INK SIGIL!";
            DrillBoard.CommandTime = Time.time;
        }

        private void TickTether()
        {
            if (!tetherOn) return;
            var hp = Ctx.PlayerHealth;
            if (Time.time >= tetherUntil || hp == null || hp.IsDead || tetherSigil == null) { EndTether(false); return; }
            float L = Float("tether_len", 1.4f);
            Vector2 d = Feet - tetherPin;
            float dl = d.magnitude;
            var st = RenStatus;
            if (dl > L && st != null) st.Push(-d / dl * Mathf.Min(6f, (dl - L) * 10f + 1.5f), 0.1f);        // elastic tether pulls back
            Vector2 end = Feet + Vector2.up * 0.3f;
            float sag = Mathf.Max(0f, L - dl) * 0.35f, now = Time.time;
            int n = tetherBeads.Count;
            for (int i = 0; i < n; i++)
            {
                var b = tetherBeads[i];
                if (b == null) continue;
                float u = (i + 1f) / (n + 1f);
                Vector2 p = Vector2.Lerp(tetherPin, end, u) + Vector2.down * Mathf.Sin(u * Mathf.PI) * sag;
                b.transform.position = p;
                float s = 0.09f + 0.02f * Mathf.Sin(now * 8f + i);
                b.transform.localScale = new Vector3(s, s, 1f);
                b.color = Color.Lerp(new Color(0.18f, 0.05f, 0.28f, 1f), VoidMid, 0.25f + 0.25f * Mathf.Sin(now * 6f - i * 0.7f));
            }
            float sr = 0.32f + 0.03f * Mathf.Sin(now * 7f);
            tetherSigil.transform.localScale = new Vector3(sr, sr * 0.56f, 1f);
            tetherSigil.color = new Color(VoidMid.r, VoidMid.g, VoidMid.b, 0.7f + 0.3f * Mathf.Sin(now * 10f));
            if (Random.value < 0.2f) AddMote(tetherPin + Random.insideUnitCircle * 0.25f, Vector2.up * 0.5f, new Color(0.25f, 0.08f, 0.35f, 0.9f), 0.6f, 0.04f);
        }

        private void StrikeSigil(Vector2 c, float r)
        {
            if (!tetherOn || (tetherPin - c).magnitude > r + 0.45f) return;
            EndTether(true);
        }

        private void EndTether(bool cut)
        {
            if (!tetherOn) return;
            tetherOn = false;
            for (int k = 0; k < (cut ? 16 : 6); k++) AddMote(tetherPin, Random.insideUnitCircle.normalized * Random.Range(1f, 3f), cut ? VoidRim : new Color(0.2f, 0.06f, 0.3f, 1f), 0.4f, 0.045f, true);
            foreach (var b in tetherBeads) DestroyObj(b);
            tetherBeads.Clear();
            DestroyObj(tetherSigil); DestroyObj(tetherCore);
            tetherSigil = tetherCore = null;
            if (DrillBoard.Command == "STRIKE THE INK SIGIL!") DrillBoard.Command = "";
            if (cut) { Sfx.Play("glass_break", tetherPin, 0.7f); DrillBoard.Say("SEVERED", RelicBlue); }
        }
    }
}
