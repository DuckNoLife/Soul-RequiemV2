namespace SoulRequiem.Core
{
    /// <summary>
    /// In-game text processing (shared by every UI: HUD, note panels, menus).
    /// </summary>
    public static class TextUtil
    {
        /// <summary>
        /// Replace characters missing from the pixel font with equivalents (— -> -, … -> ..., “ ” -> ")
        /// and strip rich-text special characters (&lt; &gt; [ ] # &amp;). "\n" written in Tiled -> line break.
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
