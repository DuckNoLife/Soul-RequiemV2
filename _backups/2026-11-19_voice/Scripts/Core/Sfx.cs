using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// 17/11 (§294): hệ âm thanh hiệu ứng của Soul Requiem (bộ âm tạo bằng ElevenLabs, danh sách ở _tools/sfx/sfx_manifest.json).
    ///   File: Resources/Audio/SFX/&lt;id&gt;.ogg; biến thể &lt;id&gt;_1, &lt;id&gt;_2... (mỗi lần phát chọn ngẫu nhiên một bản).
    ///   Thiếu file -> im lặng, không báo lỗi (chưa tải đủ bộ âm vẫn chạy được).
    ///   Âm lượng = GameSettings.Sfx × độ lớn gốc của từng âm (Master đã áp qua AudioListener).
    ///   Âm có vị trí nhỏ dần theo khoảng cách tới camera và lệch trái / phải theo trục x.
    ///   Chống dồn: mỗi id có khoảng cách tối thiểu giữa hai lần phát và tối đa vài tiếng cùng lúc.
    /// Gọi:
    ///   Sfx.Play(id, vịTrí)            âm tại một điểm
    ///   Sfx.PlayAt(id, transform)      âm bám theo vật đang di chuyển (vd khúc gỗ lăn)
    ///   Sfx.Play2D(id)                 âm giao diện, không theo vị trí
    ///   Sfx.Loop(khoá, id, transform)  âm lặp bám theo vật: GỌI MỖI KHUNG khi còn chạy; thôi gọi 0.3 s là tự tắt (hoặc Sfx.StopLoop)
    ///   Sfx.Ambience(id)               nền âm theo map (SfxRunner tự đổi theo tên scene)
    /// </summary>
    public static class Sfx
    {
        /// <summary>Trong Near (đơn vị thế giới) nghe rõ, tới Far thì tắt hẳn.</summary>
        public const float Near = 3.5f, Far = 11f;
        public const string Folder = "Audio/SFX";

        private static Dictionary<string, AudioClip[]> clips;
        private static readonly Dictionary<string, float> lastAt = new Dictionary<string, float>();

        /// <summary>Độ lớn gốc từng âm (mặc định 1): âm lặp lại nhiều thì nhỏ hơn để không át âm chính.</summary>
        private static readonly Dictionary<string, float> Gain = new Dictionary<string, float>
        {
            { "footstep", 0.22f }, { "pl_swing", 0.7f }, { "en_swing", 0.55f }, { "hit_flesh", 0.8f }, { "hit_armor", 0.75f },
            { "trap_spikes", 0.55f }, { "trap_saw", 0.5f }, { "trap_roller", 0.85f }, { "trap_plate", 0.7f },
            { "ui_move", 0.45f }, { "ui_confirm", 0.55f }, { "ui_back", 0.5f }, { "ui_open", 0.55f }, { "dialog_next", 0.4f },
            { "quest_update", 0.6f }, { "checkpoint", 0.7f }, { "proj_magic", 0.6f }, { "arrow_shot", 0.7f },
        };

        /// <summary>Khoảng cách tối thiểu (giây, thời gian thật) giữa hai lần phát cùng một id. Mặc định 0.05.</summary>
        private static readonly Dictionary<string, float> MinGap = new Dictionary<string, float>
        {
            { "footstep", 0.12f }, { "trap_spikes", 0.2f }, { "trap_plate", 0.3f }, { "hit_flesh", 0.04f }, { "en_swing", 0.08f },
            { "ui_move", 0.04f }, { "checkpoint", 0.6f }, { "quest_update", 1f }, { "boss_intro", 2f }, { "st_bleed", 0.15f },
        };

        /// <summary>19/11 (§335): Resources/Audio/sfx_mix.json {"sounds":[{"id","gain","gap"}]} — ghi đè bảng Gain / MinGap ở trên.
        /// Sinh bằng _tools/sfx/manifest_v2.py --mix. Chỉnh độ to từng âm = sửa file này, không cần sửa code.</summary>
        [System.Serializable] private class MixEntry { public string id; public float gain = -1f; public float gap = -1f; }
        [System.Serializable] private class MixFile { public MixEntry[] sounds; }

        private static void LoadMix()
        {
            var ta = Resources.Load<TextAsset>("Audio/sfx_mix");
            if (ta == null) return;
            MixFile mf = null;
            try { mf = JsonUtility.FromJson<MixFile>(ta.text); } catch (System.Exception e) { Debug.LogWarning("[Sfx] sfx_mix.json lỗi: " + e.Message); }
            if (mf == null || mf.sounds == null) return;
            foreach (var m in mf.sounds)
            {
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                if (m.gain >= 0f) Gain[m.id] = m.gain;
                if (m.gap > 0f) MinGap[m.id] = m.gap;
            }
        }

        private static void EnsureLoaded()
        {
            if (clips != null) return;
            LoadMix();
            var groups = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>(Folder))
            {
                if (c == null) continue;
                string n = c.name;
                int u = n.LastIndexOf('_');
                string key = u > 0 && int.TryParse(n.Substring(u + 1), out _) ? n.Substring(0, u) : n;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            clips = new Dictionary<string, AudioClip[]>();
            foreach (var kv in groups) clips[kv.Key] = kv.Value.ToArray();
        }

        /// <summary>Có file cho id này không.</summary>
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

        /// <summary>true = được phát (đủ khoảng cách với lần trước).</summary>
        internal static bool Throttle(string id)
        {
            float now = Time.unscaledTime, gap = MinGap.TryGetValue(id, out float g) ? g : 0.05f;
            if (lastAt.TryGetValue(id, out float t) && now - t < gap) return false;
            lastAt[id] = now;
            return true;
        }

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
    }
}
