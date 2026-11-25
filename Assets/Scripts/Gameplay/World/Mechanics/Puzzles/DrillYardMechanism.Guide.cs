using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// GUIDANCE for Burlap's Proving Grounds (3 yards share one scene: each yard only clears the quest board it set itself) — the player always
    /// knows what to do and where:
    ///   • Outside a station, standing in an unfinished yard: arrow + quest board point to the HORN ("Sound the horn - Station n of 3").
    ///   • A GUIDE ARROW bobs above the current target (lantern pedestal / rack / the right lever).
    ///   • OBJECTIVE LINE + TRACK BAR on the HUD board (DrillBoard.Objective / Track*): Lantern Run = pedestal -> rack bar with a lantern icon moving with the carried distance.
    ///   • The HUD QUEST BOARD + target arrow (QuestGuide) follow the station's goal instead of the main quest while playing.
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private SpriteRenderer guideArrow;
        private Mechanism horn;
        private bool hornLooked;

        /// <summary>This yard's horn (drill_horn whose `yard` = this yard's name).</summary>
        private Mechanism Horn
        {
            get
            {
                if (hornLooked) return horn;
                hornLooked = true;
                foreach (var m in Ctx.All) if (m is DrillHornMechanism h && h.YardName == Name) { horn = m; break; }
                if (horn == null) foreach (var m in Ctx.All) if (m is DrillHornMechanism && m.Rect.Overlaps(A)) { horn = m; break; }
                return horn;
            }
        }
        private const string QuestTitle = "Burlap's Proving Grounds";

        // ------------------------------------------------------------------ guide arrow + objective line
        private void Guide(Vector2? target)
        {
            if (target == null || inScene) { if (guideArrow != null) guideArrow.enabled = false; return; }
            if (guideArrow == null)
            {
                guideArrow = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Guide Arrow", target.Value, Ctx.Player, 12);
                FxLighting.MakeUnlit(guideArrow);
                var f = SpriteVfx.Load("drill/fx_arrow");
                if (f.Length > 0) guideArrow.sprite = f[f.Length - 1];
                guideArrow.transform.rotation = Quaternion.Euler(0f, 0f, -90f);           // pointing down
                guideArrow.color = new Color(0.6f, 1f, 0.65f);
            }
            guideArrow.enabled = true;
            float bob = Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.08f;
            guideArrow.transform.position = target.Value + Vector2.up * (0.62f + bob);
            float s = 1.3f + 0.1f * Mathf.Sin(Time.time * 8f);
            guideArrow.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>HUD quest board set by this yard (3 yards share the scene — another yard must not clear the one in use).</summary>
        private bool questOwned;

        private void SetQuest(string title, string label, Vector2? target, int done = 0, int total = 0)
        {
            QuestGuide.Set(title, label, target, done, total);
            questOwned = true;
        }

        /// <summary>Every frame: objective line, track bar, guide arrow, HUD quest board.</summary>
        private void UpdateGuide(bool inside)
        {
            if (phase != Phase.Running || inScene)
            {
                // waiting in an unfinished yard -> point to the horn
                var horn = Horn;
                if (!inScene && phase == Phase.Idle && !passed && horn != null && inside && (station > 0 || Ctx.Flags.HasAll(MusterFlag)))
                {
                    Vector2 hp = new Vector2(horn.Rect.center.x, horn.Rect.yMin + 0.25f);
                    Guide(hp);
                    SetQuest(QuestTitle, "Sound the horn - Station " + (station + 1) + " of 3", hp, station, 3);
                    return;
                }
                // last station passed, key still on the pedestal -> point to the key
                if (!inScene && passed && station == 2 && inside && keyring != null && keyring.enabled)
                {
                    Guide(keyAt);
                    SetQuest(QuestTitle, "Take the sergeant's key", keyAt, 3, 3);
                    return;
                }
                Guide(null);
                if (questOwned) { QuestGuide.Clear(); questOwned = false; }
                return;
            }
            DrillBoard.TrackFrac = -1f;
            switch (station)
            {
                case 0:
                    DrillBoard.Objective = "Dodge the spinning squads. Red arrows = incoming, green = the gap.";
                    Guide(null);
                    SetQuest(QuestTitle + " - Blade Drill", "Survive the drill", null);
                    break;
                case 1: LanternGuide(); break;
                default: ExamGuide(); break;
            }
        }

        private void LanternGuide()
        {
            // HUD progress bar puts pedestal / rack on the same sides as in the yard (pedestal right, rack left -> the bar runs right to left)
            bool flip = rackAt.x < lanternAt.x;
            DrillBoard.TrackLeft = flip ? "VFX/props/drill_rack" : "VFX/props/drill_keyped";
            DrillBoard.TrackRight = flip ? "VFX/props/drill_keyped" : "VFX/props/drill_rack";
            DrillBoard.TrackMarker = carrying || (lanternHome && lanternOn) ? "VFX/props/drill_lantern" : "VFX/props/drill_lantern_out";
            float span = Mathf.Max(0.5f, (lanternAt - rackAt).magnitude);
            Vector2 target;
            string label;
            if (carrying)
            {
                DrillBoard.Objective = "Carry the lantern to the rack. No rolling - weave between the soldiers!";
                float f = Mathf.Clamp01(1f - (PlayerFeet - rackAt).magnitude / span);
                DrillBoard.TrackFrac = flip ? 1f - f : f;
                target = rackAt + Vector2.up * 0.6f; label = "Hang the lantern on the rack";
            }
            else if (lanternHome && lanternOn)
            {
                DrillBoard.Objective = "Walk onto the pedestal to take the lantern (" + delivered + " of " + Lanterns3 + " hung).";
                DrillBoard.TrackFrac = flip ? 1f : 0f;
                target = lanternAt + Vector2.up * 0.3f; label = "Take the lantern";
            }
            else
            {
                DrillBoard.Objective = "Your light went out! Go back to the pedestal.";
                DrillBoard.TrackFrac = flip ? 1f : 0f;
                target = lanternAt + Vector2.up * 0.3f; label = "Relight the lantern";
            }
            Guide(target);
            SetQuest(QuestTitle + " - Lantern Run", label, target, delivered, Lanterns3);
        }
    }
}
