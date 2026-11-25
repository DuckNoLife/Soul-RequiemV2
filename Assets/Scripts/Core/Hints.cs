using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// HINT LINE in the middle of the screen ("The gate is open", gravestone text...). Gameplay calls Show; the HUD (RequiemHUD) reads Text / Visible to draw it.
    ///   Hints.Show("The bell tolls.", 1.5f);   — key tokens {roll}, {heal}... are replaced automatically (GameInput.ExpandKeys).
    /// Long text stays longer (at least ~16 characters / second). MapEncounterDirector calls Clear when a new scene starts.
    /// </summary>
    public static class Hints
    {
        private const float CharsPerSecond = 16f;

        /// <summary>Text currently shown ("" = none).</summary>
        public static string Text { get; private set; } = "";
        /// <summary>Time.time at which the hint expires (the HUD uses it to fade out).</summary>
        public static float Until { get; private set; }
        public static bool Visible => !string.IsNullOrEmpty(Text) && Time.time < Until;

        /// <summary>Show text for `seconds` (replaces the previous hint). Empty strings are ignored.</summary>
        public static void Show(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            Text = GameInput.ExpandKeys(text);
            Until = Time.time + Mathf.Max(seconds, Text.Length / CharsPerSecond);
        }

        public static void Clear()
        {
            Text = "";
            Until = 0f;
        }
    }
}
