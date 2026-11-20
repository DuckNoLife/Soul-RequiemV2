using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Xử lý layer "triggers" của map: điểm nghỉ, bia/biển, vật nhặt, dịch chuyển, lối ra.
    /// Thêm loại trigger mới: thêm hằng số ở <see cref="MapSchema.ObjectType"/> + một nhánh trong <see cref="OnEnter"/>.
    /// </summary>
    internal sealed class MapTriggerHandler
    {
        private readonly MapEncounterDirector director;
        private readonly List<MapTrigger> triggers;

        // Bia tự hiện khi bước vào (lời dẫn mở map, mẹo né bẫy) — còn lại phải lại gần nhấn T.
        private static readonly HashSet<string> AutoSignNames = new HashSet<string> { "r1_hatch", "r2_spout_hint", "stair_sign" };

        // 02/11: vật nhặt (bình máu, nâng cấp bình, thuốc xanh) trước đây vô hình -> đốm sáng ấm nhấp nháy, tắt khi nhặt
        private readonly Dictionary<MapTrigger, ExitBeacon> pickupGlow = new Dictionary<MapTrigger, ExitBeacon>();
        // 02/11: dấu hiệu riêng của điểm nghỉ (vòng phù văn + lửa linh hồn xanh), thắp khi bước vào
        private readonly Dictionary<MapTrigger, ShrineMarker> shrines = new Dictionary<MapTrigger, ShrineMarker>();
        private Vector2 lastPos;                 // vị trí khung trước (dải turn_back đẩy về phía người chơi vừa đi tới)
        private bool hasLast;
        private float nextTurnBackHint;

        public MapTriggerHandler(MapEncounterDirector director, List<MapTrigger> triggers)
        {
            this.director = director;
            this.triggers = triggers;
            var psr = director.Player != null ? director.Player.GetComponent<SpriteRenderer>() : null;
            ShrineMarker.Noun = "shrine";
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Checkpoint)
                {
                    string n = t.props.GetString("rest_noun", "");
                    if (!string.IsNullOrEmpty(n)) ShrineMarker.Noun = n;
                }
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Checkpoint)
                {
                    shrines[t] = ShrineMarker.Create(t.rect, director.mapRoot, psr != null ? psr.sortingLayerID : 0);
                    // 04/11: điểm nghỉ nhỏ khó thấy (cụm nến dưới cống Map 2): thuộc tính marker = true -> vòng sáng + đốm lấp lánh tới khi thắp
                    if (t.props.GetBool("marker", false))
                    {
                        // 04/11 (ảnh chạy thử): vùng nghỉ đã nới rộng cho dễ bấm -> tâm vùng lệch khỏi nến; vòng đặt dưới chân cụm nến thật
                        Vector2 foot = new Vector2(t.rect.center.x, t.rect.center.y - 0.12f);
                        float top = t.rect.center.y + 0.35f;
                        if (shrines[t] != null && shrines[t].TryFlameBounds(out var fb)) { foot = new Vector2(fb.center.x, fb.min.y + 0.05f); top = fb.max.y + 0.05f; }
                        var m = NoteMarker.CreateAt(foot, top, t.name);
                        var pulse = m.AddComponent<MarkerPulse>();
                        string lit = "lit_" + t.name;
                        var fl = director.Flags;
                        pulse.hideWhen = () => fl != null && fl.Has(lit);
                        m.SetActive(true);
                    }
                }
            foreach (var t in triggers)                                  // 04/11: định nghĩa chìa / đồ then chốt cho túi đồ
                if (t.type == MapSchema.ObjectType.Pickup && t.props.GetString(MapSchema.Prop.Item, "") == "key") KeyRing.Register(t.props);
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Pickup) continue;
                string item = t.props.GetString(MapSchema.Prop.Item, "potion");
                if (item != "potion" && item != "flask_upgrade" && item != "green_tonic" && item != "skill_scroll") continue;
                var glow = ExitBeacon.Create(new Vector3(t.rect.center.x, t.rect.center.y, 0f), new Color(1f, 0.82f, 0.45f), 0.55f);
                glow.name = "Pickup Glow " + t.name;
                pickupGlow[t] = glow;
            }
            // 03/11: đồ nhặt quan trọng (thuộc tính marker = true, vd tay quay của Malric): vòng sáng + đốm lấp lánh dưới hình món đồ
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Pickup || !t.props.GetBool("marker", false)) continue;
                Vector2 foot = t.rect.center; float top = foot.y + 0.3f;
                string hide = t.props.GetString(MapSchema.Prop.Hide, "");
                if (!string.IsNullOrEmpty(hide) && director.mapRoot != null)
                    foreach (var so in director.mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                    {
                        if (so.m_TiledName != hide) continue;
                        var r = so.GetComponentInChildren<SpriteRenderer>(true);
                        if (r != null) { foot = new Vector2(r.bounds.center.x, r.bounds.min.y); top = r.bounds.max.y; }
                        break;
                    }
                var m = NoteMarker.CreateAt(foot, top, t.name);
                m.AddComponent<MarkerPulse>();
                m.SetActive(true);
                pickupMarker[t] = m;
            }
        }

        private readonly Dictionary<MapTrigger, GameObject> pickupMarker = new Dictionary<MapTrigger, GameObject>();

        public IReadOnlyList<MapTrigger> All => triggers;

        /// <summary>02/11: chiêu nằm trong cuộn giấy (item skill_scroll) mà chưa nhặt -> lượt chơi mới bắt đầu KHÔNG có chiêu đó
        /// (Map 1: Earthshatter / ô F học được ở rương của Hob). Mang từ map trước sang (GameSession) thì giữ nguyên.</summary>
        public void ApplyScrollLocks()
        {
            if (GameSession.HasLoadout) return;
            var lo = director.Player != null ? director.Player.GetComponent<PlayerLoadout>() : null;
            if (lo == null) return;
            bool changed = false;
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Pickup || t.props.GetString(MapSchema.Prop.Item, "") != "skill_scroll") continue;
                if (director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Flag, "?"))) continue;
                var s = PlayerLoadout.Find(t.props.GetString(MapSchema.Prop.SkillId, ""));
                if (s == null) continue;
                lo.owned.Remove(s);
                for (int i = 0; i < PlayerLoadout.SlotCount; i++) if (lo.slots[i] == s) lo.slots[i] = null;
                changed = true;
            }
            if (changed) lo.NotifyChanged();
        }

        /// <summary>02/11: sau khi nạp bản lưu: đồ đã nhặt (cờ picked_*) không hiện lại, lửa trại đã thắp (lit_*) sáng xanh sẵn.</summary>
        public void RestoreFromFlags()
        {
            var flags = director.Flags;
            foreach (var t in triggers)
            {
                if (t.type == MapSchema.ObjectType.Pickup && flags.Has("picked_" + t.name))
                {
                    t.fired = true;
                    if (pickupGlow.TryGetValue(t, out var g) && g != null) UnityEngine.Object.Destroy(g.gameObject);
                    if (pickupMarker.TryGetValue(t, out var pm) && pm != null) UnityEngine.Object.Destroy(pm);
                    string hide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(hide)) HideMapObject(hide);
                    // 04/11: bản lưu cũ (chìa nhặt trước khi có túi đồ) -> đưa chìa vào túi
                    if (t.props.GetString(MapSchema.Prop.Item, "") == "key")
                    {
                        string kid = KeyRing.Register(t.props);
                        if (kid.Length > 0 && !KeyRing.Has(flags, kid)) flags.Set(KeyRing.Flag(kid));
                    }
                }
                else if (t.type == MapSchema.ObjectType.Checkpoint && flags.Has("lit_" + t.name) && shrines.TryGetValue(t, out var sh) && sh != null)
                    sh.SetLit();
            }
        }

        /// <summary>08/11 (ải): điểm hồi sinh của ải `stage` (checkpoint có thuộc tính stage, đã thắp) — mép dưới giữa vùng nghỉ
        /// (trước bàn thờ, không vướng nến). False nếu ải không có điểm hoặc chưa thắp.</summary>
        public bool TryStageSpawn(string stage, out Vector2 pos)
        {
            pos = default;
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Checkpoint || t.props == null) continue;
                if (!HasStage(t, stage)) continue;
                if (!director.Flags.Has("lit_" + t.name)) continue;
                pos = new Vector2(t.rect.center.x, t.rect.yMin + 0.1f);
                return true;
            }
            return false;
        }

        /// <summary>08/11 (§187): checkpoint là điểm hồi sinh của ải `stage` — thuộc tính stage có thể là danh sách "a,b" (dùng chung).</summary>
        private static bool HasStage(MapTrigger t, string stage)
        {
            if (t.props == null || string.IsNullOrEmpty(stage)) return false;
            foreach (var s in t.props.GetString(MapSchema.Prop.Stage, "").Split(','))
                if (s.Trim() == stage) return true;
            return false;
        }

        /// <summary>08/11: tên ải (stage_name trên checkpoint của ải; không có thì viết hoa mã ải).
        /// Checkpoint dùng chung nhiều ải: stage_name = "a=Tên A;b=Tên B".</summary>
        public string StageName(string stage)
        {
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Checkpoint && HasStage(t, stage))
                {
                    string n = t.props.GetString(MapSchema.Prop.StageName, "");
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.IndexOf('=') < 0) return n;
                    foreach (var part in n.Split(';'))
                    {
                        int eq = part.IndexOf('=');
                        if (eq > 0 && part.Substring(0, eq).Trim() == stage) return part.Substring(eq + 1).Trim();
                    }
                }
            // 09/11 (§200): ải không có lửa trại riêng (hồi sinh tại chỗ qua ải trước) -> tên ghi kèm trong stage_name của checkpoint khác ("a=Tên;b=Tên")
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Checkpoint || t.props == null) continue;
                foreach (var part in t.props.GetString(MapSchema.Prop.StageName, "").Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0 && part.Substring(0, eq).Trim() == stage) return part.Substring(eq + 1).Trim();
                }
            }
            return string.IsNullOrEmpty(stage) ? "Trial" : char.ToUpperInvariant(stage[0]) + stage.Substring(1);
        }

        /// <summary>Người chơi đang đứng trong vùng checkpoint.</summary>
        public bool PlayerAtCheckpoint
        {
            get
            {
                // 04/11 (chạy thử): đứng sát cụm nến (ngay mép vùng) thì [T] lại đọc biển bên cạnh -> nới vùng nghỉ thêm 0,25 đv
                Vector2 p = director.Player != null ? (Vector2)director.Player.position : new Vector2(float.NaN, float.NaN);
                foreach (var t in triggers)
                {
                    if (t.type != MapSchema.ObjectType.Checkpoint) continue;
                    if (t.inside) return true;
                    var r = t.rect;
                    if (p.x >= r.xMin - 0.25f && p.x <= r.xMax + 0.25f && p.y >= r.yMin - 0.25f && p.y <= r.yMax + 0.25f) return true;
                }
                return false;
            }
        }

        /// <summary>Vị trí player_start (nếu map có).</summary>
        public bool TryGetPlayerStart(out Vector2 pos)
        {
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.PlayerStart) { pos = t.rect.center; return true; }
            pos = default;
            return false;
        }

        /// <summary>Gọi mỗi khung hình: phát hiện người chơi vừa bước vào trigger.</summary>
        public void Tick(Vector2 playerPos)
        {
            foreach (var t in triggers)
            {
                bool inside = t.rect.Contains(playerPos);
                // 07/11 (người dùng: lăn Q xuyên qua dải chặn): dải turn_back mỏng — lăn đi xa hơn bề dày dải trong một khung nên không
                // khung nào đứng "trong" dải. Xét cả đoạn di chuyển từ khung trước: cắt qua dải = coi như bước vào.
                // (bỏ qua bước nhảy > 1 đv: dịch chuyển / hồi sinh / cảnh diễn không phải là đi qua dải)
                if (!inside && hasLast && t.type == MapSchema.ObjectType.TurnBack && (playerPos - lastPos).sqrMagnitude < 1f
                    && SegmentHitsRect(lastPos, playerPos, t.rect)) inside = true;
                if (!inside && t.type == MapSchema.ObjectType.TurnBack) t.safeSide = SideOf(t.rect, playerPos);   // nhớ phía an toàn
                if (inside && !t.inside) OnEnter(t);
                t.inside = inside;
            }
            // vị trí THẬT sau khi xử lý (dải chặn / dịch chuyển có thể vừa dời người chơi) — không dùng playerPos đầu khung
            lastPos = director.Player != null ? (Vector2)director.Player.position : playerPos; hasLast = true;
        }

        /// <summary>Phía của p so với dải (theo trục mỏng): -1 dưới / trái, +1 trên / phải.</summary>
        private static float SideOf(Rect r, Vector2 p) =>
            r.width >= r.height ? (p.y < r.center.y ? -1f : 1f) : (p.x < r.center.x ? -1f : 1f);

        /// <summary>Đoạn a -> b có cắt hình chữ nhật r không (Liang–Barsky).</summary>
        private static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect r)
        {
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            float[] p = { -d.x, d.x, -d.y, d.y };
            float[] q = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-7f) { if (q[i] < 0f) return false; continue; }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            return true;
        }

        // ------------------------------------------------------------------ bia / biển: nhấn T để đọc
        private static bool AutoSign(MapTrigger t)
        {
            if (t.props.GetBool(MapSchema.Prop.Auto, false) || AutoSignNames.Contains(t.name)) return true;
            return t.props.GetString(MapSchema.Prop.HintText, "").StartsWith("Press ");
        }

        private static string SignVerb(MapTrigger t)
        {
            string v = t.props.GetString(MapSchema.Prop.Verb, "");
            if (!string.IsNullOrEmpty(v)) return v;
            string n = t.name ?? "";
            if (n.Contains("sexton") || n.Contains("seer")) return "Talk";
            foreach (var k in new[] { "poster", "note", "ledger", "grave", "vey_", "exit", "hub_south", "door_hint", "notice" })
                if (n.Contains(k)) return "Read";
            return "Examine";
        }

        /// <summary>Đăng ký bia/biển (không tự hiện) với InteractionSystem để đọc bằng phím T.</summary>
        public void RegisterSigns()
        {
            InteractionSystem.Ensure();
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Sign || AutoSign(t)) continue;
                string text = t.props.GetString(MapSchema.Prop.HintText, "");
                if (string.IsNullOrEmpty(text)) continue;
                var r = t.rect;
                string hideWhen = t.props.GetString(MapSchema.Prop.HideWhen, "");   // vd biển "cổng cài then" thôi hiện khi cổng đã mở
                var flags = director.Flags;
                InteractionSystem.Add(new Interactable
                {
                    id = t.name, verb = SignVerb(t), rect = r, range = t.props.GetFloat(MapSchema.Prop.Range, 0.55f), calmOnly = true,
                    anchor = new Vector2(r.center.x, Mathf.Min(r.yMax, r.center.y + 0.45f)),
                    available = string.IsNullOrEmpty(hideWhen) ? (System.Func<bool>)null : () => !flags.HasAll(hideWhen),
                    use = () =>
                    {
                        // 02/11: đọc bảng / thư bằng khung hội thoại (trang ngăn bằng '|'; `speaker` = dòng tiêu đề) thay dòng chữ mờ
                        DialogueBox.Open(t.props.GetString("speaker", ""), DialogueBox.Pages(GameInput.ExpandKeys(text)));
                        // 01/11: biển có `flag` -> bật cờ khi đọc (vd hợp đồng của Todd trên bảng tin = bước nhiệm vụ)
                        string readFlag = t.props.GetString(MapSchema.Prop.Flag, "");
                        if (!string.IsNullOrEmpty(readFlag) && !flags.HasAll(readFlag)) flags.Set(readFlag);
                    }
                });
            }
        }

        /// <summary>08/11 (§190): lối ra có `interact` = true -> lại gần nhấn T (chữ `verb`, mặc định "Enter"; tầm `range`).
        /// Chưa đủ cờ / khu chưa dọn thì không hiện (cơ quan mở khoá ở gần nhận phím T), trừ khi `show_locked` = true
        /// (khi đó nhấn T nhắc locked_text).</summary>
        public void RegisterExits()
        {
            InteractionSystem.Ensure();
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Exit || t.props == null || !t.props.GetBool(MapSchema.Prop.Interact, false)) continue;
                var exit = t;
                bool showLocked = t.props.GetBool("show_locked", false);
                var r = t.rect;
                InteractionSystem.Add(new Interactable
                {
                    id = t.name, verb = t.props.GetString(MapSchema.Prop.Verb, "Enter"), rect = r, clue = false,
                    range = t.props.GetFloat(MapSchema.Prop.Range, 0.55f),
                    anchor = new Vector2(r.center.x, Mathf.Min(r.yMax, r.center.y + 0.45f)),
                    available = () => showLocked || ExitOpen(exit),
                    use = () => OnExit(exit),
                });
            }
        }

        // ------------------------------------------------------------------ bước vào trigger
        private void OnEnter(MapTrigger t)
        {
            switch (t.type)
            {
                case MapSchema.ObjectType.Checkpoint:
                    // 02/11: vùng nghỉ bao quanh đống lửa (vào từ hướng nào cũng được) -> hồi sinh ở chỗ người chơi vừa đứng (chắc chắn đi được),
                    // không phải tâm vùng (giữa đống lửa có collider)
                    director.SetRespawnPoint(director.Player != null ? (Vector2)director.Player.position : t.rect.center);
                    if (shrines.TryGetValue(t, out var shrine) && shrine != null) shrine.Kindle();
                    director.Flags.Set("lit_" + t.name);
                    director.SaveProgress();                          // 02/11: thắp lửa trại = lưu game
                    string noun = t.props.GetString("rest_noun", "shrine"); if (string.IsNullOrEmpty(noun)) noun = "shrine";
                    ShrineMarker.RestLabel = "Rest at the " + noun;
                    // 02/11: chỉ MỘT lời nhắc: ô [T] Rest at the campfire của HUD. Lần đầu: ô đó thêm dòng giải thích (ShrineMarker.Notice), không Hints.Show
                    if (!director.Flags.HasAll("tut_shrine"))
                    {
                        director.Flags.Set("tut_shrine");
                        ShrineMarker.Notice = char.ToUpperInvariant(noun[0]) + noun.Substring(1) + " lit - if you fall, you wake at the last one you lit. Resting restores health and flasks.";
                        ShrineMarker.NoticeUntil = Time.time + 7f;
                    }
                    break;
                case MapSchema.ObjectType.Sign:
                    // 28/10: chỉ mẹo điều khiển / lời dẫn mở map tự hiện; bia, biển, xác... phải lại gần nhấn T (RegisterSigns)
                    // 03/11 (chạy thử Map 2): lời dẫn tự hiện chỉ MỘT lần (trước đây hồi sinh ở điểm xuất phát lại hiện lại lời rơi xuống hầm)
                    if (AutoSign(t) && !director.Flags.Has("seen_" + t.name))
                    {
                        director.Flags.Set("seen_" + t.name);
                        Hints.Show(t.props.GetString(MapSchema.Prop.HintText, ""), 4f);
                    }
                    break;
                case MapSchema.ObjectType.Pickup:
                    OnPickup(t);
                    break;
                case MapSchema.ObjectType.Portal:
                    OnPortal(t);
                    break;
                case MapSchema.ObjectType.Exit:
                    // 08/11 (§190): lối ra `interact` không tự đi khi bước vào (lỡ chân rơi xuống hố; đứng sẵn trong vùng rồi mới
                    // kéo dây thì không có lần "bước vào" mới -> phải đi ra đi vào). Nhấn T qua RegisterExits.
                    if (!t.props.GetBool(MapSchema.Prop.Interact, false)) OnExit(t);
                    break;
                case MapSchema.ObjectType.TurnBack:
                    OnTurnBack(t);
                    break;
            }
        }

        private void OnPickup(MapTrigger t)
        {
            if (t.fired) return;
            var flags = director.Flags;
            // vật phẩm khoá theo cờ (vd. chìa khoá rơi ra khi dọn xong khu mini-boss): chưa đủ cờ thì chưa nhặt được
            if (!flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, "")))
            {
                string locked = t.props.GetString(MapSchema.Prop.LockedText, "");
                if (!string.IsNullOrEmpty(locked)) Hints.Show(locked, 2.5f);
                return;
            }
            t.fired = true;
            flags.Set("picked_" + t.name);                     // 02/11: bản lưu nhớ đồ đã nhặt
            if (pickupGlow.TryGetValue(t, out var glowObj) && glowObj != null) UnityEngine.Object.Destroy(glowObj.gameObject);
            if (pickupMarker.TryGetValue(t, out var markObj) && markObj != null) UnityEngine.Object.Destroy(markObj);
            var player = director.Player;
            string item = t.props.GetString(MapSchema.Prop.Item, "potion");
            switch (item)
            {
                case "potion":
                    var kit = player.GetComponent<PlayerCombat>();
                    if (kit != null) { kit.AddPotion(1); Hints.Show("Picked up a potion.", 2.5f); }
                    break;
                case "key":   // 04/11: chìa khoá / đồ then chốt -> túi đồ (KeyRing). Cửa chỉ mở khi Ren tới đúng cửa (requires_key)
                    KeyRing.Give(flags, KeyRing.Register(t.props), t.props.GetString(MapSchema.Prop.HintText, ""));
                    string keyFlag = t.props.GetString(MapSchema.Prop.Flag, "");   // vd tay quay: cờ lever_handle cho bàn điều khiển
                    if (!string.IsNullOrEmpty(keyFlag)) flags.Set(keyFlag);
                    string keyHide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(keyHide)) HideMapObject(keyHide);
                    KeyRing.Say(player, t.props.GetString("say", ""));
                    break;
                case "flag":
                    flags.Set(t.props.GetString(MapSchema.Prop.Flag, t.name));
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "You picked something up."), 4.5f);
                    string pickSay = t.props.GetString("say", "");     // 02/11: Ren nói một câu khi nhặt (vd nanh Grok: lời hứa mang về làng)
                    if (!string.IsNullOrEmpty(pickSay) && player != null)
                        SpeechBubble.Show(player.position + new Vector3(0f, 0.82f, 0f), pickSay, 3f).Follow(player);
                    string hide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(hide)) HideMapObject(hide);
                    // 02/11: nhặt xong thì đi luôn qua lối ra (vd nanh Grok -> nền Stone Ring sụp -> rơi xuống Map 2)
                    string thenExit = t.props.GetString("then_exit", "");
                    if (!string.IsNullOrEmpty(thenExit)) director.StartCoroutine(CollapseThenExit(thenExit));
                    break;
                case "skill":   // 28/10: sách chiêu tháo lắp: skill_id = id SkillDefinition
                    var lo = player.GetComponent<PlayerLoadout>();
                    if (lo != null) lo.Unlock(t.props.GetString(MapSchema.Prop.SkillId, ""));
                    flags.Set("new_mantra");                                     // 04/11: CombatTutorials -> màn thử chiêu mới
                    string skillHide = t.props.GetString(MapSchema.Prop.Hide, "");   // 07/11: cuộn chiêu (Map 2) -> ẩn hình cuộn
                    if (!string.IsNullOrEmpty(skillHide)) HideMapObject(skillHide);
                    string skillFlag = t.props.GetString(MapSchema.Prop.Flag, "");   // 01/11: vd cổ vật Con Mắt -> tắt đèn chỗ nhặt
                    if (!string.IsNullOrEmpty(skillFlag)) flags.Set(skillFlag);
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, $"A new mantra. Rest at a {ShrineMarker.Noun} to attune it."), 4.5f);
                    break;
                case "skill_scroll":   // 02/11: cuộn giấy chiêu + 1 bình máu (rương của Hob): học chiêu, lắp vào ô trống đầu tiên (F)
                    var slo = player.GetComponent<PlayerLoadout>();
                    if (slo != null) slo.Unlock(t.props.GetString(MapSchema.Prop.SkillId, ""));
                    var sck = player.GetComponent<PlayerCombat>();
                    if (sck != null) { int n = t.props.GetInt("potions", 1); sck.maxPotions += n; sck.AddPotion(n); }   // bình tối đa +1
                    string scrollFlag = t.props.GetString(MapSchema.Prop.Flag, "");
                    if (!string.IsNullOrEmpty(scrollFlag)) flags.Set(scrollFlag);
                    string learn = t.props.GetString("learn_flag", "");
                    if (!string.IsNullOrEmpty(learn)) flags.Set(learn);
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "A mantra scroll. You learned a new mantra."), 6f);
                    break;
                case "flask_upgrade":   // 02/11: trước đây nhặt không có tác dụng -> mỗi lần uống hồi thêm 15% máu + 1 bình
                    var ck = player.GetComponent<PlayerCombat>();
                    if (ck != null) { ck.healPercent = Mathf.Min(0.8f, ck.healPercent + 0.15f); ck.AddPotion(1); }
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "An old miner's copper flask. Every drink now heals more."), 4.5f);
                    string flaskFlag = t.props.GetString(MapSchema.Prop.Flag, "");   // vd z2b_flask_taken -> rương mở
                    if (!string.IsNullOrEmpty(flaskFlag)) flags.Set(flaskFlag);
                    break;
                case "green_tonic":   // 02/11: thuốc xanh của người chữa bệnh — giải độc
                    var hp = player.GetComponent<Health>();
                    if (hp != null) hp.CurePoison();
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "A green tonic. It burns the goblin poison out of your blood."), 4.5f);
                    break;
                case "weapon":   // 10/11 (§217): nhặt được một thanh katana -> giá kiếm (túi đồ ▸ Weapons), KHÔNG tự đeo
                    WeaponRing.Give(flags, t.props.GetString("weapon_id", ""), t.props.GetString(MapSchema.Prop.HintText, ""));
                    string wepFlag = t.props.GetString(MapSchema.Prop.Flag, "");
                    if (!string.IsNullOrEmpty(wepFlag)) flags.Set(wepFlag);
                    string wepHide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(wepHide)) HideMapObject(wepHide);
                    KeyRing.Say(player, t.props.GetString("say", ""), 3.4f);
                    break;
                case "skill_book":
                    flags.Set("book_" + t.props.GetString(MapSchema.Prop.Book, t.name));
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "You found a book of mantras."), 5f);
                    HideMapObject(t.name + "_book");
                    break;
            }
        }

        private void HideMapObject(string tiledName)
        {
            foreach (var so in director.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == tiledName) so.gameObject.SetActive(false);
        }

        /// <summary>02/11: dải chặn mềm (vd lối ra khỏi làng khi chưa đọc hợp đồng của Todd): chưa đủ cờ -> đẩy người chơi ra khỏi dải
        /// về phía vừa đi tới (theo cạnh mỏng của dải) và nhắc locked_text. Đủ cờ thì đi qua bình thường.</summary>
        private void OnTurnBack(MapTrigger t)
        {
            if (director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, ""))) return;
            var r = t.rect;
            var pl = director.Player;
            Vector2 cur = pl.position;
            // chỉ dời theo cạnh MỎNG của dải, giữ nguyên toạ độ kia; chưa có vị trí khung trước (vừa vào map) hoặc dải dày bất thường
            // (> 2 ô) thì không đẩy — tránh hất người chơi đi xa (lỗi 02/11: dải sai kích thước phủ cả điểm xuất phát)
            if (!hasLast || Mathf.Min(r.width, r.height) > 0.7f) return;
            Vector2 p = cur;
            const float gap = 0.12f;
            // 07/11: đẩy về PHÍA AN TOÀN đã nhớ (trước đây theo lastPos: lăn qua rồi bị đẩy, khung sau lastPos nằm bên kia -> đẩy sang bên kia)
            float side = t.safeSide != 0f ? t.safeSide : SideOf(r, lastPos);
            if (r.width >= r.height) p.y = side < 0f ? r.yMin - gap : r.yMax + gap;
            else p.x = side < 0f ? r.xMin - gap : r.xMax + gap;
            var rb = pl.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
            pl.position = new Vector3(p.x, p.y, pl.position.z);
            lastPos = p;                                                   // đoạn di chuyển khung sau tính từ chỗ vừa đẩy về
            t.inside = false;
            if (Time.time >= nextTurnBackHint)
            {
                // 02/11: nhân vật tự nhủ (bóng thoại trên đầu) thay vì dòng gợi ý; `say` trống thì dùng locked_text
                string say = t.props.GetString("say", "");
                if (string.IsNullOrEmpty(say)) say = t.props.GetString(MapSchema.Prop.LockedText, "Not yet.");
                SpeechBubble.Show(pl.position + new Vector3(0f, 0.82f, 0f), say, 3.2f).Follow(pl);
                nextTurnBackHint = Time.time + 3.4f;
                Debug.Log($"[TurnBack] {t.name}: thiếu cờ '{t.props.GetString(MapSchema.Prop.Requires, "")}' -> quay lại. Cờ đang có: {string.Join(", ", director.Flags.All)}");
            }
        }

        private void OnPortal(MapTrigger t)
        {
            if (!director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, "")))
            {
                Hints.Show(t.props.GetString(MapSchema.Prop.LockedText, "The way is blocked."), 2.5f);
                return;
            }
            var to = t.props.GetString(MapSchema.Prop.Target, "").Split(',');
            if (to.Length == 2 &&
                float.TryParse(to[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float tx) &&
                float.TryParse(to[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ty))
            {
                // tối màn hình rồi mới dời (không lia camera qua map, khu đích ở xa như một bản đồ riêng)
                SceneTransition.Warp(director.Player, director.TileToWorld(tx, ty),
                    t.props.GetString(MapSchema.Prop.HintText, ""), t.props.GetFloat(MapSchema.Prop.Shake, 0f));
            }
        }

        private void OnExit(MapTrigger t)
        {
            string target = t.props.GetString(MapSchema.Prop.Target, "");
            if (!ExitOpen(t))
            {
                Hints.Show(t.props.GetString(MapSchema.Prop.LockedText, "The way is sealed. Defeat the guardian first."), 2.5f);
                return;
            }
            if (!SceneTransition.CanGo(target))
            {
                Hints.Show($"The road ahead is not built yet ({target}).", 3f);
                Debug.LogWarning($"[Map] Chưa có scene '{SceneTransition.SceneName(target)}' trong Build Settings.");
                return;
            }
            Debug.Log($"[Map] Lối ra '{t.name}' -> {target}");
            if (SceneTransition.Busy) return;
            // 07/11: chỗ tới trong map đích (ô Tiled) — vd từ hầm leo lên đúng miệng hố, không về player_start của map chính
            var at = t.props.GetString(MapSchema.Prop.ArriveAt, "").Split(',');
            float ax = 0f, ay = 0f;
            bool hasAt = at.Length == 2 &&
                float.TryParse(at[0], NumberStyles.Float, CultureInfo.InvariantCulture, out ax) &&
                float.TryParse(at[1], NumberStyles.Float, CultureInfo.InvariantCulture, out ay);
            GameSession.SetArrival(hasAt, hasAt ? new Vector2(ax, ay) : Vector2.zero);
            SceneTransition.Go(director.Player, target,
                t.props.GetString(MapSchema.Prop.DepartText, "The stone groans... and gives way beneath you!"),
                SceneManager.GetActiveScene().name,
                t.props.GetBool(MapSchema.Prop.Quake, true),
                t.props.GetString(MapSchema.Prop.ArriveTitle, ""), t.props.GetString(MapSchema.Prop.ArriveSubtitle, ""));
        }

        /// <summary>Lối ra mở khi khu của nó đã dọn xong (vd. hạ boss) — hoặc lối ra không gắn khu nào.</summary>
        public bool ExitOpen(MapTrigger t) =>
            (string.IsNullOrEmpty(t.zoneId) || director.IsZoneClearedOrMissing(t.zoneId)) &&
            director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, ""));   // 02/11: lối ra có thể cần cờ (vd grok_tusk)

        /// <summary>02/11: lối ra có `collapse = true` -> cảnh sập hầm (RingCollapse) rồi mới chuyển map; không có thì đợi 1.8 s như cũ.</summary>
        private System.Collections.IEnumerator CollapseThenExit(string exitName)
        {
            MapTrigger exit = null;
            foreach (var t in triggers) if (t.type == MapSchema.ObjectType.Exit && t.name == exitName) { exit = t; break; }
            if (exit == null || !exit.props.GetBool("collapse", false)) { yield return ExitAfter(exitName, 1.8f); yield break; }
            yield return new WaitForSeconds(0.6f);                          // đọc kịp dòng chữ + câu thốt của Ren
            Vector2 c = new Vector2(float.NaN, 0f);
            var parts = exit.props.GetString("collapse_center", "").Split(',');
            if (parts.Length == 2 &&
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy)) c = new Vector2(cx, cy);
            if (float.IsNaN(c.x) && QuestTracker.Instance != null) c = QuestTracker.Instance.PlayerTile;
            yield return RingCollapse.Play(director, c);
            OnExit(exit);
            if (!SceneTransition.Busy) CutsceneState.End();                  // không chuyển được map: trả HUD
        }

        private System.Collections.IEnumerator ExitAfter(string exitName, float delay)
        {
            yield return new WaitForSeconds(delay);
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Exit && t.name == exitName) { OnExit(t); yield break; }
            Debug.LogWarning($"[Map] then_exit: không thấy lối ra '{exitName}'");
        }

        /// <summary>Khu có lối ra vừa dọn xong: báo cho người chơi + đèn hiệu nhấp nháy ở lối ra.</summary>
        public void RevealExits(string zoneId)
        {
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Exit || t.zoneId != zoneId) continue;
                if (!director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, ""))) continue;   // 02/11: chưa đủ cờ thì chưa hiện
                Hints.Show(t.props.GetString(MapSchema.Prop.OpenText, "The altar cracks open. Something hollow lies beneath..."), 4f);
                ExitBeacon.Create(new Vector3(t.rect.center.x, t.rect.center.y, 0f), new Color(0.75f, 1f, 0.45f), 1.1f);
            }
        }
    }
}
