using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Reactions to hits / stun / death / revive: animations Hurt, Stagger, Stunned (+ stars spinning over the head), Death, Revive.
    /// Hits with knockback ≥ heavyHitKnockback cause a stagger and cancel the current action.
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
            if (force >= owner.heavyHitKnockback && anim.HasStagger && !WeaponTraits.Unflinching(owner))   // Last Stand
            {
                actions.Interrupt();                       // heavy hit: stagger, cancel the slash / stomp in progress
                actions.LockUntil = Time.time + owner.staggerLock;
                anim.Stagger();
                return;
            }
            if (actions.IsLocked || h.Invulnerable) return;  // slashing / stomping / rolling: keep the current motion
            anim.Hurt();
        }

        /// <summary>Called every frame: real stun (not a drink lock) -> Stagger + Stunned loop + stars.</summary>
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
            actions.Interrupt();                            // cancel the pending slash / stomp
            actions.LockUntil = 0f;
            anim.Die();
        }

        public void OnRevived(Health h)
        {
            if (!anim.HasDeath) return;
            anim.Revive();
            actions.LockUntil = Time.time + 0.75f;          // can only move once back on the feet (matches the Revive animation)
        }
    }
}
