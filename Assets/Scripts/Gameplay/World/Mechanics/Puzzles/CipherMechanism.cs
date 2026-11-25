using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// cipher - letter lock: press T -> lock panel, each wheel has up/down arrows to change a Vael glyph (images, no digits).
    /// Wheel i shows digit perm[position] (`perm`, default "0,1,...,7"); `answer` ("3,7,4") correct on every wheel -> flag `sets` + text `text`.
    /// Glyph images come from the hidden objects glyphpal_&lt;digit&gt; in the map, or from Resources via `glyph_res`.
    /// </summary>
    public sealed class CipherMechanism : Mechanism
    {
        private int[] wheels;
        private string[] perm;

        public override string DefaultVerb => "Examine";
        public override bool Clue => true;

        public int WheelCount => wheels.Length;

        public override void Init()
        {
            wheels = new int[Str("answer", "0,0,0").Split(',').Length];
            perm = Str("perm", "0,1,2,3,4,5,6,7").Split(',');
        }

        public override bool KeepPanelOpen(Vector2 playerPos) => DistanceTo(playerPos) <= MapMechanics.CloseRange;

        public override void Use()
        {
            if (PanelIsOpen) { MechanismPanels.Close(); return; }
            if (Done) return;
            MechanismPanels.Open(new CipherPanel { Owner = this, Lock = this, Title = TextUtil.Clean(Str("title", "Lock")), Hint = TextUtil.Clean(Str("hint", "")) });
        }

        /// <summary>`glyph_res` = Resources image prefix (e.g. "UI/Kroven/crest_") -> images &lt;prefix&gt;&lt;digit&gt; replace the Vael glyphs
        /// (the Kroven crown lock in the Forgotten Store). Missing image -> back to the map's glyphpal.</summary>
        public Sprite GlyphAt(int i)
        {
            if (!int.TryParse(perm[wheels[i] % perm.Length].Trim(), out int d)) return null;
            string res = Str("glyph_res", "");
            if (res.Length > 0)
            {
                var s = Resources.Load<Sprite>(res + d);
                if (s != null) return s;
            }
            return Ctx.Glyph(d);
        }

        public void Turn(int i, int delta)
        {
            if (Done || i < 0 || i >= wheels.Length) return;
            wheels[i] = ((wheels[i] + delta) % perm.Length + perm.Length) % perm.Length;
            var ans = Str("answer", "0,0,0").Split(',');
            for (int k = 0; k < wheels.Length; k++)
                if (perm[wheels[k] % perm.Length].Trim() != ans[Mathf.Min(k, ans.Length - 1)].Trim()) return;
            Done = true;
            SfxHooks.MechanismEvent(this, "solved", "puzzle_solved"); // sound
            MechanismPanels.Close();
            Hints.Show(Str("text", "The lock opens."), 3.5f);
            Ctx.Flags.Set(Str("sets", Name));
        }
    }
}
