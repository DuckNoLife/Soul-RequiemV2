using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Dialogue box (replaces the faint line at the bottom of the screen when talking to NPCs / reading signs, letters).
    /// DialogueBox.Open("Todd", new[] { "line 1", "line 2" }, onClose) - DialogueRunner handles the typewriter text and the [T]/Space/Enter/left mouse keys
    /// (show the whole line -> next line -> close), and locks movement while open. The HUD (RequiemHUD) draws from the fields below.
    /// </summary>
    public static class DialogueBox
    {
        public static bool Active { get; private set; }
        public static string Speaker { get; private set; } = "";
        public static string[] Lines { get; private set; } = new string[0];
        public static int Index { get; private set; }
        /// <summary>Number of characters of the current line shown so far (typewriter).</summary>
        public static int Shown { get; internal set; }
        public static string Line => Index >= 0 && Index < Lines.Length ? Lines[Index] : "";
        public static bool LineDone => Shown >= Line.Length;
        public static bool HasMore => Index < Lines.Length - 1;

        /// <summary>&gt; 0 = auto-advance this many seconds after the line finishes typing (cutscenes: Grok speaking); pressing still skips ahead.</summary>
        public static float AutoSeconds { get; private set; }
        private static Action onClose;
        internal static int OpenedFrame;

        public static void Open(string speaker, string[] lines, Action closed = null, float autoSeconds = 0f)
        {
            if (lines == null || lines.Length == 0) return;
            if (Active) Close();
            Speaker = speaker ?? ""; Lines = lines; Index = 0; Shown = 0; AutoSeconds = autoSeconds;
            onClose = closed; Active = true; OpenedFrame = Time.frameCount;
            DialogueRunner.Ensure();
        }

        internal static void Next()
        {
            if (Speaker.Length > 0) SoulRequiem.Core.Sfx.Play2D("dialog_next");   // sound; narration already has a page-turn sound (DialogueVoice) //
            if (!LineDone) { Shown = Line.Length; return; }
            if (HasMore) { Index++; Shown = 0; return; }
            Close();
        }

        public static void Close()
        {
            if (!Active) return;
            Active = false;
            var c = onClose; onClose = null;
            c?.Invoke();
        }

        /// <summary>Split long text into pages at '|' (used for notice boards / letters).</summary>
        public static string[] Pages(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            var parts = text.Split('|');
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            return Array.FindAll(parts, p => p.Length > 0);
        }
    }
}
