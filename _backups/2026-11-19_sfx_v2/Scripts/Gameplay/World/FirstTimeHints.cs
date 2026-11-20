using System.Collections.Generic;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: gợi ý chỉ hiện LẦN ĐẦU (chết, đòn gồng nhấp nháy, vòng đỏ, trúng độc...).
    /// Nhớ bằng cờ thế giới của map (MapEncounterDirector.Flags) nếu có, không thì nhớ trong phiên chơi.
    /// </summary>
    public static class FirstTimeHints
    {
        private static readonly HashSet<string> shownThisSession = new HashSet<string>();

        public static void Show(string key, string text, float seconds = 5f)
        {
            var dir = MapEncounterDirector.Instance;
            if (dir != null && dir.Flags != null)
            {
                if (dir.Flags.HasAll(key)) return;
                dir.Flags.Set(key);
            }
            else if (!shownThisSession.Add(key)) return;
            Hints.Show(text, seconds);
        }
    }
}
