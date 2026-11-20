using System;
using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 07/11 (§183) "Requiem Wail" — bộ "Mantras of the Vessel", ô III (chiêu tối thượng). Ren chắp tay niệm <see cref="channel"/> s
    /// (thân ánh tím, đứng yên — Roll huỷ và hoàn hồi chiêu); xoáy hồn tím (PixelLab sk_wail_pull) thu dần về chân Ren trong lúc niệm.
    /// Niệm xong: mọi quái trong <see cref="radius"/> đv bị KÉO về sát chân Ren (dừng cách <see cref="pullTo"/> đv) + <see cref="damage"/> +
    /// SPENT (+<see cref="spentBonus"/> sát thương nhận trong <see cref="spentTime"/> s — cả boss). Ren hồi <see cref="healPerHit"/> máu
    /// mỗi quái trúng (tối đa <see cref="healMax"/>). Tường che thì không trúng. Boss (miễn khống chế) không bị kéo, vẫn bị Spent.
    /// 08/11 (§191, gỡ trùng): trước đây ĐẨY văng ra — trùng vai Earthshatter (vòng quanh người + đẩy). Nay GOM quái lại: bộ Vessel thành
    /// "gom (Wail) — trói (Roots) — nghiền (Hand)"; đổi lại không có khiên, gom quái sát người là liều.
    /// Cân bằng: 45 AoE + 30% × 48 DPS × 4 s ≈ 103 lên boss mỗi 30 s; quái thường bị gom vào tầm chém quét 140°.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Requiem Wail", fileName = "VesselWail")]
    public class RequiemWailSkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Area - Pull";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Radius", Tiles(radius));
            Stat(o, "Spent", "+" + Pct(spentBonus) + " damage taken for " + Secs(spentTime));
            Stat(o, "Heal", healPerHit.ToString("0") + " per foe (max " + healMax.ToString("0") + ")");
        }

        public float channel = 0.8f;
        [Tooltip("Bán kính gom + gây sát thương (đv).")]
        public float radius = 2f;
        [Tooltip("Quái bị kéo tới cách chân Ren khoảng này (đv).")]
        public float pullTo = 0.45f;
        public float damage = 45f;
        public float spentBonus = 0.3f;
        public float spentTime = 4f;
        public float healPerHit = 3f;
        public float healMax = 15f;
        public string vfx = "sk_wail_pull";
        [Tooltip("Tỉ lệ ảnh xoáy (vòng ngoài ~1,8 đv ở tỉ lệ 1) — mặc định khớp radius.")]
        public float vfxScale = 1.1f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.PlayVeilCast();
            float end = Time.time + channel;
            Vector2 feet = p.transform.position;
            // xoáy thu về đúng lúc niệm xong (11 khung trải đều thời gian niệm)
            SpriteVfx swirl = p.showVfx ? SpriteVfx.Spawn(vfx, feet, vfxScale, p.VfxLayer, p.PlayerOrder - 1, false, 11f / Mathf.Max(0.2f, channel)) : null;
            p.StartCoroutine(SkillKit.TintWhile(p.Sprite, SkillKit.SpentTint, () => Time.time < end && p.ActionAlive(id)));
            float nextShake = 0f;
            while (Time.time < end)
            {
                p.LockActions(0.1f);
                if (!p.ActionAlive(id)) { if (swirl != null) swirl.Stop(); cancel?.Invoke(); yield break; }
                if (Time.time >= nextShake) { nextShake = Time.time + 0.2f; p.KickCamera(UnityEngine.Random.insideUnitCircle.normalized, 0.25f, 0.08f); }
                yield return null;
            }
            p.LockActions(0.25f);
            feet = p.transform.position;
            int n = 0;
            foreach (var ai in SkillKit.EnemiesIn(feet, radius, feet))
            {
                Vector2 toward = feet - (Vector2)ai.transform.position;
                if (!ai.Health.TakeDamage(damage, toward, 0f)) continue;
                SkillKit.HitFx(p, ai, toward);
                SkillKit.Vulnerable(ai, spentBonus, spentTime, SkillKit.SpentTint, p.VfxLayer, p.PlayerOrder);
                SkillKit.Pull(ai, feet, pullTo);
                n++;
            }
            PlayerCombat.RaiseStruck(feet, radius);
            float heal = Mathf.Min(healMax, n * healPerHit);
            if (heal > 0f)
            {
                p.Health.Heal(heal);
                if (p.showVfx) SpriteVfx.Spawn("heal_front", feet, 0.6f, p.VfxLayer, p.PlayerOrder + 1, false, 14f)?.Follow(p.transform);
            }
            p.KickCamera(Vector2.down, 1.6f, 0.3f);
            if (n > 0) p.HitStopNow();
        }
    }
}
