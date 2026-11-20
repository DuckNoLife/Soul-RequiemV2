using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: khung hội thoại (thay dòng chữ mờ ở đáy màn hình khi nói chuyện với NPC / đọc bảng, thư).
    /// DialogueBox.Open("Todd", new[] { "câu 1", "câu 2" }, onClose) — DialogueRunner lo chữ chạy, phím [T]/Space/Enter/chuột trái
    /// (hiện hết câu -> câu sau -> đóng), khoá di chuyển khi đang mở. HUD (RequiemHUD) vẽ theo các trường dưới.
    /// </summary>
    public static class DialogueBox
    {
        public static bool Active { get; private set; }
        public static string Speaker { get; private set; } = "";
        public static string[] Lines { get; private set; } = new string[0];
        public static int Index { get; private set; }
        /// <summary>Số ký tự đang hiện của câu hiện tại (chữ chạy).</summary>
        public static int Shown { get; internal set; }
        public static string Line => Index >= 0 && Index < Lines.Length ? Lines[Index] : "";
        public static bool LineDone => Shown >= Line.Length;
        public static bool HasMore => Index < Lines.Length - 1;

        /// <summary>02/11: &gt; 0 = tự sang câu sau khi chữ chạy xong được ngần này giây (cảnh diễn: Grok nói); vẫn bấm để tua được.</summary>
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
            if (Speaker.Length > 0) SoulRequiem.Core.Sfx.Play2D("dialog_next");   // 17/11 (§294): âm; lời kể đã có tiếng lật trang (DialogueVoice) // 19/11 (§336)
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

        /// <summary>Tách văn bản dài thành các trang theo '|' (dùng cho bảng tin / thư).</summary>
        public static string[] Pages(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            var parts = text.Split('|');
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            return Array.FindAll(parts, p => p.Length > 0);
        }
    }
}
