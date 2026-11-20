using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 07/11 (§183) "Severing Draw" — chiêu lẻ, nhặt ở Archive (Map 2). Ren lướt <see cref="distance"/> đv theo hướng ngắm (tường chặn),
    /// bất tử trong lúc lướt, rút kiếm chém MỌI quái trên đường (<see cref="damage"/> mỗi con, một lần). Cuối đường khựng
    /// <see cref="recover"/> s (tra kiếm) — lướt vào giữa bầy là phải trả giá. Vệt chém PixelLab sk_draw kéo dài đúng quãng lướt.
    /// Cân bằng: 32 / 6 s ≈ 5,3 DPS mỗi con + một lần né có gây sát thương; không thay được Roll (hồi 0,7 s).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Severing Draw", fileName = "SeveringDraw")]
    public class SeveringDrawSkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Dash - Strike";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Dash distance", Tiles(distance));
            Stat(o, "Untouchable", Secs(dashTime) + " mid-dash");
            Stat(o, "Knockback", knockback.ToString("0.#"));
            Stat(o, "Sheathe pause", Secs(recover));
        }

        [Tooltip("Quãng lướt tối đa (đơn vị Unity; 1 ô = 0,32).")]
        public float distance = 1.6f;
        public float dashTime = 0.16f;
        public float damage = 32f;
        [Tooltip("Bề rộng đường chém.")]
        public float hitWidth = 0.5f;
        public float knockback = 1.2f;
        [Tooltip("Khựng cuối đường (tra kiếm).")]
        public float recover = 0.3f;
        [Tooltip("Bán kính thân Ren khi dò tường.")]
        public float bodyRadius = 0.13f;
        public string vfx = "sk_draw";
        [Tooltip("Chiều dài vệt chém trong ảnh VFX (px, PPU 100) — để kéo giãn đúng quãng lướt.")]
        public float vfxLength = 113f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            p.BeginAction();
            Vector2 dir = SkillKit.AimDir(p, p.Facing.sqrMagnitude > 0.01f ? p.Facing : Vector2.down);
            Vector2 from = p.transform.position;
            float len = Mathf.Max(0f, SkillKit.WallDistance(from, dir, distance, bodyRadius) - 0.02f);
            p.SetFacing(dir);
            p.PlayDash(dir);
            p.LockActions(dashTime + recover);
            p.Health.Shield(dashTime + 0.08f);

            var targets = SkillKit.EnemiesAlong(from, dir, len + 0.1f, hitWidth);
            var done = new HashSet<EnemyAI>();
            var rb = p.GetComponent<Rigidbody2D>();
            bool sim = rb == null || rb.simulated;
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }

            if (p.showVfx)
            {
                var v = SpriteVfx.Spawn(vfx, from + new Vector2(0f, 0.18f), 1f, p.VfxLayer, p.PlayerOrder + 2, false, 22f);
                if (v != null)
                {
                    v.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    v.transform.localScale = new Vector3(Mathf.Max(0.35f, (len + 0.2f) / (vfxLength / 100f)), 0.9f, 1f);
                }
            }

            int hits = 0;
            for (float t = 0f; t < dashTime; t += Time.deltaTime)
            {
                float u = 1f - (1f - t / dashTime) * (1f - t / dashTime);
                Vector2 pos = from + dir * len * u;
                p.transform.position = pos;
                if (rb != null) rb.position = pos;
                hits += Strike(p, targets, done, len * u + 0.15f, dir);
                yield return null;
            }
            p.transform.position = from + dir * len;
            if (rb != null) { rb.position = from + dir * len; rb.simulated = sim; }
            hits += Strike(p, targets, done, len + 0.3f, dir);
            PlayerCombat.RaiseStruck(from + dir * len * 0.5f, len * 0.5f + 0.2f);   // cơ quan (đèn, thùng...) trên đường chém
            p.PlaySlashPose(dir);                                   // 09/11 (§201): thay Iaido
            p.KickCamera(dir, 0.9f, 0.16f);
            if (hits > 0) p.HitStopNow();
        }

        private int Strike(PlayerCombat p, List<KeyValuePair<EnemyAI, float>> targets, HashSet<EnemyAI> done, float reached, Vector2 dir)
        {
            int n = 0;
            foreach (var kv in targets)
            {
                var ai = kv.Key;
                if (kv.Value > reached || ai == null || ai.Health == null || ai.Health.IsDead || !done.Add(ai)) continue;
                Vector2 side = new Vector2(-dir.y, dir.x);
                if (Vector2.Dot((Vector2)ai.transform.position - (Vector2)p.transform.position, side) < 0f) side = -side;
                if (ai.Health.TakeDamage(damage, side, knockback)) { SkillKit.HitFx(p, ai, dir); n++; }
            }
            return n;
        }
    }
}
