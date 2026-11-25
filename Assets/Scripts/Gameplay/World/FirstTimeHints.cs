using System.Collections.Generic;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Hints shown only the FIRST time (death, flashing wind-up attack, red ring, poisoned...).
    /// Remembered with the map world flags (MapEncounterDirector.Flags) when available, otherwise for the play session.
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
