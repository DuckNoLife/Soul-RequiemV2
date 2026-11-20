using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Phản ứng khi trúng đòn / choáng / gục / hồi sinh: hoạt ảnh Hurt, Stagger, Stunned (+ sao quay trên đầu), Death, Revive.
    /// Đòn có lực đẩy ≥ heavyHitKnockback làm loạng choạng và huỷ hành động đang làm.
    /// </summary>
    internal sealed class PlayerHurtReactions
    {
        private readonly PlayerCombat owner;
        private readonly PlayerAnimation anim;
        private readonly PlayerActionState actions;
        private readonly PlayerFlask flask;
        private bool wasStunned;
        private SpriteVfx stunStars;

        public PlayerHurtReactions(PlayerCombat owner, PlayerAnimation anim, PlayerActionState actions, PlayerFlask flask)
        {
            this.owner = owner; this.anim = anim; this.actions = actions; this.flask = flask;
        }

        public void OnHit(Health h, float amount, Vector2 dir, float force)
        {
            if (!anim.Exists || h.IsDead) return;
            if (force >= owner.heavyHitKnockback && anim.HasStagger && !WeaponTraits.Unflinching(owner))   // 16/11 (§254): Last Stand
            {
                actions.Interrupt();                       // đòn nặng: loạng choạng, huỷ chém / dậm đang làm
                actions.LockUntil = Time.time + owner.staggerLock;
                anim.Stagger();
                return;
            }
            if (actions.IsLocked || h.Invulnerable) return;  // đang chém / dậm / lộn: giữ nguyên động tác
            anim.Hurt();
        }

        /// <summary>Gọi mỗi khung hình: choáng thật (không phải khựng do uống thuốc) -> Stagger + vòng Stunned + sao.</summary>
        public void UpdateStun()
        {
            var h = owner.Health;
            bool stunned = h.IsStunned && Time.time >= flask.DrinkingUntil && !h.IsDead;
            anim.SetStunned(stunned);
            if (stunned && !wasStunned)
            {
                anim.Stagger();
                if (owner.showVfx && owner.Sprite != null)
                    stunStars = SpriteVfx.SpawnAttached("stun_stars", owner.transform, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder + 1, 12f, h.StunTimeLeft);
            }
            if (!stunned && wasStunned && stunStars != null) stunStars.Stop();
            wasStunned = stunned;
        }

        public void OnDied(Health h)
        {
            actions.Interrupt();                            // huỷ chém / dậm đang chờ
            actions.LockUntil = 0f;
            anim.Die();
        }

        public void OnRevived(Health h)
        {
            if (!anim.HasDeath) return;
            anim.Revive();
            actions.LockUntil = Time.time + 0.75f;          // đứng dậy xong mới đi được (khớp hoạt ảnh Revive)
        }
    }
}
