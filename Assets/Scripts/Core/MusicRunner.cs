using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulRequiem.Core
{
    /// <summary>
    /// MUSIC PLAYER behind <see cref="Music"/> — created once (GameObject "SR_Music", DontDestroyOnLoad), runs on unscaled time.
    /// 2 alternating AudioSources:
    ///   • track change : the old track fades out on the `dying` channel, the new one fades in (fadeIn).
    ///   • loop         : at loopEnd - Xfade the other channel starts from loopStart and both cross-fade for Xfade seconds (no silent gap).
    ///     No valid loopEnd -> AudioSource.loop over the whole track.
    /// Moving to a scene OTHER than the one that called Play -> fades out by itself (dying and reloading the same scene keeps the music going).
    /// </summary>
    public sealed class MusicRunner : MonoBehaviour
    {
        private const float Xfade = 1.2f;               // loop seam length (seconds)
        private const float LevelSpeed = 1.25f;         // Level change per second
        private const float DuckDownSpeed = 2.6f;       // Duck goes down fast (~0.4 s to 0)
        private const float DuckUpSpeed = 0.9f;         // Duck comes back slowly (~1.1 s to 1)

        private static MusicRunner inst;

        private AudioSource cur, next;                  // cur = main channel, next = channel waiting for the loop seam / new track
        private AudioSource dying;                      // old track fading out after a track change
        private float dyingVol, dyingSpeed;

        private AudioClip clip;
        private string id, ownerScene;
        private float loopStart, loopEnd, gain = 0.55f;
        private float fade, fadeGoal, fadeSpeed = 1f;   // fade in / out of the current track (0..1)
        private float level = 1f, duck = 1f;
        private float xT = -1f;                         // >= 0: loop seam in progress (seconds cross-faded)

        /// <summary>Targets of Music.Level / Music.Duck (the real values move smoothly in Update).</summary>
        public float LevelGoal = 1f;
        public float DuckGoal = 1f;
        public string CurrentId => fadeGoal > 0f ? id : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => inst = null;

        public static MusicRunner Get()
        {
            if (inst != null || !Application.isPlaying) return inst;
            var go = new GameObject("SR_Music");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<MusicRunner>();
            return inst;
        }

        private void Awake()
        {
            cur = NewSource("music_a");
            next = NewSource("music_b");
            SceneManager.sceneLoaded += OnScene;
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
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
            s.priority = 0;                             // music never loses its channel to sound effects
            s.volume = 0f;
            return s;
        }

        private void OnScene(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || fadeGoal <= 0f) return;
            if (s.name != ownerScene) Stop(1.5f);
        }

        // ================================================================== commands
        internal void Play(string newId, float fadeIn, float ls, float le, float g)
        {
            if (string.IsNullOrEmpty(newId)) return;
            ownerScene = SceneManager.GetActiveScene().name;
            if (newId == id && fadeGoal > 0f && cur.isPlaying) return;           // already playing this track
            var c = Resources.Load<AudioClip>(Music.Folder + "/" + newId);
            if (c == null) { Debug.LogWarning("[Music] missing Resources/" + Music.Folder + "/" + newId); return; }

            if (cur.isPlaying && fade > 0.01f)                                   // old track moves to the fade-out channel
            {
                if (dying != null) dying.Stop();
                dying = cur; dyingVol = cur.volume; dyingSpeed = dyingVol / Mathf.Max(0.1f, fadeIn);
                (cur, next) = (next, cur);
            }
            next.Stop();
            xT = -1f;
            clip = c; id = newId; gain = g;
            loopStart = Mathf.Clamp(ls, 0f, Mathf.Max(0f, c.length - 1f));
            loopEnd = le > loopStart + Xfade * 2f && le <= c.length ? le : 0f;
            cur.clip = c;
            cur.loop = loopEnd <= 0f;
            cur.time = 0f;
            cur.Play();
            fade = 0f; fadeGoal = 1f; fadeSpeed = 1f / Mathf.Max(0.05f, fadeIn);
        }

        internal void Stop(float f)
        {
            fadeGoal = 0f;
            fadeSpeed = 1f / Mathf.Max(0.05f, f);
        }

        // ================================================================== every frame
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, fadeGoal, fadeSpeed * dt);
            level = Mathf.MoveTowards(level, LevelGoal, LevelSpeed * dt);
            duck = Mathf.MoveTowards(duck, DuckGoal, (DuckGoal < duck ? DuckDownSpeed : DuckUpSpeed) * dt);
            UpdateDying(dt);
            if (clip == null) return;

            if (fadeGoal <= 0f && fade <= 0f)                                    // fade-out done
            {
                cur.Stop(); next.Stop(); xT = -1f; clip = null; id = null;
                return;
            }

            float vol = Mathf.Clamp01(GameSettings.Music * gain * fade * level * duck);
            if (loopEnd > 0f && xT < 0f && cur.isPlaying && cur.time >= loopEnd - Xfade) StartLoopSeam();
            if (xT >= 0f) UpdateLoopSeam(dt, vol);
            else
            {
                cur.volume = vol;
                if (!cur.isPlaying && fadeGoal > 0f && loopEnd > 0f) { cur.time = loopStart; cur.Play(); }   // safety net (long frame hitch)
            }
        }

        private void UpdateDying(float dt)
        {
            if (dying == null) return;
            dyingVol = Mathf.MoveTowards(dyingVol, 0f, dyingSpeed * dt);
            dying.volume = dyingVol;
            if (dyingVol <= 0f) { dying.Stop(); dying = null; }
        }

        /// <summary>Start the loop seam: channel `next` plays from loopStart.</summary>
        private void StartLoopSeam()
        {
            next.clip = clip;
            next.loop = false;
            next.time = loopStart;
            next.Play();
            xT = 0f;
        }

        /// <summary>Cross-fade both channels (square root -> constant loudness), then swap the cur / next roles.</summary>
        private void UpdateLoopSeam(float dt, float vol)
        {
            xT += dt;
            float w = Mathf.Clamp01(xT / Xfade);
            cur.volume = vol * Mathf.Sqrt(1f - w);
            next.volume = vol * Mathf.Sqrt(w);
            if (w < 1f) return;
            cur.Stop();
            (cur, next) = (next, cur);
            xT = -1f;
        }
    }
}
