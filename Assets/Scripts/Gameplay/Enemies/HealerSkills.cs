using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Black Lantern Monk (Map 3): never attacks, stays behind its allies and heals the most wounded one nearby.
    /// The Lychgate fight teaches the player to kill the Monk first. Backs away when the player gets close.
    /// Animator: Idle, Move, Heal (optional).
    /// </summary>
    public class HealerSkills : MonoBehaviour, IEnemySkills
    {
        public float healRange = 3.2f;          // Unity units
        public float healPercent = 0.25f;       // % of the ally's max HP
        public float healCooldown = 3.5f;
        public float castTime = 0.8f;
        public float fleeDistance = 1.3f;
        public Color healFlash = new Color(0.55f, 1f, 0.6f, 1f);

        private float nextHeal;

        public IEnumerator Act(EnemyAI ai)
        {
            if (ai.Player == null) yield break;
            Vector2 me = transform.position;
            Vector2 toPlayer = (Vector2)ai.Player.position - me;

            if (Time.time >= nextHeal)
            {
                EnemyAI target = null;
                float worst = 0.95f;
                foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                {
                    if (e == null || e == ai || e.Health == null || e.Health.IsDead) continue;
                    if (Vector2.Distance(me, e.transform.position) > healRange) continue;
                    if (e.Health.Percent < worst) { worst = e.Health.Percent; target = e; }
                }
                if (target != null)
                {
                    nextHeal = Time.time + healCooldown;
                    ai.Desired = Vector2.zero;
                    ai.FaceX(target.transform.position.x - transform.position.x);
                    ai.PlayState(ai.HasAnimState("Heal") ? "Heal" : "Idle");
                    if (ai.Vfx != null) ai.Vfx.Cast();
                    yield return ai.Flash(castTime, healFlash);
                    if (ai.Health.IsDead) yield break;
                    if (target != null && !target.Health.IsDead)
                    {
                        target.Health.Heal(target.Health.Max * healPercent);
                        if (ai.Vfx != null) ai.Vfx.HealOn(target.transform);
                        StartCoroutine(Glow(target.Body));
                    }
                    ai.PlayState("Idle");
                    yield return ai.WaitFor(0.3f);
                    yield break;
                }
            }

            // keep away from the player, stay near the allies
            if (toPlayer.magnitude < fleeDistance)
            {
                ai.Desired = -toPlayer.normalized * ai.moveSpeed;
                ai.FaceX(toPlayer.x);
                ai.PlayState("Move", false);
            }
            else
            {
                ai.Desired = Vector2.zero;
                ai.FaceX(toPlayer.x);
                ai.PlayState("Idle", false);
            }
            yield return null;
        }

        private IEnumerator Glow(SpriteRenderer sr)
        {
            if (sr == null) yield break;
            Color c0 = sr.color;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (sr == null) yield break;
                sr.color = Color.Lerp(healFlash, c0, t / 0.5f);
                yield return null;
            }
            if (sr != null) sr.color = c0;
        }
    }
}
