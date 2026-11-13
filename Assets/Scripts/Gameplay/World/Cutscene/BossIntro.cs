using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: bảng tên boss (kiểu Enter the Gungeon / Hollow Knight) khi boss xuất hiện.
    /// Lần đầu gặp boss (cờ "bossintro_&lt;tên&gt;", giữ suốt lượt chơi): khoá điều khiển, boss đứng yên, camera lia tới boss,
    /// boss gầm (hoạt ảnh Windup + rung màn hình), bảng tên hiện ~3 giây, trả camera và quyền điều khiển, rồi mới hiện gợi ý của khu.
    /// Gặp lại (sau khi gục): chỉ hiện bảng tên ngắn, không khoá gì. Space / Enter: bỏ qua.
    /// Bảng tên do HUD (RequiemHUD) vẽ, đọc các trường tĩnh CardName / CardSub / CardStart / CardLength.
    /// Tên lấy từ displayName: "Grok, the Greystone Chieftain" -> GROK + THE GREYSTONE CHIEFTAIN (tách ở "—" hoặc ", ").
    /// </summary>
    public class BossIntro : MonoBehaviour
    {
        public static string CardName = "", CardSub = "";
        public static float CardStart = -99f, CardLength = 0f;
        /// <summary>02/11: chân dung (sprite hiện tại của boss) + màu dải của bảng giới thiệu kiểu Soul Knight.</summary>
        public static Sprite CardSprite;
        /// <summary>02/11: tên ảnh chân dung riêng trong Resources/UI/Requiem (thuộc tính `portrait` trên object boss), rỗng = dùng sprite.</summary>
        public static string CardPortrait = "";
        public static Color CardColor = new Color(0.47f, 0.36f, 0.16f);
        /// <summary>Bảng tên đang hiện (HUD ẩn thanh máu boss trong lúc này).</summary>
        public static bool CardActive => Time.time - CardStart < CardLength;

        private MapEncounterDirector dir;
        private bool skip;

        private void Awake()
        {
            dir = GetComponent<MapEncounterDirector>();
            if (dir != null) dir.BossSpawned += OnBossSpawned;
            CardStart = -99f; CardLength = 0f;
        }

        private void OnDestroy()
        {
            if (dir != null) dir.BossSpawned -= OnBossSpawned;
        }

        public static void SplitName(string full, out string name, out string sub)
        {
            full = full ?? "";
            int cut = full.IndexOf('—');
            int len = 1;
            if (cut < 0) { cut = full.IndexOf(", "); len = 2; }
            if (cut > 0) { name = full.Substring(0, cut).Trim(); sub = full.Substring(cut + len).Trim(); }
            else { name = full.Trim(); sub = ""; }
        }

        private void OnBossSpawned(EnemyAI boss, string zoneHint)
        {
            if (boss == null) return;
            StartCoroutine(Run(boss, zoneHint));
        }

        private IEnumerator Run(EnemyAI boss, string zoneHint)
        {
            SplitName(boss.displayName, out var name, out var sub);
            string flag = "bossintro_" + name.ToLowerInvariant().Replace(' ', '_');
            bool first = dir.Flags != null && !dir.Flags.HasAll(flag);
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;

            // 02/11: thuộc tính trên object "boss" của Tiled (vd grok_fight): intro_line1..N (boss nói trước khi gầm),
            // toss_minions (đánh bay lũ đệ đứng cạnh, chúng choáng tới khi boss còn nửa máu), wake_line (câu gọi đệ dậy)
            var props = BossProps(boss);
            // 03/11: thẻ tên đặt riêng (card_name / card_sub), màn xuất hiện (entrance = burst | leap | wake — xem Entrance)
            string entrance = props != null ? props.GetString("entrance", "") : "";
            string eflag = props != null ? props.GetString("entrance_flag", "") : "";
            if (props != null)
            {
                string cn = props.GetString("card_name", ""), cs = props.GetString("card_sub", "");
                if (cn.Length > 0) name = cn;                      // cờ bossintro_* vẫn theo tên gốc
                if (cs.Length > 0) sub = cs;
            }
            var hidden = new List<SpriteRenderer>();                // burst / leap: boss chưa lộ diện tới lúc xông ra
            if (first && cam != null && dir.Player != null && (entrance == "burst" || entrance == "leap" || entrance == "mist" || entrance == "rise"))
                foreach (var r in boss.GetComponentsInChildren<SpriteRenderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
            var dead = new List<EnemyAI>();                         // sao choáng của lũ đệ lượt trước (đã bị xoá khi đấu trường làm lại)
            foreach (var kv in stars) if (kv.Key == null) { if (kv.Value != null) kv.Value.Stop(); dead.Add(kv.Key); }
            foreach (var k in dead) stars.Remove(k);
            boss.Held = true;                                       // giữ ngay khung đầu (đệ sinh cùng đợt, xét ở khung sau)
            yield return null;
            var minions = new List<EnemyAI>();
            if (props != null && props.GetBool("toss_minions", false))
                foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                    if (e != null && e != boss && e.Health != null && !e.Health.IsDead &&
                        ((Vector2)(e.transform.position - boss.transform.position)).sqrMagnitude < 16f) { e.Held = true; minions.Add(e); }
            if (minions.Count > 0) HookWake(boss, minions, props);

            // 11/11 (§229): GẶP LẠI boss (đã xem cảnh / gục rồi quay lại) trước đây chỉ hiện bảng tên, boss + đệ cử động ngay, Ren đi lại được,
            // đệ của Grok đã nằm sẵn (không thấy cú đánh bay). Giờ gặp lại cũng là cảnh ngắn có khoá: không hội thoại / màn xuất hiện,
            // nhưng vẫn gồng-nện-đánh bay đệ + bảng tên, người chơi và quái đứng yên, Space/Enter bỏ qua được.
            bool replay = !first;
            if (cam == null || dir.Player == null)
            {
                if (eflag.Length > 0 && dir.Flags != null) dir.Flags.Set(eflag);
                foreach (var m in minions) StartCoroutine(Toss(boss, m, 0f));    // gặp lại: đệ nằm choáng sẵn
                SetPortrait(boss, props);
                ShowCard(name, sub, 2.2f);
                if (!string.IsNullOrEmpty(zoneHint)) Hints.Show(zoneHint, 4f);
                boss.Held = false;
                yield break;
            }

            if (first) dir.Flags.Set(flag);
            else if (eflag.Length > 0 && dir.Flags != null) dir.Flags.Set(eflag);
            skip = false;
            var ph = dir.Player.GetComponent<Health>();            // bộ chiêu riêng của vài boss (Update / coroutine) vẫn chạy: không để trúng đòn lúc bị khoá
            if (ph != null) ph.Invulnerable = true;
            var pc = dir.Player.GetComponent<PlayerCombat>();
            if (pc != null) pc.LookAt(boss.transform.position - dir.Player.position, 12f);   // Ren nhìn về phía boss suốt cảnh
            bool holding = true;
            StartCoroutine(HoldInput(() => holding));
            CutsceneState.Begin();

            float size = Camera.main.orthographicSize;
            float oldSharp = cam.focusSharpness;
            cam.focusSharpness = 3f;
            if (first && entrance.Length > 0) yield return Entrance(boss, props, entrance, eflag, cam, size, hidden);
            foreach (var r in hidden) if (r != null) r.enabled = true;
            // 02/11: camera đưa hẳn về boss (boss ở giữa khung hình) suốt lời phàn nàn + cú đánh bay đệ
            Vector3 at = boss.transform.position + Vector3.up * 0.45f;
            cam.Focus(at, size);                                    // giữ nguyên cỡ khung: đổi cỡ làm ảnh pixel co giãn lệch nhịp
            yield return Wait(0.8f);

            // boss nói: 02/11 khung hội thoại dưới màn hình (tên boss, tự sang câu; T/chuột = câu tiếp, Space = bỏ cảnh)
            var talk = new List<string>();
            for (int i = 1; i <= 6 && props != null; i++)
            {
                string line = props.GetString("intro_line" + i, "");
                if (string.IsNullOrEmpty(line)) break;
                talk.Add(line);
            }
            if (first && talk.Count > 0 && !skip)
            {
                DialogueBox.Open(name, talk.ToArray(), null, 1.4f);
                while (DialogueBox.Active && !skip) yield return null;
                if (DialogueBox.Active) DialogueBox.Close();
            }

            // 07/11: gồng + nện + rung CHỈ khi boss có đệ để đánh bay (Grok) hoặc `intro_slam` = true. Trước đây MỌI boss vung một
            // đòn thường giữa cảnh (người chơi tưởng bị đánh trước khi trận bắt đầu). Boss khác diễn động tác riêng `intro_anim`
            // (Vow, Toll, Cast, Prepare...), `intro_roar` = true thì rung nhẹ theo.
            bool slam = minions.Count > 0 || (props != null && props.GetBool("intro_slam", false));
            if (slam)
            {
                // gồng MỘT lần rồi nện xuống (Windup là clip lặp 0.625 s: để lặp suốt cảnh thì boss giật giật liên tục); lúc nện: đệ bay
                float windT = Clip(boss, "Windup", 0.625f), slamT = Clip(boss, "Attack", 0.6f);
                if (!skip && boss.HasAnimState("Windup")) { boss.PlayState("Windup"); yield return Wait(windT); }
                if (!skip && boss.HasAnimState("Attack")) boss.PlayState("Attack");
                foreach (var m in minions) StartCoroutine(Toss(boss, m, skip ? 0f : 0.45f));
                if (!skip) StartCoroutine(Rumble(0.45f, 0.07f));
                yield return Wait(slamT);
                if (boss != null && !boss.Health.IsDead) boss.PlayState("Idle");
                yield return Wait(0.35f);                           // đệ vừa bay đi -> bảng giới thiệu làm điểm nhấn
            }
            else
            {
                string ia = props != null ? props.GetString("intro_anim", "") : "";
                if (!skip && ia.Length > 0 && boss.HasAnimState(ia))
                {
                    boss.PlayState(ia);
                    if (props.GetBool("intro_roar", false)) StartCoroutine(Rumble(0.4f, 0.05f));
                    var bsr = boss.GetComponentInChildren<SpriteRenderer>(true);
                    if (bsr != null) PlayVfx(props, "intro_vfx", boss.transform.position, bsr.sortingLayerID, bsr.sortingOrder - 1, BossScale(bsr));   // 07/11: vd hồi chuông Morrow
                    yield return Wait(Clip(boss, ia, 0.8f));
                    if (boss != null && !boss.Health.IsDead) boss.PlayState("Idle");
                }
                yield return Wait(0.25f);
            }
            SetPortrait(boss, props);
            ShowCard(name, sub, replay ? 2.2f : 2.8f);
            yield return Wait(replay ? 1.8f : 2.6f);
            if (skip && CardActive) CardLength = Mathf.Min(CardLength, Time.time - CardStart + 0.5f);   // bỏ qua: tắt dần ngay

            cam.Release();
            StartCoroutine(FadeFlares(1.2f));                       // 07/11: đèn bùng lên trong cảnh dịu về
            yield return new WaitForSeconds(0.35f);
            cam.focusSharpness = oldSharp;
            holding = false;
            CutsceneState.End();
            if (pc != null) pc.StopLook();
            if (ph != null) ph.Invulnerable = false;
            if (boss != null && !boss.Health.IsDead) boss.PlayState("Idle");
            if (!string.IsNullOrEmpty(zoneHint)) Hints.Show(zoneHint, 4.5f);
            yield return new WaitForSeconds(0.4f);                  // một nhịp cho người chơi định thần trước khi boss lao tới
            if (boss != null) boss.Held = false;
        }

        // ---- 03/11: màn xuất hiện của boss (thuộc tính trên object "boss" của Tiled)
        //   burst : nện thình thịch sau `entrance_shake` (nồi hơi, cửa...) rồi phá ra, lao từ `entrance_from` tới chỗ đứng (hoạt ảnh entrance_anim, mặc định ChargeRun)
        //   leap  : `entrance_shake` (bồn nuôi) rung, cờ `entrance_flag` bật (cơ quan toggle đổi bồn có sinh vật -> bồn rỗng), boss nhảy vòng cung ra
        //   wake  : boss đang ngủ tại chỗ: 3 nhịp tim (quầng sáng `wake_color` + rung) mạnh dần rồi mới gầm
        private IEnumerator Entrance(EnemyAI boss, SuperCustomProperties p, string kind, string eflag, CameraFollow cam, float size, List<SpriteRenderer> hidden)
        {
            if (boss == null || p == null) yield break;
            Vector3 home = boss.transform.position;
            Vector3 from = CutsceneFx.TileProp(dir, p, "entrance_from", home);
            var shake = CutsceneFx.Find(dir.mapRoot, p.GetString("entrance_shake", ""));
            var sr0 = boss.GetComponentInChildren<SpriteRenderer>(true);
            int layer = sr0 != null ? sr0.sortingLayerID : 0, order = sr0 != null ? sr0.sortingOrder : 2;

            // ---- 07/11: màn xuất hiện riêng cho từng boss (thay cho "đất rung tại chỗ" dùng chung)
            //   mist     : hiện ra từ sương máu xoáy (Isolde) — `mist_color`
            //   rise     : trồi lên từ lòng đất, lớn dần từ chân, đất nứt văng sỏi (Ent)
            //   approach : đèn `entrance_lights` bùng lên lần lượt, boss từ `entrance_from` bước tới chỗ đứng (Lantern Knight)
            //   turn     : boss quay lưng (nhìn Bắc) cầu nguyện, `entrance_lights` bùng lên lần lượt, rồi quay lại nhìn Ren (Morrow)
            if (kind == "mist")
            {
                Color mc = ColorUtility.TryParseHtmlString(p.GetString("mist_color", "#ff3a6a"), out var mcc) ? mcc : new Color(1f, 0.25f, 0.4f);
                var glow = Flare(home + Vector3.up * 0.4f, mc, 0f, 3.2f);
                cam.Focus(home + Vector3.up * 0.6f, size);
                yield return Wait(0.5f);
                for (int i = 0; i < 12 && !skip; i++)                      // sương xoáy co dần về chỗ boss đứng
                {
                    float a = i * 0.9f, rr = Mathf.Lerp(1.3f, 0.25f, i / 11f);
                    StartCoroutine(CutsceneFx.Puff(home + new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.55f + 0.2f, 0f), layer, order + 1, 16f));
                    glow.intensity = Mathf.Lerp(0.4f, 2.4f, i / 11f);
                    if (i == 6) PlayVfx(p, "entrance_vfx", home, layer, order + 1, BossScale(sr0));   // 07/11: dơi + sương máu (PixelLab) nổ đúng lúc hiện hình
                    if (i % 4 == 0) CameraFollow.KickMain(Random.insideUnitCircle.normalized, 0.02f, 0.15f);
                    yield return Wait(0.11f);
                }
                var cols = new List<Color>();
                foreach (var r in hidden) if (r != null) { cols.Add(r.color); r.color = new Color(r.color.r, r.color.g, r.color.b, 0f); r.enabled = true; }
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    float u = t / 0.7f;
                    for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) { var c0 = cols[i]; hidden[i].color = new Color(c0.r, c0.g, c0.b, c0.a * u); }
                    boss.TeleportTo(home + Vector3.up * 0.3f * (1f - u * u));
                    yield return null;
                }
                for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].color = cols[i];
                hidden.Clear();
                boss.TeleportTo(home);
                CameraFollow.KickMain(Vector2.down, 0.06f, 0.3f);
                CutsceneFx.Burst(this, home, 0.6f, 0.22f, 7, 0, layer, order);
                glow.intensity = 1.4f;
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                string ea = p.GetString("entrance_anim", "");
                if (ea.Length > 0 && boss.HasAnimState(ea)) { boss.PlayState(ea); yield return Wait(Clip(boss, ea, 0.7f)); boss.PlayState("Idle"); }
                yield break;
            }
            if (kind == "rise")
            {
                var body = boss.Body != null ? boss.Body.transform : boss.transform;
                Vector3 s0 = body.localScale;
                body.localScale = new Vector3(s0.x, s0.y * 0.05f, s0.z);
                foreach (var r in hidden) if (r != null) r.enabled = true;
                hidden.Clear();
                cam.Focus(home + Vector3.up * 0.6f, size);
                yield return Wait(0.6f);
                float[] steps = { 0.3f, 0.65f, 1f };
                for (int i = 0; i < steps.Length; i++)
                {
                    CameraFollow.KickMain(Vector2.down, 0.05f + 0.035f * i, 0.3f);
                    if (i == 0) PlayVfx(p, "entrance_vfx", home, layer, order + 1, BossScale(sr0));   // 07/11: rễ gai + đất nứt (PixelLab)
                    CutsceneFx.Burst(this, home, 0.5f + 0.15f * i, 0.18f, 4 + 2 * i, 3 + 2 * i, layer, order + 1);
                    float h0 = body.localScale.y / Mathf.Max(0.0001f, s0.y);
                    for (float t = 0f; t < 0.32f; t += Time.deltaTime)
                    {
                        float u = 1f - (1f - t / 0.32f) * (1f - t / 0.32f);
                        body.localScale = new Vector3(s0.x, s0.y * Mathf.Lerp(h0, steps[i], u), s0.z);
                        yield return null;
                    }
                    if (!skip) yield return Wait(i < 2 ? 0.45f : 0.2f);
                }
                body.localScale = s0;
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                yield break;
            }
            if (kind == "approach" || kind == "turn")
            {
                Color lc = ColorUtility.TryParseHtmlString(p.GetString("lights_color", "#ffd27a"), out var lcc) ? lcc : new Color(1f, 0.82f, 0.48f);
                if (kind == "turn" && boss.HasAnimState("Idle_N")) boss.PlayState("Idle_N");
                if (kind == "approach") boss.TeleportTo(from);
                cam.Focus((kind == "approach" ? Vector3.Lerp(from, home, 0.5f) : home) + Vector3.up * 0.5f, size);
                yield return Wait(0.6f);
                foreach (var n in p.GetString("entrance_lights", "").Split(','))  // đèn bùng lên lần lượt
                {
                    if (skip) break;
                    var t = CutsceneFx.Find(dir.mapRoot, n.Trim());
                    if (t == null) continue;
                    var col = t.GetComponent<Collider2D>();
                    Vector3 lp = col != null ? (Vector3)col.bounds.center : t.position;
                    StartCoroutine(FlareUp(Flare(lp, lc, 0f, 2.4f), 2.6f, 1.1f));
                    var lsr = t.GetComponentInChildren<SpriteRenderer>();
                    PlayVfx(p, "light_vfx", lp, lsr != null ? lsr.sortingLayerID : layer, (lsr != null ? lsr.sortingOrder : order) + 5, 0.6f);   // 07/11: lửa bùng ở đầu đèn
                    CameraFollow.KickMain(Vector2.down, 0.025f, 0.15f);
                    yield return Wait(0.42f);
                }
                if (kind == "turn")
                {
                    yield return Wait(0.3f);
                    boss.PlayState("Idle");                                  // quay lại nhìn Ren
                    yield return Wait(0.35f);
                }
                else
                {
                    float spd = p.GetFloat("entrance_speed", 1.1f), dist = Vector3.Distance(from, home);
                    float walkT = Mathf.Max(0.4f, dist / Mathf.Max(0.2f, spd)), nextStep = 0f;
                    if (boss.HasAnimState("Move")) boss.PlayState("Move");
                    for (float t = 0f; t < walkT && !skip; t += Time.deltaTime)
                    {
                        boss.TeleportTo(Vector3.Lerp(from, home, t / walkT));
                        if (t >= nextStep) { nextStep = t + 0.5f; CameraFollow.KickMain(Vector2.down, 0.02f, 0.12f); StartCoroutine(CutsceneFx.Puff(boss.transform.position, layer, order - 1, 16f)); }
                        cam.Focus(boss.transform.position + Vector3.up * 0.45f, size);
                        yield return null;
                    }
                    boss.TeleportTo(home);
                    boss.PlayState("Idle");
                }
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                yield break;
            }

            if (kind == "wake")
            {
                Color wc = ColorUtility.TryParseHtmlString(p.GetString("wake_color", "#b48cff"), out var c) ? c : new Color(0.7f, 0.55f, 1f);
                var go = new GameObject("Boss Wake Glow");
                go.transform.position = Head(boss, -0.5f);
                var glow = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
                glow.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
                glow.pointLightOuterRadius = 3f; glow.pointLightInnerRadius = 0.3f;
                glow.color = wc; glow.intensity = 0f;
                cam.Focus(home + Vector3.up * 0.6f, size);
                yield return Wait(0.9f);
                for (int beat = 0; beat < 3 && !skip; beat++)
                {
                    float peak = 1.3f + beat * 1.0f;
                    CameraFollow.KickMain(Vector2.down, 0.03f + beat * 0.025f, 0.25f);
                    StartCoroutine(CutsceneFx.Puff(home + new Vector3(Random.Range(-0.5f, 0.5f), 0.05f, 0f), layer, order + 1));
                    for (float t = 0f; t < 0.75f - beat * 0.15f && !skip; t += Time.deltaTime)
                    {
                        float u = t / (0.75f - beat * 0.15f);
                        glow.intensity = peak * (u < 0.2f ? u / 0.2f : 1f - (u - 0.2f) / 0.8f);
                        yield return null;
                    }
                }
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                if (boss.HasAnimState("Awake")) boss.PlayState("Awake");
                glow.intensity = 3f;
                CameraFollow.KickMain(Vector2.down, 0.12f, 0.4f);
                CutsceneFx.Burst(this, home, 0.6f, 0.22f, 8, 0, layer, order);
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) { glow.intensity = Mathf.Lerp(3f, 0f, t / 0.8f); yield return null; }
                Destroy(go);
                yield break;
            }

            // burst / leap: camera nhìn vào chỗ boss sắp xông ra
            cam.Focus(Vector3.Lerp(from, home, 0.4f) + Vector3.up * 0.3f, size);
            boss.TeleportTo(from);
            yield return Wait(0.8f);
            int thuds = kind == "burst" ? 3 : 2;
            for (int i = 0; i < thuds && !skip; i++)
            {
                CameraFollow.KickMain(Vector2.down, 0.035f + 0.02f * i, 0.22f);
                StartCoroutine(CutsceneFx.Jiggle(shake, 0.22f, 0.025f + 0.012f * i));
                StartCoroutine(CutsceneFx.Puff(from + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.1f, 0.15f), 0f), layer, order + 1));
                yield return Wait(kind == "burst" ? 0.55f - 0.1f * i : 0.65f);
            }
            if (eflag.Length > 0) dir.Flags.Set(eflag);
            CutsceneFx.Toggle(dir.mapRoot, p.GetString("entrance_hide", ""), false);
            CutsceneFx.Toggle(dir.mapRoot, p.GetString("entrance_show", ""), true);
            CameraFollow.KickMain(Vector2.down, 0.15f, 0.45f);
            CutsceneFx.Burst(this, from, 0.55f, 0.22f, 9, 7, layer, order);
            foreach (var r in hidden) if (r != null) r.enabled = true;
            cam.Focus(home + Vector3.up * 0.45f, size);

            string anim = p.GetString("entrance_anim", kind == "burst" ? "ChargeRun" : "Move");
            if (boss.HasAnimState(anim)) boss.PlayState(anim);
            float dur = p.GetFloat("entrance_time", kind == "burst" ? 0.45f : 0.7f), arc = kind == "leap" ? p.GetFloat("entrance_arc", 0.9f) : 0f;
            float nextTrail = 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                boss.TeleportTo(Vector3.Lerp(from, home, kind == "burst" ? 1f - (1f - u) * (1f - u) : u) + Vector3.up * (arc * 4f * u * (1f - u)));
                if (kind == "burst" && t >= nextTrail)                    // bụi cuốn theo vó
                {
                    nextTrail = t + 0.07f;
                    StartCoroutine(CutsceneFx.Puff(boss.transform.position, layer, order - 1, 16f));
                }
                yield return null;
            }
            boss.TeleportTo(home);
            CameraFollow.KickMain(Vector2.down, kind == "leap" ? 0.12f : 0.07f, 0.3f);
            CutsceneFx.Burst(this, home, 0.5f, 0.18f, kind == "leap" ? 8 : 5, kind == "leap" ? 4 : 0, layer, order);
            if (boss.HasAnimState("Idle")) boss.PlayState("Idle");
            yield return Wait(0.4f);
        }

        // ---- 07/11: hiệu ứng PixelLab trong cảnh (Resources/VFX/<tên>, xem SpriteVfx). Tên lấy từ thuộc tính `key` trên object boss
        //   (rỗng / "none" = không phát); `<key>_scale` ghi đè tỉ lệ, `<key>_fps` ghi đè tốc độ khung (mặc định 14).
        private static void PlayVfx(SuperCustomProperties p, string key, Vector3 at, int layer, int order, float defScale)
        {
            if (p == null) return;
            string n = p.GetString(key, "");
            if (n.Length == 0 || n == "none") return;
            SpriteVfx.Spawn(n, at, p.GetFloat(key + "_scale", defScale), layer, order, false, p.GetFloat(key + "_fps", 14f));
        }

        /// 07/11: tỉ lệ hiệu ứng theo cỡ thật của boss (hiệu ứng vẽ quanh sprite gốc 1 điểm ảnh = 1 điểm ảnh, PPU 100).
        private static float BossScale(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return 1f;
            return Mathf.Abs(sr.transform.lossyScale.y) * 100f / Mathf.Max(1f, sr.sprite.pixelsPerUnit);
        }

        private static readonly Color BossTalk = new Color(1f, 0.45f, 0.35f);

        // ---- 07/11: đèn bùng lên trong cảnh (tắt dần khi cảnh xong)
        private readonly List<UnityEngine.Rendering.Universal.Light2D> flares = new List<UnityEngine.Rendering.Universal.Light2D>();

        private UnityEngine.Rendering.Universal.Light2D Flare(Vector3 at, Color c, float intensity, float radius)
        {
            var go = new GameObject("Boss Intro Flare");
            go.transform.position = at;
            var l = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            l.color = c; l.intensity = intensity;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * 0.12f;
            flares.Add(l);
            return l;
        }

        private static IEnumerator FlareUp(UnityEngine.Rendering.Universal.Light2D l, float peak, float settle)
        {
            for (float t = 0f; t < 0.35f && l != null; t += Time.deltaTime)
            {
                float u = t / 0.35f;
                l.intensity = u < 0.4f ? peak * u / 0.4f : Mathf.Lerp(peak, settle, (u - 0.4f) / 0.6f);
                yield return null;
            }
            if (l != null) l.intensity = settle;
        }

        private IEnumerator FadeFlares(float seconds)
        {
            var list = new List<UnityEngine.Rendering.Universal.Light2D>(flares);
            flares.Clear();
            var start = new List<float>();
            foreach (var l in list) start.Add(l != null ? l.intensity : 0f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                for (int i = 0; i < list.Count; i++) if (list[i] != null) list[i].intensity = start[i] * (1f - t / seconds);
                yield return null;
            }
            foreach (var l in list) if (l != null) Destroy(l.gameObject);
        }

        // ---- 07/11: ảnh bảng tên: cắt sát phần có hình (lưới Tight của sprite) -> ảnh to, cân giữa, không còn khoảng trong suốt
        private static readonly Dictionary<Sprite, Sprite> trimmed = new Dictionary<Sprite, Sprite>();

        public static Sprite Trim(Sprite s)
        {
            if (s == null) return null;
            if (trimmed.TryGetValue(s, out var t) && t != null) return t;
            var v = s.vertices;
            if (v == null || v.Length < 3 || s.packed) { trimmed[s] = s; return s; }
            float ppu = s.pixelsPerUnit;
            Vector2 piv = s.pivot;                                       // pixel trong rect
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in v)
            {
                float x = p.x * ppu + piv.x, y = p.y * ppu + piv.y;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            var r = s.rect;
            minX = Mathf.Clamp(Mathf.Floor(minX), 0f, r.width); minY = Mathf.Clamp(Mathf.Floor(minY), 0f, r.height);
            maxX = Mathf.Clamp(Mathf.Ceil(maxX), 0f, r.width); maxY = Mathf.Clamp(Mathf.Ceil(maxY), 0f, r.height);
            if (maxX - minX < 2f || maxY - minY < 2f || (maxX - minX >= r.width - 1f && maxY - minY >= r.height - 1f)) { trimmed[s] = s; return s; }
            var cut = new Rect(r.x + minX, r.y + minY, maxX - minX, maxY - minY);
            var ns = Sprite.Create(s.texture, cut, new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
            ns.name = s.name + "_card";
            trimmed[s] = ns;
            return ns;
        }

        /// <summary>Object type "boss" của Tiled gần boss nhất (vd grok_fight) — chứa lời thoại / tuỳ chọn cảnh.</summary>
        private SuperCustomProperties BossProps(EnemyAI boss) => PropsFor(dir, boss);

        /// <summary>Thuộc tính object "boss" (lớp triggers) gần boss nhất. 04/11: public để BossRelic dùng chung.</summary>
        public static SuperCustomProperties PropsFor(MapEncounterDirector dir, EnemyAI boss)
        {
            if (dir == null || dir.mapRoot == null || boss == null) return null;
            SuperCustomProperties best = null; float bd = 400f;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.m_Type != MapSchema.ObjectType.BossArena) continue;
                var col = so.GetComponent<Collider2D>();
                Vector2 c = col != null ? (Vector2)col.bounds.center : (Vector2)so.transform.position;
                float d = (c - (Vector2)boss.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = so.GetComponent<SuperCustomProperties>(); }
            }
            return best;
        }

        private static Vector3 Head(EnemyAI e, float above)
        {
            var sr = e.GetComponentInChildren<SpriteRenderer>();
            float top = sr != null ? sr.bounds.max.y : e.transform.position.y + 0.8f;
            return new Vector3(e.transform.position.x, top + above, e.transform.position.z);
        }

        // ---- 02/11: lũ đệ bị boss đánh bay, nằm choáng (sao quay trên đầu) tới khi boss còn nửa máu
        private readonly Dictionary<EnemyAI, SpriteVfx> stars = new Dictionary<EnemyAI, SpriteVfx>();

        private IEnumerator Toss(EnemyAI boss, EnemyAI m, float flight)
        {
            if (m == null || boss == null) yield break;
            m.Health.Invulnerable = true;                           // 02/11: nằm choáng thì không đánh được (tới khi Grok gọi dậy)
            float side = m.transform.position.x >= boss.transform.position.x ? 1f : -1f;
            Vector2 from = m.transform.position, to = from + new Vector2(side * 0.75f, -0.45f);
            float t = 0f;
            while (flight > 0f && t < flight && m != null && !m.Health.IsDead)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / flight);
                Vector2 p = Vector2.Lerp(from, to, u) + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 0.32f);
                m.TeleportTo(p);
                m.transform.rotation = Quaternion.Euler(0f, 0f, -side * 720f * u);
                yield return null;
            }
            if (m == null || m.Health.IsDead) yield break;
            m.TeleportTo(to);
            m.transform.rotation = Quaternion.Euler(0f, 0f, -side * 90f);   // nằm sóng soài, đầu hướng ra xa boss
            if (m.HasAnimState("Idle")) m.PlayState("Idle");
            var an = m.GetComponentInChildren<Animator>();
            if (an != null) an.speed = 0f;                          // 02/11: bất tỉnh — đứng hình (không còn thở phập phồng)
            var sr = m.GetComponentInChildren<SpriteRenderer>();
            var v = SpriteVfx.Spawn("stun_stars", to + new Vector2(side * 0.42f, 0.2f), 0.6f,
                                    sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 2) + 3, false, 14f);
            if (v != null) { v.Looping(99999f); stars[m] = v; }
        }

        private void HookWake(EnemyAI boss, List<EnemyAI> minions, SuperCustomProperties props)
        {
            bool woke = false;
            void Wake()
            {
                if (woke) return;
                woke = true;
                StartCoroutine(WakeRoutine(boss, minions, props));
            }
            boss.Phase2Reached += _ => Wake();
            boss.Health.Died += _ => Wake();
        }

        private IEnumerator WakeRoutine(EnemyAI boss, List<EnemyAI> minions, SuperCustomProperties props)
        {
            string line = props != null ? props.GetString("wake_line", "") : "";
            if (!string.IsNullOrEmpty(line) && boss != null && !boss.Health.IsDead)
                SpeechBubble.Show(Head(boss, 0.18f), line, 2.4f, BossTalk).Follow(boss.transform);
            yield return new WaitForSeconds(0.6f);
            bool any = false;
            foreach (var m in minions)
            {
                if (stars.TryGetValue(m, out var v) && v != null) v.Stop();
                if (m == null || m.Health.IsDead) continue;
                any = true;
                StartCoroutine(GetUp(m));
            }
            if (any) Hints.Show("Grok's raiders stagger back to their feet!", 3f);
        }

        private static IEnumerator GetUp(EnemyAI m)
        {
            Vector2 p0 = m.transform.position;
            float z0 = m.transform.eulerAngles.z;
            for (float t = 0f; t < 0.3f && m != null; t += Time.deltaTime)
            {
                float u = t / 0.3f;
                m.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(z0, 0f, u));
                m.TeleportTo(p0 + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 0.12f));
                yield return null;
            }
            if (m == null) yield break;
            m.transform.rotation = Quaternion.identity;
            m.TeleportTo(p0);
            var an = m.GetComponentInChildren<Animator>();
            if (an != null) an.speed = 1f;
            m.Health.Invulnerable = false;
            m.Held = false;
        }

        /// <summary>Độ dài clip của trạng thái (nếu tìm thấy trong Animator), không thì dùng giá trị mặc định.</summary>
        private static float Clip(EnemyAI e, string state, float fallback)
        {
            var an = e != null ? e.GetComponentInChildren<Animator>() : null;
            if (an == null || an.runtimeAnimatorController == null) return fallback;
            foreach (var c in an.runtimeAnimatorController.animationClips)
                if (c != null && c.name.EndsWith("_" + state)) return Mathf.Clamp(c.length, 0.2f, 1.5f);
            return fallback;
        }

        private static void SetPortrait(EnemyAI boss, SuperCustomProperties props)
        {
            CardSprite = boss != null ? Trim(boss.PortraitSprite()) : null; // 07/11: ảnh nhìn xuống, cắt sát hình (to + cân giữa)
            CardPortrait = props != null ? props.GetString("portrait", "") : "";
            CardColor = new Color(0.47f, 0.36f, 0.16f);
            string hex = props != null ? props.GetString("card_color", "") : "";
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) CardColor = c;
        }

        private static void ShowCard(string name, string sub, float seconds)
        {
            CardName = name; CardSub = sub;
            CardStart = Time.time; CardLength = seconds;
        }

        /// <summary>Giữ khoá điều khiển suốt cảnh (đóng menu Pause sẽ nhả Blocked -> khoá lại mỗi khung); Space/Enter = bỏ qua.</summary>
        private IEnumerator HoldInput(System.Func<bool> active)
        {
            while (active())
            {
                GameInput.Blocked = true;
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) skip = true;
                yield return null;
            }
            GameInput.Blocked = false;
        }

        /// <summary>Rung màn hình: chuỗi giật ngắn đổi hướng liên tục (tiếng gầm).</summary>
        private IEnumerator Rumble(float seconds, float amp)
        {
            float end = Time.time + seconds;
            int i = 0;
            while (Time.time < end)
            {
                float a = i * 2.39996f;                               // góc vàng: hướng giật không lặp
                CameraFollow.KickMain(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), amp, 0.08f);
                i++;
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skip) yield return null;
        }
    }
}
