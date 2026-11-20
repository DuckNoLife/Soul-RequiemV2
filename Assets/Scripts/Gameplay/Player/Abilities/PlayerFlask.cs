using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Bình máu (E): hồi healPercent máu tối đa, khựng healLock giây (hoạt ảnh Drink). Hết bình / đầy máu / Hemorrhage thì không uống.
    /// Số bình nằm ở PlayerCombat.potions (mang sang map sau bằng GameSession).
    /// </summary>
    internal sealed class PlayerFlask
    {
        private readonly PlayerCombat owner;
        private readonly PlayerAnimation anim;

        /// <summary>Đến lúc này đang uống: khựng này là do uống thuốc, không phải choáng (không hiện sao choáng).</summary>
        public float DrinkingUntil { get; private set; }

        public PlayerFlask(PlayerCombat owner, PlayerAnimation anim) { this.owner = owner; this.anim = anim; }

        public void Drink()
        {
            var health = owner.Health;
            var st = owner.GetComponent<PlayerStatus>();
            if (st != null && st.FlaskTaken) { st.SayRestraint(st.FlaskText); return; }   // 17/11: bị thu bình (Map 4)
            if (owner.potions <= 0)
            {
                Hints.Show("Out of potions!", 1.5f);
                SoulRequiem.Core.Sfx.Play("flask_empty", owner.transform.position); // 19/11 (§335): âm
                return;
            }
            if (health.Percent >= 0.999f) return;
            if (st != null && st.HealBlocked)
            {
                Hints.Show("Hemorrhage: the potion will not take!", 1.4f);
                return;
            }
            owner.potions--;
            DrinkingUntil = Time.time + owner.healLock + 0.05f;
            anim.Drink();
            SoulRequiem.Core.Sfx.Play("flask_drink", owner.transform.position);   // 17/11 (§294): âm
            health.Stun(owner.healLock);
            owner.StartCoroutine(Gulp());
        }

        /// <summary>10/11 (§212): thời điểm NGỤM THUỐC trong hoạt ảnh drink (khung 3 = ngửa cổ, chai chạm môi):
        /// tổng DrinkMs khung 0-2 trong RenAnimatorBuilder (50 + 60 + 50 ms). Máu + VFX hồi đúng lúc này, không cộng ngay lúc bấm.</summary>
        public const float GulpTime = 0.16f;

        private IEnumerator Gulp()
        {
            yield return new WaitForSeconds(GulpTime);
            var health = owner.Health;
            if (health.IsDead) yield break;
            health.Heal(health.Max * owner.healPercent * WeaponTraits.FlaskMul(owner));   // 16/11 (§254): Hunger
            if (owner.showVfx) HealVfx();
        }

        /// <summary>Vòng sáng xanh dưới chân + dấu cộng bay lên, đúng khung ngửa cổ uống. VFX "Ronin Heal".</summary>
        private void HealVfx()
        {
            var pos = owner.transform.position;
            var b = SpriteVfx.Spawn("heal_back", pos, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder - 1, false, 14f);
            var f = SpriteVfx.Spawn("heal_front", pos, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder + 1, false, 14f);
            if (b != null) b.Follow(owner.transform);
            if (f != null) f.Follow(owner.transform);
        }
    }
}
