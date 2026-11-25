using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// vent - toxic gas vent: bubbles (&lt;name&gt;_warn) -> poison cloud (&lt;name&gt;_fx) poisoning `poison_percent` over `poison_seconds`.
    /// Cycle `period` + `cloud_seconds`, warning `warn_seconds`.
    /// </summary>
    public sealed class VentMechanism : HazardMechanismBase
    {
        public override void Init()
        {
            Ctx.Show(Name + "_fx", false);
            Ctx.Show(Name + "_warn", false);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            float period = Float("period", 4f), warn = Float("warn_seconds", 1f), cloud = Float("cloud_seconds", 2f);
            float t = Mathf.Repeat(Time.time + Float("offset", 0f), period + cloud);
            int p = t < period - warn ? 0 : t < period ? 1 : 2;
            if (p != phase) { phase = p; Ctx.Show(Name + "_warn", p == 1); Ctx.Show(Name + "_fx", p == 2); }
            var hp = Ctx.PlayerHealth;
            if (p == 2 && inside && hp != null && !hp.Invulnerable)
                hp.ApplyPoison(Float("poison_percent", 4f) / 100f, Float("poison_seconds", 2f));
        }
    }
}
