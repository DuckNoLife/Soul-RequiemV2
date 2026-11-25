using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE 1 HOLLOW (pure physical, pale grey Vessel) + HAND MOVES SHARED by every phase.
    ///   SLAM        — the hand shadow follows Ren then locks and slams down; the hand LIES for 5 s (`down_seconds`): joints flash BLUE -> sword hits on the hand
    ///                 damage the Vessel. In the last 1 s the joints turn red + twitch hard = about to lift (do not get greedy).
    ///   CLAP        — both hands stand UPRIGHT (hand_point) at the two ends of a horizontal band in front of the Vessel and slam together:
    ///                 the WHOLE BAND is hit -> leave the band (southward) before the hands close.
    ///   RAKE        — diagonal claw: warning band -> claws sweep and hit IMMEDIATELY (16) -> 3 void scars along the claw direction, radiating aura,
    ///                 bursting after 0.9 s, lasting ~4 s.
    ///   TRIPLE SLAM — the fist slams 3 times in a row after Ren, each with a QUAKE WAVE; the last one lies for 2.5 s (short window).
    ///   GRAB        — grabs Ren and squeezes; strike 4–5 times in a row to break free.
    ///   STOMP (base rule) — standing next to a RESTING hand for ~0.8 s -> that hand slams exactly there (shadow locks) — stops hand-hugging.
    /// Damage to the Vessel: ONLY while a hand is lying down (PlayerCombat.Struck hits the hand) — each hit = 1 / `phase_hits` of the phase HP.
    /// Called by: PhaseLoop move tables; OnStruckHand is subscribed to PlayerCombat.Struck and routes hits to hands / snowballs / rods / orbs / anvil chain.
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static readonly Color DownBlue = new Color(0.55f, 0.85f, 1.35f);
        private static readonly Color WakeRed = new Color(1.4f, 0.45f, 0.45f);

        private readonly float[] downUntil = new float[2], nextCounter = new float[2], nearT = new float[2], windowDmg = new float[2];
        private bool struckHooked, grabbing;
        private int grabStrikes;

        private static int Idx(int side) => side < 0 ? 0 : 1;
        private bool HandDown(int side) => Time.time < downUntil[Idx(side)];
        private bool AnyDown => HandDown(-1) || HandDown(1);
        /// <summary>Hand free to attack (not lying down, not holding Ren with the right hand).</summary>
        private bool HandFree(int side) => !HandDown(side) && !handBusy[Idx(side)] && !(side > 0 && grabbing);   // handBusy = hand is carrying / picking up a trap
        private int FreeSide(int prefer)
        {
            if (HandFree(prefer)) return prefer;
            return HandFree(-prefer) ? -prefer : 0;
        }

        private void HookStruck()
        {
            if (struckHooked) return;
            PlayerCombat.Struck += OnStruckHand;
            struckHooked = true;
        }

        private void ResetHands()
        {
            for (int i = 0; i < 2; i++) { downUntil[i] = 0f; nearT[i] = 0f; }
            if (rig != null) { rig.SetHandTint(-1, Color.white); rig.SetHandTint(1, Color.white); }
            if (DrillBoard.Command == "STRIKE THE HAND!") DrillBoard.Command = "";
        }

        // ------------------------------------------------------------------ counter window
        /// <summary>Hand lies on the floor at `at` for `seconds`: blue joints (hittable), last 1 s red + twitch (about to lift), then back to rest.</summary>
        private IEnumerator LieDown(int side, Vector2 at, float seconds, string hand)
        {
            int i = Idx(side);
            downUntil[i] = Time.time + seconds; nextCounter[i] = 0f; windowDmg[i] = 0f;
            frozenHand[i] = false;
            if (phase == 2 && actState == ActState.Playing && Time.time < iceUntil)         // slammed onto the ice floor -> hand STUCK IN ICE, lies longer
            {
                frozenHand[i] = true;
                downUntil[i] += Float("frozen_hand_bonus", 1.5f);
                DrillBoard.Say("THE HAND FREEZES TO THE ICE", IceCol);
                Sfx.Play("trap_ice", at, 0.9f);
                for (int k = 0; k < 3; k++) SpriteVfx.Spawn("abs_rime", at + new Vector2((k - 1) * 0.5f, -0.3f), 1f, layer, order + 2, k == 2, 20f, false);
            }
            HookStruck();
            DrillBoard.Command = "STRIKE THE HAND!";
            DrillBoard.CommandTime = Time.time;
            var g = new GameObject("vessel weak point");
            fieldObjs.Add(g);
            var l = g.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = 1.3f; l.pointLightInnerRadius = 0.05f; l.intensity = 0f;
            bool warned = false;
            while (Time.time < downUntil[i] && !aborted)
            {
                float left = downUntil[i] - Time.time;
                bool waking = left < 1f;
                if (waking && !warned) { warned = true; Sfx.Play("quake_rumble", at, 0.4f); }
                float shake = waking ? 0.07f : 0.015f;
                rig.SetHand(side, at + new Vector2(Mathf.Sin(Time.time * 31f) * shake, 0f), 60f, hand);
                Vector2 h = rig.HandWorld(side);
                g.transform.position = h;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (waking ? 18f : 8f));
                l.color = waking ? new Color(1f, 0.35f, 0.35f) : RelicBlue;
                l.intensity = 0.9f + 0.6f * pulse;
                rig.SetHandTint(side, Color.Lerp(Color.white, waking ? WakeRed : frozenHand[i] ? new Color(0.8f, 1.1f, 1.45f) : DownBlue, 0.5f + 0.5f * pulse));
                yield return null;
            }
            downUntil[i] = 0f;
            frozenHand[i] = false;
            rig.SetHandTint(side, Color.white);
            DestroyObj(g);
            if (DrillBoard.Command == "STRIKE THE HAND!" && !AnyDown) DrillBoard.Command = "";
            if (aborted) yield break;
            rig.RestHand(side, 4f);
        }

        /// <summary>Ren's sword hit (centre c, radius r): hits a lying hand -> damages the Vessel; while grabbed -> counts break-free hits.</summary>
        private void OnStruckHand(Vector2 c, float r)
        {
            if (Ctx == null || Ctx.Host == null) { PlayerCombat.Struck -= OnStruckHand; struckHooked = false; return; }
            if (rig == null || actState != ActState.Playing) return;
            if (phase == 2) StrikeRollers(c, r);                                       // hit a rolling snowball
            if (phase == 3) StrikeRods(c, r);                                          // hit a lightning rod
            StrikeCage(c, r);                                                          // ice cage (P3 searchlight)
            StrikeBalls(c, r);                                                         // lightning ball (P4)
            StrikeOrbs(c, r);                                                          // dark orb (P5)
            StrikeWraiths(c, r);                                                       // Ren's shadow (P5 searchlight)
            StrikeTether(c, r); StrikeSigil(c, r);                                                        // anvil chain (P5)
            if (grabbing)
            {
                grabStrikes++;
                Vector2 gh = rig.HandWorld(1);
                SpriteVfx.Spawn("hit", gh + Random.insideUnitCircle * 0.2f, 0.7f, layer, order + 6, false, 18f, false);
                Sfx.Play("hit_block", gh, 0.6f);
                DrillBoard.Say("STRIKE " + grabStrikes, RelicBlue);
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                if (!HandDown(side) || Time.time < nextCounter[i]) continue;
                Vector2 h = rig.HandWorld(side);
                if ((h - c).magnitude > r + 0.8f) continue;
                nextCounter[i] = Time.time + 0.14f;
                float cap = Float("window_cap", 0.34f) + (frozenHand[i] ? 0.12f : 0f);   // HAND ARMOUR — each time a hand lies down it loses at most 34 % of the phase HP; a frozen hand gets +12 %
                if (lastGrasp) cap = 2f;                                                  // LAST GRASP has no armour
                if (windowDmg[i] >= cap - 0.0001f)
                {
                    SpriteVfx.Spawn("abs_crackle", h + Random.insideUnitCircle * 0.3f, 0.6f, layer, order + 6, false, 20f, false)?.Tint(new Color(0.8f, 0.8f, 0.9f));
                    Sfx.Play("hit_block", h, 0.35f);
                    DrillBoard.Say("THE HAND HARDENS", new Color(0.8f, 0.8f, 0.9f));
                    continue;
                }
                float before = vhp;
                DamageVessel(1f, h);                                                    // 1 hit each (no EXPOSED bonus: the tower lights are watchers now)
                windowDmg[i] += before - vhp;
            }
        }

        /// <summary>Reduce the current phase HP by a number of hits (1 hit = 1 / phase_hits).</summary>
        private void DamageVessel(float hits, Vector2 at)
        {
            vhp = Mathf.Max(0f, vhp - hits / (lastGrasp ? Float("grasp_hits", 14f) : PhaseHits(phase)));   // LAST GRASP has its own bar
            SpriteVfx.Spawn("hit", at + Random.insideUnitCircle * 0.25f, 0.85f, layer, order + 6, false, 18f, false);
            Sfx.Play("hit_block", at, 0.75f);
            CameraFollow.KickMain(at - Feet, 0.05f, 0.12f);
            var pc = Ctx.Player != null ? Ctx.Player.GetComponent<PlayerCombat>() : null;
            if (pc != null) pc.HitStopNow();
            BossJuice.Flash(at, RelicBlue, 1.2f, 1.2f, 0.15f);
            if (vhp <= 0f) for (int i = 0; i < 2; i++) downUntil[i] = Mathf.Min(downUntil[i], Time.time + 0.2f);
        }

        // ------------------------------------------------------------------ STOMP (base rule)
        private void TickStomp(float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                bool near = HandFree(side) && rig.IsResting(side) && (Feet - rig.RestWorld(side)).magnitude < 1.5f;
                nearT[i] = near ? nearT[i] + dt : Mathf.Max(0f, nearT[i] - dt * 2f);
            }
        }

        private int StompSide()
        {
            for (int i = 0; i < 2; i++) if (nearT[i] >= Float("stomp_delay", 0.8f)) return i == 0 ? -1 : 1;
            return 0;
        }

        private IEnumerator Stomp(int side)
        {
            nearT[Idx(side)] = 0f;
            DrillBoard.Say("STOMP", WarnViolet);
            Vector2 target = ClampFloor(Feet);
            var shadow = Shadow(side, target, "hand_fist");
            Sfx.Play("drill_whoosh", rig.HandWorld(side), 0.6f);
            const float Rise = 0.6f;
            for (float t = 0f; t < Rise && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(side, target + Vector2.up * 1.8f, 12f, "hand_fist");
                GrowShadow(shadow, side, target, t / Rise, t > Rise * 0.7f, t);
                yield return null;
            }
            DestroyObj(shadow);
            if (aborted) yield break;
            rig.SetHand(side, target, 50f, "hand_fist");
            yield return Wait(0.07f);
            if (aborted) yield break;
            SlamFx(target, 2);
            if (InHandPrint(target, side, "hand_fist")) HeavyHit(16f, target);           // only hits inside the fist shadow, no wave ring
            yield return Wait(0.5f);
            rig.RestHand(side, 5f);
        }

        /// <summary>Wait `s` seconds but stop early if a Stomp is needed.</summary>
        private IEnumerator WaitOrStomp(float s)
        {
            float end = Time.time + s;
            while (Time.time < end && !aborted && StompSide() == 0 && !spottedReady) yield return null;   // + caught by the searchlight
        }

        // ------------------------------------------------------------------ SLAM (hand lies 5 s)
        private IEnumerator Slam(int side, float pressure)
        {
            side = FreeSide(side);
            if (side == 0) yield break;
            float track = Mathf.Lerp(1.0f, 0.65f, pressure * 0.5f), lockT = 0.45f;
            Vector2 target = ClampFloor(Feet + PlayerVel * 0.25f);
            DrillBoard.Say("SLAM", WarnViolet);
            Sfx.Play("drill_whoosh", rig.HandWorld(side), 0.6f);
            var shadow = Shadow(side, target);
            for (float t = 0f; t < track + lockT && !aborted; t += Time.deltaTime)
            {
                if (t < track) target = Vector2.Lerp(target, ClampFloor(Feet), 1f - Mathf.Exp(-5f * Time.deltaTime));
                rig.SetHand(side, target + Vector2.up * 2.1f, 8f, "hand_open");
                GrowShadow(shadow, side, target, Mathf.Clamp01(t / (track + lockT)), t >= track, t);
                yield return null;
            }
            if (aborted) { DestroyObj(shadow); yield break; }
            rig.SetHand(side, target, 45f, "hand_open");
            yield return Wait(0.09f);
            DestroyObj(shadow);
            if (aborted) yield break;
            SlamFx(target, 2);
            if (InHandPrint(target, side, "hand_open")) HeavyHit(18f, target);           // only hits inside the hand shadow (no unavoidable 3–4 u wave ring)
            if (noLie) { yield return Wait(0.35f); rig.RestHand(side, 6f); yield break; }   // LAST GRASP — nonstop attacks, hands do not stay down
            yield return LieDown(side, target, Float("down_seconds", 5f), "hand_open");
        }

        // ------------------------------------------------------------------ CLAP (band in front of the Vessel)
        private IEnumerator Clap(float pressure)
        {
            if (!HandFree(-1) || !HandFree(1)) yield break;
            DrillBoard.Say("CLAP", WarnViolet);
            float yTop = Floor.yMax - 0.15f, yBot = Floor.yMax - Float("clap_depth", 3.6f);
            float cy = (yTop + yBot) * 0.5f, half = (yTop - yBot) * 0.5f;
            float xl = Floor.xMin + 0.2f, xr = Floor.xMax - 0.2f, cx = Floor.center.x;
            float warnT = Mathf.Lerp(1.3f, 0.95f, pressure * 0.5f);
            var band = Warn(new Vector2(xl, cy), new Vector2(xr, cy), half, WarnViolet, warnT);
            DrillBoard.Yell("Out of my reach? Nowhere is.", 1.4f);
            for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime)
            {
                float lift = Mathf.Sin(Mathf.Clamp01(t / warnT) * Mathf.PI * 0.5f) * 0.6f;
                rig.SetHand(-1, new Vector2(xl - 0.1f, cy + 0.9f + lift), 9f, "hand_point");
                rig.SetHand(1, new Vector2(xr + 0.1f, cy + 0.9f + lift), 9f, "hand_point");
                yield return null;
            }
            if (band != null) band.Release();
            if (aborted) yield break;
            Sfx.Play("drill_whoosh", new Vector2(cx, cy), 1f);
            const float Close = 0.26f;
            bool hit = false;
            for (float t = 0f; t < Close && !aborted; t += Time.deltaTime)
            {
                float u = t / Close; u *= u;
                float hl = Mathf.Lerp(xl, cx - 0.5f, u), hr = Mathf.Lerp(xr, cx + 0.5f, u);
                rig.SetHand(-1, new Vector2(hl, cy + 0.9f), 90f, "hand_point");
                rig.SetHand(1, new Vector2(hr, cy + 0.9f), 90f, "hand_point");
                yield return null;
            }
            if (aborted) yield break;
            if (Mathf.Abs(Feet.y - cy) <= half && Feet.x > xl - 0.3f && Feet.x < xr + 0.3f) hit = HeavyHit(20f, new Vector2(cx, cy));
            Sfx.Play("hit_block", new Vector2(cx, cy), 1f);
            Sfx.Play("quake_rumble", new Vector2(cx, cy), 0.9f);
            BossJuice.Shake(3);
            BossJuice.Flash(new Vector2(cx, cy + 0.6f), PhaseColor, 3f, 3f, 0.35f);
            CameraFollow.KickMain(Vector2.down, 0.1f, 0.3f);
            for (int k = 0; k < 6; k++)
                SpriteVfx.Spawn("m3_dash_dust", new Vector2(cx + Random.Range(-1.2f, 1.2f), cy + Random.Range(-half, half) * 0.6f), 0.8f, layer, order + 1, k % 2 == 0, 16f, false);
            StartR(Shockwave(new Vector2(cx, cy), 0.6f, 2.6f, 5f, 0f));
            if (!hit) DrillBoard.Say("MISSED", RelicBlue);
            yield return Wait(0.7f);
            rig.RestHand(-1, 5f); rig.RestHand(1, 5f);
        }

        // ------------------------------------------------------------------ RAKE (diagonal claw)
        // A rake must hurt IMMEDIATELY on contact and leave VOID SCARS with a VOID AURA along the claw direction.
        // Flow: warning band (BeamWarn) -> claws sweep a -> b, 3 VOID SCARS (vc_void_scar, rotated to the claw direction) open behind the claws; claw hit = 16 + knockback along the rake.
        //      Scars stay ~4 s: pulsing violet glow, rising void particles, standing on a scar = 4 / 0.5 s + slow; 0.9 s after the rake the scars BURST their aura (fast blinking warning) = 10.
        private static readonly Color VoidCol = new Color(0.72f, 0.42f, 1f);

        private IEnumerator Rake(float pressure)
        {
            int side = FreeSide(1);
            if (side == 0) yield break;
            DrillBoard.Say("RAKE", WarnViolet);
            float dx = Feet.x < Floor.center.x ? 1f : -1f;
            Vector2 a = ClampFloor(new Vector2(Feet.x - dx * 2.6f, Feet.y + 1.6f), 0.2f), b = ClampFloor(new Vector2(Feet.x + dx * 2.6f, Feet.y - 1.6f), 0.2f);
            Vector2 dir = (b - a).normalized, n = new Vector2(-dir.y, dir.x);
            const float Gap = 0.34f, Band = 0.62f;                                       // 3 claws 0.34 u apart; hit band ±0.62
            // 1) raise the claws at the start of the rake path + warning band
            float warnT = Mathf.Lerp(0.75f, 0.55f, pressure * 0.5f);
            var warn = Warn(a, b, Band, WarnViolet, warnT);
            for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(side, a - dir * 0.5f + Vector2.up * 0.9f, 10f, "hand_claw");  // pull back for the wind-up
                yield return null;
            }
            if (warn != null) warn.Release();
            if (aborted) yield break;
            // 2) sweep: scars open behind the claws, hit the moment the claws pass
            var scars = new List<SpriteRenderer>();
            for (int k = 0; k < 3; k++)
            {
                var g = new GameObject("void scar");
                fieldObjs.Add(g);
                g.transform.position = a + n * (k - 1) * Gap;
                g.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                g.transform.localScale = new Vector3(0.001f, 1.3f, 1f);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = Resources.Load<Sprite>("VFX/vc_void_scar_" + k);
                if (sr.sprite == null) { sr.sprite = MarginArt.Pixel(); g.transform.localScale = new Vector3(0.001f, 8f, 1f); sr.color = new Color(0.1f, 0.03f, 0.18f); }
                FxLighting.MakeUnlit(sr);
                sr.sortingLayerID = layer; sr.sortingOrder = -15;
                scars.Add(sr);
            }
            float baseLen = scars[0].sprite != null ? scars[0].sprite.bounds.size.x : 1.92f;
            float drag = Mathf.Lerp(0.32f, 0.26f, pressure * 0.5f);
            Sfx.Play("drill_whoosh", a, 1f);
            bool hit = false;
            Vector2 prev = a;
            for (float t = 0f; t < drag + 0.0001f && !aborted; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / drag);
                Vector2 tip = Vector2.Lerp(a, b, u);
                rig.SetHand(side, tip + Vector2.up * 0.4f, 70f, "hand_claw");
                float sx = Mathf.Max(0.001f, (tip - a).magnitude / Mathf.Max(0.01f, baseLen));
                foreach (var s in scars) if (s != null) { var ls = s.transform.localScale; s.transform.localScale = new Vector3(sx, ls.y, 1f); }
                if (Random.value < 0.6f) VoidMote(tip + n * Random.Range(-Band, Band), 1.4f);
                // hit: Ren is inside the band and the claws just passed him (projected on the rake direction)
                if (!hit)
                {
                    Vector2 f = Feet + Vector2.up * 0.15f - a;
                    float along = Vector2.Dot(f, dir), across = Mathf.Abs(Vector2.Dot(f, n));
                    float p0 = Vector2.Dot(prev - a, dir) - 0.25f, p1 = Vector2.Dot(tip - a, dir) + 0.3f;
                    if (across <= Band && along >= p0 && along <= p1 && Hurt(16f, Feet - dir, 1.3f))
                    {
                        hit = true;
                        BossJuice.Flash(Feet, VoidCol, 2f, 2f, 0.2f);
                        SpriteVfx.Spawn("abs_void_burst", Feet + Vector2.up * 0.2f, 0.7f, layer, order + 2, false, 22f, false);
                    }
                }
                prev = tip;
                if (u >= 1f) break;
                yield return null;
            }
            if (aborted) yield break;
            Sfx.Play("glass_break", (a + b) * 0.5f, 0.7f);
            BossJuice.Shake(1);
            CameraFollow.KickMain(dir, 0.08f, 0.2f);
            rig.RestHand(side, 5f);
            StartR(VoidScars(scars, a, b, n, Gap));
            yield return Wait(0.6f);
        }

        /// <summary>Void scar after a Rake: glow + rising particles, standing on it drains HP, BURSTS its aura after 0.9 s, closes after ~4 s.</summary>
        private IEnumerator VoidScars(List<SpriteRenderer> scars, Vector2 a, Vector2 b, Vector2 n, float gap)
        {
            float len = (b - a).magnitude;
            Vector2 mid = (a + b) * 0.5f, dir = (b - a).normalized;
            var glow = new GameObject("void scar glow");
            fieldObjs.Add(glow);
            glow.transform.position = mid;
            glow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var gs = glow.AddComponent<SpriteRenderer>();
            gs.sprite = Resources.Load<Sprite>("VFX/vc_void_glow");
            if (gs.sprite == null) gs.sprite = Disc();
            FxLighting.MakeUnlit(gs);
            gs.sortingLayerID = layer; gs.sortingOrder = -16;
            float gk = (len + 0.6f) / Mathf.Max(0.01f, gs.sprite.bounds.size.x);
            glow.transform.localScale = new Vector3(gk, (gap * 2f + 0.9f) / Mathf.Max(0.01f, gs.sprite.bounds.size.y), 1f);
            var light = BossJuice.Light(mid, VoidCol, len * 0.6f, 0f, 0f);
            const float Flare = 0.9f, Life = 4f;
            float t0 = Time.time, nextHurt = 0f, nextMote = 0f;
            bool flared = false;
            while (!aborted)
            {
                float now = Time.time, t = now - t0;
                if (t >= Life + 0.45f) break;
                float close = Mathf.Clamp01((t - Life) / 0.45f);                         // closing: flattens out
                float pre = t < Flare ? Mathf.Clamp01(t / Flare) : 0f;
                float pulse = t < Flare ? 0.5f + 0.5f * Mathf.Sin(t * (10f + 30f * pre)) : 0.5f + 0.5f * Mathf.Sin(t * 4f);
                float glowA = (t < Flare ? 0.35f + 0.5f * pre * pulse : flared && t < Flare + 0.3f ? 1f : 0.45f + 0.15f * pulse) * (1f - close);
                gs.color = new Color(1f, 1f, 1f, glowA);
                if (light != null) light.intensity = (0.5f + 1.2f * glowA) * (1f - close);
                foreach (var s in scars)
                    if (s != null) { var ls = s.transform.localScale; s.transform.localScale = new Vector3(ls.x, 1.3f * (1f - close) + 0.001f, 1f); }
                // void particles rising along the scar
                if (now >= nextMote && close <= 0f)
                {
                    nextMote = now + (t < Flare ? 0.03f : 0.06f);
                    VoidMote(Vector2.Lerp(a, b, Random.value) + n * (Random.Range(-1, 2) * gap + Random.Range(-0.06f, 0.06f)), 1f);
                }
                // aura BURST
                if (!flared && t >= Flare)
                {
                    flared = true;
                    Sfx.Play("teleport_dark", mid, 0.8f);
                    BossJuice.Shake(1);
                    BossJuice.Flash(mid, VoidCol, len * 0.5f, 2f, 0.3f);
                    for (int k = 0; k < 3; k++)
                        for (int j = 0; j < 3; j++)
                            SpriteVfx.Spawn("abs_void_burst", Vector2.Lerp(a, b, (j + 0.5f) / 3f) + n * (k - 1) * gap, 0.95f, layer, order + 2, (j + k) % 2 == 0, 20f, false);
                    if (ScarDist(Feet, a, b, n, gap) < 0.55f && Hurt(10f, Feet - n * Mathf.Sign(Vector2.Dot(Feet - mid, n) + 0.0001f), 1f))
                        DrillBoard.Say("VOID-TOUCHED", VoidCol);
                }
                // standing on the scar = HP drains
                if (close <= 0f && now >= nextHurt && ScarDist(Feet, a, b, n, gap) < 0.24f && Hurt(4f, Feet + Vector2.up * 0.1f, 0f))
                {
                    nextHurt = now + 0.5f;
                    var st = RenStatus;
                    if (st != null) st.Slow(0.75f, 0.6f, "The void gnaws at you!");
                }
                yield return null;
            }
            foreach (var s in scars) if (s != null) DestroyObj(s.gameObject);
            DestroyObj(glow);
            if (light != null) Object.Destroy(light.gameObject);
        }

        private static float ScarDist(Vector2 p, Vector2 a, Vector2 b, Vector2 n, float gap)
        {
            Vector2 q = p + Vector2.up * 0.1f;
            float d = float.MaxValue;
            for (int k = -1; k <= 1; k++) d = Mathf.Min(d, SegDist(q, a + n * k * gap, b + n * k * gap));
            return d;
        }

        /// <summary>Void particle: small violet pixel rising, swaying slightly, fading.</summary>
        private void VoidMote(Vector2 at, float speedMul)
        {
            var g = new GameObject("void mote");
            fieldObjs.Add(g);
            g.transform.position = at;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = MarginArt.Pixel(); FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order + 3;
            float sz = (Random.value < 0.3f ? 0.05f : 0.03f) * 100f;
            g.transform.localScale = new Vector3(sz, sz, 1f);
            Color c = Random.value < 0.25f ? new Color(0.92f, 0.85f, 1f) : Random.value < 0.6f ? VoidCol : new Color(0.25f, 0.08f, 0.45f);
            Ctx.Run(MoteAnim(g, sr, c, Random.Range(0.35f, 0.7f) * speedMul, Random.Range(0.6f, 1.1f), Random.Range(0f, 6f)));
        }

        private IEnumerator MoteAnim(GameObject g, SpriteRenderer sr, Color c, float rise, float life, float ph)
        {
            Vector2 p0 = g.transform.position;
            for (float t = 0f; t < life && g != null; t += Time.deltaTime)
            {
                float u = t / life;
                g.transform.position = p0 + new Vector2(Mathf.Sin(t * 5f + ph) * 0.06f, rise * t);
                sr.color = new Color(c.r, c.g, c.b, Mathf.Sin(u * Mathf.PI) * 0.95f);
                yield return null;
            }
            if (g != null) DestroyObj(g);
        }

        // ------------------------------------------------------------------ TRIPLE SLAM
        private IEnumerator TripleSlam(float pressure)
        {
            int side = FreeSide(Feet.x < chest.x ? -1 : 1);
            if (side == 0) yield break;
            DrillBoard.Say(phase == 3 ? "THUNDER DRUM" : "TRIPLE SLAM", phase == 3 ? StormCol : WarnViolet);
            Vector2 target = ClampFloor(Feet);
            for (int i = 0; i < 3 && !aborted; i++)
            {
                float rise = Mathf.Lerp(0.62f, 0.45f, pressure * 0.5f);
                var shadow = Shadow(side, target, "hand_fist");
                for (float t = 0f; t < rise && !aborted; t += Time.deltaTime)
                {
                    bool locked = t > rise * 0.65f;
                    if (!locked) target = Vector2.Lerp(target, ClampFloor(Feet + PlayerVel * 0.2f), 1f - Mathf.Exp(-6f * Time.deltaTime));
                    rig.SetHand(side, target + Vector2.up * 1.6f, 14f, "hand_fist");
                    GrowShadow(shadow, side, target, t / rise, locked, t);
                    yield return null;
                }
                DestroyObj(shadow);
                if (aborted) yield break;
                rig.SetHand(side, target, 55f, "hand_fist");
                yield return Wait(0.07f);
                SlamFx(target, i == 2 ? 3 : 1);
                if (InHandPrint(target, side, "hand_fist")) HeavyHit(14f, target);
                // QUAKE WAVE with fixed reach (1.4 / 1.4 / 2.2 u), slow enough to read — only this move has a wave
                StartR(QuakeWave(target, i == 2 ? Float("quake_final", 2.2f) : Float("quake_range", 1.4f), i == 2 ? 10f : 8f, i == 2 ? 2 : 1));
                if (i < 2) yield return Wait(0.15f);
            }
            if (aborted) yield break;
            if (noLie) { yield return Wait(0.3f); rig.RestHand(side, 6f); yield break; }
            yield return LieDown(side, target, Float("triple_down", 2.5f), "hand_fist");
        }

        // ------------------------------------------------------------------ GRAB (right hand)
        private IEnumerator Grab(float pressure)
        {
            const int side = 1;
            if (!HandFree(side)) yield break;
            DrillBoard.Say("GRAB", WarnViolet);
            Vector2 target = ClampFloor(Feet);
            var shadow = Shadow(side, target, "hand_claw");
            float track = Mathf.Lerp(0.85f, 0.6f, pressure * 0.5f);
            for (float t = 0f; t < track + 0.35f && !aborted; t += Time.deltaTime)
            {
                if (t < track) target = Vector2.Lerp(target, ClampFloor(Feet + PlayerVel * 0.2f), 1f - Mathf.Exp(-6f * Time.deltaTime));
                rig.SetHand(side, target + Vector2.up * 1.6f, 10f, "hand_claw");
                GrowShadow(shadow, side, target, Mathf.Clamp01(t / (track + 0.35f)), t >= track, t);
                yield return null;
            }
            DestroyObj(shadow);
            if (aborted) yield break;
            rig.SetHand(side, target, 50f, "hand_claw");
            yield return Wait(0.1f);
            var hp0 = Ctx.PlayerHealth;
            if ((Feet - target).magnitude > 0.62f || hp0 == null || hp0.IsDead || hp0.Invulnerable)
            {
                SlamFx(target, 1);
                yield return Wait(0.5f);
                rig.RestHand(side, 5f);
                yield break;
            }
            grabbing = true; grabStrikes = 0;
            HookStruck();
            rig.SetHand(side, target, 60f, "hand_fist");
            Sfx.Play("reg_grip", target, 1f);
            BossJuice.Shake(1);
            DrillBoard.Command = "STRIKE TO BREAK FREE!";
            DrillBoard.CommandTime = Time.time;
            float nextSqueeze = Time.time + 0.5f;
            int need = pressure >= 1.2f ? 5 : 4;
            for (float t = 0f; t < 3.2f && !aborted && grabStrikes < need; t += Time.deltaTime)
            {
                if (Time.time >= nextSqueeze) { nextSqueeze = Time.time + 0.55f; Hurt(4f, target + Vector2.up, 0f); BossJuice.Shake(0); }
                rig.SetHand(side, target + new Vector2(Mathf.Sin(t * 25f) * 0.02f, 0.25f + Mathf.Sin(t * 3f) * 0.05f), 30f, "hand_fist");
                yield return null;
            }
            bool broke = grabStrikes >= need;
            ReleaseGrab(true);
            if (aborted) yield break;
            if (broke) { Sfx.Play("glass_break", target, 0.7f); DrillBoard.Say("BROKE FREE", RelicBlue); }
            else Hurt(8f, target + Vector2.up, 1.8f);                                    // throw out
            yield return Wait(0.5f);
            rig.RestHand(side, 5f);
        }

        private void HoldRen()
        {
            if (Ctx.Player == null) return;
            Vector2 h = rig.HandWorld(1) + Vector2.down * 0.15f;
            var rb = Ctx.Player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = h; rb.linearVelocity = Vector2.zero; }
            Ctx.Player.position = new Vector3(h.x, h.y, Ctx.Player.position.z);
        }

        private void ReleaseGrab(bool keep)
        {
            if (!grabbing) return;
            grabbing = false;
            if (DrillBoard.Command == "STRIKE TO BREAK FREE!") DrillBoard.Command = "";
            if (Ctx.Player != null)
            {
                Vector2 p = ClampFloor(Feet + Vector2.down * 0.5f);
                var rb = Ctx.Player.GetComponent<Rigidbody2D>();
                if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
                Ctx.Player.position = new Vector3(p.x, p.y, Ctx.Player.position.z);
            }
        }
    }
}
