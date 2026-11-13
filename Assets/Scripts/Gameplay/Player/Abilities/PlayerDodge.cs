using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Lộn nhào (Roll): lướt theo hướng nhìn với tốc độ rollSpeed, bất tử rollIFrames giây (nhân vật mờ đi), hồi rollCooldown.
    /// Huỷ đòn chém / dậm đang dở.
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
            owner.LastRollTime = Time.time; owner.CounterReady = true;            // 10/11 (§217): mở cửa sổ đòn phản
            actions.Interrupt();
            actions.LockUntil = 0f;
            anim.Dash(facing);

            var health = owner.Health;
            health.AddImpulse(facing * owner.rollSpeed);
            health.Invulnerable = true;
            var sr = owner.Sprite;
            Color c0 = sr != null ? sr.color : Color.white;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, 0.5f);
            yield return new WaitForSeconds(owner.rollIFrames);
            health.Invulnerable = false;
            // 02/11: chỉ trả lại độ trong suốt — c0 có thể là màu đỏ của lúc đang nháy trúng đòn (Health đã tự trả màu gốc) -> đỏ mãi
            if (sr != null) { var c = sr.color; sr.color = new Color(c.r, c.g, c.b, c0.a); }
        }
    }
}
