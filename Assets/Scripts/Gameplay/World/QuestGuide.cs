using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// TEMPORARY OBJECTIVE of the HUD quest panel - a running mechanism (e.g. the Burlap's Proving Grounds minigame) sets the title / target line /
    /// target point (arrow + distance + minimap dot) / progress n/N in place of the main quest step; Clear() restores the main quest.
    /// Read by QuestTracker.Evaluate (every 0.25 s).
    /// Sub-lines (Check) - a mechanism lists tasks / dodge tips right on the quest panel (Absalom fight). Set() clears the old lines.
    /// </summary>
    public static class QuestGuide
    {
        public static bool Active { get; private set; }
        public static string Title = "", Label = "";
        public static bool HasTarget;
        public static Vector3 World;
        public static int Done, Total;
        public static readonly List<QuestTracker.Check> Checks = new List<QuestTracker.Check>();

        public static void Set(string title, string label, Vector2? target, int done = 0, int total = 0)
        {
            Active = true; Title = title ?? ""; Label = label ?? "";
            HasTarget = target.HasValue;
            if (target.HasValue) World = target.Value;
            Done = done; Total = total;
            Checks.Clear();
        }

        /// <summary>Add a sub-line under the objective (call after Set). done = filled box; active = bright text (current task).</summary>
        public static void Check(string text, bool done = false, bool active = false)
        {
            if (!string.IsNullOrEmpty(text)) Checks.Add(new QuestTracker.Check { text = text, done = done, active = active });
        }

        public static void Clear() { Active = false; HasTarget = false; Checks.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = false; HasTarget = false; Checks.Clear(); }
    }
}
