using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§336): GIỌNG LẦM BẦM theo lời thoại (kiểu Hollow Knight) + tiếng bút dẫn chuyện — chung mọi map, tự tạo khi chạy game.
    /// Mỗi khung đọc câu đang hiện (khung hội thoại <see cref="DialogueBox"/> hoặc phụ đề <see cref="Subtitles"/>):
    ///   - Người nói -> giọng (SfxHooks.SpeechId: Ren, Todd, Pell, Aldous, goblin, boss...). Lời kể / chữ viết (không tên, bảng tin, thư) -> tiếng bút "cs_narrate".
    ///   - Nói ĐÚNG lúc chữ còn chạy (55 ký tự / s khung hội thoại, 70 ký tự / s phụ đề), tối thiểu 0.45 s cho câu rất ngắn.
    ///   - Bấm bỏ qua (chữ hiện hết sớm hơn tốc độ chạy), đóng khung, mở menu -> IM NGAY (SfxRunner tắt trong 0.07 s).
    ///   - Mỗi trang lời kể mới -> tiếng lật giấy "cs_page".
    /// Âm thật phát ở kênh riêng của SfxRunner (Sfx.Speech), độ to theo sfx_mix.json.
    /// </summary>
    public sealed class DialogueVoice : MonoBehaviour
    {
        private const float BoxCps = 55f;      // khớp DialogueRunner.CharsPerSecond
        private const float SubCps = 70f;      // khớp RequiemHUD (phụ đề chạy ~70 ký tự / giây)
        private const float MinSpeak = 0.45f;

        private static DialogueVoice inst;
        private string lineKey = "";
        private float lineStart, lineReveal;
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
            string key = null, speaker = "";
            bool box = false, done = false;
            int len = 0;
            float cps = BoxCps;
            if (DialogueBox.Active)
            {
                box = true;
                key = "box:" + DialogueBox.OpenedFrame + ":" + DialogueBox.Index;
                speaker = DialogueBox.Speaker;
                len = DialogueBox.Line.Length;
                done = DialogueBox.LineDone;
            }
            else if (Subtitles.Visible)
            {
                key = "sub:" + Subtitles.Serial;
                speaker = Subtitles.Speaker;
                len = Subtitles.Text.Length;
                cps = SubCps;
                done = (Time.time - Subtitles.Born) * SubCps >= len;
            }
            if (key == null) { lineKey = ""; Sfx.Speech(null); return; }

            float now = Time.unscaledTime;
            if (key != lineKey)
            {
                lineKey = key;
                lineStart = now;
                lineReveal = len / cps;
                skipped = false;
                if (box && string.IsNullOrEmpty(speaker)) Sfx.Play2D("cs_page");   // trang lời kể mới
            }
            // chữ hiện hết sớm hơn tốc độ chạy = người chơi bấm bỏ qua -> im ngay, không nói nốt
            if (done && now < lineStart + lineReveal - 0.05f) skipped = true;
            bool talking = !skipped && !GameInput.MenuOpen && now < lineStart + Mathf.Max(lineReveal, MinSpeak);
            Sfx.Speech(talking ? SfxHooks.SpeechId(speaker, box) : null);
        }
    }
}
