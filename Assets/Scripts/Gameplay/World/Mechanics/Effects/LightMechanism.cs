namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// light — đèn (Light2D "Light &lt;tên&gt;" do Map2SceneBuilder / "Dựng scene Map 3" tạo):
    /// `lit_when` = chỉ sáng khi có cờ, `off_when` = tắt khi có cờ. Có LightAnimator thì mờ dần thay vì tắt phụt.
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
