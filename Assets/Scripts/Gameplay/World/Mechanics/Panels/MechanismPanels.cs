using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Bảng lớn giữa màn hình do cơ quan mở (ghi chú, khoá ký tự, cửa sổ hoa hồng).
    /// Gameplay chỉ giữ DỮ LIỆU của bảng đang mở (<see cref="Current"/>); assembly UI (MechanismPanelView) tự vẽ.
    /// Mỗi lần mở / đóng / đổi nội dung lớn, <see cref="Version"/> tăng -> giao diện dựng lại.
    /// </summary>
    public static class MechanismPanels
    {
        public static MechanismPanel Current { get; private set; }
        public static int Version { get; private set; }
        private static int closedFrame = -1;

        /// <summary>Có bảng đang mở — hoặc vừa đóng trong khung hình này (để phím Esc/chuột đóng bảng không mở menu Pause / chém).</summary>
        public static bool BlocksInput => Current != null || closedFrame == Time.frameCount;

        internal static void Open(MechanismPanel panel)
        {
            Current = panel;
            Version++;
        }

        public static void Close()
        {
            if (Current == null) return;
            Current = null;
            closedFrame = Time.frameCount;
            Version++;
        }

        /// <summary>Nội dung bảng đổi (vd vừa giải xong) -> giao diện dựng lại.</summary>
        internal static void MarkDirty() => Version++;

        internal static void Reset()
        {
            Current = null;
            closedFrame = -1;
            Version++;
        }
    }

    /// <summary>Dữ liệu chung của một bảng.</summary>
    public abstract class MechanismPanel
    {
        public string Title = "";
        internal Mechanism Owner;
    }

    /// <summary>Tờ ghi chú / hồ sơ: chữ, tranh, hàng ký tự Vael, hoặc sơ đồ một ô của khoá ký tự.</summary>
    public sealed class NotePanel : MechanismPanel
    {
        public string Body = "";
        public Sprite Picture;
        public readonly System.Collections.Generic.List<(Sprite glyph, string caption)> Glyphs =
            new System.Collections.Generic.List<(Sprite, string)>();
        /// <summary>Sơ đồ khoá: số ô (0 = không có sơ đồ), ô đã biết (0-based), ký tự của ô đó.</summary>
        public int SealSlots;
        public int SealSlot = -1;
        public Sprite SealGlyph;
    }

    /// <summary>Khoá ký tự: mỗi bánh xe hiện một ký tự Vael, mũi tên lên/xuống để xoay.</summary>
    public sealed class CipherPanel : MechanismPanel
    {
        public string Hint = "";
        internal CipherMechanism Lock;
        public int WheelCount => Lock.WheelCount;
        public Sprite WheelGlyph(int i) => Lock.GlyphAt(i);
        /// <summary>Xoay bánh xe i (+1 lên / -1 xuống). Giải xong thì bảng tự đóng.</summary>
        public void Turn(int i, int delta) => Lock.Turn(i, delta);
    }

    /// <summary>Cửa sổ hoa hồng: 4 mảnh (I trên trái, II trên phải, III dưới trái, IV dưới phải), mỗi mảnh xoay 90°.</summary>
    public sealed class RoseWindowPanel : MechanismPanel
    {
        internal RoseWindowMechanism Window;
        public Sprite Picture, Truth;
        public bool Solved => Window.Done;
        /// <summary>Time.unscaledTime lúc giải xong (-1 nếu giải bằng phím test) — dùng cho ảnh "sự thật" hiện dần.</summary>
        public float SolvedAt => Window.SolvedAt;
        public string Subtitle => Window.Subtitle;
        /// <summary>Góc của mảnh i, tính bằng số lần 90° theo chiều kim đồng hồ (0 = đúng).</summary>
        public int Rotation(int i) => Window.Rotation(i);
        public void Rotate(int i, bool clockwise) => Window.Rotate(i, clockwise);
    }
}
