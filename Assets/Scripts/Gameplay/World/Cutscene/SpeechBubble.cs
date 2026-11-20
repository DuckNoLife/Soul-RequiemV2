using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: bóng thoại nhỏ trên đầu nhân vật / trong cảnh (chữ pixel RequiemBody trên nền tối), tự mờ dần rồi biến mất.
    /// SpeechBubble.Show(vịTríThếGiới, "Help!", 2.5f)
    /// 17/11 (người dùng): mặc định KHÔNG vẽ bóng trên đầu nữa — câu nói chuyển xuống khung hội thoại dưới đáy màn hình (<see cref="Subtitles"/>),
    /// không chặn điều khiển. Tên người nói: tham số speaker, hoặc tự suy ra khi gọi Follow (Ren / NPC / quái). WorldBubbles = true để quay lại kiểu cũ.
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        private static Font font;
        private static Sprite white;
        private static readonly List<SpeechBubble> active = new List<SpeechBubble>();
        /// <summary>17/11: true = vẽ bóng thoại trên đầu như cũ; false (mặc định) = đưa câu xuống khung hội thoại dưới đáy.</summary>
        public static bool WorldBubbles = false;
        private int subtitleSerial = -1;                    // >= 0: bóng này chỉ là "chỗ giữ" cho một câu phụ đề (không vẽ gì)

        private TextMesh text;
        private SpriteRenderer bg;
        private MeshRenderer textRenderer;
        private float born, until;
        private Color textColor, bgColor;
        private Transform follow;
        private Vector3 followOffset;

        /// <summary>02/11: bóng thoại bám theo người nói (đang chạy / bị đẩy) thay vì đứng yên ở chỗ vừa nói. Gọi ngay sau Show.</summary>
        public SpeechBubble Follow(Transform speaker)
        {
            follow = speaker;
            if (speaker != null) followOffset = transform.position - speaker.position;
            if (subtitleSerial >= 0) Subtitles.SetSpeaker(subtitleSerial, Subtitles.NameOf(speaker));
            return this;
        }

        private void LateUpdate()
        {
            if (follow != null) transform.position = follow.position + followOffset;
        }

        public static SpeechBubble Show(Vector3 at, string message, float seconds, Color? color = null, string speaker = null, string voice = null)
        {
            if (!WorldBubbles)
            {
                // 17/11: phụ đề dưới đáy. Vẫn trả về một SpeechBubble (rỗng, tự huỷ) để code cũ gọi .Follow(...) không lỗi.
                if (string.IsNullOrEmpty(message)) return null;
                var sgo = new GameObject("Speech (subtitle)");
                sgo.transform.position = at;
                var sb = sgo.AddComponent<SpeechBubble>();
                sb.subtitleSerial = Subtitles.Say(speaker, message, seconds, color, voice);   // 19/11 (§338): voice = giọng bíp khi không ghi tên
                sb.born = Time.time; sb.until = Subtitles.Until;
                return sb;
            }
            if (font == null) font = Resources.Load<Font>("UI/Requiem/Fonts/RequiemBody");
            if (white == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white); tex.Apply();
                white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            // 02/11: câu mới của CÙNG người nói (bóng cũ ở gần) thay hẳn câu cũ — trước đây bấm Space tua thì câu trước còn đang mờ dần
            // nằm chồng lên câu sau, rất khó đọc. Bóng ở xa (người khác nói cùng lúc, như cảnh mở đầu) vẫn giữ.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var o = active[i];
                if (o == null) { active.RemoveAt(i); continue; }
                Vector3 d = o.transform.position - at;
                if (Mathf.Abs(d.x) < 1.2f && Mathf.Abs(d.y) < 0.6f) { active.RemoveAt(i); Destroy(o.gameObject); }
            }
            var go = new GameObject("Speech Bubble");
            go.transform.position = at;
            var b = go.AddComponent<SpeechBubble>();
            b.Build(Wrap(message, 30), seconds, color ?? new Color(1f, 0.95f, 0.85f));
            active.Add(b);
            return b;
        }

        /// <summary>02/11: xuống dòng theo từ (~30 ký tự/dòng) để bóng thoại không quá rộng, bị cắt ở mép màn hình.</summary>
        private static string Wrap(string s, int width)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= width) return s;
            var sb = new System.Text.StringBuilder();
            int lineLen = 0;
            foreach (var w in s.Split(' '))
            {
                if (lineLen > 0 && lineLen + 1 + w.Length > width) { sb.Append('\n'); lineLen = 0; }
                else if (lineLen > 0) { sb.Append(' '); lineLen++; }
                sb.Append(w); lineLen += w.Length;
            }
            return sb.ToString();
        }

        private void Build(string message, float seconds, Color c)
        {
            born = Time.time; until = born + seconds;
            textColor = c; bgColor = new Color(0.07f, 0.05f, 0.05f, 0.8f);

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(transform, false);
            text = tgo.AddComponent<TextMesh>();
            text.text = message;
            text.anchor = TextAnchor.MiddleCenter;                  // 02/11: cả bóng nằm TRÊN điểm neo (không che đầu người nói)
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.026f;
            text.color = textColor;
            if (font != null) text.font = font;
            textRenderer = tgo.GetComponent<MeshRenderer>();
            if (font != null) textRenderer.sharedMaterial = font.material;
            textRenderer.sortingOrder = 32001;

            var bgo = new GameObject("Back");
            bgo.transform.SetParent(transform, false);
            bg = bgo.AddComponent<SpriteRenderer>();
            bg.sprite = white;
            bg.color = bgColor;
            bg.sortingOrder = 32000;
            Vector3 size = textRenderer.bounds.size;
            float h = Mathf.Max(0.14f, size.y + 0.06f);
            bgo.transform.localScale = new Vector3(Mathf.Max(0.3f, size.x + 0.12f), h, 1f);
            bgo.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            tgo.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
        }

        private void OnDestroy() { active.Remove(this); }

        private void Update()
        {
            float t = Time.time;
            if (subtitleSerial >= 0) { if (t > until) Destroy(gameObject); return; }
            // nảy nhẹ khi hiện, mờ dần 0.35 giây cuối
            float pop = Mathf.Clamp01((t - born) / 0.12f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, pop);
            float a = Mathf.Clamp01((until - t) / 0.35f);
            text.color = new Color(textColor.r, textColor.g, textColor.b, textColor.a * a);
            bg.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * a);
            if (t > until) Destroy(gameObject);
        }
    }
}
