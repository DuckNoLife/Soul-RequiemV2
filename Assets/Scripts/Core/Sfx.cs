using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// SOUND EFFECTS — the commands called from gameplay / UI. The actual player: <see cref="SfxRunner"/>.
    ///   Files     : Resources/Audio/SFX/&lt;id&gt;.ogg, variants &lt;id&gt;_1, &lt;id&gt;_2... (one picked at random each time). Missing file -> silent, no error.
    ///   Volume    : GameSettings.Sfx × gain of the id. TUNE IT IN Resources/Audio/sfx_mix.json ({"sounds":[{"id","gain","gap"}]});
    ///               the Gain / MinGap tables below are only defaults for ids missing from the json.
    ///   Position  : fades with distance to the camera (Near -> Far), panned left / right by x; a zone other than the camera's zone is not heard (ZoneOf).
    ///   Anti-spam : each id at least MinGap seconds apart, at most a few voices at once.
    /// Calls:
    ///   Sfx.Play(id, position)          sound at a point
    ///   Sfx.PlayAt(id, transform)       sound following a moving object
    ///   Sfx.Play2D(id)                  UI sound (no position, still plays while paused)
    ///   Sfx.Loop(key, id, transform)    looping sound on an object: CALL EVERY FRAME while it runs, stops by itself 0.3 s after the last call (or StopLoop)
    ///   Sfx.Ambience(id) / Speech(id) / Bed(id)   map ambience / dialogue mumble voice / cutscene narration bed
    /// </summary>
    public static class Sfx
    {
        /// <summary>Clear within Near (world units), silent at Far.</summary>
        public const float Near = 3.5f, Far = 11f;
        public const string Folder = "Audio/SFX";

        /// <summary>Point -> id of the smallest zone containing it (null = outside every zone). Set by MapEncounterDirector; null = distance falloff only.</summary>
        public static System.Func<Vector2, string> ZoneOf;
        /// <summary>One side inside a zone, the other outside (corridor) -> this much volume remains.</summary>
        public const float OutsideBleed = 0.4f;

        private const float DefaultGap = 0.05f;

        private static readonly Dictionary<string, float> Gain = new Dictionary<string, float>
        {
            { "footstep", 0.22f }, { "pl_swing", 0.7f }, { "en_swing", 0.55f }, { "hit_flesh", 0.8f }, { "hit_armor", 0.75f },
            { "trap_spikes", 0.55f }, { "trap_saw", 0.5f }, { "trap_roller", 0.85f }, { "trap_plate", 0.7f },
            { "ui_move", 0.45f }, { "ui_confirm", 0.55f }, { "ui_back", 0.5f }, { "ui_open", 0.55f }, { "dialog_next", 0.4f },
            { "quest_update", 0.6f }, { "checkpoint", 0.7f }, { "proj_magic", 0.6f }, { "arrow_shot", 0.7f },
        };

        private static readonly Dictionary<string, float> MinGap = new Dictionary<string, float>
        {
            { "footstep", 0.12f }, { "trap_spikes", 0.2f }, { "trap_plate", 0.3f }, { "hit_flesh", 0.04f }, { "en_swing", 0.08f },
            { "ui_move", 0.04f }, { "checkpoint", 0.6f }, { "quest_update", 1f }, { "boss_intro", 2f }, { "st_bleed", 0.15f },
        };

        private static Dictionary<string, AudioClip[]> clips;
        private static readonly Dictionary<string, float> lastAt = new Dictionary<string, float>();

        // ================================================================== play commands
        public static void Play(string id, Vector2 pos, float vol = 1f) => SfxRunner.Get()?.Play(id, pos, null, false, vol);

        public static void PlayAt(string id, Transform follow, float vol = 1f)
        {
            if (follow == null) return;
            SfxRunner.Get()?.Play(id, follow.position, follow, false, vol);
        }

        public static void Play2D(string id, float vol = 1f) => SfxRunner.Get()?.Play(id, Vector2.zero, null, true, vol);

        public static void Loop(object key, string id, Transform follow, float vol = 1f)
        {
            if (key == null || follow == null) return;
            SfxRunner.Get()?.Loop(key, id, follow, vol);
        }

        public static void StopLoop(object key)
        {
            if (key == null) return;
            SfxRunner.Get()?.StopLoop(key);
        }

        public static void Ambience(string id) => SfxRunner.Get()?.SetAmbience(id);
        /// <summary>Current speaking voice — call EVERY FRAME while the text is still typing; null = silent at once.</summary>
        public static void Speech(string id) => SfxRunner.Get()?.SetSpeech(id);
        /// <summary>Looping narration bed while a cutscene picture is shown; null = fade out.</summary>
        public static void Bed(string id) => SfxRunner.Get()?.SetBed(id);

        // ================================================================== sound library
        public static bool Has(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureLoaded();
            return clips.ContainsKey(id);
        }

        internal static AudioClip Pick(string id)
        {
            if (!Has(id)) return null;
            var a = clips[id];
            return a[Random.Range(0, a.Length)];
        }

        internal static float GainOf(string id) => Gain.TryGetValue(id, out float g) ? g : 1f;

        /// <summary>true = may play (MinGap since the last play of the same id has passed).</summary>
        internal static bool Throttle(string id)
        {
            float now = Time.unscaledTime, gap = MinGap.TryGetValue(id, out float g) ? g : DefaultGap;
            if (lastAt.TryGetValue(id, out float t) && now - t < gap) return false;
            lastAt[id] = now;
            return true;
        }

        /// <summary>"hit_flesh_2" -> "hit_flesh"; names without a number suffix stay as they are.</summary>
        internal static string BaseId(string clipName)
        {
            int u = clipName.LastIndexOf('_');
            return u > 0 && int.TryParse(clipName.Substring(u + 1), out _) ? clipName.Substring(0, u) : clipName;
        }

        /// <summary>First use: load every clip in Resources/Audio/SFX, grouped by base id; read sfx_mix.json.</summary>
        private static void EnsureLoaded()
        {
            if (clips != null) return;
            LoadMix();
            var groups = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>(Folder))
            {
                if (c == null) continue;
                string key = BaseId(c.name);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            clips = new Dictionary<string, AudioClip[]>();
            foreach (var kv in groups) clips[kv.Key] = kv.Value.ToArray();
        }

        [System.Serializable] private class MixEntry { public string id; public float gain = -1f; public float gap = -1f; }
        [System.Serializable] private class MixFile { public MixEntry[] sounds; }

        /// <summary>Resources/Audio/sfx_mix.json overrides Gain / MinGap (generated by _tools/sfx/manifest_v2.py --mix).</summary>
        private static void LoadMix()
        {
            var ta = Resources.Load<TextAsset>("Audio/sfx_mix");
            if (ta == null) return;
            MixFile mf = null;
            try { mf = JsonUtility.FromJson<MixFile>(ta.text); }
            catch (System.Exception e) { Debug.LogWarning("[Sfx] sfx_mix.json error: " + e.Message); }
            if (mf == null || mf.sounds == null) return;
            foreach (var m in mf.sounds)
            {
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                if (m.gain >= 0f) Gain[m.id] = m.gain;
                if (m.gap > 0f) MinGap[m.id] = m.gap;
            }
        }
    }
}
