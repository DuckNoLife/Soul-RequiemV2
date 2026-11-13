using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// spout — ống phun theo nhịp: nghỉ `off_seconds` -> báo trước `warn_seconds` (&lt;tên&gt;_warn) -> phun `on_seconds`
    /// (&lt;tên&gt;_fx, gây `damage` + đẩy lùi). `offset` lệch pha giữa các ống.
    /// </summary>
    public sealed class SpoutMechanism : HazardMechanismBase
    {
        public override void Init()
        {
            Ctx.Show(Name + "_fx", false);
            Ctx.Show(Name + "_warn", false);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            float on = Float("on_seconds", 1.5f), off = Float("off_seconds", 2.5f), warn = Float("warn_seconds", 1f);
            float t = Mathf.Repeat(Time.time + Float("offset", 0f), on + off + warn);
            int p = t < off ? 0 : t < off + warn ? 1 : 2;
            if (p != phase) { phase = p; Ctx.Show(Name + "_warn", p == 1); Ctx.Show(Name + "_fx", p == 2); }
            if (p == 2 && inside && Time.time >= nextTick)
            {
                nextTick = Time.time + Ctx.DamageTick;
                Ctx.HurtPlayer(Float("damage", 12f), Vector2.down, 2.5f);
            }
        }
    }
}
