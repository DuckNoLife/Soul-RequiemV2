using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Short character lines (formerly overhead SpeechBubble bubbles) now show in the DIALOGUE BOX AT THE BOTTOM OF THE SCREEN
    /// (same box as DialogueBox) but do NOT block control and close by themselves after the reading time. Applies to every SpeechBubble.Show call in the game.
    /// The HUD (RequiemHUD.UpdateDialogue) reads <see cref="Speaker"/> / <see cref="Text"/> / <see cref="Visible"/> to draw it.
    /// A new line replaces the old one (cutscenes already wait long enough between lines).
    /// </summary>
    public static class Subtitles
    {
        public static string Speaker { get; private set; } = "";
        public static string Text { get; private set; } = "";
        /// <summary>Speaker name colour (default = the box's default yellow).</summary>
        public static Color NameColor { get; private set; }
        public static float Born { get; private set; }
        public static float Until { get; private set; }
        /// <summary>Incremented for every new line - so an old bubble does not rename the speaker of a newer line.</summary>
        public static int Serial { get; private set; }
        public static bool Visible => Text.Length > 0 && Time.time < Until;
        /// <summary>Blip voice for a line WITHOUT a speaker name (e.g. villagers / goblins in the opening scene) - not shown in the box, only picks the sound.</summary>
        public static string Voice { get; private set; } = "";

        /// <summary>Show one line. Minimum time long enough to read (~15 characters / second).</summary>
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

        /// <summary>Set the speaker name for line <paramref name="serial"/> (if it is still the line showing and has no name yet).</summary>
        public static void SetSpeaker(int serial, string speaker)
        {
            if (serial == Serial && Speaker.Length == 0 && !string.IsNullOrEmpty(speaker)) Speaker = speaker;
        }

        public static void Clear() { Text = ""; Until = 0f; }

        /// <summary>Speaker display name from a Transform: Ren (has PlayerController) / "NPC Pell" -> Pell / "The_Registrar(Clone)" -> The Registrar.</summary>
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
