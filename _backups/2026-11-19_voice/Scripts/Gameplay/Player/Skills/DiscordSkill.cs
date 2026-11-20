using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§252) "Discord" — bộ "Mantras of the Ear", ô II. Ren thì thầm vào tai con quái gần điểm ngắm nhất (≤ <see cref="range"/>):
    /// một đốm hymn bay tới nó; trong <see cref="duration"/> s nó nghe giọng chủ ở sai chỗ và quay sang đánh con quái gần nhất
    /// (≤ <see cref="allyRadius"/>), con kia đánh trả (EnemyAI.Retarget — mọi đòn / chiêu sẵn có của quái tự nhắm vào nhau).
    /// Boss không bị lừa: khựng <see cref="bossHold"/> s + nhận thêm <see cref="bossVuln"/> sát thương trong <see cref="bossVulnTime"/> s.
    /// Không có con nào bên cạnh: quái bị choáng <see cref="fallbackStun"/> s. Không có mục tiêu: huỷ, hoàn hồi chiêu.
    /// Hạn chế: quái bắn đạn vẫn bắn về phía mục tiêu mới nhưng đạn chỉ trúng người chơi (khống chế, không gây sát thương).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Discord", fileName = "EarDiscord")]
    public class DiscordSkill : SkillDefinition
    {
        public override string Kind => "Targeted - Control";
        public override void Stats(List<KeyValuePair<string, string>> o)
        {
            Stat(o, "Range", Tiles(range));
            Stat(o, "Turned", Secs(duration));
            Stat(o, "Vs great foes", "halt " + Secs(bossHold) + ", +" + Pct(bossVuln) + " damage for " + Secs(bossVulnTime));
        }

        public float range = 3.5f;
        public float searchRadius = 1.1f;
        public float duration = 5f;
        [Tooltip("Tìm đồng bọn để quay sang đánh trong bán kính này quanh mục tiêu (đv).")]
        public float allyRadius = 3f;
        public float bossHold = 1f;
        public float bossVuln = 0.2f;
        public float bossVulnTime = 4f;
        public float fallbackStun = 2f;
        public float castLock = 0.35f;
        public float flightTime = 0.3f;
        public Color tint = new Color(0.65f, 0.82f, 1f, 1f);

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(castLock);
            Vector2 aim = SkillKit.AimPoint(p, range);
            Vector2 feet = p.transform.position;
            if ((aim - feet).sqrMagnitude > 0.0001f) p.SetFacing(aim - feet);
            EnemyAI target = null;
            float best = float.MaxValue;
            foreach (var ai in SkillKit.EnemiesIn(aim, searchRadius, feet))
            {
                float dd = ((Vector2)ai.transform.position - aim).sqrMagnitude;
                if (dd < best) { best = dd; target = ai; }
            }
            if (target == null) { cancel?.Invoke(); yield break; }
            p.PlayEmbersCast();
            int layer = p.VfxLayer, order = p.PlayerOrder;

            // đốm thì thầm bay tới tai mục tiêu
            Vector2 from = feet + Vector2.up * 0.4f;
            var wisp = EtherVfx.Spawn("Discord Whisper", EtherVfx.Orb, from, tint, layer, order + 2);
            for (float t = 0f; t < flightTime && target != null; t += Time.deltaTime)
            {
                if (!p.ActionAlive(id)) { if (wisp != null) Destroy(wisp.gameObject); cancel?.Invoke(); yield break; }
                Vector2 to = Head(target);
                float k = t / flightTime;
                if (wisp != null)
                {
                    wisp.transform.position = Vector2.Lerp(from, to, k * k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
                    wisp.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 0.55f, k);
                }
                yield return null;
            }
            if (wisp != null) Destroy(wisp.gameObject);
            if (target == null || target.Health == null || target.Health.IsDead) yield break;
            if (p.showVfx)
            {
                var pop = SpriteVfx.Spawn("m3_hymn_pop", Head(target), 0.6f, layer, order + 3, false, 16f);
                if (pop != null) pop.Tint(tint);
            }

            if (target.IsBoss || target.Health.ImmuneToCrowdControl)
            {
                SkillKit.Vulnerable(target, bossVuln, bossVulnTime, tint, layer, order);
                p.StartCoroutine(Hold(target, bossHold));
                yield break;
            }
            EnemyAI ally = null;
            best = float.MaxValue;
            foreach (var ai in SkillKit.EnemiesIn(target.transform.position, allyRadius))
            {
                if (ai == target) continue;
                float dd = ((Vector2)(ai.transform.position - target.transform.position)).sqrMagnitude + (ai.IsBoss ? 100f : 0f);
                if (dd < best) { best = dd; ally = ai; }
            }
            if (ally == null) { target.Health.Stun(fallbackStun); yield break; }
            target.Retarget(ally, duration);
            ally.Retarget(target, duration);                                     // boss bỏ qua (Retarget tự từ chối)
            p.StartCoroutine(SkillKit.TintWhile(target.Body, tint, () => target != null && target.Retargeted));
            p.RaiseSkillCast(target.transform.position, 0.5f);
        }

        private static Vector2 Head(EnemyAI ai)
        {
            var b = ai.Body;
            return b != null ? new Vector2(b.bounds.center.x, b.bounds.max.y - 0.05f) : (Vector2)ai.transform.position + new Vector2(0f, 0.5f);
        }

        private static IEnumerator Hold(EnemyAI ai, float seconds)
        {
            if (ai == null) yield break;
            ai.Held = true;
            yield return new WaitForSeconds(seconds);
            if (ai != null) ai.Held = false;
        }
    }
}
