using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Undertale-style DIALOGUE BLIPS + narration sounds. Shared by every map, created automatically at runtime.
    /// Every frame it reads the line being shown (dialogue box <see cref="DialogueBox"/> or subtitles <see cref="Subtitles"/>):
    ///   - Each LETTER that appears -> one blip of the speaker (SfxHooks.BlipId: Ren, Todd, Pell, goblin, Grok, boss...; narration / writing -> bl_narrate).
    ///     Spaces / punctuation are skipped (Undertale style: a beat of silence at a full stop). Minimum gap per voice (big voices are slower).
    ///   - Dialogue box: counts the letters ACTUALLY shown (DialogueBox.Shown) -> matches the line length exactly.
    ///     Skipping (text jumps to full) -> silent at once, no leftover blips. Subtitles: counted with the HUD's typewriter formula (70 chars / s).
    ///   - Menu open -> silent. Each new narration page -> page-turn sound "cs_page"; a narration hint line (Hints) appearing mid-cutscene -> "cs_page".
    /// Played with Sfx.Play2D (short 20-80 ms blips, can overlap), volume from sfx_mix.json (bl_* ~0.3 - tier 2).
    /// </summary>
    public sealed class DialogueVoice : MonoBehaviour
    {
        private const float SubCps = 70f;      // matches RequiemHUD (subtitles type ~70 characters / second)
        private const float DefaultGap = 0.055f;

        /// <summary>Minimum gap between 2 blips per voice (seconds). Voices not listed here = DefaultGap.</summary>
        private static readonly Dictionary<string, float> Gap = new Dictionary<string, float>
        {
            { "bl_grok", 0.09f }, { "bl_ent", 0.1f }, { "bl_lantern_knight", 0.075f }, { "bl_morrow", 0.075f }, { "bl_aldous", 0.07f },
            { "bl_elder", 0.07f }, { "bl_hooded", 0.075f }, { "bl_goblin", 0.045f }, { "bl_goblin_child", 0.05f }, { "bl_narrate", 0.04f },
        };

        private static DialogueVoice inst;
        private string lineKey = "", hintSeen = "";
        private int lastShown;
        private float lastBlip;
        private bool skipped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => inst = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (inst != null || !Application.isPlaying) return;
            var go = new GameObject("SR_DialogueVoice");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<DialogueVoice>();
        }

        private void OnDestroy()
        {
            if (inst == this) inst = null;
        }

        private void Update()
        {
            Narration();

            string key = null, speaker = "", line = "";
            bool box = false;
            int shown = 0;
            if (DialogueBox.Active)
            {
                box = true;
                key = "box:" + DialogueBox.OpenedFrame + ":" + DialogueBox.Index;
                speaker = DialogueBox.Speaker;
                line = DialogueBox.Line;
                shown = Mathf.Clamp(DialogueBox.Shown, 0, line.Length);
            }
            else if (Subtitles.Visible)
            {
                key = "sub:" + Subtitles.Serial;
                speaker = Subtitles.Speaker;
                if (string.IsNullOrEmpty(speaker)) speaker = Subtitles.Voice;          // line without a speaker name but with a voice (opening scene)
                line = Subtitles.Text;
                shown = Mathf.Clamp(Mathf.FloorToInt((Time.time - Subtitles.Born) * SubCps), 0, line.Length);
            }
            if (key == null) { lineKey = ""; return; }

            if (key != lineKey)
            {
                lineKey = key;
                lastShown = 0;
                skipped = false;
                if (box && string.IsNullOrEmpty(speaker)) Sfx.Play2D("cs_page");   // new narration page
            }
            if (shown <= lastShown) return;
            int from = lastShown;
            lastShown = shown;
            // skipped: text jumped to full within one frame (> 6 letters) -> silence the rest of the line
            if (box && shown - from > 6 && shown >= line.Length) skipped = true;
            if (skipped || GameInput.MenuOpen) return;

            bool letter = false;
            for (int i = from; i < shown; i++)
                if (char.IsLetterOrDigit(line[i])) { letter = true; break; }
            if (!letter) return;

            string id = SfxHooks.BlipId(speaker, box);
            if (string.IsNullOrEmpty(id)) return;
            float now = Time.unscaledTime;
            if (now - lastBlip < (Gap.TryGetValue(id, out float g) ? g : DefaultGap)) return;
            lastBlip = now;
            Sfx.Play2D(id);
        }

        /// <summary>A new narration hint line (Hints) appears mid-cutscene -> narration page-turn sound.</summary>
        private void Narration()
        {
            string h = Hints.Visible ? Hints.Text : "";
            if (h == hintSeen) return;
            hintSeen = h;
            if (h.Length > 0 && CutsceneState.Active && !DialogueBox.Active) Sfx.Play2D("cs_page");
        }
    }
}
