namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Base for danger zones (spout, vent, hazard, flood): stops for good once flag `stop_when` is set
    /// (hides &lt;name&gt;_fx and &lt;name&gt;_warn).
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
