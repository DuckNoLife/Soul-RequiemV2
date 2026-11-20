using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulRequiem.Core
{
    /// <summary>
    /// 17/11 (§294): bộ phát của <see cref="Sfx"/> — tự tạo một lần (DontDestroyOnLoad), giữ 24 kênh AudioSource dùng lại,
    /// cập nhật độ to / lệch tai theo camera mỗi khung, quản lý âm lặp bám vật và nền âm theo map (đổi mượt khi sang scene).
    /// 0.8 giây đầu sau khi vào scene không phát âm có vị trí (tránh loạt tiếng lúc map khôi phục trạng thái đã lưu).
    /// </summary>
    public sealed class SfxRunner : MonoBehaviour
    {
        private const int MaxVoices = 24, MaxPerId = 4;
        private const float AmbienceGain = 0.5f, AmbienceFade = 1.6f, QuietAfterLoad = 0.8f, LoopKeepAlive = 0.3f;

        private sealed class Voice
        {
            public AudioSource src;
            public string id;
            public Vector2 pos;
            public Transform follow;
            public bool flat;
            public float vol;
            public object loopKey;
            public float startedAt;
            public float touchedAt;                 // âm lặp: lần cuối được gọi Loop()
        }

        private static SfxRunner inst;
        private readonly List<Voice> voices = new List<Voice>();
        private AudioSource ambA, ambB;            // ambA = đang lên, ambB = đang tắt dần

        // 19/11 (§336): kênh GIỌNG NÓI (lầm bầm theo câu thoại / tiếng bút dẫn chuyện) — nối các bản ngẫu nhiên tới khi bị tắt, tắt là tắt NGAY (fade 0.07 s)
        private AudioSource speech;
        private string speechId;
        private float speechFade, speechGapUntil;
        private AudioClip speechLast;
        // 19/11 (§336): NỀN DẪN CHUYỆN (khi tranh cutscene hiện) — lặp, lên / xuống mượt
        private AudioSource bed;
        private string bedId;
        private float bedT;
        private string ambId;
        private float ambT = 1f, sceneAt = -99f;

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
            ambA = NewSource("amb_a"); ambA.loop = true;
            ambB = NewSource("amb_b"); ambB.loop = true;
            speech = NewSource("speech"); // 19/11 (§336): giọng lầm bầm / dẫn chuyện
            bed = NewSource("story_bed"); bed.loop = true;
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
            s.spatialBlend = 0f;                   // tự tính độ to / lệch tai theo camera (game nhìn từ trên xuống)
            s.ignoreListenerPause = true;           // âm giao diện vẫn kêu khi mở menu (timeScale = 0)
            return s;
        }

        private void OnScene(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            sceneAt = Time.unscaledTime;
            foreach (var v in voices) if (v.loopKey != null) Release(v);
            SetAmbience(AmbienceFor(s.name));
        }

        /// <summary>Nền âm theo tên scene (Map1 đồng bằng, Map1b rừng đêm, Map2 cống ngầm, Map2b bể chứa, Map3 nghĩa địa / hầm mộ).</summary>
        private static string AmbienceFor(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            if (n.StartsWith("Map1b")) return "amb_hollow";
            if (n.StartsWith("Map1")) return "amb_plains";
            if (n.StartsWith("Map2b")) return "amb_cistern";
            if (n.StartsWith("Map2")) return "amb_sewer";
            if (n.StartsWith("Map3")) return "amb_crypt";
            return null;
        }

        private bool Quiet => Time.unscaledTime - sceneAt < QuietAfterLoad;

        internal void Play(string id, Vector2 pos, Transform follow, bool flat, float vol)
        {
            if (string.IsNullOrEmpty(id) || (!flat && Quiet) || !Sfx.Has(id)) return;
            if (!flat && Attenuation(pos) <= 0.001f) return;          // ngoài tầm nghe: khỏi chiếm kênh
            int same = 0;
            foreach (var v in voices) if (v.id == id && v.src.isPlaying) same++;
            if (same >= MaxPerId || !Sfx.Throttle(id)) return;
            var clip = Sfx.Pick(id);
            if (clip == null) return;
            var voice = FreeVoice();
            if (voice == null) return;
            voice.id = id; voice.pos = pos; voice.follow = follow; voice.flat = flat; voice.vol = vol;
            voice.loopKey = null; voice.startedAt = Time.unscaledTime;
            voice.src.loop = false;
            voice.src.clip = clip;
            voice.src.pitch = 1f + Random.Range(-1f, 1f) * (flat ? 0.02f : 0.06f);
            Apply(voice);
            voice.src.Play();
        }

        internal void Loop(object key, string id, Transform follow, float vol)
        {
            foreach (var v in voices)
                if (v.loopKey != null && v.loopKey.Equals(key)) { v.follow = follow; v.vol = vol; v.touchedAt = Time.unscaledTime; return; }
            if (Quiet || !Sfx.Has(id) || Attenuation(follow.position) <= 0.001f) return;   // xa thì chưa giữ kênh
            var voice = FreeVoice();
            if (voice == null) return;
            voice.id = id; voice.pos = follow.position; voice.follow = follow; voice.flat = false; voice.vol = vol;
            voice.loopKey = key; voice.startedAt = voice.touchedAt = Time.unscaledTime;
            voice.src.loop = true;
            voice.src.clip = Sfx.Pick(id);
            voice.src.pitch = 1f;
            Apply(voice);
            voice.src.Play();
        }

        internal void StopLoop(object key)
        {
            foreach (var v in voices)
                if (v.loopKey != null && v.loopKey.Equals(key)) Release(v);
        }

        internal void SetAmbience(string id)
        {
            if (id == ambId) return;
            ambId = id;
            (ambA, ambB) = (ambB, ambA);           // bản cũ sang ambB để tắt dần
            ambA.Stop();
            ambA.clip = string.IsNullOrEmpty(id) ? null : Sfx.Pick(id);
            ambA.volume = 0f;
            if (ambA.clip != null) ambA.Play();
            ambT = 0f;
        }

        /// <summary>Giọng đang nói: id (vd vo_pell) = nói tiếp / đổi người; null = im ngay. Gọi MỖI KHUNG (DialogueVoice).</summary>
        internal void SetSpeech(string id)
        {
            if (string.IsNullOrEmpty(id) || !Sfx.Has(id)) id = null;
            if (id == speechId) return;
            if (id != null && speechId != null) speech.Stop();     // đổi người nói: cắt luôn người cũ
            speechId = id;
            if (id != null) { speechFade = 1f; speechGapUntil = 0f; }
        }

        /// <summary>Nền dẫn chuyện: id = bật (lặp), null = tắt dần.</summary>
        internal void SetBed(string id)
        {
            if (string.IsNullOrEmpty(id) || !Sfx.Has(id)) id = null;
            if (id == bedId) return;
            bedId = id;
            if (id != null)
            {
                var c = Sfx.Pick(id);
                if (bed.clip != c || !bed.isPlaying) { bed.clip = c; bed.volume = 0f; bed.Play(); bedT = 0f; }
            }
        }

        private void UpdateSpeech()
        {
            float dt = Time.unscaledDeltaTime;
            if (speechId == null)
            {
                if (!speech.isPlaying) return;
                speechFade -= dt / 0.07f;
                if (speechFade <= 0f) { speech.Stop(); speechFade = 0f; }
                else speech.volume = Mathf.Clamp01(GameSettings.Sfx) * Sfx.GainOf(speech.clip != null ? SpeechKey(speech.clip.name) : "") * speechFade;
                return;
            }
            if (!speech.isPlaying && Time.unscaledTime >= speechGapUntil)
            {
                // nối bản kế: tránh lặp lại đúng bản vừa nói, chỉnh cao độ nhẹ cho đỡ máy móc
                AudioClip c = null;
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

        private static string SpeechKey(string clipName)
        {
            int u = clipName.LastIndexOf('_');
            return u > 0 && int.TryParse(clipName.Substring(u + 1), out _) ? clipName.Substring(0, u) : clipName;
        }

        private void UpdateBed()
        {
            if (!bed.isPlaying) return;
            float g = Mathf.Clamp01(GameSettings.Sfx) * Sfx.GainOf(bed.clip != null ? SpeechKey(bed.clip.name) : "");
            float step = Time.unscaledDeltaTime;
            bedT = bedId != null ? Mathf.Min(1f, bedT + step / 1.2f) : Mathf.Max(0f, bedT - step / 0.9f);   // lên 1.2 s, tắt 0.9 s
            bed.volume = g * bedT;
            if (bedId == null && bedT <= 0f) bed.Stop();
        }

        /// <summary>Kênh rảnh; hết kênh thì lấy kênh một-lần phát lâu nhất (không đụng âm lặp).</summary>
        private Voice FreeVoice()
        {
            Voice oldest = null;
            foreach (var v in voices)
            {
                if (v.loopKey == null && !v.src.isPlaying) return v;
                if (v.loopKey == null && (oldest == null || v.startedAt < oldest.startedAt)) oldest = v;
            }
            return oldest;
        }

        private static void Release(Voice v)
        {
            v.src.Stop();
            v.src.loop = false;
            v.loopKey = null; v.follow = null; v.id = null;
        }

        private static Vector2 Listener()
        {
            var cam = Camera.main;
            return cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        }

        private static float Attenuation(Vector2 p)
        {
            Vector2 lp = Listener();
            float d = Vector2.Distance(p, lp);
            float a = 1f - Mathf.Clamp01((d - Sfx.Near) / (Sfx.Far - Sfx.Near));
            return a <= 0f ? 0f : a * ZoneFactor(p, lp);
        }

        // 19/11 (§343): âm theo khu — bẫy / cơ quan / quái ở phòng bên không lọt sang phòng người chơi đang đứng
        private static int zoneFrame = -1;
        private static string listenerZone;
        private static float ZoneFactor(Vector2 p, Vector2 lp)
        {
            var f = Sfx.ZoneOf;
            if (f == null) return 1f;
            if (zoneFrame != Time.frameCount) { zoneFrame = Time.frameCount; listenerZone = f(lp); }
            string src = f(p);
            if (src == listenerZone) return 1f;                       // cùng khu (hoặc cả hai ngoài mọi khu)
            if (src == null || listenerZone == null) return Sfx.OutsideBleed;   // hành lang <-> phòng: lọt nhỏ
            return 0f;                                                 // hai khu khác nhau: không nghe
        }

        private static void Apply(Voice v)
        {
            float master = Mathf.Clamp01(GameSettings.Sfx) * Sfx.GainOf(v.id) * v.vol;
            if (v.flat) { v.src.volume = master; v.src.panStereo = 0f; return; }
            float a = Attenuation(v.pos);
            v.src.volume = master * a * a;          // giảm theo bình phương: xa nghe nhỏ nhanh hơn
            v.src.panStereo = Mathf.Clamp((v.pos.x - Listener().x) / 6f, -0.6f, 0.6f);
        }

        private void Update()
        {
            foreach (var v in voices)
            {
                if (v.id == null) continue;
                if (v.loopKey != null)
                {
                    if (v.follow == null) { Release(v); continue; }          // vật bị huỷ
                    if (Time.unscaledTime - v.touchedAt > LoopKeepAlive) { Release(v); continue; }   // nơi gọi thôi gọi (bẫy dừng / tắt)
                    v.pos = v.follow.position;
                    if (Attenuation(v.pos) <= 0.001f) { Release(v); continue; }   // ra xa: trả kênh, lại gần sẽ lấy lại
                }
                else
                {
                    if (!v.src.isPlaying) { v.id = null; v.follow = null; continue; }
                    if (v.follow != null) v.pos = v.follow.position;
                }
                Apply(v);
            }

            UpdateSpeech(); // 19/11 (§336): giọng lầm bầm / dẫn chuyện
            UpdateBed();

            // nền âm: đổi mượt giữa hai kênh
            ambT = Mathf.Min(1f, ambT + Time.unscaledDeltaTime / AmbienceFade);
            float g = Mathf.Clamp01(GameSettings.Sfx) * AmbienceGain;
            ambA.volume = g * ambT;
            ambB.volume = g * (1f - ambT);
            if (ambT >= 1f && ambB.isPlaying) ambB.Stop();
        }
    }
}
