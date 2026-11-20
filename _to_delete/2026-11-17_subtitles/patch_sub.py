# 17/11: bóng thoại -> khung hội thoại dưới đáy (Subtitles). Chạy trên máy, trong Assets/Scripts. Kiểm md5 trước khi sửa.
import hashlib, sys
def patch(p, md5, reps):
    b=open(p,'rb').read(); h=hashlib.md5(b).hexdigest(); assert h==md5,(p,h)
    s=b.decode('utf-8'); crlf='\r\n' in s
    if crlf: s=s.replace('\r\n','\n')
    for a,c in reps:
        assert s.count(a)==1,(p,a[:60]); s=s.replace(a,c)
    if crlf: s=s.replace('\n','\r\n')
    open(p,'wb').write(s.encode('utf-8'))

patch('Gameplay/World/Cutscene/SpeechBubble.cs','967d4f5bb321c458fe4d22b36a53cf38',[
("""    /// SpeechBubble.Show(vịTríThếGiới, "Help!", 2.5f)
    /// </summary>""","""    /// SpeechBubble.Show(vịTríThếGiới, "Help!", 2.5f)
    /// 17/11 (người dùng): mặc định KHÔNG vẽ bóng trên đầu nữa — câu nói chuyển xuống khung hội thoại dưới đáy màn hình (<see cref="Subtitles"/>),
    /// không chặn điều khiển. Tên người nói: tham số speaker, hoặc tự suy ra khi gọi Follow (Ren / NPC / quái). WorldBubbles = true để quay lại kiểu cũ.
    /// </summary>"""),
("""        private static readonly List<SpeechBubble> active = new List<SpeechBubble>();
""","""        private static readonly List<SpeechBubble> active = new List<SpeechBubble>();
        /// <summary>17/11: true = vẽ bóng thoại trên đầu như cũ; false (mặc định) = đưa câu xuống khung hội thoại dưới đáy.</summary>
        public static bool WorldBubbles = false;
        private int subtitleSerial = -1;                    // >= 0: bóng này chỉ là "chỗ giữ" cho một câu phụ đề (không vẽ gì)
"""),
("""            follow = speaker;
            if (speaker != null) followOffset = transform.position - speaker.position;
            return this;""","""            follow = speaker;
            if (speaker != null) followOffset = transform.position - speaker.position;
            if (subtitleSerial >= 0) Subtitles.SetSpeaker(subtitleSerial, Subtitles.NameOf(speaker));
            return this;"""),
("""        public static SpeechBubble Show(Vector3 at, string message, float seconds, Color? color = null)
        {""","""        public static SpeechBubble Show(Vector3 at, string message, float seconds, Color? color = null, string speaker = null)
        {
            if (!WorldBubbles)
            {
                // 17/11: phụ đề dưới đáy. Vẫn trả về một SpeechBubble (rỗng, tự huỷ) để code cũ gọi .Follow(...) không lỗi.
                if (string.IsNullOrEmpty(message)) return null;
                var sgo = new GameObject("Speech (subtitle)");
                sgo.transform.position = at;
                var sb = sgo.AddComponent<SpeechBubble>();
                sb.subtitleSerial = Subtitles.Say(speaker, message, seconds, color);
                sb.born = Time.time; sb.until = Subtitles.Until;
                return sb;
            }"""),
("""        private void Update()
        {
            float t = Time.time;""","""        private void Update()
        {
            float t = Time.time;
            if (subtitleSerial >= 0) { if (t > until) Destroy(gameObject); return; }"""),
])

patch('Gameplay/World/Cutscene/StoryScenes.cs','663c1a005f2c7474d98daa711bc92024',[
("""                SpeechBubble.Show(dir.Player.position + new Vector3(0f, 0.95f, 0f), Whispers[whisperIndex % Whispers.Length], 3.4f,
                                  new Color(0.62f, 1f, 0.6f)).Follow(dir.Player);""","""                SpeechBubble.Show(dir.Player.position + new Vector3(0f, 0.95f, 0f), Whispers[whisperIndex % Whispers.Length], 3.4f,
                                  new Color(0.62f, 1f, 0.6f), "The Eye").Follow(dir.Player);   // 17/11: phụ đề ghi rõ Con Mắt nói"""),
]) + who.Substring(1));   // 17/11: phụ đề: tên người nói
            if (follow == null && TryTile(who, out _)) shownName = "";                                     // toạ độ ô -> không tên
            var b = SpeechBubble.Show(at + new Vector3(0f, 0.85f, 0f), body, secs, null, shownName.Length > 0 ? shownName : null);"""),
])

patch('UI/RequiemHUD.cs','f73f224f87938161b0b622b8bf0145bb',[
("""        private void UpdateDialogue()
        {
            bool on = DialogueBox.Active;
            dlgRoot.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;""","""        private void UpdateDialogue()
        {
            bool on = DialogueBox.Active;
            bool sub = !on && Subtitles.Visible;             // 17/11: lời nói ngắn (trước là bóng thoại) -> cùng khung, không chặn điều khiển
            dlgRoot.style.display = on || sub ? DisplayStyle.Flex : DisplayStyle.None;
            if (sub) { UpdateSubtitle(); return; }
            dlgRoot.style.opacity = 1f;
            dlgName.style.color = RequiemUI.Gold;
            if (!on) return;"""),
("""        private void UpdateHint(MapEncounterDirector dir, float bossTop)
        {""","""        /// <summary>17/11: phụ đề (Subtitles) trong khung hội thoại: tên + chữ chạy nhanh, không có [phím] next, mờ vào / mờ ra.</summary>
        private void UpdateSubtitle()
        {
            float w = Mathf.Min(1040f, PanelW - 2f * M - 40f);
            dlgRoot.style.width = w;
            dlgRoot.style.left = (PanelW - w) / 2f;
            float t = Time.time;
            dlgRoot.style.opacity = Mathf.Min(Mathf.Clamp01((t - Subtitles.Born) / 0.12f), Mathf.Clamp01((Subtitles.Until - t) / 0.35f));
            string nm = RequiemUI.Clean(Subtitles.Speaker);
            if (dlgName.text != nm) dlgName.text = nm;
            dlgName.style.display = nm.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            dlgName.style.color = Subtitles.NameColor.a > 0f ? Subtitles.NameColor : RequiemUI.Gold;
            string line = Subtitles.Text;
            int n = Mathf.Clamp(Mathf.FloorToInt((t - Subtitles.Born) * 70f), 0, line.Length);   // chữ chạy ~70 ký tự / giây
            string shown = RequiemUI.Clean(line.Substring(0, n));
            if (dlgText.text != shown) dlgText.text = shown;
            if (dlgNext.text != "") dlgNext.text = "";
        }

        private void UpdateHint(MapEncounterDirector dir, float bossTop)
        {"""),
("""            float baseY = Mathf.Min(PanelH * 0.8f, bossTop - 14f);""","""            float baseY = Mathf.Min(PanelH * 0.8f, bossTop - 14f);
            if (Subtitles.Visible && !DialogueBox.Active && dlgRoot.layout.height > 1f)              // 17/11: gợi ý không đè lên khung phụ đề
                baseY = Mathf.Min(baseY, PanelH - (M + 6) - dlgRoot.layout.height - 16f);"""),
])
print('patched')
