using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// rosewindow - rose window (chapel): press T -> a 2x2 panel of glass pieces from picture `picture` (hidden object picpal_&lt;name&gt;),
    /// starting angles `start` ("1,2,3,1" = number of 90° turns). Click a piece to rotate it (right click rotates back).
    /// All correct -> flag `sets`, text `solved_hint`, picture `truth` fades in. Still viewable after solving.
    /// Line under the title: `hint` while unsolved, `text` once solved.
    /// </summary>
    public sealed class RoseWindowMechanism : Mechanism
    {
        private readonly int[] wheels = new int[4];

        public override string DefaultVerb => "Examine";
        public override bool Clue => true;
        public override bool Usable => true;

        public float SolvedAt { get; private set; } = -1f;
        public string Subtitle => TextUtil.Clean(Done ? Str("text", "") : Str("hint", ""));
        public int Rotation(int i) => wheels[i];

        public override void Init()
        {
            var st = Str("start", "1,2,3,1").Split(',');
            for (int i = 0; i < 4; i++) wheels[i] = i < st.Length && int.TryParse(st[i].Trim(), out int sv) ? ((sv % 4) + 4) % 4 : 1;
        }

        public override bool KeepPanelOpen(Vector2 playerPos) => DistanceTo(playerPos) <= MapMechanics.CloseRange;

        public override void Use()
        {
            if (PanelIsOpen) { MechanismPanels.Close(); return; }
            MechanismPanels.Open(new RoseWindowPanel
            {
                Owner = this, Window = this,
                Title = TextUtil.Clean(Str("title", "The Rose Window")),
                Picture = Ctx.Picture(Str("picture", "-")),
                Truth = Ctx.Picture(Str("truth", "-")),
            });
        }

        public void Rotate(int i, bool clockwise)
        {
            if (Done || i < 0 || i >= 4) return;
            wheels[i] = (wheels[i] + (clockwise ? 1 : 3)) % 4;
            foreach (var v in wheels) if (v != 0) return;
            Done = true;
            SolvedAt = Time.unscaledTime;
            SfxHooks.MechanismEvent(this, "solved", "puzzle_solved"); // sound
            Ctx.Flags.Set(Str("sets", Name));
            Hints.Show(Str("solved_hint", "The altar grinds aside. Stairs lead down beneath the chapel."), 5f);
            MechanismPanels.MarkDirty();
        }
    }
}
