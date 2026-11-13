using System;
using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 07/11 (§183) "Riposte" — chiêu lẻ, nhặt ở Warden's Office (Map 2). Ren vào thế đỡ <see cref="window"/> s (thân loé vàng).
    /// Đòn ĐẦU TIÊN trúng Ren trong thế đỡ bị chặn hoàn toàn (Health.ParryWindow): Ren rút kiếm phản đòn về phía kẻ đánh
    /// (<see cref="counterDamage"/>, choáng <see cref="stun"/> s — boss: ×<see cref="bossCounterMul"/> sát thương), bất tử <see cref="safeAfter"/> s, hoàn <see cref="refund"/> thời gian hồi chiêu.
    /// Đỡ hụt: khựng thêm <see cref="whiffLock"/> s.
    /// 08/11 (§191, gỡ trùng): boss không còn bị STAGGERED (+25% — trùng nguồn lộ sơ hở với Flask / Hand / Wail, không cộng dồn) mà ăn
    /// nhát phản mạnh ×<see cref="bossCounterMul"/>; thế đỡ CHỈ chặn đòn cận chiến (Health.IncomingRanged) — đạn để nội tại Turned Blade.
    /// Cân bằng: thưởng cho người đọc đòn của boss — 60 + 25% × 48 DPS × 2 s ≈ 84 mỗi lần trúng, hồi thực tế ~4 s; đỡ hụt bị phạt.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Riposte", fileName = "Riposte")]
    public class RiposteSkill : SkillDefinition
    {
        public float window = 0.45f;
        public float counterDamage = 60f;
        public float counterRadius = 0.55f;
        public float stun = 1.2f;
        [Tooltip("08/11 (§191): sát thương phản đòn nhân hệ số này lên kẻ miễn khống chế (boss).")]
        public float bossCounterMul = 1.5f;
        [Tooltip("Phần thời gian hồi chiêu được hoàn khi đỡ trúng.")]
        public float refund = 0.5f;
        public float whiffLock = 0.35f;
        public float safeAfter = 0.3f;
        [Tooltip("Kẻ đánh xa hơn tầm này (đơn vị Unity) -> chỉ chặn, không phản đòn.")]
        public float reach = 1.8f;
        public string vfx = "sk_riposte";

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(window);
            bool parried = false;
            Vector2 kdir = Vector2.zero;
            Action<float, Vector2> onParry = (a, d) => { parried = true; kdir = d; };
            p.Health.Parried += onParry;
            p.Health.ParryWindow(window);
            float end = Time.time + window;
            p.StartCoroutine(SkillKit.TintWhile(p.Sprite, new Color(1f, 0.86f, 0.5f), () => !parried && Time.time < end && p.ActionAlive(id)));
            while (Time.time < end && !parried && p.ActionAlive(id)) yield return null;
            p.Health.Parried -= onParry;
            p.Health.EndParry();
            if (!parried)
            {
                if (p.ActionAlive(id)) p.LockActions(whiffLock);
                yield break;
            }

            // phản đòn: tìm kẻ vừa đánh (đứng ngược hướng bị đẩy, trong tầm)
            Vector2 feet = p.transform.position;
            Vector2 toward = kdir.sqrMagnitude > 0.0001f ? -kdir.normalized : (p.Facing.sqrMagnitude > 0.01f ? p.Facing.normalized : Vector2.down);
            EnemyAI attacker = null;
            float best = float.MaxValue;
            foreach (var ai in SkillKit.EnemiesIn(feet, reach, feet))
            {
                Vector2 d = (Vector2)ai.transform.position - feet;
                float score = d.magnitude - Vector2.Dot(d.normalized, toward) * 0.6f;
                if (score < best) { best = score; attacker = ai; }
            }
            Vector2 dir = attacker != null ? ((Vector2)attacker.transform.position - feet).normalized : toward;
            p.Health.Shield(safeAfter);
            p.BeginAction();
            p.LockActions(0.35f);
            p.SetFacing(dir);
            p.PlayIaido(dir);
            if (p.showVfx) SpriteVfx.Spawn(vfx, feet + dir * 0.32f + new Vector2(0f, 0.22f), 0.9f, p.VfxLayer, p.PlayerOrder + 3, dir.x < 0f, 18f);
            if (attacker != null)
            {
                Vector2 c = attacker.transform.position;
                foreach (var ai in SkillKit.EnemiesIn(c, counterRadius, feet))
                {
                    float dmg = ai.Health.ImmuneToCrowdControl ? counterDamage * bossCounterMul : counterDamage;
                    if (!ai.Health.TakeDamage(dmg, dir, 2f)) continue;
                    SkillKit.HitFx(p, ai, dir);
                    SkillKit.Control(ai, stun, 0f, 0f, p.VfxLayer, p.PlayerOrder);
                }
                p.HitStopNow();
            }
            p.KickCamera(dir, 1.2f, 0.18f);
            p.Loadout.Refund(this, refund);
        }
    }
}
