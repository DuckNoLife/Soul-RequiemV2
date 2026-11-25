using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// CUTSCENE FAST-FORWARD (test tool - DevCheats F11 / Shift+F11, and turned on automatically when F4 / F10 jump to a boss / area).
    /// While skipping: the dialogue box (DialogueRunner) flips through all pages at once; <see cref="CutsceneSkipper"/> pushes timeScale to 12× until the scene ends
    /// (wait / walk / fade / slide / mechanism scenes all run on Time.deltaTime, so they speed up too). Flags, show/hide, exit... still all run.
    /// Players can skip too - hold Space / Y for 0.8 seconds during a cutscene (<see cref="CutsceneSkipper"/>).
    /// </summary>
    public static class CutsceneSkip
    {
        /// <summary>Shift+F11: always skip EVERY cutscene (not NPC dialogue / signs the player opened with T).</summary>
        public static bool Always;

        private static float until = -1f;

        /// <summary>A cutscene / dialogue is running.</summary>
        public static bool Playing => CutsceneState.Active || StoryScenes.Busy || DialogueBox.Active;

        /// <summary>Skipping: the skip window is still open (F11 / area jump) or always-skip mode is on and a cutscene is running.</summary>
        public static bool Now => Time.unscaledTime < until || (Always && (CutsceneState.Active || StoryScenes.Busy));

        /// <summary>Skip every scene that happens within the next `seconds` seconds (real time) - chained scenes are skipped too (DevCheats extends it while scenes remain).</summary>
        public static void For(float seconds) => until = Mathf.Max(until, Time.unscaledTime + seconds);

        public static void Stop() => until = -1f;
    }
}
