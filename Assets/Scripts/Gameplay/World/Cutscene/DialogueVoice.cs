using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§338): TIẾNG BÍP LỜI THOẠI kiểu Undertale (thay giọng lầm bầm §336 — người dùng không thích) + âm dẫn chuyện. Chung mọi map, tự tạo khi chạy game.
    /// Mỗi khung đọc câu đang hiện (khung hội thoại <see cref="DialogueBox"/> hoặc phụ đề <see cref="Subtitles"/>):
    ///   - Mỗi CHỮ CÁI vừa hiện ra -> một tiếng bíp của người nói (SfxHooks.BlipId: Ren, Todd, Pell, goblin, Grok, boss...; lời kể / chữ viết -> bl_narrate).
    ///     Bỏ qua dấu cách / dấu câu (đúng kiểu Undertale: chữ chạy tới dấu chấm thì lặng một nhịp). Giãn cách tối thiểu theo giọng (giọng to xác chậm hơn).
    ///   - Khung hội thoại: đếm theo số chữ ĐÃ HIỆN THẬT (DialogueBox.Shown) -> khớp tuyệt đối độ dài câu.
    ///     Bấm bỏ qua (chữ nhảy hiện hết) -> im ngay, không bíp nốt. Phụ đề: đếm theo đúng công thức chữ chạy của HUD (70 ký tự / s).
    ///   - Mở menu -> im. Mỗi trang lời kể mới -> tiếng lật giấy "cs_page"; dòng lời dẫn (Hints) hiện lên giữa cảnh diễn -> "cs_page".
    /// Âm phát bằng Sfx.Play2D (bíp ngắn 20–80 ms, chồng được), độ to theo sfx_mix.json (bl_* ~0.3 — tầng 2).
    /// </summary>
    public sealed class DialogueVoice : MonoBehaviour
    {
        private const float SubCps = 70f;      // khớp RequiemHUD (phụ đề chạy ~70 ký tự / giây)
        private const float DefaultGap = 0.055f;

        /// <summary>Giãn cách tối thiểu giữa 2 tiếng bíp theo giọng (giây). Giọng không có ở đây = DefaultGap.</summary>
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
                if (string.IsNullOrEmpty(speaker)) speaker = Subtitles.Voice;          // câu không ghi tên nhưng có giọng (cảnh mở đầu)
                line = Subtitles.Text;
                shown = Mathf.Clamp(Mathf.FloorToInt((Time.time - Subtitles.Born) * SubCps), 0, line.Length);
            }
            if (key == null) { lineKey = ""; return; }

            if (key != lineKey)
            {
                lineKey = key;
                lastShown = 0;
                skipped = false;
                if (box && string.IsNullOrEmpty(speaker)) Sfx.Play2D("cs_page");   // trang lời kể mới
            }
            if (shown <= lastShown) return;
            int from = lastShown;
            lastShown = shown;
            // bấm bỏ qua: chữ nhảy hiện hết trong một khung (> 6 chữ) -> im luôn phần còn lại của câu
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

        /// <summary>Dòng lời dẫn (Hints) mới hiện lên giữa cảnh diễn -> tiếng lật trang dẫn chuyện.</summary>
        private void Narration()
        {
            string h = Hints.Visible ? Hints.Text : "";
            if (h == hintSeen) return;
            hintSeen = h;
            if (h.Length > 0 && CutsceneState.Active && !DialogueBox.Active) Sfx.Play2D("cs_page");
        }
    }
}
