using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§362): CHỈ DẪN cho Burlap's Proving Grounds — người chơi lúc nào cũng biết phải làm gì, ở đâu:
    ///   • (§368: trạm 3 nay là FINAL EXAM — chỉ dẫn ở ExamGuide / ArtilleryGuide)
    ///   • Ngoài trạm, đứng trong sân chưa qua: mũi tên + bảng nhiệm vụ chỉ tới TÙ VÀ ("Sound the horn - Station n of 3").
    ///   • MŨI TÊN CHỈ ĐƯỜNG nảy trên đầu đích hiện tại (bệ đèn / giá đèn / cần gạt đúng máy).
    ///   • DÒNG NHIỆM VỤ + THANH THEO DÕI trên bảng HUD (DrillBoard.Objective / Track*): Lantern Run = thanh bệ -> giá, đèn lồng chạy theo quãng đường đã mang.
    ///   • BẢNG NHIỆM VỤ + mũi tên đích của HUD (QuestGuide) chỉ theo mục tiêu của trạm thay cho nhiệm vụ chính trong lúc chơi.
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private SpriteRenderer guideArrow;
        private Mechanism horn;
        private bool hornLooked;

        /// <summary>Tù và của sân này (drill_horn có `yard` = tên sân).</summary>
        private Mechanism Horn
        {
            get
            {
                if (hornLooked) return horn;
                hornLooked = true;
                foreach (var m in Ctx.All) if (m is DrillHornMechanism && m.Rect.Overlaps(A)) { horn = m; break; }
                return horn;
            }
        }
        private const string QuestTitle = "Burlap's Proving Grounds";

        // ------------------------------------------------------------------ mũi tên chỉ đường + dòng nhiệm vụ
        private void Guide(Vector2? target)
        {
            if (target == null || inScene) { if (guideArrow != null) guideArrow.enabled = false; return; }
            if (guideArrow == null)
            {
                guideArrow = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Guide Arrow", target.Value, Ctx.Player, 12);
                FxLighting.MakeUnlit(guideArrow);
                var f = SpriteVfx.Load("drill/fx_arrow");
                if (f.Length > 0) guideArrow.sprite = f[f.Length - 1];
                guideArrow.transform.rotation = Quaternion.Euler(0f, 0f, -90f);           // chĩa xuống
                guideArrow.color = new Color(0.6f, 1f, 0.65f);
            }
            guideArrow.enabled = true;
            float bob = Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.08f;
            guideArrow.transform.position = target.Value + Vector2.up * (0.62f + bob);
            float s = 1.3f + 0.1f * Mathf.Sin(Time.time * 8f);
            guideArrow.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Mỗi khung khi đang chơi: dòng nhiệm vụ, thanh theo dõi, mũi tên, bảng nhiệm vụ HUD.</summary>
        private void UpdateGuide()
        {
            if (phase != Phase.Running || inScene)
            {
                // §365: đang chờ trong sân (chưa qua) -> chỉ tới tù và: "Sound the horn - Station n"
                var horn = Horn;
                if (!inScene && phase == Phase.Idle && !passed && horn != null && A.Contains(PlayerFeet) && Ctx.Flags.HasAll(MusterFlag))
                {
                    Vector2 hp = new Vector2(horn.Rect.center.x, horn.Rect.yMin + 0.25f);
                    Guide(hp);
                    QuestGuide.Set(QuestTitle, "Sound the horn - Station " + (checkpoint + 1) + " of 3" + (checkpoint > 0 && lives > 0 ? " (morale " + lives + "/" + MoraleMax + ")" : ""), hp, checkpoint, 3);
                    return;
                }
                Guide(null);
                if (QuestGuide.Active) QuestGuide.Clear();
                return;
            }
            DrillBoard.TrackFrac = -1f;
            switch (station)
            {
                case 0:
                    DrillBoard.Objective = "Dodge the spinning squads. Red arrows = incoming, small green arrows = the gap.";
                    Guide(null);
                    QuestGuide.Set(QuestTitle + " - Blade Drill", "Survive the drill", null);
                    break;
                case 1: LanternGuide(); break;
                default: ExamGuide(); break;
            }
        }

        private void LanternGuide()
        {
            DrillBoard.TrackLeft = "VFX/props/drill_keyped";
            DrillBoard.TrackRight = "VFX/props/drill_rack";
            DrillBoard.TrackMarker = carrying || (lanternHome && lanternOn) ? "VFX/props/drill_lantern" : "VFX/props/drill_lantern_out";
            float span = Mathf.Max(0.5f, (lanternAt - rackAt).magnitude);
            Vector2 target;
            string label;
            if (carrying)
            {
                DrillBoard.Objective = "Carry the lantern to the rack. No rolling while you hold it - weave between the soldiers!";
                DrillBoard.TrackFrac = Mathf.Clamp01(1f - (PlayerFeet - rackAt).magnitude / span);
                target = rackAt + Vector2.up * 0.6f; label = "Hang the lantern on the rack";
            }
            else if (lanternHome && lanternOn)
            {
                DrillBoard.Objective = "Walk onto the pedestal to take the lantern (" + delivered + " of " + Lanterns3 + " hung).";
                DrillBoard.TrackFrac = 0f;
                target = lanternAt + Vector2.up * 0.3f; label = "Take the lantern";
            }
            else
            {
                DrillBoard.Objective = "Your light went out! It flew back to the pedestal - go relight it.";
                DrillBoard.TrackFrac = 0f;
                target = lanternAt + Vector2.up * 0.3f; label = "Relight the lantern";
            }
            Guide(target);
            QuestGuide.Set(QuestTitle + " - Lantern Run", label, target, delivered, Lanterns3);
        }
    }
}
