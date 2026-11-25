using System.Collections;
using System.Globalization;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// vessel_domain — THE CRYSTAL VESSEL, last phase of the Absalom fight, inside the domain THE UNWRITTEN (scene Map4n_TheUnwritten).
    /// The Vessel (<see cref="VesselRig"/>) rises half-body behind the north edge of the island, both hands resting on the floor; it breathes and its head /
    /// jaw / hands bob. First entry: a DORMANT husk (grey, slumped) -> awakening cutscene (.Intro.cs) -> flag `vessel_awake`.
    /// Fight = 5 ELEMENT PHASES (.Phases.cs): HOLLOW / CINDER / RIME / TEMPEST / UMBRA, then LAST GRASP and the ending (.Finale.cs, .Ending.cs).
    /// The hands only take damage while LYING on the floor after a Slam (.Physical.cs). Close camera follows Ren (below).
    /// Files: .Phases (phase loop, move picker, map tint, pylons), .Physical (Phase 1 + shared hand moves), .Fire (P2), .Ice + .Frost (P3),
    /// .Storm + .Gaze (P4 + eye / mouth moves), .Shadow (P5), .Light (watchtower searchlights), .Rework (element moves added later),
    /// .Hands (shared kit: floor, damage, cleanup), .Quake (hand-print hits, quake waves), .FxKit (beams, motes, rings), .Pose (head reach, looping VFX),
    /// .Music, .Intro, .Finale, .Ending. Helpers: VesselRig (body), MagmaField (lava), WatchTowerMechanism / VoidIsleMechanism (towers, pylons).
    /// Tiled properties (mechanics layer object): chest "x,y" (tile — sternum centre), scale (1.6), cam "x,y,w,h" (tiles — WIDE camera frame), cam_size (override
    ///   wide frame size), cam_view, cam_lift (units the close frame is raised above Ren, 0.9), cam_lunge_pad (0.45) — while the Vessel lunges down
    ///   (eye / mouth moves) the close frame shifts up to show its eyes (Ren stays in frame),
    ///   torso_order (-21: behind island tiles -20 -> torso rises BEHIND the island edge), arm_order (4: arms + hands IN FRONT of the island and above Ren).
    /// Test key F5: mark the domain as cleared (flags vessel_awake + absalom_p3).
    /// </summary>
    public sealed partial class VesselDomainMechanism : Mechanism
    {
        public const string AwakeFlag = "vessel_awake";
        public const string DoneFlag = "absalom_p3";

        private VesselRig rig;
        private Vector2 chest;
        private int layer, order;
        private bool started, busy, camOwned;
        private float slumpTarget;
        private Coroutine main;

        public VesselRig Rig => rig;

        public override void Init()
        {
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            layer = psr != null ? psr.sortingLayerID : 0;
            order = psr != null ? psr.sortingOrder : 2;
            chest = Ctx.TilePoint(Str("chest", ""), Rect.center + Vector2.up * 2f);
            bool awake = Ctx.Flags.HasAll(AwakeFlag) && !Ctx.Flags.HasAll(SlainFlag);   // slain -> grey husk
            rig = new VesselRig(chest, Float("scale", 1.6f), Ctx.Host.transform, layer, Int("torso_order", -21), Int("arm_order", 4), awake);
            rig.SetElement(StartElement());                                            // phase colour set (not awake yet = pale grey)
            rig.Slump = slumpTarget = awake ? 0f : 1f;
            if (Ctx.Flags.HasAll(SlainFlag)) rig.Slump = slumpTarget = 1f;
            SpawnFreedOrbs();                                                          // freed relics floating beside the island
            rig.LungeOrder = Int("lunge_order", -12);                                  // lowered head draws in front of the floor
            rig.HeadLungePx = Float("head_lunge_px", 170f);                            // how many source px the head lowers
            portalPending = Ctx.Flags.HasAll(SlainFlag);                                // Vessel slain but Ren has not entered the portal -> the portal is already open
        }

        public override void Animate()
        {
            if (rig == null) return;
            float dt = Time.deltaTime;
            rig.Slump = Mathf.MoveTowards(rig.Slump, slumpTarget, dt * 0.9f);
            rig.Tick(dt);
            TickActor();
            if (SceneTransition.Busy) return;
            DomainCam(true);
            if (!started && Ctx.Player != null && Ctx.Director != null && !Ctx.Director.Respawning)
            {
                started = true;
                if (!Ctx.Flags.HasAll(AwakeFlag)) main = Ctx.Run(Awakening());
            }
            TickFight(dt);                                                              // phase fight + hand moves (.Phases.cs)
        }

        // ------------------------------------------------------------------ camera
        // CLOSE camera — frame `cam_view` tiles (default 30) FOLLOWS REN, raised a little
        // (`cam_lift` units) to show the Vessel's hands + chest, clamped inside `cam`. Big moments (awakening, phase start, pylon break…) call CamWide -> pull back
        // to the whole `cam` frame, then return to close on their own.
        private float wideUntil;
        private float camNear; private Vector2 camGoal; private bool camWideNow;
        private bool endCam;                                                           // ending — close frame follows Absalom + portal; also tells CamSettle which frame is targeted
        private Rect camRect;
        private bool camRectOk;

        /// <summary>Pull the camera back to the whole `cam` frame for `seconds`.</summary>
        private void CamWide(float seconds) => wideUntil = Mathf.Max(wideUntil, Time.time + seconds);

        private void DomainCam(bool on)
        {
            var cf = Ctx.Camera != null ? Ctx.Camera : (Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null);
            if (cf == null) return;
            if (!on) { if (camOwned) { camOwned = false; cf.Release(); } return; }
            if (!camRectOk) { camRect = ParseCam(); camRectOk = true; }
            var cam = Camera.main;
            float aspect = cam != null ? Mathf.Max(0.5f, cam.aspect) : 16f / 9f;
            float wideSize = Float("cam_size", 0f);
            if (wideSize <= 0f) wideSize = Mathf.Max(camRect.height * 0.5f, camRect.width * 0.5f / aspect);
            float tile = Ctx.Director != null ? Mathf.Abs(Ctx.Director.TileToWorld(1f, 0f).x - Ctx.Director.TileToWorld(0f, 0f).x) : 0.32f;
            float near = Mathf.Min(wideSize, Float("cam_view", 30f) * tile * 0.5f / aspect);
            if (endCam)
            {
                float es = Mathf.Min(near, Float("end_cam_size", 2.2f));
                Vector2 ea = actor != null ? actor.Pos : GatePos;
                Vector2 eg = Vector2.Lerp(ea, GatePos, 0.4f) + Vector2.up * 0.2f;
                float ehw = es * aspect;
                eg = new Vector2(ClampAxis(eg.x, camRect.xMin + ehw, camRect.xMax - ehw), ClampAxis(eg.y, camRect.yMin + es, camRect.yMax - es));
                camNear = es; camGoal = eg; camWideNow = true;
                cf.Focus(eg, es);
                camOwned = true;
                return;
            }
            bool wide = Time.time < wideUntil || busy || CutsceneState.Active || !Ctx.Flags.HasAll(AwakeFlag);
            float size = wide ? wideSize : near;
            Vector2 goal;
            if (wide) goal = camRect.center;
            else
            {
                // Close frame follows Ren. For every eye / mouth move
                // the Vessel LOWERS ITS HEAD onto the arena — while lowered the frame shifts UP (no zoom out) to show mouth / eyes, but Ren stays >= 0.7 u from the bottom edge.
                Vector2 p = Feet + Vector2.up * Float("cam_lift", 0.9f);
                float hw = size * aspect, hh = size;
                float gy = p.y;
                if (rig != null && rig.Lunge > 0.25f)
                {
                    float want = rig.EyeWorld(1).y + Float("cam_lunge_pad", 0.45f);              // level with the Vessel's eyes (head lowered)
                    float lift = Mathf.Min(want - (gy + hh), (Feet.y - 0.7f) - (gy - hh));
                    if (lift > 0f) gy += lift * Mathf.Clamp01((rig.Lunge - 0.25f) / 0.45f);
                }
                goal = new Vector2(ClampAxis(p.x, camRect.xMin + hw, camRect.xMax - hw), ClampAxis(gy, camRect.yMin + hh, camRect.yMax - hh));
            }
            camNear = near; camGoal = goal; camWideNow = wide;
            cf.Focus(goal, size);                                                      // called every frame: CameraFollow glides to the target + frame size
            camOwned = true;
        }

        /// <summary>End the wide frame, then WAIT for the camera to fully return to the close frame
        /// (size + position, max 3 s) + rest `cam_settle_beat` (0.5 s) — only then does the Vessel use its first move.</summary>
        private IEnumerator CamSettle()
        {
            wideUntil = 0f;
            yield return null;
            var cam = Camera.main;
            for (float t = 0f; t < 3f && !aborted; t += Time.deltaTime)
            {
                if (cam == null || camWideNow) { yield return null; continue; }
                float ds = Mathf.Abs(cam.orthographicSize - camNear);
                float dp = Vector2.Distance(cam.transform.position, camGoal);
                if (t >= 0.4f && ds < Mathf.Max(0.04f, camNear * 0.02f) && dp < 0.12f) break;
                yield return null;
            }
            yield return Wait(Float("cam_settle_beat", 0.5f));
            HoldLights();
        }

        private static float ClampAxis(float v, float lo, float hi) => lo > hi ? (lo + hi) * 0.5f : Mathf.Clamp(v, lo, hi);

        /// <summary>Frame `cam` (Tiled tiles "x,y,w,h") -> world; missing -> the object box.</summary>
        private Rect ParseCam()
        {
            var v = Str("cam", "").Split(',');
            if (v.Length == 4 &&
                float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float w) &&
                float.TryParse(v[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
            {
                Vector3 a = Ctx.Director.TileToWorld(x, y), b = Ctx.Director.TileToWorld(x + w, y + h);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
            return Rect;
        }

        // ------------------------------------------------------------------ test keys
        /// <summary>F5: wake the Vessel now + mark the domain cleared (opens the way back to the Hall).</summary>
        internal override void DebugSolve()
        {
            AbortAct();
            MusicEnd(1f);
            restartAt = float.MaxValue;
            if (main != null) { Ctx.Host.StopCoroutine(main); main = null; }
            EndCutscene();
            rig.SetAllAwake(true);
            slumpTarget = 0f;
            Ctx.Flags.Set(AwakeFlag);
            Ctx.Flags.Set(DoneFlag);
            Ctx.Flags.Set(NamedFlag);
            Ctx.Flags.Set(SlainFlag);
            Hints.Show("[TEST] The Unwritten: Vessel skipped. The way back (south edge) is open.", 3f);
        }
    }
}
