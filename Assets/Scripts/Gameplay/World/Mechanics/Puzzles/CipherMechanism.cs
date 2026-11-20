using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// cipher — khoá ký tự: nhấn T -> bảng khoá, mỗi bánh xe có mũi tên lên/xuống đổi ký tự Vael (hình, không số).
    /// Bánh xe i hiện chữ số perm[vị trí] (`perm`, mặc định "0,1,...,7"); đúng `answer` ("3,7,4") ở mọi bánh -> cờ `sets` + chữ `text`.
    /// Ảnh ký tự lấy từ object ẩn glyphpal_&lt;chữ số&gt; trong map, hoặc từ Resources theo `glyph_res` (§281).
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

        /// <summary>16/11 (§281): `glyph_res` = tiền tố ảnh Resources (vd "UI/Kroven/crest_") -> ảnh &lt;tiền tố&gt;&lt;chữ số&gt; thay ký tự Vael
        /// (khoá vương miện Kroven ở Forgotten Store). Thiếu ảnh -> quay về glyphpal của map.</summary>
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
            SfxHooks.MechanismEvent(this, "solved", "puzzle_solved"); // 19/11 (§335): âm
            MechanismPanels.Close();
            Hints.Show(Str("text", "The lock opens."), 3.5f);
            Ctx.Flags.Set(Str("sets", Name));
        }
    }
}
