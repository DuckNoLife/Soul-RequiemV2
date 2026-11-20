namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Nền cho vùng nguy hiểm (spout, vent, hazard, flood): dừng hẳn khi có cờ `stop_when`
    /// (ẩn &lt;tên&gt;_fx và &lt;tên&gt;_warn).
    /// </summary>
    public abstract class HazardMechanismBase : Mechanism
    {
        protected int phase = -1;
        protected float nextTick;

        public override void OnFlagsChanged()
        {
            string stop = Str("stop_when", "");
            if (string.IsNullOrEmpty(stop) || Done || !Ctx.Flags.HasAll(stop)) return;
            Done = true;
            Ctx.Show(Name + "_fx", false);
            Ctx.Show(Name + "_warn", false);
            OnStopped();
        }

        protected virtual void OnStopped() { }
    }
}
