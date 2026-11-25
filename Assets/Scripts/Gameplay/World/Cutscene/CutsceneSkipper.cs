using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PLAYER CUTSCENE SKIP.
    /// Hold Space (gamepad: Y / △) for 0.8 seconds during a cutscene -> skip it: dialogue flips pages at once, time runs at 12× until the scene ends
    /// (same mechanism as <see cref="CutsceneSkip"/> used by the F11 test tool - flags, show/hide, rewards, exits... all still run, just faster).
    /// Hold (not press) to avoid accidents: Space also advances dialogue pages.
    /// No skipping while: a choice card (Spare / Kill...) is open - skipping pauses and resumes after the choice; a key tutorial screen; the pause menu.
    /// NPC dialogue / signs the player opened with T do not count as cutscenes (no skip prompt).
    /// The HUD (RequiemHUD) draws the "Hold SPACE to skip" prompt + progress bar from <see cref="Hold01"/>.
    /// This is the ONLY place that sets timeScale while skipping.
    /// </summary>
    public sealed class CutsceneSkipper : MonoBehaviour
    {
        public const float HoldTime = 0.8f;

        /// <summary>Hold progress 0..1 (HUD draws the bar).</summary>
        public static float Hold01 { get; private set; }
        /// <summary>Skipping (timeScale is pushed up).</summary>
        public static bool Skipping { get; private set; }
        /// <summary>A cutscene is running (not counting dialogue / signs opened by the player).</summary>
        public static bool Cutscene => CutsceneState.Active || StoryScenes.Busy;
        /// <summary>Can the skip key be held right now.</summary>
        public static bool CanSkip => Cutscene && !ChoiceCard.Active && !TutorialPrompt.Active && !GameInput.MenuOpen;
        /// <summary>Should the HUD show the skip prompt (a scene is running and not skipping yet).</summary>
        public static bool Offer => CanSkip && !CutsceneSkip.Now;

        /// <summary>A gamepad is connected (the HUD adds "/ Y").</summary>
        public static bool PadConnected => Gamepad.current != null;

        private const float FastScale = 12f;
        private static bool armed;
        private static float prevScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<CutsceneSkipper>() != null) return;
            var g = new GameObject("CutsceneSkipper");
            DontDestroyOnLoad(g);
            g.AddComponent<CutsceneSkipper>();
        }

        private static bool HeldNow()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.isPressed) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonNorth.isPressed;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool can = CanSkip && !CutsceneSkip.Now;
            bool held = HeldNow();
            // must be PRESSED during the scene (Space already held before the scene started - e.g. a roll - does not count)
            if (!Cutscene) armed = false;
            else if (!held) armed = true;
            if (can && armed && held) Hold01 = Mathf.Min(1f, Hold01 + dt / HoldTime);
            else Hold01 = Mathf.Max(0f, Hold01 - dt * 3f);                       // released: the bar shrinks back
            if (can && Hold01 >= 1f)
            {
                Hold01 = 0f;
                CutsceneSkip.For(1.5f);
                Sfx.Play2D("ui_move");
            }
            Tick();
        }

        /// <summary>Skipping and a scene is running -> 12× (extends the skip window so chained scenes are skipped too); no scene -> restore the old speed.
        /// </summary>
        private static void Tick()
        {
            bool playing = CutsceneSkip.Playing;
            if (CutsceneSkip.Now && playing) CutsceneSkip.For(0.6f);
            bool fast = CutsceneSkip.Now && playing && Time.timeScale > 0f
                        && !TutorialPrompt.Active && !ChoiceCard.Active && !GameInput.MenuOpen;
            if (fast)
            {
                if (!Skipping)
                {
                    // speed before skipping (DevCheats may be running at 2×/4×); a 12× left over from the pause menu counts as 1×
                    prevScale = Time.timeScale >= FastScale - 0.1f ? 1f : Time.timeScale;
                    Skipping = true;
                }
                Time.timeScale = FastScale;
            }
            else if (Skipping)
            {
                Skipping = false;
                if (Time.timeScale > 0f) Time.timeScale = prevScale;
            }
        }

        private void OnDisable()
        {
            if (Skipping) { Skipping = false; if (Time.timeScale > 0f) Time.timeScale = prevScale; }
            Hold01 = 0f;
        }
    }
}
