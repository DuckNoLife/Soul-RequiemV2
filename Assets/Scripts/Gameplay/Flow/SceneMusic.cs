using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// BACKGROUND MUSIC PICKER: one track per scene, one per boss. Runs by itself (GameObject "SR_SceneMusic"), checks every 0.1 s.
    ///   Scene tracks      : table <see cref="SceneTrack"/>. Scenes not in the table -> music left alone (Map4n: the Vessel fight plays its own).
    ///   Boss tracks       : table <see cref="BossTrack"/> (boss GameObject name prefix) + AbsalomTrack / BurlapTrack.
    ///   Volume + loops    : table <see cref="Mix"/> (measured with _tools/s453/music_scan.py; re-measure when a music file changes).
    /// Rules:
    ///   1. A scene track only starts once the opening is over (no dialogue / cutscene / scene change for QuietNeeded seconds).
    ///   2. A boss that has seen Ren + its name card gone -> boss track (Absalom: while fighting; Drill Yard: while a station runs). Boss over -> back to the scene track.
    ///   3. Dialogue / cutscene -> music off (Duck 0), back on afterwards; pausing menus 0.5; Ren down 0.35.
    /// Music files: Resources/Audio/Music/&lt;id&gt;.mp3 (missing file -> silent, warned once). Calls: Music (Core/Music.cs).
    /// </summary>
    public sealed class SceneMusic : MonoBehaviour
    {
        /// <summary>Scene name -> track id. (Map4m The Margin, Map4d Drill Yard: no scene track, boss music only.)</summary>
        private static readonly Dictionary<string, string> SceneTrack = new Dictionary<string, string>
        {
            { "Title", "bgm_title" },                       // spencer_yk — Time's Running Out (Pixabay 151012)
            { "Map1_GreystonePlains", "bgm_map1" },         // spencer_yk — Little Slime's Adventure (151007)
            { "Map2_UndercityOfVael", "bgm_map2" },         // leberch — Mystery (626023)
            { "Map3_ValeOfGraves", "bgm_map3" },            // spencer_yk — Witch's Dancing Doll (151008)
            { "Map4_CatacombsOfNaming", "bgm_map4" },       // The Forgotten Keep (AI, doc BGM E11)
            { "Map1b_HermitsHollow", "bgm_hollow" },        // damp corridors (AI, doc BGM E4)
            // 2b / 3b share: spencer_yk — Unknowns Down Below (151013)
            { "Map2b_KrovenDeep", "bgm_secret" },
            { "Map3b_KingsBarrow", "bgm_secret" },
        };

        /// <summary>Boss GameObject name prefix -> track id.</summary>
        private static readonly Dictionary<string, string> BossTrack = new Dictionary<string, string>
        {
            { "Brazen_Reliquary", "boss_reliquary" },       // bentomusic — Comic Style Music of Pranks and Silliness (427600)
            { "Hollow_Champion", "boss_reliquary" },        // (shared by The Reliquary Lists duo)
            { "The_Registrar", "boss_registrar" },
            // Map 1–3b bosses: AI-generated retro tracks (doc BGM B1–B10), imported by _tools/s469/music469.py as .ogg
            { "Grok_Troll", "boss_grok" },                 // B1 Goblin War Chief
            { "Brood_Toad", "boss_map2_mini" },            // B2 Mini-Boss Hop (shared by the two Map 2 mini-bosses)
            { "Ichor_Ram", "boss_map2_mini" },
            { "Plague_Eye", "boss_plague_eye" },           // B3 Eternal Death Ray
            { "Specimen_Zero", "boss_specimen_zero" },     // B4 Heartbeat of the Void
            { "Last_Guard", "boss_last_guard" },           // B5 The Last Stand (Map 2b)
            { "Ent_", "boss_ent" },                        // B6 Forest Guardian (Ancient Ent, all 4 forms)
            { "Lantern_Knight", "boss_lantern_knight" },   // B7
            { "Matriarch_Isolde", "boss_isolde" },         // B8 Gothic Waltz of the Dark Lord
            { "Precentor_Morrow", "boss_morrow" },         // B9 The Watcher's Chorus
            { "Red_Field", "boss_red_field" },             // B10 Clashing Armies (Map 3b)
        };
        /// <summary>Story scene (Tiled object name, StoryScenes.Playing) -> track. Plays at full volume over the slides instead of muting the music.</summary>
        private static readonly Dictionary<string, string> StoryTrack = new Dictionary<string, string>
        {
            { "prologue", "story_prologue" },               // Map 1 opening slides — Intro_Box (doc BGM S1)
            { "angel_bird", "story_angel_bird" },           // Map 4 execution slides — Angel_Bird (doc BGM S4)
        };
        private const float StoryFadeIn = 1.5f;

        private const string AbsalomTrack = "boss_absalom";  // Psychronic — Arcade Warlord (548268)
        private const string BurlapTrack = "boss_burlap";    // ihatetuesdays — Video Game Boss Fiight (259885)

        /// <summary>Volume (gain) + loop points (seconds) per track. gain ≈ 0.55 × 10^((-16 − LUFS)/20), max 1.</summary>
        private readonly struct TrackMix
        {
            public readonly float Gain, LoopStart, LoopEnd;
            public TrackMix(float gain, float loopStart, float loopEnd) { Gain = gain; LoopStart = loopStart; LoopEnd = loopEnd; }
        }

        private static readonly Dictionary<string, TrackMix> Mix = new Dictionary<string, TrackMix>
        {
            { "bgm_title",      new TrackMix(0.47f, 0f,    63.3f) },
            { "bgm_map1",       new TrackMix(0.57f, 0f,   100.8f) },
            { "bgm_map2",       new TrackMix(0.37f, 0.2f, 140.0f) },
            { "bgm_map3",       new TrackMix(0.91f, 0.25f, 117.1f) },
            { "bgm_secret",     new TrackMix(1.00f, 1.05f, 166.3f) },
            { "bgm_hollow",            new TrackMix(0.44f, 0.15f, 102.65f) },
            { "bgm_map4",              new TrackMix(0.45f, 0f, 106.60f) },
            { "story_prologue",        new TrackMix(0.38f, 0f, 57.55f) },
            { "story_angel_bird",      new TrackMix(0.31f, 0.60f, 55.15f) },
            { "boss_absalom",   new TrackMix(0.42f, 0f,   163.15f) },
            { "boss_burlap",    new TrackMix(0.57f, 0f,    75.45f) },
            { "boss_registrar", new TrackMix(0.45f, 0f,   189.95f) },
            { "boss_reliquary", new TrackMix(0.45f, 0.05f, 78.4f) },
            { "boss_grok",             new TrackMix(0.47f, 0f, 149.30f) },
            { "boss_map2_mini",        new TrackMix(0.46f, 0f, 148.30f) },
            { "boss_plague_eye",       new TrackMix(0.43f, 0f, 148.25f) },
            { "boss_specimen_zero",    new TrackMix(0.43f, 0f, 124.25f) },
            { "boss_last_guard",       new TrackMix(0.43f, 0f, 149.85f) },
            { "boss_ent",              new TrackMix(0.46f, 0.20f, 149.05f) },
            { "boss_lantern_knight",   new TrackMix(0.43f, 0f, 124.25f) },
            { "boss_isolde",           new TrackMix(0.43f, 0f, 147.35f) },
            { "boss_morrow",           new TrackMix(0.43f, 0f, 148.95f) },
            { "boss_red_field",        new TrackMix(0.46f, 0f, 177.95f) },
        };

        private const float CheckEvery = 0.1f;
        private const float SettleTime = 0.75f;   // at least this many seconds in the scene before the scene track starts
        private const float QuietNeeded = 0.6f;   // no dialogue / cutscene for this many seconds before the scene track starts
        private const float SceneFadeIn = 2f, BossFadeIn = 1.2f, BossFadeOut = 2f;

        private static SceneMusic inst;
        private readonly Dictionary<string, bool> fileExists = new Dictionary<string, bool>();
        private float nextCheck, sceneAt, quietSince;
        private bool sceneStarted;                // has the scene track started (once started, later cutscenes only mute it via Duck)
        private Health playerHp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => inst = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (inst != null) return;
            var go = new GameObject("SR_SceneMusic");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SceneMusic>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnScene;
        private void OnDisable() => SceneManager.sceneLoaded -= OnScene;

        private void OnScene(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            AbsalomHallMechanism.InFight = false;      // scene reload: the Absalom fight resets itself when it starts
            nextCheck = 0f;
            sceneAt = quietSince = Time.unscaledTime;
            sceneStarted = false;
            playerHp = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckEvery;
            Music.Duck = DuckNow();
            if (StoryBusy) quietSince = Time.unscaledTime;

            string scene = SceneManager.GetActiveScene().name;
            string story = StoryNow();
            string boss = story == null ? BossNow(scene) : null;
            if (boss != null && !Has(boss)) boss = null;
            string want = story ?? boss ?? (SceneTrack.TryGetValue(scene, out string t) ? t : null);

            if (want == null)                                                        // boss over and the scene has no track: stop the boss music
            {
                string cur = Music.Current;
                if (cur != null && cur.StartsWith("boss_", System.StringComparison.Ordinal)) Music.Stop(BossFadeOut);
                return;
            }
            if (!Has(want)) return;
            if (Music.Current == want) { if (story == null) sceneStarted = true; return; }
            bool waitingForIntro = Time.unscaledTime - sceneAt < SettleTime || Time.unscaledTime - quietSince < QuietNeeded;
            if (story == null && boss == null && !sceneStarted && waitingForIntro) return;
            if (story == null) sceneStarted = true;
            float fadeIn = story != null ? StoryFadeIn : boss != null ? BossFadeIn : SceneFadeIn;
            if (Mix.TryGetValue(want, out var m)) Music.Play(want, fadeIn, m.LoopStart, m.LoopEnd, m.Gain);
            else Music.Play(want, fadeIn);
        }

        // ================================================================== situation
        /// <summary>Dialogue / narration picture / cutscene / scene change / end screen in progress.</summary>
        private static bool StoryBusy => StoryScenes.Busy || CutsceneState.Active || DialogueBox.Active || SceneTransition.Busy
                                         || ChoiceCard.Active || ToBeContinuedScreen.Active;

        /// <summary>Current music volume factor (Music.Duck).</summary>
        private float DuckNow()
        {
            if (playerHp == null)
            {
                var d = MapEncounterDirector.Instance;
                if (d != null && d.Player != null) playerHp = d.Player.GetComponent<Health>();
            }
            if (playerHp != null && playerHp.IsDead) return 0.35f;
            if (GameInput.MenuOpen && Time.timeScale <= 0f) return 0.5f;
            if (StoryNow() != null) return 1f;                                     // story scene with its own track: keep it audible
            if (DialogueBox.Active || StoryScenes.Busy || ChoiceCard.Active || CutsceneState.Active) return 0f;
            return 1f;
        }

        /// <summary>Track of the story scene now playing (only if the file exists), null = none.</summary>
        private string StoryNow()
        {
            string n = StoryScenes.Playing;
            return n != null && StoryTrack.TryGetValue(n, out string t) && Has(t) ? t : null;
        }

        /// <summary>Boss track that should play now, null = no boss.</summary>
        private static string BossNow(string scene)
        {
            var dir = MapEncounterDirector.Instance;
            if (dir != null)
            {
                string t = TrackFor(dir.Boss) ?? TrackFor(dir.BossPartner);
                if (t != null) return t;
            }
            if (AbsalomHallMechanism.InFight) return AbsalomTrack;
            if (scene == "Map4d_DrillYard" && DrillBoard.Active) return BurlapTrack;
            return null;
        }

        /// <summary>Boss alive, has seen Ren, name card gone -> its track id.</summary>
        private static string TrackFor(EnemyAI e)
        {
            if (e == null || e.Health == null || e.Health.IsDead || !e.Aggro || BossIntro.CardActive) return null;
            string n = e.gameObject.name;
            foreach (var kv in BossTrack)
                if (n.StartsWith(kv.Key, System.StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        /// <summary>Does Resources/Audio/Music/&lt;id&gt; exist (cached; warned once when missing).</summary>
        private bool Has(string id)
        {
            if (fileExists.TryGetValue(id, out bool ok)) return ok;
            ok = Resources.Load<AudioClip>(Music.Folder + "/" + id) != null;
            if (!ok) Debug.LogWarning("[SceneMusic] Missing Resources/" + Music.Folder + "/" + id);
            fileExists[id] = ok;
            return ok;
        }
    }
}
