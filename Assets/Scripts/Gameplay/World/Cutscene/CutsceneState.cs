using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// State shared between cutscenes (Gameplay) and the HUD (UI).
    /// Begin/End: during a cutscene the HUD fades out (HP, skills, flask, minimap, quests), keeping only the hint strip + boss title card.
    /// </summary>
    public static class CutsceneState
    {
        private static int depth;
        public static bool Active => depth > 0;
        public static void Begin() => depth++;
        public static void End() => depth = Mathf.Max(0, depth - 1);
        public static void Reset() => depth = 0;
    }

    /// <summary>
    /// Tutorial pause screen (game frozen, key blinking, waiting for the player to press the right key). The HUD draws from these fields.
    /// </summary>
    public static class TutorialPrompt
    {
        public static bool Active { get; private set; }
        public static string Title = "", Keys = "", Sub = "";

        public static void Show(string title, string keys, string sub)
        {
            Title = title; Keys = keys; Sub = sub; Active = true;
        }

        public static void Hide() => Active = false;

        /// <summary>Freeze the game (timeScale 0), the HUD blinks the key; wait for the right key, then resume and perform that action toward `dir`.
        /// Pause menu opened midway: keys ignored; closing the menu (timeScale back to 1) freezes again. Runs via StartCoroutine.</summary>
        public static IEnumerator Run(PlayerCombat combat, GameAction a, string title, string keys, string sub, Vector2 dir)
        {
            Show(title, keys, sub);
            Time.timeScale = 0f;
            yield return null;                                       // ignore the key from the very frame that froze the game
            while (true)
            {
                GameInput.Blocked = true;
                if (!GameInput.MenuOpen)
                {
                    Time.timeScale = 0f;
                    if (GameInput.PressedRaw(a)) break;
                }
                yield return null;
            }
            Hide();
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            if (combat != null) combat.QueueAction(a, dir);
        }
    }
}
