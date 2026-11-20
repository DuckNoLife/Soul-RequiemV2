namespace SoulRequiem.Core
{
    /// <summary>
    /// Xử lý chữ hiển thị trong game (dùng chung cho mọi giao diện: HUD, bảng ghi chú, menu).
    /// </summary>
    public static class TextUtil
    {
        /// <summary>
        /// Đổi ký tự mà phông pixel không có sang ký tự tương đương (— -> -, … -> ..., “ ” -> ")
        /// và bỏ ký tự đặc biệt của rich text (&lt; &gt; [ ] # &amp;). "\n" viết trong Tiled -> xuống dòng.
        /// </summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Replace('—', '-').Replace('–', '-').Replace("…", "...").Replace('’', '\'')
                    .Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Replace('×', 'x')
                    .Replace(">", "-").Replace("<", "-").Replace("[", "(").Replace("]", ")").Replace("#", "No.")
                    .Replace("&", "and").Replace("\\n", "\n");
        }
    }
}
