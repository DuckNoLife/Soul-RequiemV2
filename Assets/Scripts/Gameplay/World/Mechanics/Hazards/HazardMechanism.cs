using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// hazard - stinging zone: loses `dps_percent` % of max HP per second; `poison_percent` / `poison_seconds` = poisoned.
    /// `shape = ellipse` -> only the ellipse inscribed in the box counts (round-ish Ichor pools). Stops once flag `stop_when` is set.
    /// `start_when` -> harmless (and &lt;name&gt;_fx hidden) until the flag is set (e.g. an Ichor pool spilling out when a glass tank breaks).
    /// </summary>
    public sealed class HazardMechanism : HazardMechanismBase
    {
        private bool Started => string.IsNullOrEmpty(Str("start_when", "")) || Ctx.Flags.HasAll(Str("start_when", ""));

        public override void Init()
        {
            if (!Started) Ctx.Show(Name + "_fx", false);
        }

        public override void OnFlagsChanged()
        {
            base.OnFlagsChanged();
            if (!Done && !string.IsNullOrEmpty(Str("start_when", "")) && Started) Ctx.Show(Name + "_fx", true);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (!Started) return;
            if (inside && Str("shape", "") == "ellipse")
            {
                Vector2 d = (playerPos - Rect.center) / (Rect.size * 0.5f);
                inside = d.sqrMagnitude <= 1f;
            }
            var hp = Ctx.PlayerHealth;
            if (!inside || Time.time < nextTick || hp == null || hp.IsDead) return;
            nextTick = Time.time + Ctx.DamageTick;
            float dps = Float("dps_percent", 3f);
            if (dps > 0f) Ctx.HurtPlayer(hp.Max * dps / 100f * Ctx.DamageTick, Vector2.zero, 0f);
            float poison = Float("poison_percent", 0f);
            if (poison > 0f && !hp.Invulnerable)
                hp.ApplyPoison(poison / 100f, Float("poison_seconds", 2f));
        }
    }
}
