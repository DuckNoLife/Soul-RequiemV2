using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 17/11: lời nói ngắn của nhân vật (trước đây là bóng thoại trên đầu — SpeechBubble) nay hiện ở KHUNG HỘI THOẠI DƯỚI ĐÁY MÀN HÌNH
    /// (cùng khung với DialogueBox) nhưng KHÔNG chặn điều khiển và tự tắt sau thời gian đọc. Áp dụng cho mọi chỗ gọi SpeechBubble.Show trong game.
    /// HUD (RequiemHUD.UpdateDialogue) đọc <see cref="Speaker"/> / <see cref="Text"/> / <see cref="Visible"/> để vẽ.
    /// Câu mới thay câu cũ (cảnh diễn đã chờ đủ lâu giữa các câu).
    /// </summary>
    public static class Subtitles
    {
        public static string Speaker { get; private set; } = "";
        public static string Text { get; private set; } = "";
        /// <summary>Màu chữ tên người nói (default = màu vàng mặc định của khung).</summary>
        public static Color NameColor { get; private set; }
        public static float Born { get; private set; }
        public static float Until { get; private set; }
        /// <summary>Tăng mỗi câu mới — để bóng thoại cũ không đổi tên người nói của câu mới.</summary>
        public static int Serial { get; private set; }
        public static bool Visible => Text.Length > 0 && Time.time < Until;
        /// <summary>19/11 (§338): giọng bíp cho câu KHÔNG ghi tên người nói (vd dân / goblin trong cảnh mở đầu) — không hiện lên khung, chỉ để chọn tiếng.</summary>
        public static string Voice { get; private set; } = "";

        /// <summary>Hiện một câu. Thời gian tối thiểu đủ đọc (~15 ký tự / giây).</summary>
        public static int Say(string speaker, string text, float seconds, Color? nameColor = null, string voice = null)
        {
            if (string.IsNullOrEmpty(text)) return Serial;
            Speaker = speaker ?? "";
            Voice = voice ?? "";
            Text = text;
            NameColor = nameColor ?? default;
            Born = Time.time;
            Until = Time.time + Mathf.Max(seconds, 1.2f + text.Length / 15f);
            return ++Serial;
        }

        /// <summary>Đặt tên người nói cho câu <paramref name="serial"/> (nếu đó vẫn là câu đang hiện và chưa có tên).</summary>
        public static void SetSpeaker(int serial, string speaker)
        {
            if (serial == Serial && Speaker.Length == 0 && !string.IsNullOrEmpty(speaker)) Speaker = speaker;
        }

        public static void Clear() { Text = ""; Until = 0f; }

        /// <summary>Tên hiển thị của người nói từ Transform: Ren (có PlayerController) / "NPC Pell" -> Pell / "The_Registrar(Clone)" -> The Registrar.</summary>
        public static string NameOf(Transform t)
        {
            if (t == null) return "";
            if (t.GetComponentInParent<PlayerController>() != null) return "Ren";
            var e = t.GetComponentInParent<EnemyAI>();
            if (e != null && !string.IsNullOrEmpty(e.displayName) && e.displayName != "Enemy") return e.displayName;
            string n = t.name.Replace("(Clone)", "").Trim();
            if (n.StartsWith("NPC ")) n = n.Substring(4);
            return n.Replace('_', ' ').Trim();
        }
    }
}
