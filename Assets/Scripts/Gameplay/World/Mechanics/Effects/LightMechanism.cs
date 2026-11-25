namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// light - lamps (Light2D "Light &lt;name&gt;" created by Map2SceneBuilder / Soul Requiem ▸ Scenes ▸ Map 3):
    /// `lit_when` = only on while the flag is set, `off_when` = off once the flag is set. With a LightAnimator it fades instead of snapping off.
    /// </summary>
    public sealed class LightMechanism : Mechanism
    {
        public override void OnFlagsChanged()
        {
            string litWhen = Str("lit_when", ""), offWhen = Str("off_when", "");
            bool on = (string.IsNullOrEmpty(litWhen) || Ctx.Flags.HasAll(litWhen)) && (string.IsNullOrEmpty(offWhen) || !Ctx.Flags.HasAll(offWhen));
            var l = Ctx.Light(Name);
            if (l == null) return;
            var la = l.GetComponent<LightAnimator>();
            if (la != null) la.SetOn(on); else l.enabled = on;
        }
    }
}
