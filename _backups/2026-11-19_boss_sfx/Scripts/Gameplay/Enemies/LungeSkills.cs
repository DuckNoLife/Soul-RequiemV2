using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Quái "vồ" (Map 2 v2): áp sát -> gồng (prepState) -> lao thẳng (lungeState) -> hồi (endState).
    ///   Skinned Hopper (cóc lột da): Prepare -> Jump -> Recover
    ///   Barrel Goblin: Prepare -> Dash (lăn thùng) -> Idle; đứng sát thì chém (Attack)
    ///   Mimic: nằm im như cái rương tới khi người chơi lại gần (aggroRange nhỏ), rồi Dash_Begin -> Dash_Full -> Dash_End
    /// </summary>
    public class LungeSkills : MonoBehaviour, IEnemySkills
    {
        public string prepState = "Prepare", lungeState = "Dash", endState = "Recover", meleeState = "Attack";
        [Tooltip("Khoảng cách (đơn vị Unity) bắt đầu vồ")]
        public float lungeRange = 1.3f;
        public float lungeSpeed = 3.2f;
        public float lungeTime = 0.45f;
        public float prepTime = 0.35f;
        public float recoverTime = 0.5f;
        public float lungeCooldown = 1.6f;
        public float lungeDamageMul = 1.2f;
        public float lungePush = 2f;

        private float nextLunge;

        public IEnumerator Act(EnemyAI ai)
        {
            if (ai.Player == null) yield break;
            Vector2 to = (Vector2)ai.Player.position - (Vector2)transform.position;
            float d = to.magnitude, gap = ai.GapToPlayer();
            if (!string.IsNullOrEmpty(meleeState) && ai.HasAnimState(meleeState) && gap <= ai.attackRange)
            {
                ai.Desired = Vector2.zero;
                ai.PlayState(meleeState);
                if (ai.Vfx != null) ai.Vfx.Windup(ai.DirToPlayer);
                yield return ai.WaitFor(ai.windup);
                if (ai.Vfx != null) ai.Vfx.Swing(ai.DirToPlayer);
                if (ai.GapToPlayer() <= ai.attackRange + 0.2f) ai.HitPlayerNow(ai.damage);
                yield return ai.WaitFor(Mathf.Max(0f, ai.attackTime - ai.windup));
                ai.PlayState("Idle");
                yield return ai.WaitFor(ai.cooldown);
                yield break;
            }
            if (Time.time >= nextLunge && d <= lungeRange)
            {
                nextLunge = Time.time + lungeCooldown;
                ai.Desired = Vector2.zero;
                ai.PlayState(prepState);
                yield return ai.Flash(prepTime, new Color(1f, 0.7f, 0.45f));
                if (ai.Player == null || ai.Health.IsDead) yield break;
                Vector2 dir = ((Vector2)ai.Player.position - (Vector2)transform.position).normalized;
                ai.FaceX(dir.x);
                ai.PlayState(lungeState);
                if (ai.Vfx != null) ai.Vfx.Dash(dir);
                bool hit = false;
                float t = 0f;
                while (t < lungeTime && !ai.Health.IsDead)
                {
                    ai.Desired = dir * lungeSpeed;
                    if (!hit && ai.GapToPlayer() <= 0.08f) { ai.Hurt(ai.damage * lungeDamageMul, transform.position, lungePush); hit = true; }
                    t += Time.deltaTime;
                    yield return null;
                }
                ai.Desired = Vector2.zero;
                ai.PlayState(endState);
                yield return ai.WaitFor(recoverTime);
                ai.PlayState("Idle");
                yield break;
            }
            ai.Desired = to.normalized * ai.moveSpeed;
            ai.PlayState("Move", false);
        }
    }
}
