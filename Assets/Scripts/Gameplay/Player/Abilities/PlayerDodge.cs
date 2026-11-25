using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Dodge roll: dashes along the facing direction at rollSpeed, invulnerable for rollIFrames seconds (the character fades), cooldown rollCooldown.
    /// Cancels a slash / stomp in progress. Called by: PlayerCombat (Roll key).
    /// </summary>
    internal sealed class PlayerDodge
    {
        private readonly PlayerCombat owner;
        private readonly PlayerAnimation anim;
        private readonly PlayerActionState actions;

        public float NextRoll;

        public PlayerDodge(PlayerCombat owner, PlayerAnimation anim, PlayerActionState actions)
        {
            this.owner = owner; this.anim = anim; this.actions = actions;
        }

        public bool Ready => Time.time >= NextRoll;
        public float CooldownLeft => Mathf.Max(0f, NextRoll - Time.time);

        public IEnumerator Roll(Vector2 facing)
        {
            NextRoll = Time.time + owner.rollCooldown;
            owner.LastRollTime = Time.time; owner.CounterReady = true;            // open the counter-attack window
            actions.Interrupt();
            actions.LockUntil = 0f;
            anim.Dash(facing);
            SoulRequiem.Core.Sfx.Play("pl_dodge", owner.transform.position);   // sound

            var health = owner.Health;
            health.AddImpulse(facing * owner.rollSpeed);
            health.Invulnerable = true;
            var sr = owner.Sprite;
            Color c0 = sr != null ? sr.color : Color.white;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, 0.5f);
            yield return new WaitForSeconds(owner.rollIFrames);
            health.Invulnerable = false;
            // only restore the alpha — c0 may be the red of a hit flash (Health restores the original colour itself) -> would stay red forever
            if (sr != null) { var c = sr.color; sr.color = new Color(c.r, c.g, c.b, c0.a); }
        }
    }
}
