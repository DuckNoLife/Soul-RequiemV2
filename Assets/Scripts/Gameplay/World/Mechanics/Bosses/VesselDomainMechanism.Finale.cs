using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ENDING of The Crystal Vessel.
    ///   LAST GRASP — Phase 5 HP empty: the Vessel roars, throws 7 hand attacks with NO rest (Grab / Slam / Clap / Triple Slam, hands do not stay down),
    ///                then both hands COLLAPSE onto the arena for 8 s (`grasp_down`) with no armour cap -> strike until the bar is empty (`grasp_hits` 14 hits).
    ///                Too slow -> the hands lift, 4 more attacks, collapse again.
    ///                Dying in LAST GRASP: replay from LAST GRASP (flag `vessel_grasp`), not the whole of Phase 5.
    ///   FINAL GRIP (.Ending.cs) -> VESSEL FALLS — each hand goes dark, the whole body greys out and slumps; the map returns to a neutral tint;
    ///                all 3 relics burst out. Flags vessel_p5 + vessel_slain + absalom_p3.
    ///   ABSALOM FLEES — appears beside the portal spot, 2 lines, opens a SPACE PORTAL (dom_portal_open -> dom_portal_loop, Volcano pack)
    ///                and rushes in. The portal STAYS OPEN — control returns with the hint "Absalom fled through the portal. Follow him."
    ///   REN ENTERS THE PORTAL — pulled in, shrinks, spins, flashes -> flag `vessel_fled` -> end-of-chapter screen (<see cref="ToBeContinuedScreen"/>).
    ///   Re-entering the map after the Vessel fell but before entering the portal -> the portal is already open at the same spot.
    /// Called by: PhaseLoop (.Phases.cs) after Phase 5; TickPortal every frame (from TickFight).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        public const string GraspFlag = "vessel_grasp";
        private bool lastGrasp, noLie, portalOpen, portalPending, entering;
        private SpriteVfx portalFx;
        private Light2D portalLight;

        private Vector2 GatePos => new Vector2(Floor.center.x, Floor.yMax - Float("portal_dy", 1.1f));

        // ------------------------------------------------------------------ LAST GRASP
        private IEnumerator Finale()
        {
            Ctx.Flags.Set(GraspFlag);
            actState = ActState.Playing;
            phase = 4; toneP = 4;
            lastGrasp = true;
            vhp = 1f;
            HookStruck();
            BeginBoard(4);
            DrillBoard.Begin(5, "LAST GRASP", RelicsFreed());
            DrillBoard.Stations = 5; DrillBoard.StationLabel = "PHASE"; DrillBoard.PipLabel = ""; DrillBoard.ShoutName = "THE VESSEL";
            DrillBoard.MoraleMax = 0;
            DrillBoard.Objective = "Survive its last grasp. When both hands fall, strike them until it breaks.";
            DrillBoard.Finish("LAST GRASP", UmbraCol);
            slumpTarget = 0f;
            Roar(1.6f);
            BossJuice.Shake(3);
            DrillBoard.Yell("Mine... they are MINE!", 2f);
            yield return Wait(1.4f);
            int round = 0;
            while (!aborted && vhp > 0f)
            {
                int n = round == 0 ? Mathf.RoundToInt(Float("grasp_moves", 7f)) : 4, last = -1;
                noLie = true;
                for (int i = 0; i < n && !aborted && vhp > 0f; i++)
                {
                    int k;
                    do k = Random.Range(0, 4); while (k == last);
                    last = k;
                    if (k == 0) yield return Grab(1.8f);
                    else if (k == 1) yield return Slam(Feet.x < chest.x ? -1 : 1, 1.8f);
                    else if (k == 2) yield return Clap(1.8f);
                    else yield return TripleSlam(1.8f);
                    yield return Wait(0.3f);
                }
                noLie = false;
                if (aborted || vhp <= 0f) break;
                yield return Collapse();
                if (aborted || vhp <= 0f) break;
                round++;
                DrillBoard.Yell("Not... yet...", 1.4f);
                yield return Wait(0.6f);
            }
            if (aborted) yield break;
            yield return FinalGrip();                                                  // hand grabs Ren -> Ren breaks the hand -> the Vessel cracks open
            yield return VesselDies();
            yield return AbsalomFlees();
        }

        /// <summary>Both hands collapse onto the middle of the arena and lie for `grasp_down` s — strike until the bar is empty.</summary>
        private IEnumerator Collapse()
        {
            DrillBoard.Say("IT COLLAPSES", RelicBlue);
            DrillBoard.Yell("...", 1f);
            Vector2 l = ClampFloor(new Vector2(Floor.center.x - 1.5f, Floor.center.y + 0.5f), 0.6f), r = ClampFloor(new Vector2(Floor.center.x + 1.5f, Floor.center.y + 0.5f), 0.6f);
            for (float t = 0f; t < 0.55f && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(-1, l + Vector2.up * 1.4f, 10f, "hand_open");
                rig.SetHand(1, r + Vector2.up * 1.4f, 10f, "hand_open");
                yield return null;
            }
            if (aborted) yield break;
            rig.SetHand(-1, l, 40f, "hand_open"); rig.SetHand(1, r, 40f, "hand_open");
            yield return Wait(0.1f);
            SlamFx(l, 3); SlamFx(r, 3);
            if (InHandPrint(l, -1, "hand_open")) HeavyHit(10f, l);
            else if (InHandPrint(r, 1, "hand_open")) HeavyHit(10f, r);
            slumpTarget = 0.5f;
            float secs = Float("grasp_down", 8f);
            StartR(LieDown(-1, l, secs, "hand_open"));
            StartR(LieDown(1, r, secs, "hand_open"));
            yield return null;
            while (!aborted && AnyDown && vhp > 0f) yield return null;
            slumpTarget = 0f;
        }

        // ------------------------------------------------------------------ VESSEL FALLS
        private IEnumerator VesselDies()
        {
            actState = ActState.Rest;
            lastGrasp = false;
            ClearField();
            DrillBoard.TrackFrac = 0f; DrillBoard.GoalFrac = 0f; DrillBoard.Goal = "VESSEL 0%";
            busy = true;
            BeginScene();
            MusicEnd(3.5f);                                                             // Vessel falls -> music fades out
            Sfx.Play("bell_toll", chest, 1f);
            Sfx.Play("quake_rumble", chest, 1f);
            BossJuice.Shake(3);
            BossJuice.Flash(rig.PointWorld(Vector2.zero), Color.white, 7f, 3f, 0.8f);
            jawHold = 1f;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                rig.Tint = Color.Lerp(Color.white, new Color(1.5f, 1.4f, 1.8f), 0.5f + 0.5f * Mathf.Sin(t * 30f));
                yield return null;
            }
            rig.Tint = Color.white;
            rig.SetArmAwake(-1, false); Sfx.Play("glass_break", rig.HandWorld(-1), 0.8f);
            yield return new WaitForSeconds(0.6f);
            rig.SetArmAwake(1, false); Sfx.Play("glass_break", rig.HandWorld(1), 0.8f);
            yield return new WaitForSeconds(0.6f);
            jawHold = -1f;
            rig.SetAllAwake(false);
            slumpTarget = 1f;
            toneP = -1;                                                                 // map returns to a neutral tint
            Ctx.Flags.Set(PhaseFlag(4));
            Ctx.Flags.Set(SlainFlag);
            Ctx.Flags.Set(DoneFlag);
            DrillBoard.Finish("THE VESSEL FALLS", RelicBlue);
            yield return new WaitForSeconds(1.2f);
            // ALL 3 relics (Eye / Spine / Ears) burst out of the husk together and float around the island
            for (int a = 0; a < 3; a++) { Ctx.Run(RelicBreaksFree(a, false)); yield return new WaitForSeconds(0.35f); }
            yield return new WaitForSeconds(1.5f);
            DrillBoard.Finish("THE RELICS BREAK FREE", RelicBlue);
            yield return new WaitForSeconds(2f);
            DrillBoard.Active = false;
        }

        // ------------------------------------------------------------------ ABSALOM FLEES THROUGH THE PORTAL
        private IEnumerator AbsalomFlees()
        {
            Vector2 at = GatePos + new Vector2(1.5f, 0.45f);                            // appears beside the portal, inside the camera frame (at the Vessel's shoulder he was hidden off screen)
            if (actor != null) actor.Destroy();
            endCam = true;                                                              // CLOSE camera follows Absalom + portal (no pull-out)
            actor = new AbsalomActor(at, Ctx.Host.transform, Ctx.Player) { Alpha = 0f };
            actor.Airborne = true;
            actor.Lift = 0.35f;
            actor.Face(Feet - at);
            actor.Play("idle", 5f);
            Sfx.Play("teleport_dark", at, 0.9f);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime) { actor.Alpha = t / 0.9f; yield return null; }
            actor.Alpha = 1f;
            yield return Say("...Hollow again. As if I never wrote you.", 3f);
            yield return Say("Keep your relics, Nameless. My page is not finished.", 3.2f);

            Vector2 gate = GatePos, stand = gate + new Vector2(0.9f, 0.35f);
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                float u = t / 1.3f, e = u * u * (3f - 2f * u);
                actor.Pos = Vector2.Lerp(at, stand, e);
                actor.Face(stand - at);
                yield return null;
            }
            actor.Face(gate - stand);
            actor.Play("strike", 8f, false);
            yield return OpenPortal(gate, true);
            actor.Play("idle", 5f);
            actor.Face(Feet - actor.Pos);
            yield return Say("Follow, if you want the rest of the story.", 2.6f);
            // rushes into the portal
            Vector2 from = actor.Pos;
            Sfx.Play("teleport_dark", gate, 1f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float u = t / 0.6f;
                actor.Pos = Vector2.Lerp(from, gate, u * u);
                actor.Size = Mathf.Lerp(1f, 0.15f, u);
                actor.Alpha = 1f - u * 0.8f;
                yield return null;
            }
            BossJuice.Flash(gate, VesselViolet, 3f, 3f, 0.4f);
            BossJuice.Shake(1);
            if (actor != null) { actor.Destroy(); actor = null; }
            yield return new WaitForSeconds(0.6f);                                      // portal STAYS OPEN — the end screen only shows once Ren walks in (EnterPortal)
            endCam = false;
            busy = false;
            EndCutscene();
            actState = ActState.Idle;
            actMain = null;
            Hints.Show("Absalom fled through the portal. Follow him.", 5f);
        }

        private void BeginScene()
        {
            if (sceneOwned) return;
            sceneOwned = true;
            CutsceneState.Begin();
            GameInput.Blocked = true;
            Ctx.SetPlayerControl(false);
        }

        /// <summary>Open the space portal at `gate` (animate = 29-frame opening; false = already open when re-entering the map).</summary>
        private IEnumerator OpenPortal(Vector2 gate, bool animate)
        {
            var lg = new GameObject("vessel exit portal light");
            lg.transform.SetParent(Ctx.Host.transform, false);
            lg.transform.position = gate;
            portalLight = lg.AddComponent<Light2D>();
            portalLight.lightType = Light2D.LightType.Point;
            portalLight.color = VesselViolet; portalLight.pointLightOuterRadius = 2.4f; portalLight.pointLightInnerRadius = 0.2f;
            portalLight.intensity = animate ? 0f : 1.4f;
            if (animate)
            {
                Sfx.Play("teleport_dark", gate, 1f);
                Sfx.Play("quake_rumble", gate, 0.55f);
                CameraFollow.KickMain(Vector2.down, 0.05f, 0.5f);
                var open = SpriteVfx.Spawn("dom_portal_open", gate, 1f, layer, order - 1, false, 14f, false);
                for (float t = 0f; t < 29f / 14f; t += Time.deltaTime)
                {
                    portalLight.intensity = Mathf.Lerp(0f, 1.4f, t / (29f / 14f));
                    yield return null;
                }
                if (open != null) open.Stop();
            }
            portalFx = SpriteVfx.Spawn("dom_portal_loop", gate, 1f, layer, order - 1, false, 12f, false);
            if (portalFx != null) portalFx.Looping(1e6f);
            portalOpen = true;
        }

        // ------------------------------------------------------------------ every frame: portal open, Ren walks in
        private void TickPortal()
        {
            // portal waits for Ren; already went through and re-entered the map -> show the end screen directly
            if (portalPending && rig != null)
            {
                portalPending = false;
                if (Ctx.Flags.HasAll(FledFlag)) Ctx.Run(ComingSoonAgain());
                else Ctx.Run(OpenPortal(GatePos, false));
            }
            if (!portalOpen || entering || Ctx.Player == null || CutsceneState.Active || SceneTransition.Busy) return;
            if (portalLight != null) portalLight.intensity = 1.4f + 0.2f * Mathf.Sin(Time.time * 4f);
            if (Random.value < 0.35f)                                                   // violet particles swirl into the portal
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                AddMote(GatePos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f) * Random.Range(0.8f, 1.6f), Vector2.zero, new Color(0.85f, 0.55f, 1f, 0.9f), 1.2f, 0.03f, true, GatePos, 1.4f, 1.6f);
            }
            Vector2 d = Feet - GatePos;
            if (new Vector2(d.x, d.y / 0.7f).magnitude < Float("portal_radius", 0.55f)) Ctx.Run(EnterPortal());
        }

        private IEnumerator EnterPortal()
        {
            entering = true;
            BeginScene();
            var player = Ctx.Player;
            var rb = player.GetComponent<Rigidbody2D>();
            var psr = player.GetComponentInChildren<SpriteRenderer>();
            Vector3 sprScale = psr != null ? psr.transform.localScale : Vector3.one;
            Quaternion sprRot = psr != null ? psr.transform.localRotation : Quaternion.identity;
            Vector2 gate = GatePos, p = player.position;
            Sfx.Play("teleport_dark", gate, 1f);
            BossJuice.Shake(2);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float u = t / 0.7f;
                Vector2 q = Vector2.Lerp(p, gate, u * u);
                if (rb != null) { rb.position = q; rb.linearVelocity = Vector2.zero; }
                player.position = new Vector3(q.x, q.y, player.position.z);
                if (psr != null)
                {
                    psr.transform.localScale = sprScale * Mathf.Lerp(1f, 0.08f, u * u);
                    psr.transform.localRotation = sprRot * Quaternion.Euler(0f, 0f, u * 540f);
                }
                yield return null;
            }
            BossJuice.Flash(gate, Color.white, 4f, 4f, 0.5f);
            if (psr != null) psr.enabled = false;
            Ctx.Flags.Set(FledFlag);
            yield return new WaitForSeconds(0.4f);
            ToBeContinuedScreen.Show(() =>
            {
                // leaving the screen (to Title, or stay in the map if there is no Title): restore state
                if (psr != null) { psr.transform.localScale = sprScale; psr.transform.localRotation = sprRot; psr.enabled = true; }
                if (portalFx != null) portalFx.Stop();
                if (portalLight != null) Object.Destroy(portalLight.gameObject);
                portalOpen = false;
                Vector2 back = gate + Vector2.down * 1.2f;
                if (rb != null) { rb.position = back; rb.linearVelocity = Vector2.zero; }
                if (player != null) player.position = new Vector3(back.x, back.y, player.position.z);
                EndCutscene();
            });
        }
    }
}
