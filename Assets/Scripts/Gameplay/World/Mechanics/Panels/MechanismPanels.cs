using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Large centre-screen panels opened by mechanisms (notes, letter locks, rose window).
    /// Gameplay only holds the DATA of the open panel (<see cref="Current"/>); the UI assembly (MechanismPanelView) draws it.
    /// Every open / close / big content change increments <see cref="Version"/> -> the view rebuilds.
    /// </summary>
    public static class MechanismPanels
    {
        public static MechanismPanel Current { get; private set; }
        public static int Version { get; private set; }
        private static int closedFrame = -1;

        /// <summary>A panel is open - or was just closed this frame (so the Esc/mouse press that closed it does not open the Pause menu / slash).</summary>
        public static bool BlocksInput => Current != null || closedFrame == Time.frameCount;

        /// <summary>Point Ren faces while reading (centre of the area of the mechanism that opened the panel).</summary>
        public static Vector2? Focus { get; private set; }

        internal static void Open(MechanismPanel panel)
        {
            Current = panel;
            Focus = panel != null && panel.Owner != null ? panel.Owner.Rect.center : (Vector2?)null;
            SoulRequiem.Core.GameInput.Reading = panel != null;                // stand still reading until the panel closes
            Version++;
        }

        public static void Close()
        {
            if (Current == null) return;
            Current = null;
            Focus = null;
            SoulRequiem.Core.GameInput.Reading = false;
            closedFrame = Time.frameCount;
            Version++;
        }

        /// <summary>Panel content changed (e.g. just solved) -> the view rebuilds.</summary>
        internal static void MarkDirty() => Version++;

        internal static void Reset()
        {
            Current = null;
            Focus = null;
            SoulRequiem.Core.GameInput.Reading = false;
            closedFrame = -1;
            Version++;
        }
    }

    /// <summary>Shared data of one panel.</summary>
    public abstract class MechanismPanel
    {
        public string Title = "";
        internal Mechanism Owner;
    }

    /// <summary>Note / record sheet: text, picture, a row of Vael glyphs, or a diagram of one letter lock slot.</summary>
    public sealed class NotePanel : MechanismPanel
    {
        public string Body = "";
        /// <summary>"Received" line (sword / key) printed at the bottom of the panel - instead of a line at the bottom of the screen.</summary>
        public string Obtained = "";
        public Sprite Picture;
        public readonly System.Collections.Generic.List<(Sprite glyph, string caption)> Glyphs =
            new System.Collections.Generic.List<(Sprite, string)>();
        /// <summary>Lock diagram: slot count (0 = no diagram), the known slot (0-based), that slot's glyph.</summary>
        public int SealSlots;
        public int SealSlot = -1;
        public Sprite SealGlyph;
    }

    /// <summary>Letter lock: each wheel shows one Vael glyph, up/down arrows to turn it.</summary>
    public sealed class CipherPanel : MechanismPanel
    {
        public string Hint = "";
        internal CipherMechanism Lock;
        public int WheelCount => Lock.WheelCount;
        public Sprite WheelGlyph(int i) => Lock.GlyphAt(i);
        /// <summary>Turn wheel i (+1 up / -1 down). Once solved the panel closes by itself.</summary>
        public void Turn(int i, int delta) => Lock.Turn(i, delta);
    }

    /// <summary>Rose window: 4 pieces (I top left, II top right, III bottom left, IV bottom right), each rotating by 90°.</summary>
    public sealed class RoseWindowPanel : MechanismPanel
    {
        internal RoseWindowMechanism Window;
        public Sprite Picture, Truth;
        public bool Solved => Window.Done;
        /// <summary>Time.unscaledTime when solved (-1 if solved with a test key) - used to fade in the "truth" image.</summary>
        public float SolvedAt => Window.SolvedAt;
        public string Subtitle => Window.Subtitle;
        /// <summary>Angle of piece i, in clockwise 90° steps (0 = correct).</summary>
        public int Rotation(int i) => Window.Rotation(i);
        public void Rotate(int i, bool clockwise) => Window.Rotate(i, clockwise);
    }
}
