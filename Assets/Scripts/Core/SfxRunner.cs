using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulRequiem.Core
{
    /// <summary>
    /// SOUND EFFECT PLAYER behind <see cref="Sfx"/> — created on game start (GameObject "SR_Sfx", DontDestroyOnLoad).
    ///   This file          : 24 reusable channels (Voice), volume / panning from the camera, looping sounds on objects, world sounds paused while the game is paused.
    ///   SfxRunner.Channels : scene ambience (AmbienceFor), voice, narration bed, spare AudioListener.
    /// Main rules:
    ///   • No positional sounds for the first 0.8 s after entering a scene (a map restoring its saved state does not fire a burst of sounds).
    ///   • One id plays at most MaxPerId voices at once; loops of the same id at most MaxLoopsPerId (the closest ones are kept).
    ///   • Game paused (timeScale = 0) -> positional sounds pause; 2D (UI) sounds keep playing.
    /// </summary>
    public sealed partial class SfxRunner : MonoBehaviour
    {
        private const int MaxVoices = 24, MaxPerId = 4, MaxLoopsPerId = 2;
        private const float QuietAfterLoad = 0.8f;     // seconds of silent world sound after entering a scene
        private const float LoopKeepAlive = 0.3f;      // the caller of Loop() stopped calling for this long -> stop
        private const float PanRange = 6f, MaxPan = 0.6f;

        /// <summary>One playback channel.</summary>
        private sealed class Voice
        {
            public AudioSource src;
            public string id;                           // null = free channel
            public Vector2 pos;
            public Transform follow;                    // follows an object (null = static)
            public bool flat;                           // 2D (UI) sound: no position, never paused
            public float vol;
            public object loopKey;                      // non-null = looping sound
            public float startedAt, touchedAt;          // touchedAt: last Loop() call
            public bool paused;                         // paused with the game
        }

        private static SfxRunner inst;
        private readonly List<Voice> voices = new List<Voice>();
        private float sceneAt = -99f;
        private bool worldPaused;

        private bool Quiet => Time.unscaledTime - sceneAt < QuietAfterLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => inst = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => Get();

        public static SfxRunner Get()
        {
            if (inst != null || !Application.isPlaying) return inst;
            var go = new GameObject("SR_Sfx");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SfxRunner>();
            return inst;
        }

        private void Awake()
        {
            for (int i = 0; i < MaxVoices; i++) voices.Add(new Voice { src = NewSource("voice_" + i) });
            CreateChannels();
            SceneManager.sceneLoaded += OnScene;
            OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnScene;
            if (inst == this) inst = null;
        }

        private AudioSource NewSource(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;                        // volume / panning computed here (top-down game)
            s.ignoreListenerPause = true;
            return s;
        }

        private void OnScene(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            sceneAt = Time.unscaledTime;
            CheckEar();
            foreach (var v in voices) if (v.loopKey != null) Release(v);
            SetAmbience(AmbienceFor(s.name));
        }

        // ================================================================== play
        internal void Play(string id, Vector2 pos, Transform follow, bool flat, float vol)
        {
            if (string.IsNullOrEmpty(id) || !Sfx.Has(id)) return;
            if (!flat && (Quiet || worldPaused || Attenuation(pos) <= 0.001f)) return;   // out of hearing range: do not take a channel
            int same = 0;
            foreach (var v in voices) if (v.id == id && v.src.isPlaying) same++;
            if (same >= MaxPerId || !Sfx.Throttle(id)) return;
            var clip = Sfx.Pick(id);
            var voice = clip != null ? FreeVoice() : null;
            if (voice == null) return;
            voice.id = id; voice.pos = pos; voice.follow = follow; voice.flat = flat; voice.vol = vol;
            voice.loopKey = null; voice.startedAt = Time.unscaledTime;
            voice.src.loop = false;
            voice.src.clip = clip;
            voice.src.pitch = 1f + Random.Range(-1f, 1f) * (flat ? 0.02f : 0.06f);   // slight pitch variation so it sounds less mechanical
            Apply(voice);
            voice.src.Play();
        }

        internal void Loop(object key, string id, Transform follow, float vol)
        {
            foreach (var v in voices)                                                 // already looping: just extend it
                if (v.loopKey != null && v.loopKey.Equals(key)) { v.follow = follow; v.vol = vol; v.touchedAt = Time.unscaledTime; return; }
            if (Quiet || worldPaused || !Sfx.Has(id) || Attenuation(follow.position) <= 0.001f) return;
            if (!MakeRoomForLoop(id, follow.position)) return;
            var voice = FreeVoice();
            if (voice == null) return;
            voice.id = id; voice.pos = follow.position; voice.follow = follow; voice.flat = false; voice.vol = vol;
            voice.loopKey = key; voice.startedAt = voice.touchedAt = Time.unscaledTime;
            voice.src.loop = true;
            voice.src.clip = Sfx.Pick(id);
            voice.src.pitch = 1f + Random.Range(-0.03f, 0.03f);
            Apply(voice);
            voice.src.Play();
            if (voice.src.clip != null) voice.src.time = Random.Range(0f, voice.src.clip.length * 0.9f);   // random phase: 2 traps do not add up into a whine
        }

        /// <summary>MaxLoopsPerId loops of this id already playing -> only take over if at least 1 u closer to the ear than the farthest one (which gives up its channel).</summary>
        private bool MakeRoomForLoop(string id, Vector2 at)
        {
            Vector2 lp = Listener();
            int same = 0;
            float farD = -1f;
            Voice far = null;
            foreach (var v in voices)
                if (v.loopKey != null && v.id == id) { same++; float d = (v.pos - lp).sqrMagnitude; if (d > farD) { farD = d; far = v; } }
            if (same < MaxLoopsPerId) return true;
            if (far == null || Vector2.Distance(at, lp) + 1f >= Mathf.Sqrt(farD)) return false;
            Release(far);
            return true;
        }

        internal void StopLoop(object key)
        {
            foreach (var v in voices)
                if (v.loopKey != null && v.loopKey.Equals(key)) Release(v);
        }

        /// <summary>A free channel; otherwise the one-shot channel playing longest (never a loop / paused channel).</summary>
        private Voice FreeVoice()
        {
            Voice oldest = null;
            foreach (var v in voices)
            {
                if (v.paused || v.loopKey != null) continue;
                if (!v.src.isPlaying) return v;
                if (oldest == null || v.startedAt < oldest.startedAt) oldest = v;
            }
            return oldest;
        }

        private static void Release(Voice v)
        {
            v.src.Stop();
            v.src.loop = false;
            v.paused = false;
            v.loopKey = null; v.follow = null; v.id = null;
        }

        // ================================================================== volume by position
        private static Vector2 Listener()
        {
            var cam = Camera.main;
            return cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        }

        /// <summary>1 = next to the camera, 0 = beyond Far or in another zone.</summary>
        private static float Attenuation(Vector2 p)
        {
            Vector2 lp = Listener();
            float a = 1f - Mathf.Clamp01((Vector2.Distance(p, lp) - Sfx.Near) / (Sfx.Far - Sfx.Near));
            return a <= 0f ? 0f : a * ZoneFactor(p, lp);
        }

        private static int zoneFrame = -1;
        private static string listenerZone;

        /// <summary>Zone rule: same zone = 1, one side outside every zone (corridor) = OutsideBleed, two different zones = 0.</summary>
        private static float ZoneFactor(Vector2 p, Vector2 lp)
        {
            var zoneOf = Sfx.ZoneOf;
            if (zoneOf == null) return 1f;
            if (zoneFrame != Time.frameCount) { zoneFrame = Time.frameCount; listenerZone = zoneOf(lp); }
            string src = zoneOf(p);
            if (src == listenerZone) return 1f;
            if (src == null || listenerZone == null) return Sfx.OutsideBleed;
            return 0f;
        }

        private static void Apply(Voice v)
        {
            float vol = Mathf.Clamp01(GameSettings.Sfx) * Sfx.GainOf(v.id) * v.vol;
            if (v.flat) { v.src.volume = vol; v.src.panStereo = 0f; return; }
            float a = Attenuation(v.pos);
            v.src.volume = vol * a * a;                                          // squared: falls off faster with distance
            v.src.panStereo = Mathf.Clamp((v.pos.x - Listener().x) / PanRange, -MaxPan, MaxPan);
        }

        // ================================================================== every frame
        private void Update()
        {
            UpdateEar();
            UpdatePause();
            foreach (var v in voices) UpdateVoice(v);
            UpdateSpeech();
            UpdateBed();
            UpdateAmbience();
        }

        /// <summary>Game just paused / resumed -> pause / resume every positional sound.</summary>
        private void UpdatePause()
        {
            bool pause = Time.timeScale <= 0f;
            if (pause == worldPaused) return;
            worldPaused = pause;
            foreach (var v in voices)
            {
                if (v.id == null || v.flat) continue;
                if (pause) { if (v.src.isPlaying) { v.src.Pause(); v.paused = true; } }
                else if (v.paused) { v.src.UnPause(); v.paused = false; }
            }
        }

        private void UpdateVoice(Voice v)
        {
            if (v.id == null) return;
            if (v.paused) { if (v.loopKey != null) v.touchedAt = Time.unscaledTime; return; }
            if (v.loopKey != null)
            {
                if (v.follow == null || Time.unscaledTime - v.touchedAt > LoopKeepAlive) { Release(v); return; }   // object destroyed / caller stopped
                v.pos = v.follow.position;
                if (Attenuation(v.pos) <= 0.001f) { Release(v); return; }                // moved away: release the channel, it is taken again when coming close
            }
            else
            {
                if (!v.src.isPlaying) { v.id = null; v.follow = null; return; }
                if (v.follow != null) v.pos = v.follow.position;
            }
            Apply(v);
        }
    }
}
