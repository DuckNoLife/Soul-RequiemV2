using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// Dòng gợi ý / lời dẫn hiện giữa màn hình (vd "The gate is open", lời đọc bia, tên boss).
    /// Gameplay gọi <see cref="Show"/>; HUD (assembly UI) đọc <see cref="Text"/> / <see cref="Visible"/> để vẽ.
    /// Nhờ vậy code gameplay không cần biết giao diện là gì.
    ///
    ///   Hints.Show("The bell tolls.", 1.5f);
    ///
    /// Mỗi lần vào scene mới, MapEncounterDirector gọi <see cref="Clear"/>.
    /// </summary>
    public static class Hints
    {
        /// <summary>Nội dung đang hiện ("" = không có).</summary>
        public static string Text { get; private set; } = "";

        /// <summary>Mốc Time.time lúc gợi ý hết hạn (HUD dùng để mờ dần).</summary>
        public static float Until { get; private set; }

        /// <summary>Đang có gợi ý còn hạn.</summary>
        public static bool Visible => !string.IsNullOrEmpty(Text) && Time.time < Until;

        /// <summary>
        /// Hiện <paramref name="text"/> trong <paramref name="seconds"/> giây (thay gợi ý cũ). Chuỗi rỗng bị bỏ qua.
        /// Token phím ({roll}, {heal}...) được thay bằng phím đang gán — xem <see cref="GameInput.ExpandKeys"/>.
        /// </summary>
        public static void Show(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            Text = GameInput.ExpandKeys(text);
            // 04/11: chữ dài (nhặt cổ vật, sách...) đọc không kịp -> tối thiểu ~16 ký tự / giây
            Until = Time.time + Mathf.Max(seconds, Text.Length / 16f);
        }

        /// <summary>Tắt gợi ý ngay.</summary>
        public static void Clear()
        {
            Text = "";
            Until = 0f;
        }
    }
}
