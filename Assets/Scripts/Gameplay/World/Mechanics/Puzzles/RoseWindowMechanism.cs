using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// rosewindow — cửa sổ hoa hồng (nhà nguyện): nhấn T -> bảng 2x2 mảnh kính của tranh `picture` (object ẩn picpal_&lt;tên&gt;),
    /// góc bắt đầu `start` ("1,2,3,1" = số lần 90°). Bấm mảnh để xoay (chuột phải xoay ngược).
    /// Đúng hết -> cờ `sets`, chữ `solved_hint`, tranh `truth` hiện dần. Giải xong vẫn mở xem lại được.
    /// Chữ dưới tiêu đề: `hint` khi chưa giải, `text` khi đã giải.
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
            SfxHooks.MechanismEvent(this, "solved", "puzzle_solved"); // 19/11 (§335): âm
            Ctx.Flags.Set(Str("sets", Name));
            Hints.Show(Str("solved_hint", "The altar grinds aside. Stairs lead down beneath the chapel."), 5f);
            MechanismPanels.MarkDirty();
        }
    }
}
