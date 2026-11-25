using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Achievements (placeholder): saves PlayerPrefs "ach_&lt;id&gt;" = 1 and announces it on screen.
    /// To be replaced by a real achievement system (list, icons, Steam...) later — only the Unlock/Has API needs to stay.
    /// </summary>
    public static class Achievements
    {
        public static event System.Action<string, string> Unlocked;

        public static bool Has(string id) => PlayerPrefs.GetInt("ach_" + id, 0) == 1;

        public static void Unlock(string id, string title)
        {
            bool first = !Has(id);
            if (first)
            {
                PlayerPrefs.SetInt("ach_" + id, 1);
                PlayerPrefs.Save();
            }
            Debug.Log($"[Achievement] {id}: {title}{(first ? " (new)" : "")}");
            Hints.Show("ACHIEVEMENT UNLOCKED: " + title, 6f);
            Unlocked?.Invoke(id, title);
        }
    }
}
