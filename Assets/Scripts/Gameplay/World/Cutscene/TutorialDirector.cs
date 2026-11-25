using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Tutorial pause screens spread through the playthrough (Map 1, same style as the opening scene's ATTACK / DODGE):
    ///   DODGE   - lizard area (`dodgeZone`, Brookside Trail): the first time a nearby lizard's spikes / bullets are in the air: freeze, [Q], roll through them.
    ///   HEAL    - after clearing the spider + snake area (`healAfterZone`, Hollow Caves) and once things are calm (no enemies around, no damage for `calmSeconds` seconds),
    ///             while hurt and with flask charges: freeze, blink [E], drink on press. Never mid-fight, so the character does not stall.
    ///   The MANTRA [F] screen (when surrounded) was removed.
    /// Never interrupts a cutscene / another tutorial screen / an open menu. MapEncounterDirector adds it automatically when the map has an opening scene.
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

        // freeze once the bullets / spikes are ALREADY in the air (visible), not when the enemy starts winding up
        private void OnEnable() => EnemyAI.ProjectilesFired += OnAttack;
        private void OnDisable() => EnemyAI.ProjectilesFired -= OnAttack;

        private void OnAttack(EnemyAI e)
        {
            var dir = MapEncounterDirector.Instance;
            if (dodgeFrom != null || dir == null || dir.Player == null || dir.Flags.HasAll(DodgeDone)) return;
            if (!dir.Flags.HasAll(dodgeZone + "_started") || dir.Flags.HasAll(dodgeZone + "_cleared")) return;
            if (((Vector2)(e.transform.position - dir.Player.position)).sqrMagnitude > 2.6f * 2.6f) return;
            dodgeFrom = e;                                          // the loop below freezes the game on the VERY next frame (before the spikes fire)
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
                    // roll THROUGH the spike ring (diagonally toward the lizard): rolling away keeps the spikes flying alongside and they still hit when invulnerability ends
                    Vector2 toward = from != null ? (Vector2)(from.transform.position - player.position) : Vector2.zero;
                    Vector2 rollDir = toward.sqrMagnitude > 0.0001f ? (Vector2)(Quaternion.Euler(0f, 0f, 40f) * toward) : Vector2.zero;
                    yield return TutorialPrompt.Run(combat, GameAction.Roll, "DODGE", GameInput.Label(GameAction.Roll),
                        "Spikes incoming! Roll through them - nothing can touch you mid-roll.", rollDir);
                    dodgeFrom = null;
                    continue;
                }

                // calm = no living enemies (except stunned / fleeing ones) within calmRadius and no damage for calmSeconds seconds
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

                // the MANTRA [F] screen when surrounded is removed
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

        /// <summary>Surrounded = ≥ 3 enemies within 2 units, or 2 enemies within 2 units on opposite sides (more than 100° apart).</summary>
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
