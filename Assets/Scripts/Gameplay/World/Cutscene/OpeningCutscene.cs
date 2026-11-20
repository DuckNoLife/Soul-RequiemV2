using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: cảnh mở đầu Map 1 (chỉ chạy một lần, cờ "intro_done"): khoá điều khiển, lia camera tới nhà đang cháy (goblin đốt, dân kêu cứu),
    /// sang hiên nhà Todd (dân nấp cầu xin), trả camera về Ren, rồi dạy dần: đi lại -> chém khi tới gần tên cướp đầu tiên -> tên thứ hai.
    /// Dữ liệu trong Tiled (lớp triggers, type "cutscene_point"): intro_cam_1, intro_cam_2 (điểm lia);
    /// intro_say_N (shot = 1|2, text, delay giây sau khi tới cảnh, color = goblin|villager). 11/11 (§229): không bỏ qua được.
    /// </summary>
    public class OpeningCutscene : MonoBehaviour
    {
        private const string DoneFlag = "intro_done";
        private bool skip;

        private IEnumerator Start()
        {
            yield return null;                                       // chờ director dựng xong map / cờ
            yield return null;                                       // 16/11 (§237): để StoryScenes kịp nhận cảnh "start" (tranh mở đầu)
            while (StoryScenes.Busy) yield return null;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Flags == null || dir.Flags.HasAll(DoneFlag) || dir.Player == null) yield break;

            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            Transform shot1 = Find("intro_cam_1"), shot2 = Find("intro_cam_2");
            if (cam == null || shot1 == null) { dir.Flags.Set(DoneFlag); yield break; }

            float size = Camera.main.orthographicSize;
            float oldSharp = cam.focusSharpness;
            StartCoroutine(HoldInput());
            CutsceneState.Begin();

            // 11/11 (§229): khu chứa điểm lia camera (Map 1: zf, goblin đốt nhà) bắt đầu NGAY — trước đây khu chỉ bắt đầu khi Ren đi sâu vào
            // khung khu (§08/11 entryDepth) mà Ren đang bị khoá đứng ở mép khu -> camera lia tới chỉ thấy chữ, không có goblin.
            if (dir.TryGetZoneAt(shot1.position, out var shotZone, out _)) dir.ForceStartZone(shotZone);
            Hints.Show("Millbrook. New moon - and Grok's tithe-collectors have come.", 3.2f);
            Sfx.Play2D("cs_slide");          // 19/11 (§338): âm dẫn chuyện mở cảnh (lời kể -> tiếng lật trang do DialogueVoice phát)
            Sfx.Bed("cs_story_bed");         // nền dẫn chuyện suốt đoạn lia camera, tắt dần khi trả camera về Ren
            yield return Wait(1.2f);

            cam.focusSharpness = 1.4f;
            if (!skip) yield return Shot(cam, shot1, size, 1);
            if (!skip && shot2 != null) yield return Shot(cam, shot2, size, 2);

            cam.focusSharpness = 2.2f;
            cam.Release();
            Sfx.Bed(null);                   // 19/11 (§338)
            if (!skip) yield return Wait(1.6f);
            cam.focusSharpness = oldSharp;

            dir.Flags.Set(DoneFlag);                                 // HoldInput tự nhả khi có cờ
            yield return null;
            StartCoroutine(Tutorial(dir));
        }

        private IEnumerator Shot(CameraFollow cam, Transform at, float size, int shot)
        {
            cam.Focus(at.position, size);
            yield return Wait(2.4f);
            if (skip) yield break;
            float last = 0f;
            foreach (var so in GetComponentsInMap())
            {
                if (!so.m_TiledName.StartsWith("intro_say_")) continue;
                var p = so.GetComponent<SuperCustomProperties>();
                if (p == null || p.GetInt("shot", 1) != shot) continue;
                float delay = p.GetFloat("delay", 0f);
                bool goblin = p.GetString("color", "villager") == "goblin";
                // 19/11 (§338): giọng bíp — thuộc tính Tiled `voice` (vd "woman", "Goblin"), không có: goblin / dân (xen kẽ nữ - nam theo số thứ tự)
                int.TryParse(so.m_TiledName.Substring("intro_say_".Length), out int no);
                string voice = p.GetString("voice", goblin ? "goblin" : no % 2 == 0 ? "woman" : "villager");
                StartCoroutine(SayLater(so.transform.position, p.GetString("text", "..."), delay, goblin, voice));
                last = Mathf.Max(last, delay);
            }
            yield return Wait(last + 2.6f);
        }

        private IEnumerator SayLater(Vector3 at, string text, float delay, bool goblin, string voice)
        {
            yield return Wait(delay);
            if (skip) yield break;
            SpeechBubble.Show(at, text, 2.6f, goblin ? new Color(1f, 0.55f, 0.35f) : new Color(1f, 0.95f, 0.85f), null, voice);
        }

        /// <summary>Giữ khoá điều khiển suốt cảnh (menu Pause đóng sẽ nhả Blocked -> khoá lại mỗi khung); Space/Enter = bỏ qua.</summary>
        private IEnumerator HoldInput()
        {
            var dir = MapEncounterDirector.Instance;
            while (dir != null && !dir.Flags.HasAll(DoneFlag))
            {
                GameInput.Blocked = true;
                // 11/11 (§229): cảnh cốt truyện KHÔNG bỏ qua được (chỉ bảng giới thiệu boss mới bỏ qua được)
                yield return null;
            }
            GameInput.Blocked = false;
            CutsceneState.End();
        }

        private static readonly Color GoblinTalk = new Color(1f, 0.55f, 0.35f);

        /// <summary>02/11: dạy dần sau cảnh, theo kiểu "dừng game chờ đúng phím":
        /// đi tới trang trại -> tên cướp gần nhất quát doạ (khoá điều khiển ~2 giây) rồi lao tới -> sát người: DỪNG, nhấp nháy
        /// [J / chuột trái] ĐÁNH -> lần đầu nó gồng đòn: DỪNG, nhấp nháy [Q] NÉ -> tên thứ hai (đứng chờ từ đầu, cả hai cùng đợt) quát rồi lao vào.</summary>
        private IEnumerator Tutorial(MapEncounterDirector dir)
        {
            Hints.Show("Drive the raiders out! Follow the marker west to the burning farm.", 5f);
            var player = dir.Player;
            var combat = player.GetComponent<PlayerCombat>();
            var farm = Find("intro_cam_1");
            Vector2 farmPos = farm != null ? (Vector2)farm.position : (Vector2)player.position;

            // tên cướp của trận này: quái còn sống quanh trang trại
            // 02/11 (chạy thử): quái của khu ra theo kiểu "báo trước" (bụi + trễ 0.5 s/con) -> bỏ qua cảnh nhanh thì lúc này CHƯA có ai
            // -> trước đây cả bài hướng dẫn bị bỏ (không màn ATTACK, goblin chết thay vì bỏ chạy). Chờ tới khi đủ quân (tối đa 8 s).
            var raiders = new List<EnemyAI>();
            float waitUntil = Time.time + 8f, settle = -1f;
            while (Time.time < waitUntil)
            {
                raiders.Clear();
                foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                    if (e != null && e.Health != null && !e.Health.IsDead && ((Vector2)e.transform.position - farmPos).sqrMagnitude < 49f) raiders.Add(e);
                if (raiders.Count > 0 && settle < 0f) settle = Time.time + 1.2f;      // con đầu đã ra: đợi thêm cho các con sau
                if (settle > 0f && Time.time >= settle) break;
                yield return null;
            }
            if (raiders.Count == 0) yield break;
            // 02/11: cốt truyện "đuổi đi" — tên cướp không chết: còn 1 máu thì bỏ chạy, doạ về mách Grok (tên cuối có cảnh ngắn)
            foreach (var e in raiders) e.Health.Floor = 1f;
            StartCoroutine(WatchFlee(raiders, player, combat));

            // 1) chờ người chơi tới gần tên cướp đầu tiên
            EnemyAI first = null;
            float giveUp = Time.time + 180f;
            while (first == null && Time.time < giveUp && !dir.Flags.HasAll("zf_cleared"))
            {
                float best = 3.1f * 3.1f;
                foreach (var e in raiders)
                {
                    if (!Alive(e)) continue;
                    float d = ((Vector2)(e.transform.position - player.position)).sqrMagnitude;
                    if (d < best) { best = d; first = e; }
                }
                yield return null;
            }
            if (first == null) yield break;
            foreach (var e in raiders) if (Alive(e)) e.Held = true;

            // 2) 02/11: tên cướp bước lại gần (~1.35 đv, cả hai cùng trong khung hình, bóng thoại không bị cắt) rồi mới quát, sau đó lao vào
            first.LookAtPlayer = true;
            first.CutsceneActor = true;                              // 04/11: quái khác đứng im khi diễn cảnh, riêng tên này được bước tới
            CutsceneState.Begin();
            float stepEnd = Time.time + 2.5f;
            first.Held = false;
            while (Alive(first) && Time.time < stepEnd &&
                   ((Vector2)(first.transform.position - player.position)).magnitude > 1.35f)
            {
                GameInput.Blocked = true;
                if (combat != null) combat.LookAt(first.transform.position - player.position, 0.5f);
                yield return null;
            }
            if (Alive(first)) first.Held = true;
            if (combat != null && first != null) combat.LookAt(first.transform.position - player.position, 2.5f);
            if (first != null)
                SpeechBubble.Show(first.transform.position + new Vector3(0f, 0.75f, 0f), "Heh! Fresh meat for the fire!", 2f, GoblinTalk).Follow(first.transform);
            yield return Block(2f);
            CutsceneState.End();
            if (first != null) { first.Held = false; first.CutsceneActor = false; first.Provoke(); }   // 19/11 (§342): lao vào ngay, không đứng chờ

            // 3) ĐÁNH: dừng khi nó tới sát người
            while (Alive(first) && ((Vector2)(first.transform.position - player.position)).magnitude > 0.62f && !dir.Flags.HasAll("zf_cleared"))
                yield return null;
            if (Alive(first))
            {
                // 02/11 (chạy thử): goblin vung đuốc nhanh (0.2 s) hơn nhát chém (0.48 s tới khung trúng) và đẩy lùi người chơi ->
                // nhát chém đầu tiên của bài hướng dẫn hay TRƯỢT. Giữ goblin đứng yên + người chơi không bị đánh trong lúc nhát chém
                // đầu tiên ra đòn -> bài học "chém trước khi nó kịp đánh" luôn thành công.
                first.Held = true;
                yield return Prompt(combat, GameAction.Attack, "ATTACK",
                    $"{GameInput.Label(GameAction.Attack)}  /  LEFT CLICK", "Strike the raider before it strikes you.",
                    first.transform.position - player.position);
                var php = player.GetComponent<Health>();
                if (php != null) php.Invulnerable = true;
                yield return new WaitForSeconds(0.7f);
                if (php != null) php.Invulnerable = false;
                if (Alive(first) && first.Health.Current > first.Health.Floor + 0.01f) first.Held = false;   // trượt thật (hiếm): đánh tiếp như thường
            }

            // 4) 02/11: màn DODGE [Q] chuyển sang khu thằn lằn (TutorialDirector)

            // 5) tên thứ hai: chờ tên đầu gục (hoặc một lúc), quát rồi lao vào
            float next = Time.time + 8f;
            while (Alive(first) && Time.time < next) yield return null;
            yield return new WaitForSeconds(0.6f);
            foreach (var e in raiders)
            {
                if (e == first || !Alive(e) || !e.Held) continue;
                // 02/11 (chạy thử): tên thứ hai đứng chờ ngoài màn hình -> quát mà người chơi không thấy, rồi đứng im (ngoài tầm phát hiện).
                // Cho nó tự chạy tới gần (~1.5 đv) rồi mới quát + lao vào.
                if (e.HasAnimState("Move")) e.PlayState("Move");
                for (float t = 0f; t < 4f && Alive(e) && player != null; t += Time.deltaTime)
                {
                    Vector2 to = (Vector2)(player.position - e.transform.position);
                    if (to.magnitude <= 1.5f) break;
                    e.FaceX(to.x);
                    e.TeleportTo(Vector2.MoveTowards(e.transform.position, player.position, 1.9f * Time.deltaTime));
                    yield return null;
                }
                if (!Alive(e)) continue;
                if (e.HasAnimState("Idle")) e.PlayState("Idle");
                e.LookAtPlayer = true;
                if (combat != null) combat.LookAt(e.transform.position - player.position, 1.2f);
                SpeechBubble.Show(e.transform.position + new Vector3(0f, 0.75f, 0f), "You'll burn with the rest of 'em!", 2f, GoblinTalk).Follow(e.transform);
                yield return new WaitForSeconds(0.9f);
                e.Held = false;
                // 19/11 (§342): trước đây chỉ thả Held -> tên thứ hai đứng im chờ Ren tới: nó đã bị kéo xa chỗ sinh (home) quá dây xích,
                // EnemyAI không "phát hiện" Ren (Ren ngoài vùng nhà của nó) / hoặc quay về nhà. Provoke = gây hấn ngay, bỏ dây xích.
                e.Provoke();
            }
            foreach (var e in raiders) if (e != null) { e.Held = false; if (Alive(e) && e.Health.Current > e.Health.Floor + 0.01f) e.Provoke(); }   // phòng hờ: không ai bị giữ mãi / đứng im
        }

        private static bool Alive(EnemyAI e) => e != null && e.Health != null && !e.Health.IsDead;

        private IEnumerator WatchFlee(List<EnemyAI> raiders, Transform player, PlayerCombat combat)
        {
            var fleeing = new HashSet<EnemyAI>();
            while (true)
            {
                int left = 0;
                foreach (var e in raiders) if (e != null && !fleeing.Contains(e)) left++;
                if (left == 0) yield break;
                foreach (var e in raiders)
                {
                    if (e == null || fleeing.Contains(e) || e.Health.Current > e.Health.Floor + 0.01f) continue;
                    fleeing.Add(e);
                    StartCoroutine(Flee(e, left == 1, player, combat));
                    left--;
                }
                yield return null;
            }
        }

        private IEnumerator Flee(EnemyAI e, bool last, Transform player, PlayerCombat combat)
        {
            e.Held = true;
            e.Health.Invulnerable = true;
            e.LookAtPlayer = false;
            var dir = MapEncounterDirector.Instance;
            string line = last ? "You'll regret this! Grok will hear of it!" : "Ow-ow-OW! I'm telling Grok!";
            SpeechBubble.Show(e.transform.position + new Vector3(0f, 0.75f, 0f), line, 2.4f, GoblinTalk).Follow(e.transform);   // 02/11: chữ chạy theo goblin
            if (last)
            {
                CutsceneState.Begin();                               // tên cuối: khoá điều khiển một nhịp, Ren nhìn theo
                if (combat != null) combat.LookAt(e.transform.position - player.position, 3f);
                yield return Block(1.1f);
            }
            else yield return new WaitForSeconds(0.6f);
            if (e == null) { if (last) CutsceneState.End(); yield break; }

            // 02/11: chạy theo đường làng về phía ĐÔNG, ra cây cầu (đường về trại Grok), mờ dần rồi biến mất —
            // khu tính là đã dọn khi chúng đi hết
            Vector2 target = dir != null ? (Vector2)dir.TileToWorld(91.0f, 79.4f) : (Vector2)e.transform.position + Vector2.right * 10f;
            if (e.HasAnimState("Move")) e.PlayState("Move");
            e.FaceX(target.x - e.transform.position.x);
            var sr = e.GetComponentInChildren<SpriteRenderer>();
            Color c0 = sr != null ? sr.color : Color.white;
            const float runFor = 3.8f;
            for (float t = 0f; t < runFor && e != null; t += Time.deltaTime)
            {
                e.TeleportTo(Vector2.MoveTowards(e.transform.position, target, 1.7f * Time.deltaTime));   // 02/11: 2.8 -> 1.7 đv/s (người chơi 2): còn thấy nó chạy, đọc kịp câu
                if (sr != null && t > runFor - 1f) sr.color = new Color(c0.r, c0.g, c0.b, c0.a * Mathf.Clamp01((runFor - t) / 1f));
                if (last && t > 0.8f && CutsceneState.Active) { CutsceneState.End(); last = false; }
                yield return null;
            }
            if (last) CutsceneState.End();
            if (e != null) Destroy(e.gameObject);
        }

        /// <summary>Khoá điều khiển `seconds` giây (đóng menu Pause sẽ nhả Blocked -> khoá lại mỗi khung).</summary>
        private static IEnumerator Block(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { GameInput.Blocked = true; yield return null; }
            GameInput.Blocked = false;
        }

        private static IEnumerator Prompt(PlayerCombat combat, GameAction a, string title, string keys, string sub, Vector2 dir)
            => TutorialPrompt.Run(combat, a, title, keys, sub, dir);

        private IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skip) yield return null;
        }

        private Transform Find(string tiledName)
        {
            foreach (var so in GetComponentsInMap()) if (so.m_TiledName == tiledName) return so.transform;
            return null;
        }

        private SuperObject[] cache;
        private SuperObject[] GetComponentsInMap()
        {
            if (cache == null)
            {
                var dir = MapEncounterDirector.Instance;
                cache = dir != null && dir.mapRoot != null ? dir.mapRoot.GetComponentsInChildren<SuperObject>(true) : new SuperObject[0];
            }
            return cache;
        }
    }
}
