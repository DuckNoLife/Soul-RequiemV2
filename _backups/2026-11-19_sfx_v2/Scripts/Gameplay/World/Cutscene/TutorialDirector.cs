using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: các màn dừng hướng dẫn rải theo lượt chơi (Map 1, cùng kiểu ATTACK / DODGE của cảnh mở đầu):
    ///   DODGE   — khu thằn lằn (`dodgeZone`, Brookside Trail): lần đầu gai / đạn của một con ở gần đã bay ra: dừng, [Q], lăn xuyên qua.
    ///   HEAL    — 02/11: sau khi dọn xong khu nhện + rắn (`healAfterZone`, Hollow Caves) và đã yên (không quái quanh, không mất máu `calmSeconds` giây),
    ///             đang bị thương và còn bình: dừng, nhấp nháy [E], uống luôn khi bấm. Không bật giữa trận để khỏi khựng nhân vật.
    ///   (08/11 ĐÃ BỎ) MANTRA  — sau khi học chiêu ô 1 (cờ `learnFlag`, cuộn giấy trong rương Hob): lần đầu bị bao vây (≥3 quái trong 2 đv, hoặc 2 ở hai phía) và chiêu sẵn sàng:
    ///             dừng, nhấp nháy [F], thi triển luôn khi bấm.
    /// Không chen vào cảnh diễn / màn hướng dẫn khác / lúc menu mở. MapEncounterDirector tự gắn khi map có cảnh mở đầu.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public string learnFlag = "learned_ground_slam";
        public string dodgeZone = "z0";
        public string healAfterZone = "z2";
        public float calmSeconds = 1.2f, calmRadius = 7f;
        private float calmSince, lastHp = -1f;
        private const string HealDone = "tut_heal", DodgeDone = "tut_dodge";
        private EnemyAI dodgeFrom;

        // 02/11: dừng khi đạn / gai ĐÃ bay ra (thấy được), không phải lúc quái mới gồng
        private void OnEnable() => EnemyAI.ProjectilesFired += OnAttack;
        private void OnDisable() => EnemyAI.ProjectilesFired -= OnAttack;

        private void OnAttack(EnemyAI e)
        {
            var dir = MapEncounterDirector.Instance;
            if (dodgeFrom != null || dir == null || dir.Player == null || dir.Flags.HasAll(DodgeDone)) return;
            if (!dir.Flags.HasAll(dodgeZone + "_started") || dir.Flags.HasAll(dodgeZone + "_cleared")) return;
            if (((Vector2)(e.transform.position - dir.Player.position)).sqrMagnitude > 2.6f * 2.6f) return;
            dodgeFrom = e;                                          // vòng lặp dưới dừng game NGAY khung sau (trước khi gai bắn ra)
        }

        private IEnumerator Start()
        {
            yield return null;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Player == null) yield break;
            var player = dir.Player;
            var hp = player.GetComponent<Health>();
            var combat = player.GetComponent<PlayerCombat>();
            var loadout = player.GetComponent<PlayerLoadout>();
            if (hp == null || combat == null) yield break;

            while (true)
            {
                yield return null;
                if (hp.IsDead || TutorialPrompt.Active || CutsceneState.Active || GameInput.MenuOpen || GameInput.Blocked) continue;

                if (dodgeFrom != null && !dir.Flags.HasAll(DodgeDone))
                {
                    dir.Flags.Set(DodgeDone);
                    var from = dodgeFrom;
                    // lăn XUYÊN qua vòng gai (chéo về phía con thằn lằn): lăn ra xa thì gai bay cùng chiều, hết lúc bất tử vẫn trúng
                    Vector2 toward = from != null ? (Vector2)(from.transform.position - player.position) : Vector2.zero;
                    Vector2 rollDir = toward.sqrMagnitude > 0.0001f ? (Vector2)(Quaternion.Euler(0f, 0f, 40f) * toward) : Vector2.zero;
                    yield return TutorialPrompt.Run(combat, GameAction.Roll, "DODGE", GameInput.Label(GameAction.Roll),
                        "Spikes incoming! Roll through them - nothing can touch you mid-roll.", rollDir);
                    dodgeFrom = null;
                    continue;
                }

                // yên = không quái sống (trừ đang ngất / bỏ chạy) trong calmRadius và không mất máu từ calmSeconds giây
                if (lastHp >= 0f && hp.Current < lastHp - 0.01f) calmSince = Time.time;
                lastHp = hp.Current;
                bool healPending = !dir.Flags.HasAll(HealDone) && dir.Flags.HasAll(healAfterZone + "_cleared");
                if (healPending && EnemyNear(player.position, calmRadius)) calmSince = Time.time;
                bool calm = Time.time - calmSince >= calmSeconds;

                if (healPending && calm &&
                    combat.Potions > 0 && hp.Percent < 0.999f)
                {
                    dir.Flags.Set(HealDone);
                    yield return TutorialPrompt.Run(combat, GameAction.Heal, "HEAL", GameInput.Label(GameAction.Heal),
                        "The caves are quiet. Patch yourself up - each flask restores health, and resting at a campfire refills them.", Vector2.zero);
                    continue;
                }

                // 08/11 (người dùng): bỏ màn MANTRA [F] khi bị bao vây
            }
        }

        private static bool EnemyNear(Vector2 p, float r)
        {
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (e == null || e.Health == null || e.Health.IsDead || e.Held) continue;
                if (((Vector2)e.transform.position - p).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        /// <summary>02/11: bị bao vây = ≥ 3 quái trong 2 đv, hoặc 2 quái trong 2 đv ở hai phía (lệch nhau > 100°).</summary>
        private static bool Surrounded(Vector2 p)
        {
            var dirs = new System.Collections.Generic.List<Vector2>();
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (e == null || e.Health == null || e.Health.IsDead || e.Held) continue;
                Vector2 d = (Vector2)e.transform.position - p;
                if (d.sqrMagnitude < 2f * 2f && d.sqrMagnitude > 0.0001f) dirs.Add(d.normalized);
            }
            if (dirs.Count >= 3) return true;
            for (int i = 0; i < dirs.Count; i++)
                for (int j = i + 1; j < dirs.Count; j++)
                    if (Vector2.Angle(dirs[i], dirs[j]) > 100f) return true;
            return false;
        }
    }
}
