using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// SfxRunner's dedicated channels (besides the 24 effect channels):
    ///   • SCENE AMBIENCE — table <see cref="AmbienceFor"/> (scene name -> id). Cross-fades over AmbienceFade seconds; drops to 45 % while paused.
    ///   • VOICE — mumble following dialogue lines (DialogueVoice calls Sfx.Speech every frame), chains random takes, stops instantly.
    ///   • NARRATION BED — loops while a cutscene picture is shown (Sfx.Bed), fades in 1.2 s / out 0.9 s.
    ///   • SPARE LISTENER — a scene without an AudioListener (e.g. Title) gets one here; switched off when the scene has one (never 2 listeners).
    /// </summary>
    public sealed partial class SfxRunner
    {
        private const float AmbienceGain = 0.5f, AmbienceFade = 1.6f, AmbiencePausedLevel = 0.45f;
        private const float BedFadeIn = 1.2f, BedFadeOut = 0.9f, SpeechCut = 0.07f;
        private const float EarCheckEvery = 0.25f;

        private AudioSource ambA, ambB;                // ambA = fading in, ambB = fading out
        private string ambId;
        private float ambGainA = 1f, ambGainB = 1f, ambT = 1f, ambDuck = 1f;

        private AudioSource speech;
        private string speechId;
        private float speechFade, speechGapUntil;
        private AudioClip speechLast;

        private AudioSource bed;
        private string bedId;
        private float bedT;

        private AudioListener spareEar;
        private float nextEarCheck;

        private void CreateChannels()
        {
            ambA = NewSource("amb_a"); ambA.loop = true;
            ambB = NewSource("amb_b"); ambB.loop = true;
            speech = NewSource("speech");
            bed = NewSource("story_bed"); bed.loop = true;
        }

        // ================================================================== ambience
        /// <summary>Ambience by scene name. New scene: add one row (longer prefixes first, e.g. "Map1b" before "Map1").</summary>
        private static string AmbienceFor(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            if (n.StartsWith("Map1b")) return "amb_hollow";
            if (n.StartsWith("Map1")) return "amb_plains";
            if (n.StartsWith("Map2b")) return "amb_cistern";
            if (n.StartsWith("Map2")) return "amb_sewer";
            if (n.StartsWith("Map3")) return "amb_crypt";
            if (n.StartsWith("Map4n")) return "amb_void";
            if (n.StartsWith("Map4")) return "amb_catacombs";
            if (n == "Title") return "amb_title";
            return null;
        }

        internal void SetAmbience(string id)
        {
            if (id == ambId) return;
            ambId = id;
            (ambA, ambB) = (ambB, ambA);               // old take moves to ambB to fade out
            ambGainB = ambGainA;
            ambGainA = string.IsNullOrEmpty(id) ? 1f : Sfx.GainOf(id);
            ambA.Stop();
            ambA.clip = string.IsNullOrEmpty(id) ? null : Sfx.Pick(id);
            ambA.volume = 0f;
            if (ambA.clip != null) ambA.Play();
            ambT = 0f;
        }

        private void UpdateAmbience()
        {
            float dt = Time.unscaledDeltaTime;
            ambT = Mathf.Min(1f, ambT + dt / AmbienceFade);
            ambDuck = Mathf.MoveTowards(ambDuck, worldPaused ? AmbiencePausedLevel : 1f, dt * 2f);
            float g = Mathf.Clamp01(GameSettings.Sfx) * AmbienceGain * ambDuck;
            ambA.volume = g * ambGainA * ambT;
            ambB.volume = g * ambGainB * (1f - ambT);
            if (ambT >= 1f && ambB.isPlaying) ambB.Stop();
        }

        // ================================================================== voice
        /// <summary>id (e.g. vo_pell) = keep talking / switch speaker; null = silent at once. Call EVERY FRAME.</summary>
        internal void SetSpeech(string id)
        {
            if (string.IsNullOrEmpty(id) || !Sfx.Has(id)) id = null;
            if (id == speechId) return;
            if (id != null && speechId != null) speech.Stop();     // speaker changed: cut the old one
            speechId = id;
            if (id != null) { speechFade = 1f; speechGapUntil = 0f; }
        }

        private void UpdateSpeech()
        {
            if (speechId == null)
            {
                if (!speech.isPlaying) return;
                speechFade -= Time.unscaledDeltaTime / SpeechCut;
                if (speechFade <= 0f) { speech.Stop(); speechFade = 0f; }
                else speech.volume = Mathf.Clamp01(GameSettings.Sfx) * Sfx.GainOf(speech.clip != null ? Sfx.BaseId(speech.clip.name) : "") * speechFade;
                return;
            }
            if (!speech.isPlaying && Time.unscaledTime >= speechGapUntil)
            {
                AudioClip c = null;                                    // next take: avoid repeating the one just played
                for (int i = 0; i < 3 && (c == null || c == speechLast); i++) c = Sfx.Pick(speechId);
                if (c != null)
                {
                    speechLast = c;
                    speech.clip = c;
                    speech.pitch = 1f + Random.Range(-0.04f, 0.04f);
                    speech.Play();
                    speechGapUntil = Time.unscaledTime + c.length / Mathf.Max(0.5f, speech.pitch) + Random.Range(0.04f, 0.14f);
                }
            }
            speech.volume = Mathf.Clamp01(GameSettings.Sfx) * Sfx.GainOf(speechId);
        }

        // ================================================================== narration bed
        /// <summary>id = on (looping), null = fade out.</summary>
        internal void SetBed(string id)
        {
            if (string.IsNullOrEmpty(id) || !Sfx.Has(id)) id = null;
            if (id == bedId) return;
            bedId = id;
            if (id == null) return;
            var c = Sfx.Pick(id);
            if (bed.clip != c || !bed.isPlaying) { bed.clip = c; bed.volume = 0f; bed.Play(); bedT = 0f; }
        }

        private void UpdateBed()
        {
            if (!bed.isPlaying) return;
            float g = Mathf.Clamp01(GameSettings.Sfx) * Sfx.GainOf(bed.clip != null ? Sfx.BaseId(bed.clip.name) : "");
            float dt = Time.unscaledDeltaTime;
            bedT = bedId != null ? Mathf.Min(1f, bedT + dt / BedFadeIn) : Mathf.Max(0f, bedT - dt / BedFadeOut);
            bed.volume = g * bedT;
            if (bedId == null && bedT <= 0f) bed.Stop();
        }

        // ================================================================== spare listener
        private void UpdateEar()
        {
            if (Time.unscaledTime < nextEarCheck) return;
            nextEarCheck = Time.unscaledTime + EarCheckEvery;
            CheckEar();
        }

        private void CheckEar()
        {
            if (spareEar == null) { spareEar = gameObject.AddComponent<AudioListener>(); spareEar.enabled = false; }
            bool other = false;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l != spareEar && l.isActiveAndEnabled) { other = true; break; }
            if (spareEar.enabled == other) spareEar.enabled = !other;
        }
    }
}
