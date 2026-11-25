using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Mid-game CHOICE CARD (an irreversible decision, e.g. spare / kill a boss). Static data - drawn by RequiemHUD:
    /// title + subtitle, 2-3 cards (choice name, lead line, GAIN "+" and LOSS "−" lines), the selected card is enlarged with a glowing border.
    /// Controls: A / D (← / →, left stick) to select; [Interact] (T) or Enter to confirm; mouse: hover to select, click to confirm.
    /// The ATTACK key does not confirm (the player is often still mashing slash as the boss falls) + the first 0.9 s are locked against misclicks.
    /// The game is NOT paused (the running scene keeps everything still).
    /// </summary>
    public static class ChoiceCard
    {
        public sealed class Option
        {
            public string title, tagline;
            public string[] gains = new string[0], losses = new string[0];
            public Color color = Color.white;
        }

        public static bool Active { get; private set; }
        public static string Title = "", Subtitle = "";
        public static Option[] Options = new Option[0];
        public static int Selected;
        public static float OpenedAt = -99f, ChosenAt = -99f;
        public static int Chosen = -1;
        /// <summary>Confirm lock time right after opening (seconds).</summary>
        public const float Lock = 0.9f;

        public static bool CanConfirm => Active && Time.unscaledTime - OpenedAt >= Lock;

        /// <summary>Mouse hovers card i (called by the HUD).</summary>
        public static void Hover(int i) { if (Active && i >= 0 && i < Options.Length && i != Selected) { Selected = i; Sfx.Play2D("ui_move"); } }

        /// <summary>Mouse clicks card i (called by the HUD).</summary>
        public static void Click(int i) { if (CanConfirm && i >= 0 && i < Options.Length) { Selected = i; Chosen = i; } }

        /// <summary>Open the card and wait for the player's choice; calls `done(index)` when finished.</summary>
        public static IEnumerator Ask(string title, string subtitle, Option[] options, Action<int> done, int initial = 0)
        {
            Title = title ?? ""; Subtitle = subtitle ?? "";
            Options = options ?? new Option[0];
            Selected = Mathf.Clamp(initial, 0, Mathf.Max(0, Options.Length - 1));
            Chosen = -1;
            OpenedAt = Time.unscaledTime;
            Active = true;
            float nextStick = 0f;
            try
            {
                while (Chosen < 0)
                {
                    var kb = Keyboard.current;
                    int step = 0;
                    if (kb != null)
                    {
                        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) step = -1;
                        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) step = 1;
                    }
                    var pad = Gamepad.current;
                    if (pad != null && Time.unscaledTime >= nextStick)
                    {
                        float x = pad.leftStick.ReadValue().x + pad.dpad.ReadValue().x;
                        if (Mathf.Abs(x) > 0.6f) { step = x > 0f ? 1 : -1; nextStick = Time.unscaledTime + 0.28f; }
                    }
                    if (step != 0 && Options.Length > 1)
                    {
                        Selected = (Selected + step + Options.Length) % Options.Length;
                        Sfx.Play2D("ui_move");
                    }
                    bool confirm = GameInput.PressedRaw(GameAction.Interact)
                                   || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                                   || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                    if (confirm && CanConfirm) Chosen = Selected;
                    yield return null;
                }
                ChosenAt = Time.unscaledTime;
                Sfx.Play2D("ui_confirm");
                yield return new WaitForSecondsRealtime(0.35f);                   // the HUD flashes the chosen card, then closes
            }
            finally { Active = false; }
            done?.Invoke(Chosen);
        }
    }
}
