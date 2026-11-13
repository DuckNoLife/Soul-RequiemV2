using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SuperTiled2Unity;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Cơ quan trên map, đọc từ lớp object "mechanics" (do Catalog/v2/maps/*.py sinh ra). Dùng chung cho mọi map.
    ///   console    – cần gạt điều khiển: cần cờ `requires`; chém vào -> camera lia + phóng tới `focus` (ô Tiled),
    ///                người chơi bấm chuột vào ống trong lưới `grid` để xoay. Esc để thoát.
    ///   pipegrid   – lưới ống (ô tên pipe_c_r). Ống thông với VÀO (`src`) dùng sprite ướt (pipepal_wet_NN), còn lại khô
    ///                (pipepal_cell_NN); ô bắt bu-lông (`bolts` "x,y;x,y") không xoay, sprite pipepal_bolt_*.
    ///                Click trái = xoay thuận, click phải = xoay ngược, R = về trạng thái đầu.
    ///                Thắng: tới `dst`, KHÔNG tới `cut`, KHÔNG RÒ (mọi đầu ống ướt đều nối ống khác) -> cờ `sets`,
    ///                lia camera tới `after_focus`. Đầu ống rò phun Ichor (nhân bản object 'leakpal_fx').
    ///                Ống cố định ngoài lưới tên pipefix_{IN|A|B}_{mặt nạ}_{k}: A ướt khi tới cut, B ướt khi tới dst.
    ///   toggle     – khi có cờ `when`: ẩn lớp tile `hide_layers`, ẩn object `hide_objects`, hiện object `show_objects`.
    ///   light      – đèn: `lit_when` (chỉ sáng khi có cờ) / `off_when` (tắt khi có cờ). Light2D do Map2SceneBuilder tạo.
    ///   spout      – ống phun theo nhịp (<tên>_warn báo trước, <tên>_fx đang phun, gây sát thương)
    ///   vent       – cống hơi độc: sủi bọt (<tên>_warn) -> mây độc (<tên>_fx, trúng độc)
    ///   beam       – tia năng lượng trang trí từ `src` tới `dst` (ô Tiled), dùng ảnh động mẫu 'beampal' kéo dài + nhuộm `color`;
    ///                tắt dần khi có cờ `off_when`.
    ///   shield     – bong bóng năng lượng bọc quanh tâm mech (bán kính `radius` ô), phập phồng; vỡ khi có cờ `off_when`.
    ///   flood      – cống tràn: các lớp tile `layers` (mức 1..n, lỗ Ichor to dần); chu kỳ idle -> rise -> hold -> fall.
    ///                Ô rát của mức i: `cells_i` ("x,y;x,y" ô Tiled). Sủi bọt <tên>_warn trước khi tràn. Dừng khi `stop_when`.
    ///   hazard     – vùng rát (dps_percent % máu/giây) tới khi có cờ `stop_when`; `poison_percent`/`poison_seconds` = trúng độc;
    ///                `shape=ellipse` -> chỉ tính elip nội tiếp khung (vũng Ichor).
    ///   breakable  – vật cản gỗ: chém `hits` lần -> cờ `sets` (cổng cùng tên mở theo cờ đó)
    ///   lever      – cần gạt (Map 2 v2, buồng giam): chém -> cờ `sets`; `spawn` (tên quái) xuất hiện tại `spawn_at` (ô) trong
    ///                khu `zone` (mở nhầm buồng). Một lần.
    ///   dial       – vòng số của khoá (nhóm `group`): mỗi đòn chém +1 (0-9). Mọi vòng trong nhóm đúng `answer` -> cờ `sets`.
    ///   valve      – van gỉ (Boiler Room): chém không xi nhê tới khi Ichor Ram lao trúng (RamImpact); sau đó chém 1 lần -> cờ `sets`.
    ///   tank       – bể kính (Culture Hall): cóc Gorgemaw đập xuống gần (Shatter) -> ẩn `obj`, hiện `broken`.
    ///   note       – tờ ghi chú / hồ sơ: đứng trong vùng -> BẢNG ĐỌC LỚN giữa màn hình (`title`, `text`, ký tự Vael `glyphs`
    ///                "chữ số:chú thích;..."). Đọc lần đầu -> cờ `sets`. `requires`: chỉ đọc được khi có cờ.
    ///                `show_when`: chỉ xuất hiện (cùng object `<tên>_item`) khi có cờ (vd dọn sạch phòng) + vòng sáng vàng tới khi đọc.
    ///   cipher     – khoá ký tự: đứng trong vùng -> bảng khoá lớn, mỗi ô có mũi tên lên/xuống đổi ký tự Vael (hình, không số).
    ///                Ô i hiện chữ số perm[vị trí]; đúng `answer` ("3,7,4") ở mọi ô -> cờ `sets`.
    ///                Ảnh ký tự lấy từ object ẩn glyphpal_<chữ số> trong map.
    ///   rosewindow – cửa sổ hoa hồng (nhà nguyện): bảng 2x2 mảnh kính, bấm mảnh để xoay 90° (chuột phải xoay ngược).
    ///                Ảnh `picture` (hidden object picpal_<tên>), góc bắt đầu `start` ("1,2,3,1"). Đúng hết -> cờ `sets`, hiện ảnh `truth`.
    ///   bell       – quả chuông (nhóm `group`, nốt `note`): chém -> chuông ngân + loé sáng. Đánh đúng chuỗi `answer` ("3,5,2,1")
    ///                -> cờ `sets` + chữ `text`. Sai một nốt -> chuỗi xoá, thả quái `fail_spawn` vào khu `zone` (tối đa 1 con / 2.5 s).
    /// 28/10: chuông, cần gạt, vòng số, bàn điều khiển, ghi chú, khoá ký tự -> lại gần nhấn T (InteractionSystem).
    /// Đòn chém chỉ còn tác dụng với van (valve) và vật cản gỗ (breakable).
    /// </summary>
    public class MapMechanics : MonoBehaviour
    {
        public static MapMechanics Instance { get; private set; }
        /// Đang đứng ở bảng điều khiển (camera đã lia đi): PlayerController / PlayerCombatPrototype không nhận phím.
        public static bool ConsoleActive { get; private set; }

        [Tooltip("Nhịp gây sát thương khi đứng trong vùng phun / vùng rát (giây)")]
        public float tick = 0.5f;

        private class Mech
        {
            public string type, name;
            public Rect rect;
            public SuperCustomProperties props;
            public int hits;
            public bool done, announced;
            public float nextTick;
            public int phase = -1;
            // pipegrid
            public int cols, rows;
            public int[] masks, startMasks;
            public bool[] bolted;
            public SpriteRenderer[] cells;
            public Light2D[] glows;
            public readonly List<(SpriteRenderer sr, string group, int mask)> fixedPipes = new List<(SpriteRenderer, string, int)>();
            public readonly List<GameObject> leakFx = new List<GameObject>();
            // beam / shield
            public Transform fx;
            public SpriteRenderer fxSr;
            public Vector3 fxScale;
            // shield: vật bên trong nhìn qua lớp "kính" Ichor (mờ, ám xanh, gợn)
            public readonly List<(SpriteRenderer sr, Color col, Vector3 pos)> veil = new List<(SpriteRenderer, Color, Vector3)>();
            // flood
            public GameObject[] stageLayers;
            public HashSet<Vector2Int>[] stageCells;
            public float inputReady;
            // dial / valve
            public int value;
            public bool rusted;
            // note / cipher
            public bool read;
            // note có show_when: hiện khi dọn sạch phòng, kèm vòng sáng vàng gây chú ý
            public GameObject marker; public bool shown;
            public int[] wheels;
        }

        private readonly List<Mech> mechs = new List<Mech>();
        private readonly Dictionary<string, List<GameObject>> named = new Dictionary<string, List<GameObject>>();
        private readonly Dictionary<string, Light2D> lights = new Dictionary<string, Light2D>();
        private readonly Dictionary<string, Sprite> pipeSprites = new Dictionary<string, Sprite>();
        private MapEncounterDirector dir;
        private Transform player;
        private Health playerHealth;
        private Mech activeConsole, activeGrid;
        private CameraFollow camFollow;
        private GameObject leakTemplate;

        private void Awake() { Instance = this; ConsoleActive = false; }
        private void OnEnable() => PlayerCombatPrototype.Struck += OnStruck;
        private void OnDisable() { PlayerCombatPrototype.Struck -= OnStruck; ConsoleActive = false; }

        private IEnumerator Start()
        {
            yield return null;                                  // đợi Director đọc map xong
            dir = MapEncounterDirector.Instance;
            var pc = FindFirstObjectByType<PlayerController>();
            if (dir == null || dir.mapRoot == null || pc == null) { enabled = false; yield break; }
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
            camFollow = FindFirstObjectByType<CameraFollow>();

            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                string layer = so.transform.parent != null ? so.transform.parent.name : "";
                if (layer == "mechanics")
                {
                    var m = new Mech { type = so.m_Type, name = so.m_TiledName, props = so.GetComponent<SuperCustomProperties>() };
                    var col = so.GetComponent<Collider2D>();
                    m.rect = col != null ? new Rect(col.bounds.min, col.bounds.size) : new Rect(so.transform.position, Vector2.one * 0.3f);
                    mechs.Add(m);
                }
                else if (!string.IsNullOrEmpty(so.m_TiledName))
                {
                    if (so.m_TiledName.StartsWith("glyphpal_"))          // ký tự Vael (khoá ký tự, ghi chú): lấy sprite rồi ẩn
                    {
                        var gsr = so.GetComponentInChildren<SpriteRenderer>(true);
                        if (gsr != null && int.TryParse(so.m_TiledName.Substring(9), out int gd)) glyphs[gd] = gsr.sprite;
                        so.gameObject.SetActive(false);
                        continue;
                    }
                    if (so.m_TiledName.StartsWith("picpal_"))            // 28/10: tranh cho bảng đọc / cửa sổ hoa hồng: lấy sprite rồi ẩn
                    {
                        var psr = so.GetComponentInChildren<SpriteRenderer>(true);
                        if (psr != null) pictures[so.m_TiledName.Substring(7)] = psr.sprite;
                        so.gameObject.SetActive(false);
                        continue;
                    }
                    if (so.m_TiledName.StartsWith("pipepal_"))           // bảng sprite ống: lấy sprite rồi ẩn
                    {
                        var sr = so.GetComponentInChildren<SpriteRenderer>(true);
                        if (sr != null) pipeSprites[so.m_TiledName.Substring(8)] = sr.sprite;
                        so.gameObject.SetActive(false);
                        continue;
                    }
                    if (!named.TryGetValue(so.m_TiledName, out var list)) named[so.m_TiledName] = list = new List<GameObject>();
                    list.Add(so.gameObject);
                }
            }
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.name.StartsWith("Light ")) lights[l.name.Substring(6)] = l;

            foreach (var m in mechs)
            {
                switch (m.type)
                {
                    case "breakable": m.hits = Int(m, "hits", 3); break;
                    case "spout": case "vent": Show(m.name + "_fx", false); Show(m.name + "_warn", false); break;
                    case "flood": InitFlood(m); break;
                    case "beam": InitBeam(m); break;
                    case "shield": InitShield(m); break;
                    case "pipegrid": InitGrid(m); break;
                    case "toggle": Show(Str(m, "show_objects", ""), false); break;
                    case "dial": m.value = Int(m, "start", 0); break;
                    case "valve": m.rusted = true; break;
                    case "tank": Show(Str(m, "broken", ""), false); break;
                    case "cipher": m.wheels = new int[Str(m, "answer", "0,0,0").Split(',').Length]; break;
                    case "rosewindow":
                        var st = Str(m, "start", "1,2,3,1").Split(',');
                        m.wheels = new int[4];
                        for (int i = 0; i < 4; i++) m.wheels[i] = i < st.Length && int.TryParse(st[i].Trim(), out int sv) ? ((sv % 4) + 4) % 4 : 1;
                        break;
                }
            }
            foreach (var m in mechs)
                if (m.type == "note" && !string.IsNullOrEmpty(Str(m, "show_when", ""))) CreateNoteMarker(m);
            dir.Flags.Changed += _ => RefreshFlags();
            RefreshFlags();
            foreach (var m in mechs) RegisterInteract(m);
            Debug.Log($"[MapMechanics] {mechs.Count} cơ quan, {pipeSprites.Count} sprite ống.");
        }

        // ------------------------------------------------------------------ cờ -> đèn, vùng nguy hiểm, bật/tắt lớp
        private void RefreshFlags()
        {
            foreach (var m in mechs)
            {
                if (m.type == "note" && m.marker != null)
                {
                    bool on = dir.Flags.HasAll(Str(m, "show_when", ""));
                    if (on && !m.shown) Hints.Show("Something lies where the fighting was. A faint glow marks it.", 3f);
                    m.shown = on;
                    Show(m.name + "_item", on);
                    m.marker.SetActive(on && !m.read);
                }
                else if (m.type == "light")
                {
                    string litWhen = Str(m, "lit_when", ""), offWhen = Str(m, "off_when", "");
                    bool on = (string.IsNullOrEmpty(litWhen) || dir.Flags.HasAll(litWhen)) && (string.IsNullOrEmpty(offWhen) || !dir.Flags.HasAll(offWhen));
                    if (lights.TryGetValue(m.name, out var l) && l != null)
                    {
                        var la = l.GetComponent<LightAnimator>();
                        if (la != null) la.SetOn(on); else l.enabled = on;     // mờ dần thay vì tắt phụt
                    }
                }
                else if ((m.type == "beam" || m.type == "shield") && !m.done && m.fx != null)
                {
                    string off = Str(m, "off_when", "");
                    if (!string.IsNullOrEmpty(off) && dir.Flags.HasAll(off)) { m.done = true; StartCoroutine(FadeFx(m, m.type == "shield")); }
                }
                else if (m.type == "toggle" && !m.done && dir.Flags.HasAll(Str(m, "when", "-")))
                {
                    m.done = true;
                    foreach (var ln in Str(m, "hide_layers", "").Split(','))
                        if (!string.IsNullOrEmpty(ln)) StartCoroutine(FadeLayer(ln.Trim(), 2.5f));
                    Show(Str(m, "hide_objects", ""), false);
                    Show(Str(m, "show_objects", ""), true);
                }
                string stop = Str(m, "stop_when", "");
                if (!string.IsNullOrEmpty(stop) && dir.Flags.HasAll(stop) && !m.done && (m.type == "spout" || m.type == "vent" || m.type == "hazard" || m.type == "flood"))
                {
                    m.done = true;
                    Show(m.name + "_fx", false);
                    Show(m.name + "_warn", false);
                    if (m.type == "flood") SetFloodStage(m, 0);
                }
            }
        }

        /// Kênh cạn dần: lớp tile Ichor mờ đi rồi tắt (lộ lòng kênh khô bên dưới).
        private IEnumerator FadeLayer(string layerName, float seconds)
        {
            Transform t = null;
            foreach (var tr in dir.mapRoot.GetComponentsInChildren<Transform>(true))
                if (tr.name == layerName) { t = tr; break; }
            if (t == null) { Debug.LogWarning("[MapMechanics] Không thấy lớp " + layerName); yield break; }
            var maps = t.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true);
            float t0 = 0f;
            while (t0 < seconds)
            {
                t0 += Time.deltaTime;
                foreach (var tm in maps) { var c = tm.color; c.a = 1f - t0 / seconds; tm.color = c; }
                yield return null;
            }
            t.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ vòng lặp
        private void Update()
        {
            if (dir == null || player == null) return;
            foreach (var fm in mechs) if (fm.fx != null) UpdateFx(fm);
            foreach (var mm in mechs) if (mm.marker != null && mm.marker.activeSelf) AnimateNoteMarker(mm);
            if (activeConsole != null) { ConsoleUpdate(); return; }
            if ((openNote != null || openCipher != null || openWindow != null) && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                openNote = null; openCipher = null; openWindow = null; closedFrame = Time.frameCount;
            }
            if (openWindow != null && DistToRect(openWindow.rect, (Vector2)player.position) > CloseRange) openWindow = null;
            Vector2 p = player.position;
            foreach (var m in mechs)
            {
                if (m.done) continue;
                bool inside = m.rect.Contains(p);
                switch (m.type)
                {
                    case "spout": UpdateSpout(m, inside); break;
                    case "vent": UpdateVent(m, inside); break;
                    case "flood": UpdateFlood(m); break;
                    case "hazard":
                        if (inside && Str(m, "shape", "") == "ellipse")
                        {   // vũng Ichor: chỉ tính phần elip nội tiếp khung (vũng tròn méo, không rát ở 4 góc khung)
                            Vector2 d = (p - m.rect.center) / (m.rect.size * 0.5f);
                            inside = d.sqrMagnitude <= 1f;
                        }
                        if (inside && Time.time >= m.nextTick && playerHealth != null && !playerHealth.IsDead)
                        {
                            m.nextTick = Time.time + tick;
                            float dps = Float(m, "dps_percent", 3f);
                            if (dps > 0f) Hurt(playerHealth.Max * dps / 100f * tick, Vector2.zero, 0f);
                            float poison = Float(m, "poison_percent", 0f);
                            if (poison > 0f && !playerHealth.Invulnerable)
                                playerHealth.ApplyPoison(poison / 100f, Float(m, "poison_seconds", 2f));
                        }
                        break;
                    case "console":
                        if (inside && !m.announced)
                        {
                            m.announced = true;
                            Hints.Show(dir.Flags.HasAll(Str(m, "requires", "")) ? $"The pump lever. Press {GameInput.Label(GameAction.Interact)} to take the controls."
                                                                             : LockedText(m), 4f);
                        }
                        if (!inside) m.announced = false;
                        break;
                    case "breakable":
                        if (!m.announced && Vector2.Distance(p, m.rect.center) < 1.0f) { m.announced = true; Hints.Show(Str(m, "hint", "Rotten wood. A few strikes would break it."), 2.5f); }
                        break;
                    case "note":       // mở bằng T (UseMech); đi xa / hết điều kiện -> tự đóng
                        if (openNote == m && (!Usable(m) || DistToRect(m.rect, p) > CloseRange)) openNote = null;
                        break;
                    case "cipher":
                        if (openCipher == m && DistToRect(m.rect, p) > CloseRange) openCipher = null;
                        break;
                    case "lever":
                    case "dial":
                    case "valve":
                        bool near = Vector2.Distance(p, m.rect.center) < 0.7f;
                        if (near && !m.announced)
                        {
                            m.announced = true;
                            string h = m.type == "lever" ? Str(m, "hint", "")
                                     : m.type == "dial" ? "A brass dial. " + DialText(m)
                                     : m.rusted ? "The valve is rusted solid. A blow far heavier than yours might crack it loose."
                                                : "The rust is cracked. The valve will turn now.";
                            Hints.Show(h, 3f);
                        }
                        if (!near) m.announced = false;
                        break;
                }
            }
        }

        private void UpdateSpout(Mech m, bool inside)
        {
            float on = Float(m, "on_seconds", 1.5f), off = Float(m, "off_seconds", 2.5f), warn = Float(m, "warn_seconds", 1f);
            float t = Mathf.Repeat(Time.time + Float(m, "offset", 0f), on + off + warn);
            int phase = t < off ? 0 : t < off + warn ? 1 : 2;
            if (phase != m.phase) { m.phase = phase; Show(m.name + "_warn", phase == 1); Show(m.name + "_fx", phase == 2); }
            if (phase == 2 && inside && Time.time >= m.nextTick)
            {
                m.nextTick = Time.time + tick;
                Hurt(Float(m, "damage", 12f), Vector2.down, 2.5f);
            }
        }

        private void UpdateVent(Mech m, bool inside)
        {
            float period = Float(m, "period", 4f), warn = Float(m, "warn_seconds", 1f), cloud = Float(m, "cloud_seconds", 2f);
            float t = Mathf.Repeat(Time.time + Float(m, "offset", 0f), period + cloud);
            int phase = t < period - warn ? 0 : t < period ? 1 : 2;
            if (phase != m.phase) { m.phase = phase; Show(m.name + "_warn", phase == 1); Show(m.name + "_fx", phase == 2); }
            if (phase == 2 && inside && playerHealth != null && !playerHealth.Invulnerable)
                playerHealth.ApplyPoison(Float(m, "poison_percent", 4f) / 100f, Float(m, "poison_seconds", 2f));
        }

        // ------------------------------------------------------------------ tia năng lượng / bong bóng chắn
        private SpriteRenderer beamTemplate;

        private void InitBeam(Mech m)
        {
            if (beamTemplate == null && named.TryGetValue("beampal", out var tpl) && tpl.Count > 0)
                beamTemplate = tpl[0].GetComponentInChildren<SpriteRenderer>(true);
            if (beamTemplate == null) { Debug.LogWarning("[MapMechanics] Thiếu mẫu tia 'beampal'"); return; }
            beamTemplate.enabled = false;                       // mẫu nằm ở góc map: ẩn hình, ảnh động vẫn chạy để tia lấy khung
            Vector3 a = TilePoint(Str(m, "src", ""), m.rect.center), b = TilePoint(Str(m, "dst", ""), m.rect.center);
            var go = new GameObject("beam_" + m.name);
            go.transform.SetParent(transform, false);
            // sprite nằm ở con, lùi đúng bằng tâm bounds -> tia luôn căn giữa a..b dù pivot của ô Tiled ở góc dưới-trái
            var body = new GameObject("beam_sprite");
            body.transform.SetParent(go.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = beamTemplate.sprite;
            if (sr.sprite != null) body.transform.localPosition = -(Vector3)(Vector2)sr.sprite.bounds.center;
            sr.sortingLayerID = beamTemplate.sortingLayerID;
            sr.sortingOrder = beamTemplate.sortingOrder + 5;
            sr.color = ParseColor(Str(m, "color", "#8dff5a"), 0.85f);
            Vector3 d = b - a;
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one * 0.32f;
            float width = Float(m, "width", 0.8f) * dir.tileSize;
            m.fxScale = new Vector3(d.magnitude / Mathf.Max(0.01f, size.x), width / Mathf.Max(0.01f, size.y), 1f);
            go.transform.localScale = m.fxScale;
            m.fx = go.transform;
            m.fxSr = sr;
            // đèn dọc tia
            int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 1.2f));
            for (int i = 0; i <= n; i++)
            {
                var lg = new GameObject("beam_light");
                lg.transform.position = Vector3.Lerp(a, b, i / (float)n);
                lg.transform.SetParent(go.transform, true);
                var l = lg.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightOuterRadius = 0.7f;
                l.pointLightInnerRadius = 0.05f;
                l.intensity = 0.5f;
                l.color = sr.color;
            }
        }

        private void InitShield(Mech m)
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var c = ParseColor(Str(m, "color", "#7dff4a"), 1f);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (r <= 1f)
                    {
                        a = 0.18f + 0.40f * Mathf.Pow(r, 6f);                       // lõi mờ, viền đậm
                        if (r > 0.93f) a = 0.75f;                                    // vỏ
                        float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.45f, 0.5f)) / 0.28f);
                        a = Mathf.Max(a, hl * 0.55f);                                // vệt sáng phản chiếu
                    }
                    // lượng tử hoá cho hợp pixel art
                    a = Mathf.Round(a * 6f) / 6f;
                    px[y * N + x] = new Color(Mathf.Lerp(c.r, 1f, a > 0.5f ? 0.35f : 0f), Mathf.Lerp(c.g, 1f, a > 0.5f ? 0.2f : 0f), c.b, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float diameter = Float(m, "radius", 5f) * 2f * dir.tileSize;
            // điểm sắp xếp (pivot) đặt ở 3/4 bán kính phía nam tâm: người chơi đứng phía nam -> vẽ trước bong bóng,
            // phía bắc / hai bên -> vẽ sau (như một quả cầu thật); Con Mắt (chân ở nam tâm ~3 ô) vẫn nằm trong bong bóng.
            const float sortDrop = 0.75f;
            var sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f - sortDrop * 0.5f), N / diameter);
            var go = new GameObject("shield_" + m.name);
            go.transform.SetParent(transform, false);
            go.transform.position = (Vector3)m.rect.center - new Vector3(0f, diameter * 0.5f * sortDrop, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            var psr = FindFirstObjectByType<PlayerController>() is PlayerController pc ? pc.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder; }
            else sr.sortingOrder = 500;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            // vỏ năng lượng chặn người chơi (không cho bước vào hố khi Eye còn được nuôi)
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = diameter * 0.5f * 0.94f;
            col.offset = new Vector2(0f, diameter * 0.5f * sortDrop);
            var lightGo = new GameObject("shield_light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.position = m.rect.center;
            var l = lightGo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = diameter * 0.7f;
            l.pointLightInnerRadius = diameter * 0.2f;
            l.intensity = 0.7f;
            l.color = c;
            m.fx = go.transform;
            m.fxSr = sr;
            m.fxScale = Vector3.one;
            // `veil` = tên object nằm trong bong bóng (vd. boss_idle_fx): nhìn qua lớp Ichor -> mờ, ám xanh, gợn sóng
            foreach (var n in Str(m, "veil", "").Split(','))
                if (!string.IsNullOrWhiteSpace(n) && named.TryGetValue(n.Trim(), out var vl))
                    foreach (var g in vl)
                        if (g != null)
                            foreach (var vsr in g.GetComponentsInChildren<SpriteRenderer>(true))
                                m.veil.Add((vsr, vsr.color, vsr.transform.localPosition));
        }

        private void UpdateFx(Mech m)
        {
            if (m.fx == null || m.done) return;
            if (m.type == "shield")
            {
                float s = 1f + Mathf.Sin(Time.time * 2.2f) * 0.025f;
                m.fx.localScale = new Vector3(s, s, 1f);
                // vật bên trong: ám xanh Ichor + trong mờ, gợn ngang nhẹ như nhìn qua chất lỏng
                var acid = ParseColor(Str(m, "color", "#7dff4a"), 1f);
                float a = 0.5f + 0.06f * Mathf.Sin(Time.time * 3.1f);
                for (int i = 0; i < m.veil.Count; i++)
                {
                    var (vsr, col, pos) = m.veil[i];
                    if (vsr == null) continue;
                    var c = Color.Lerp(col, acid * 0.8f, 0.55f);
                    c.a = col.a * a;
                    vsr.color = c;
                    vsr.transform.localPosition = pos + new Vector3(Mathf.Sin(Time.time * 2.6f + i) * 0.02f, Mathf.Sin(Time.time * 1.7f) * 0.012f, 0f);
                }
            }
            else if (m.type == "beam" && beamTemplate != null && m.fxSr != null)
            {
                m.fxSr.sprite = beamTemplate.sprite;                               // chạy theo khung của ảnh động mẫu
                var sc = m.fxScale;
                m.fx.localScale = new Vector3(sc.x, sc.y * (1f + Mathf.Sin(Time.time * 9f) * 0.12f), 1f);
            }
        }

        private IEnumerator FadeFx(Mech m, bool pop)
        {
            if (m.fx == null) yield break;
            foreach (var c2 in m.fx.GetComponentsInChildren<Collider2D>()) c2.enabled = false;
            foreach (var (vsr, col, pos) in m.veil)                      // bong bóng vỡ: trả lại màu & chỗ cũ
                if (vsr != null) { vsr.color = col; vsr.transform.localPosition = pos; }
            var srs = m.fx.GetComponentsInChildren<SpriteRenderer>();
            var ls = m.fx.GetComponentsInChildren<Light2D>();
            float t0 = 0f, dur = pop ? 0.6f : 1.2f;
            var s0 = m.fx.localScale;
            while (t0 < dur)
            {
                t0 += Time.deltaTime;
                float k = t0 / dur;
                foreach (var sr in srs) { var c = sr.color; c.a = (1f - k) * (pop ? 1f : 0.85f); sr.color = c; }
                foreach (var l in ls) l.intensity *= 0.95f;
                if (pop) m.fx.localScale = s0 * (1f + 0.25f * k);
                else m.fx.localScale = new Vector3(s0.x, s0.y * (1f - k), 1f);
                yield return null;
            }
            m.fx.gameObject.SetActive(false);
        }

        private static Color ParseColor(string hex, float alpha)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) c = new Color(0.55f, 1f, 0.35f);
            c.a = alpha;
            return c;
        }

        // ------------------------------------------------------------------ cống tràn
        private void InitFlood(Mech m)
        {
            var names = Str(m, "layers", "").Split(',');
            m.stageLayers = new GameObject[names.Length];
            m.stageCells = new HashSet<Vector2Int>[names.Length];
            var all = dir.mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
            {
                string ln = names[i].Trim();
                foreach (var tr in all) if (tr.name == ln) { m.stageLayers[i] = tr.gameObject; break; }
                if (m.stageLayers[i] == null) Debug.LogWarning("[MapMechanics] Không thấy lớp " + ln);
                var set = new HashSet<Vector2Int>();
                foreach (var c in Str(m, "cells_" + (i + 1), "").Split(';'))
                {
                    var p = c.Split(',');
                    if (p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y)) set.Add(new Vector2Int(x, y));
                }
                m.stageCells[i] = set;
            }
            Show(m.name + "_warn", false);
            SetFloodStage(m, 0);
        }

        private void SetFloodStage(Mech m, int stage)
        {
            m.phase = stage;
            if (m.stageLayers == null) return;
            for (int i = 0; i < m.stageLayers.Length; i++) if (m.stageLayers[i] != null) m.stageLayers[i].SetActive(i == stage);
        }

        private void UpdateFlood(Mech m)
        {
            if (m.stageLayers == null || m.stageLayers.Length == 0) return;
            float idle = Float(m, "idle_seconds", 2.5f), rise = Float(m, "rise_seconds", 0.9f), hold = Float(m, "hold_seconds", 1.8f),
                  fall = Float(m, "fall_seconds", 0.9f), warn = Float(m, "warn_seconds", 1f);
            int top = m.stageLayers.Length - 1;
            float t = Mathf.Repeat(Time.time + Float(m, "offset", 0f), idle + rise + hold + fall);
            int stage = t < idle ? 0 : t < idle + rise ? Mathf.Min(1, top) : t < idle + rise + hold ? top : Mathf.Min(1, top);
            if (stage != m.phase) SetFloodStage(m, stage);
            Show(m.name + "_warn", t >= idle - warn && t < idle);
            if (playerHealth == null || playerHealth.IsDead || Time.time < m.nextTick) return;
            if (!m.stageCells[stage].Contains(WorldToTile(player.position))) return;
            m.nextTick = Time.time + tick;
            Hurt(playerHealth.Max * Float(m, "dps_percent", 8f) / 100f * tick, Vector2.zero, 0f);
            if (!playerHealth.Invulnerable)
                playerHealth.ApplyPoison(Float(m, "poison_percent", 3f) / 100f, Float(m, "poison_seconds", 2f));
        }

        private Vector2Int WorldToTile(Vector3 p)
        {
            var lp = dir.mapRoot.InverseTransformPoint(p);
            return new Vector2Int(Mathf.FloorToInt(lp.x / dir.tileSize), Mathf.FloorToInt(-lp.y / dir.tileSize));
        }

        private void Hurt(float amount, Vector2 d, float knock)
        {
            if (playerHealth == null || playerHealth.IsDead) return;
            playerHealth.TakeDamage(amount, d, knock);
        }


        // ------------------------------------------------------------------ vòng sáng vàng đánh dấu ghi chú câu đố
        private static Sprite markRing, markSpark;

        private void CreateNoteMarker(Mech m)
        {
            Vector2 foot = m.rect.center;
            float top = foot.y + 0.3f;
            if (named.TryGetValue(m.name + "_item", out var items))
                foreach (var go in items)
                {
                    var r = go != null ? go.GetComponentInChildren<SpriteRenderer>(true) : null;
                    if (r == null) continue;
                    foot = new Vector2(r.bounds.center.x, r.bounds.min.y);
                    top = r.bounds.max.y;
                }
            var root = new GameObject("NoteMarker " + m.name);
            root.transform.position = foot;
            var ring = new GameObject("ring").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(root.transform, false);
            ring.sprite = MarkRing; ring.sortingOrder = 1; ring.color = new Color(1f, 0.82f, 0.35f, 0.8f);
            for (int i = 0; i < 3; i++)
            {
                var sp = new GameObject("spark" + i).AddComponent<SpriteRenderer>();
                sp.transform.SetParent(root.transform, false);
                sp.sprite = MarkSpark; sp.sortingOrder = 6; sp.color = new Color(1f, 0.9f, 0.5f, 1f);
                sp.transform.localPosition = new Vector3(0f, top - foot.y + 0.05f, 0f);
            }
            var l = root.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(1f, 0.8f, 0.4f);
            l.pointLightOuterRadius = 1.1f; l.pointLightInnerRadius = 0.15f; l.intensity = 1f;
            root.SetActive(false);
            m.marker = root;
        }

        private void AnimateNoteMarker(Mech m)
        {
            float t = Time.time * 2.4f;
            var tr = m.marker.transform;
            var ring = tr.GetChild(0);
            float k = 0.5f + 0.5f * Mathf.Sin(t);
            ring.localScale = new Vector3(1f + 0.15f * k, (1f + 0.15f * k) * 0.55f, 1f);
            ring.GetComponent<SpriteRenderer>().color = new Color(1f, 0.82f, 0.35f, 0.45f + 0.4f * k);
            for (int i = 1; i < tr.childCount; i++)
            {
                var sp = tr.GetChild(i);
                float a = t * 0.6f + i * 2.094f;
                sp.localPosition = new Vector3(Mathf.Cos(a) * 0.22f, sp.localPosition.y, 0f);
                float tw = 0.5f + 0.5f * Mathf.Sin(t * 1.7f + i * 1.3f);
                sp.localScale = Vector3.one * (0.6f + 0.6f * tw);
                sp.GetComponent<SpriteRenderer>().color = new Color(1f, 0.9f, 0.5f, 0.4f + 0.6f * tw);
            }
            var l = m.marker.GetComponent<Light2D>();
            if (l != null) l.intensity = 0.7f + 0.6f * k;
        }

        private static Sprite MarkRing
        {
            get
            {
                if (markRing != null) return markRing;
                const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n]; float c = (n - 1) / 2f;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    byte a = r > 1f ? (byte)0 : r > 0.86f ? (byte)255 : r > 0.74f ? (byte)90 : (byte)0;
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px); tex.Apply();
                return markRing = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);   // đường kính 0,64 đv = 2 ô
            }
        }

        private static Sprite MarkSpark
        {
            get
            {
                if (markSpark != null) return markSpark;
                const int n = 7; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    int d = Mathf.Abs(x - 3) + Mathf.Abs(y - 3);
                    bool on = (x == 3 || y == 3) ? d <= 3 : d <= 1;
                    px[y * n + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
                tex.SetPixels32(px); tex.Apply();
                return markSpark = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        // ------------------------------------------------------------------ đòn chém của người chơi
        private void OnStruck(Vector2 c, float r)
        {
            if (dir == null || activeConsole != null) return;
            // 28/10: đòn chém chỉ còn tác dụng với van gỉ và vật cản gỗ (chuông / cần gạt / vòng số / bàn điều khiển dùng phím T)
            foreach (var m in mechs)
            {
                if (m.done || !Overlaps(m.rect, c, r)) continue;
                if (m.type == "valve" || m.type == "breakable") Activate(m);
            }
        }

        /// Kích hoạt cần gạt / vòng số / van / vật cản / bàn điều khiển (từ phím T hoặc đòn chém).
        private void Activate(Mech m)
        {
            switch (m.type)
            {
                case "console":
                    if (!dir.Flags.HasAll(Str(m, "requires", ""))) { Hints.Show(LockedText(m), 4f); break; }
                    EnterConsole(m);
                    return;
                case "lever":
                    m.done = true;
                    Hints.Show(Str(m, "text", "The lever grinds down."), 3f);
                    dir.Flags.Set(Str(m, "sets", m.name));
                    string spawn = Str(m, "spawn", "");
                    if (!string.IsNullOrEmpty(spawn))
                    {
                        Vector3 at = TilePoint(Str(m, "spawn_at", ""), m.rect.center);
                        foreach (var en in spawn.Split('|')) dir.SpawnInZone(en.Trim(), at, Str(m, "zone", ""));
                    }
                    break;
                case "dial":
                    m.value = (m.value + 1) % 10;
                    var group = mechs.FindAll(g => g.type == "dial" && Str(g, "group", "") == Str(m, "group", ""));
                    bool ok = group.TrueForAll(g => g.value == Int(g, "answer", 0));
                    if (ok)
                    {
                        foreach (var g in group) g.done = true;
                        Hints.Show(Str(m, "text", "The dials click into place."), 3f);
                        dir.Flags.Set(Str(m, "sets", m.name));
                    }
                    else Hints.Show(DialText(m), 1.5f);
                    break;
                case "valve":
                    if (m.rusted) { Hints.Show("Rusted solid. Your blade only rings off it.", 2f); StartCoroutine(Shake(m.name)); break; }
                    m.done = true;
                    Hints.Show(Str(m, "text", "The valve turns."), 3f);
                    dir.Flags.Set(Str(m, "sets", m.name));
                    break;
                case "breakable":
                    m.hits--;
                    StartCoroutine(Shake(m.name));
                    if (m.hits <= 0)
                    {
                        m.done = true;
                        Hints.Show(Str(m, "text", "It breaks apart."), 2.5f);
                        dir.Flags.Set(Str(m, "sets", m.name + "_broken"));
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ chuông (câu kết Requiem)
        private readonly Dictionary<string, int> bellProgress = new Dictionary<string, int>();
        private float nextBellSpawn;
        private static readonly string[] BellNames = { "", "great", "low", "middle", "high", "small" };

        private void RingBell(Mech m)
        {
            int note = Int(m, "note", 1);
            StartCoroutine(BellFlash(m.rect.center, note));
            string group = Str(m, "group", "");
            if (string.IsNullOrEmpty(group) || m.done) { Hints.Show("The bell tolls.", 1.2f); return; }
            var ans = Str(m, "answer", "").Split(',');
            if (ans.Length == 0) return;
            bellProgress.TryGetValue(group, out int step);
            string nm = note >= 1 && note < BellNames.Length ? BellNames[note] : "";
            if (ans[Mathf.Min(step, ans.Length - 1)].Trim() == note.ToString())
            {
                step++;
                if (step >= ans.Length)
                {
                    foreach (var g in mechs) if (g.type == "bell" && Str(g, "group", "") == group) g.done = true;
                    bellProgress[group] = 0;
                    Hints.Show(Str(m, "text", "The last note rings out. The bells fall silent."), 5f);
                    dir.Flags.Set(Str(m, "sets", group + "_done"));
                    return;
                }
                bellProgress[group] = step;
                Hints.Show($"The {nm} bell tolls. ({step}/{ans.Length})", 1.6f);
            }
            else
            {
                bellProgress[group] = ans[0].Trim() == note.ToString() ? 1 : 0;
                Hints.Show($"The {nm} bell clangs out of tune. Something stirs in the rafters.", 2.5f);
                string fail = Str(m, "fail_spawn", "");
                if (!string.IsNullOrEmpty(fail) && Time.time >= nextBellSpawn)
                {
                    nextBellSpawn = Time.time + 2.5f;
                    dir.SpawnInZone(fail, FrontSpot(m.rect), Str(m, "zone", ""));
                }
            }
        }

        ///  chỗ thả quái phía TRƯỚC chuông (phía người chơi, y thấp hơn) và không đè lên vật cản —

        private Vector2 FrontSpot(Rect r)
        {
            Vector2 c = r.center, pp = player != null ? (Vector2)player.position : c + Vector2.down;
            float side = pp.x >= c.x ? 1f : -1f;
            float below = r.height * 0.5f + 0.45f;
            var tries = new[]
            {
                new Vector2(c.x, r.yMin - 0.45f),
                new Vector2(c.x + side * 0.6f, r.yMin - 0.35f),
                new Vector2(c.x - side * 0.6f, r.yMin - 0.35f),
                new Vector2(c.x, c.y - below - 0.4f),
                Vector2.Lerp(c, pp, 0.5f),
            };
            var filter = new ContactFilter2D { useTriggers = false };
            var hits = new List<Collider2D>();
            foreach (var t in tries)
            {
                hits.Clear();
                Physics2D.OverlapCircle(t, 0.2f, filter, hits);
                bool blocked = false;
                foreach (var h in hits)
                    if (h != null && h.GetComponentInParent<PlayerController>() == null && h.GetComponentInParent<EnemyAI>() == null) { blocked = true; break; }
                if (!blocked) return t;
            }
            return Vector2.Lerp(c, pp, 0.6f);
        }

        private IEnumerator BellFlash(Vector2 at, int note)
        {
            var go = new GameObject("BellFlash");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.62f, 0.86f, 1f);
            float rad = Mathf.Lerp(1.6f, 0.9f, (note - 1) / 4f);
            l.pointLightOuterRadius = rad;
            l.pointLightInnerRadius = rad * 0.2f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                l.intensity = Mathf.Lerp(1.6f, 0f, t / 0.8f);
                yield return null;
            }
            Destroy(go);
        }

        private static bool Overlaps(Rect rc, Vector2 c, float r)
        {
            float dx = Mathf.Max(rc.xMin - c.x, 0f, c.x - rc.xMax);
            float dy = Mathf.Max(rc.yMin - c.y, 0f, c.y - rc.yMax);
            return dx * dx + dy * dy <= r * r;
        }

        // ------------------------------------------------------------------ bảng điều khiển (camera lia sang phòng ống)
        private void EnterConsole(Mech m)
        {
            activeGrid = mechs.Find(g => g.type == "pipegrid" && g.name == Str(m, "grid", ""));
            if (activeGrid == null) { Debug.LogWarning("[MapMechanics] Không thấy lưới ống " + Str(m, "grid", "")); return; }
            if (activeGrid.done) { Hints.Show("The flow is already turned to the drain.", 2.5f); return; }
            activeConsole = m;
            activeGrid.inputReady = Time.unscaledTime + 0.35f;     // cú chém mở bảng không tính là click xoay ống
            SetPlayerControl(false);
            // khung nhìn: cả lưới + ống cấp/miệng ống phía trên + cống B phía dưới; dời tâm xuống chút để dòng chữ
            // hướng dẫn (đáy màn hình) không che hàng ống cuối
            Vector3 focus = (Vector3)activeGrid.rect.center + new Vector3(0f, -0.22f, 0f);
            var cam = Camera.main;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float size = Mathf.Max(activeGrid.rect.height * 0.5f + 0.95f, activeGrid.rect.width * 0.5f / aspect + 0.5f);
            if (camFollow != null) camFollow.Focus(focus, size);
            Hints.Show(Str(m, "prompt", "Click a pipe to turn it. Esc to step back."), 9999f);
        }

        private void ExitConsole()
        {
            activeConsole = null;
            activeGrid = null;
            if (camFollow != null) camFollow.Release();
            SetPlayerControl(true);
            Hints.Clear();
        }

        private void SetPlayerControl(bool on)
        {
            ConsoleActive = !on;
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = on;
            var anim = player.GetComponent<Animator>();
            if (!on && anim != null) anim.SetFloat("Speed", 0f);
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        private void ConsoleUpdate()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)) { ExitConsole(); return; }
            if (playerHealth != null && playerHealth.IsDead) { ExitConsole(); return; }
            var g = activeGrid;
            if (g == null || g.done || Time.unscaledTime < g.inputReady) return;
            if (kb != null && kb.rKey.wasPressedThisFrame) { ResetGrid(g); return; }
            if (mouse == null || Camera.main == null) return;
            bool cw = mouse.leftButton.wasPressedThisFrame, ccw = mouse.rightButton.wasPressedThisFrame;
            if (!cw && !ccw) return;
            Vector3 wp = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            for (int cy = 0; cy < g.rows; cy++)
                for (int cx = 0; cx < g.cols; cx++)
                    if (CellRect(g, cx, cy).Contains((Vector2)wp)) { RotateCell(g, cy * g.cols + cx, cw); return; }
        }

        // ------------------------------------------------------------------ lưới ống
        private void InitGrid(Mech m)
        {
            m.cols = Int(m, "cols", 4);
            m.rows = Int(m, "rows", 3);
            int n = m.cols * m.rows;
            var ms = Str(m, "masks", "").Split(',');
            m.masks = new int[n];
            m.bolted = new bool[n];
            m.cells = new SpriteRenderer[n];
            m.glows = new Light2D[n];
            for (int i = 0; i < n && i < ms.Length; i++) int.TryParse(ms[i], out m.masks[i]);
            m.startMasks = (int[])m.masks.Clone();
            foreach (var b in Str(m, "bolts", "").Split(';'))
            {
                var p = b.Split(',');
                if (p.Length == 2 && int.TryParse(p[0], out int bx) && int.TryParse(p[1], out int by) && bx < m.cols && by < m.rows)
                    m.bolted[by * m.cols + bx] = true;
            }
            for (int cy = 0; cy < m.rows; cy++)
                for (int cx = 0; cx < m.cols; cx++)
                {
                    int i = cy * m.cols + cx;
                    if (!named.TryGetValue($"pipe_{cx}_{cy}", out var list) || list.Count == 0) continue;
                    m.cells[i] = list[0].GetComponentInChildren<SpriteRenderer>(true);
                    // ánh sáng xanh nhẹ cho ô đang có Ichor
                    var go = new GameObject($"glow_{cx}_{cy}");
                    go.transform.position = (Vector3)CellRect(m, cx, cy).center;
                    go.transform.SetParent(transform, true);
                    var l = go.AddComponent<Light2D>();
                    l.lightType = Light2D.LightType.Point;
                    l.pointLightOuterRadius = CellRect(m, cx, cy).width * 0.85f;
                    l.pointLightInnerRadius = 0.02f;
                    l.intensity = 0.55f;
                    l.color = new Color(0.55f, 1f, 0.35f);
                    l.enabled = false;
                    m.glows[i] = l;
                }
            foreach (var kv in named)
            {
                if (!kv.Key.StartsWith("pipefix_")) continue;                 // pipefix_{GROUP}_{MM}_{k}
                var p = kv.Key.Split('_');
                if (p.Length < 3 || !int.TryParse(p[2], out int mask)) continue;
                foreach (var go in kv.Value)
                {
                    var sr = go.GetComponentInChildren<SpriteRenderer>(true);
                    if (sr != null) m.fixedPipes.Add((sr, p[1], mask));
                }
            }
            if (named.TryGetValue("leakpal_fx", out var tpl) && tpl.Count > 0) { leakTemplate = tpl[0]; leakTemplate.SetActive(false); }
            Repaint(m);
        }

        private Rect CellRect(Mech m, int cx, int cy)
        {
            float w = m.rect.width / m.cols, h = m.rect.height / m.rows;
            return new Rect(m.rect.xMin + cx * w, m.rect.yMax - (cy + 1) * h, w, h);
        }

        private static int RotCW(int m) => ((m << 1) | (m >> 3)) & 15;
        private static int RotCCW(int m) => ((m >> 1) | (m << 3)) & 15;

        private void RotateCell(Mech g, int i, bool clockwise)
        {
            if (g.bolted[i])
            {
                Hints.Show("This joint is bolted fast.", 1.5f);
                if (g.cells[i] != null) StartCoroutine(Nudge(g.cells[i].transform));
                return;
            }
            g.masks[i] = clockwise ? RotCW(g.masks[i]) : RotCCW(g.masks[i]);
            var st = Repaint(g);
            if (st.toDst && !st.toCut && st.leaks == 0)
            {
                g.done = true;
                StartCoroutine(Solved(g));
            }
            else if (st.toDst && st.toCut) Hints.Show("Ichor reaches the drain - but it still feeds the cistern. Cut the east line.", 3f);
            else if (st.toDst && st.leaks > 0) Hints.Show("Ichor reaches the drain, but spills from an open joint. Seal every end.", 3f);
        }

        private void ResetGrid(Mech g)
        {
            g.masks = (int[])g.startMasks.Clone();
            Repaint(g);
            Hints.Show("The valves clank back to where they were.", 1.5f);
        }

        private IEnumerator Nudge(Transform t)
        {
            var p0 = t.localPosition;
            for (int k = 0; k < 6; k++) { t.localPosition = p0 + (Vector3)(Random.insideUnitCircle * 0.02f); yield return new WaitForSeconds(0.03f); }
            t.localPosition = p0;
        }

        private IEnumerator Solved(Mech g)
        {
            Hints.Show(Str(g, "solve_text", "The pipes connect."), 4f);
            dir.Flags.Set(Str(g, "sets", g.name + "_solved"));
            yield return new WaitForSeconds(1.6f);
            string after = Str(g, "after_focus", "");
            if (!string.IsNullOrEmpty(after) && camFollow != null)
            {
                camFollow.Focus(TilePoint(after, g.rect.center), Camera.main != null ? Camera.main.orthographicSize : 3f);
                Hints.Show("Far off, the pit goes dark. The Eye shrieks.", 3f);
                yield return new WaitForSeconds(2.8f);
            }
            ExitConsole();
            Hints.Show("The cistern gate grinds open.", 3f);
        }

        private struct GridState { public HashSet<int> wet; public bool toDst, toCut; public int leaks; }

        /// Tô lại ống khô/ướt, đèn, ống cố định, tia rò theo dòng chảy hiện tại.
        private GridState Repaint(Mech g)
        {
            var st = new GridState { wet = new HashSet<int>() };
            var ends = new List<(int cell, int k)>();
            Flow(g, st.wet, ends);
            ParseEnd(Str(g, "src", "1,0,N"), out int sx, out int sy, out int sb);
            ParseEnd(Str(g, "dst", "2,2,S"), out int dx, out int dy, out int db);
            ParseEnd(Str(g, "cut", "3,1,E"), out int cx, out int cy, out int cb);
            var leakPos = new List<Vector2>();
            foreach (var (cell, k) in ends)
            {
                int x = cell % g.cols, y = cell / g.cols;
                if (x == dx && y == dy && BIT[k] == db) { st.toDst = true; continue; }
                if (x == cx && y == cy && BIT[k] == cb) { st.toCut = true; continue; }
                if (x == sx && y == sy && BIT[k] == sb) continue;
                st.leaks++;
                var r = CellRect(g, x, y);
                leakPos.Add(r.center + new Vector2(BX[k] * r.width * 0.45f, -BY[k] * r.height * 0.45f));
            }
            for (int i = 0; i < g.masks.Length; i++)
            {
                bool w = st.wet.Contains(i);
                var sr = g.cells[i];
                if (sr != null)
                {
                    string key = (g.bolted[i] ? "bolt_" : "") + (w ? "wet_" : "cell_") + g.masks[i].ToString("00");
                    if (pipeSprites.TryGetValue(key, out var sp) && sp != null) sr.sprite = sp;
                }
                if (g.glows[i] != null) g.glows[i].enabled = w;
            }
            foreach (var (sr, group, mask) in g.fixedPipes)
            {
                bool w = group == "IN" || (group == "A" && st.toCut) || (group == "B" && st.toDst);
                if (group == "IN") continue;
                if (pipeSprites.TryGetValue((w ? "wet_" : "cell_") + mask.ToString("00"), out var sp) && sp != null) sr.sprite = sp;
            }
            // tia rò: dùng lại / nhân bản từ mẫu
            if (leakTemplate != null)
            {
                while (g.leakFx.Count < leakPos.Count)
                {
                    var c = Instantiate(leakTemplate, leakTemplate.transform.parent);
                    c.name = "leak_fx";
                    g.leakFx.Add(c);
                }
                for (int i = 0; i < g.leakFx.Count; i++)
                {
                    bool on = i < leakPos.Count;
                    if (on)
                    {
                        var pos = leakPos[i];
                        // mẫu đặt theo chân sprite: dời sao cho tâm ảnh rơi vào đầu ống
                        var sr = g.leakFx[i].GetComponentInChildren<SpriteRenderer>(true);
                        Vector3 off = sr != null ? g.leakFx[i].transform.position - sr.bounds.center : Vector3.zero;
                        g.leakFx[i].transform.position = new Vector3(pos.x, pos.y, 0f) + off;
                    }
                    g.leakFx[i].SetActive(on);
                }
            }
            return st;
        }

        private static readonly int[] BX = { 0, 1, 0, -1 }, BY = { -1, 0, 1, 0 }, BIT = { 1, 2, 4, 8 }, OPP = { 4, 8, 1, 2 };

        /// BFS từ VÀO. `ends` = các (ô, hướng) mở mà phía bên kia không nối lại (ra ngoài lưới hoặc ô không mở).
        private void Flow(Mech g, HashSet<int> seen, List<(int, int)> ends)
        {
            if (!ParseEnd(Str(g, "src", "1,0,N"), out int sx, out int sy, out int sb)) return;
            int s = sy * g.cols + sx;
            if ((g.masks[s] & sb) == 0) return;
            seen.Add(s);
            var q = new Queue<int>();
            q.Enqueue(s);
            while (q.Count > 0)
            {
                int i = q.Dequeue(), x = i % g.cols, y = i / g.cols;
                for (int k = 0; k < 4; k++)
                {
                    if ((g.masks[i] & BIT[k]) == 0) continue;
                    int nx = x + BX[k], ny = y + BY[k];
                    if (nx < 0 || ny < 0 || nx >= g.cols || ny >= g.rows) { ends.Add((i, k)); continue; }
                    int j = ny * g.cols + nx;
                    if ((g.masks[j] & OPP[k]) == 0) { ends.Add((i, k)); continue; }
                    if (seen.Add(j)) q.Enqueue(j);
                }
            }
        }

        private static bool ParseEnd(string s, out int x, out int y, out int b)
        {
            x = y = b = 0;
            var p = s.Split(',');
            if (p.Length != 3 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y)) return false;
            b = p[2].Trim().ToUpperInvariant() switch { "N" => 1, "E" => 2, "S" => 4, _ => 8 };
            return true;
        }

        /// <summary>Bàn điều khiển chưa đủ điều kiện: liệt kê đúng những thứ CÒN THIẾU theo `needs`
        /// ("cờ,cờ=mô tả|cờ=mô tả"); không có `needs` thì dùng `locked_text`.</summary>
        private string LockedText(Mech m)
        {
            string needs = Str(m, "needs", "");
            if (string.IsNullOrEmpty(needs)) return Str(m, "locked_text", "The lever will not move.");
            var missing = new List<string>();
            foreach (var part in needs.Split('|'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (!dir.Flags.HasAll(part.Substring(0, eq))) missing.Add(part.Substring(eq + 1).Trim());
            }
            return missing.Count == 0 ? Str(m, "locked_text", "The lever will not move.")
                                      : "The console will not wake. Still missing: " + string.Join("; ", missing) + ".";
        }

        // ------------------------------------------------------------------ Map 2 v2: khoá số, van gỉ, bể kính
        private string DialText(Mech m)
        {
            var group = mechs.FindAll(g => g.type == "dial" && Str(g, "group", "") == Str(m, "group", ""));
            group.Sort((a, b) => Int(a, "index", 0).CompareTo(Int(b, "index", 0)));
            return "The dials read " + string.Join(" - ", group.ConvertAll(g => g.value.ToString())) + ".";
        }

        /// <summary>Ichor Ram lao đâm vào tường: van gỉ trong bán kính r (đơn vị Unity) bị gõ vỡ lớp gỉ.</summary>
        public bool RamImpact(Vector2 at, float r)
        {
            bool any = false;
            foreach (var m in mechs)
            {
                if (m.type != "valve" || m.done || !m.rusted || !Overlaps(m.rect, at, r)) continue;
                m.rusted = false;
                any = true;
                StartCoroutine(Shake(m.name));
            }
            if (any && dir != null) Hints.Show("The ram's charge cracks the rust off a valve!", 2.5f);
            return any;
        }

        /// <summary>Cóc Gorgemaw đập xuống: bể kính trong bán kính r vỡ. Trả về true nếu có bể vỡ (cóc bị choáng).</summary>
        public bool Shatter(Vector2 at, float r)
        {
            bool any = false;
            foreach (var m in mechs)
            {
                if (m.type != "tank" || m.done || !Overlaps(m.rect, at, r)) continue;
                m.done = true;
                any = true;
                Show(Str(m, "obj", ""), false);
                Show(Str(m, "broken", ""), true);
            }
            return any;
        }

        // ------------------------------------------------------------------ bảng đọc ghi chú + khoá ký tự (OnGUI, co theo màn hình)
        private Mech openNote, openCipher;
        private readonly Dictionary<int, Sprite> glyphs = new Dictionary<int, Sprite>();
        private Texture2D upTex, downTex;
        private GUIStyle titleSt, bodySt, capSt, footSt;
        private float stylesFor = -1f;

        /// <summary>Bảng khoá ký tự đang mở: chuột trái dùng để bấm mũi tên, không chém.</summary>
        /// Bảng đọc ghi chú / khoá ký tự đang mở (Esc đóng bảng thay vì mở menu Pause; chuột trái không chém).
        public static bool PanelOpen => Instance != null &&
            (Instance.openNote != null || Instance.openWindow != null || (Instance.openCipher != null && !Instance.openCipher.done) || Instance.closedFrame == Time.frameCount);
        private Mech openWindow;
        private float windowSolvedAt = -1f;
        private readonly Dictionary<string, Sprite> pictures = new Dictionary<string, Sprite>();
        private int closedFrame = -1;

        private static Texture2D Arrow(bool up)
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int row = up ? y : N - 1 - y;                     // đáy rộng -> đỉnh nhọn
                    float half = (N - 1 - row) * 0.5f;
                    bool inside = row >= 4 && row <= N - 6 && Mathf.Abs(x - (N - 1) * 0.5f) <= half - 2f;
                    t.SetPixel(x, y, inside ? new Color(0.95f, 0.82f, 0.45f, 1f) : new Color(0, 0, 0, 0));
                }
            t.Apply();
            return t;
        }

        private void BuildPanelStyles(float s)
        {
            stylesFor = s;
            titleSt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            titleSt.normal.textColor = new Color(0.96f, 0.84f, 0.55f);
            bodySt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * s), wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            bodySt.normal.textColor = new Color(0.92f, 0.9f, 0.84f);
            capSt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * s), alignment = TextAnchor.UpperCenter, wordWrap = true };
            capSt.normal.textColor = new Color(0.85f, 0.8f, 0.7f);
            footSt = new GUIStyle(capSt) { fontSize = Mathf.RoundToInt(13 * s), fontStyle = FontStyle.Italic };
            footSt.normal.textColor = new Color(0.7f, 0.68f, 0.62f);
            var bf = Resources.Load<Font>("UI/Requiem/Fonts/RequiemBody");
            var tf = Resources.Load<Font>("UI/Requiem/Fonts/RequiemTitle");
            if (bf != null) { bodySt.font = bf; capSt.font = bf; footSt.font = bf; footSt.fontStyle = FontStyle.Normal; }
            if (tf != null) { titleSt.font = tf; titleSt.fontStyle = FontStyle.Normal; }
            if (upTex == null) upTex = Arrow(true);
            if (downTex == null) downTex = Arrow(false);
        }

        private void OnGUI()
        {
            if (dir == null || (openNote == null && openCipher == null && openWindow == null)) return;
            float s = Mathf.Max(0.5f, Screen.height / 720f);
            if (titleSt == null || !Mathf.Approximately(s, stylesFor)) BuildPanelStyles(s);
            if (openWindow != null) DrawWindow(openWindow, s);
            else if (openCipher != null && !openCipher.done) DrawCipher(openCipher, s);
            else if (openNote != null) DrawNote(openNote, s);
        }

        private static void Frame(Rect r, float s)
        {
            var old = GUI.color;
            GUI.color = new Color(0.55f, 0.45f, 0.3f, 0.95f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.07f, 0.065f, 0.06f, 0.95f);
            GUI.DrawTexture(new Rect(r.x + 3 * s, r.y + 3 * s, r.width - 6 * s, r.height - 6 * s), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawGlyph(Rect r, int digit)
        {
            if (!glyphs.TryGetValue(digit, out var sp) || sp == null || sp.texture == null) return;
            var tr = sp.textureRect;
            var tex = sp.texture;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
        }

        private void DrawNote(Mech m, float s)
        {
            float w = Mathf.Min(Screen.width - 40 * s, 720 * s), inner = w - 56 * s;
            string body = TextUtil.Clean(Str(m, "text", "").Replace("\\n", "\n"));
            float bh = bodySt.CalcHeight(new GUIContent(body), inner);
            var gl = new List<(int d, string cap)>();
            foreach (var part in Str(m, "glyphs", "").Split(';'))
            {
                int c = part.IndexOf(':');
                if (c > 0 && int.TryParse(part.Substring(0, c), out int d)) gl.Add((d, part.Substring(c + 1)));
            }
            // "seal" = "ô:ký tự[:số ô]" (vd "1:3" = ô I của khoá ký tự phải là ký tự 3) -> vẽ sơ đồ khoá, ô đã biết có ký tự, ô khác "?"
            int sealSlot = -1, sealGlyph = -1, sealSlots = 3;
            var sp = Str(m, "seal", "").Split(':');
            if (sp.Length >= 2 && int.TryParse(sp[0], out int ss) && int.TryParse(sp[1], out int sg))
            {
                sealSlot = ss - 1; sealGlyph = sg;
                if (sp.Length >= 3 && int.TryParse(sp[2], out int sn)) sealSlots = Mathf.Clamp(sn, 1, 6);
            }
            pictures.TryGetValue(Str(m, "picture", "-"), out var pic);
            float ph = pic != null ? 236 * s : 0f;
            float gh = (sealSlot >= 0 ? 150 * s : gl.Count > 0 ? 120 * s : 0f) + ph;
            float h = 64 * s + bh + gh + 40 * s;
            var r = new Rect((Screen.width - w) / 2f, Mathf.Max(20 * s, Screen.height * 0.1f), w, h);
            Frame(r, s);
            GUI.Label(new Rect(r.x + 20 * s, r.y + 14 * s, r.width - 40 * s, 36 * s), TextUtil.Clean(Str(m, "title", "")), titleSt);
            GUI.Label(new Rect(r.x + 28 * s, r.y + 58 * s, inner, bh), body, bodySt);
            if (pic != null)
            {
                var pr = new Rect(r.x + (r.width - 224 * s) / 2f, r.y + 62 * s + bh, 224 * s, 224 * s);
                DrawSprite(pr, pic, Color.white);
            }
            if (sealSlot >= 0)
            {
                string[] roman = { "I", "II", "III", "IV", "V", "VI" };
                float gy = r.y + 64 * s + bh + ph + 4 * s, cw = 104 * s;
                float gx = r.x + (r.width - cw * sealSlots) / 2f;
                for (int i = 0; i < sealSlots; i++)
                {
                    float cx = gx + i * cw + cw / 2f;
                    bool known = i == sealSlot;
                    GUI.Label(new Rect(cx - 40 * s, gy, 80 * s, 24 * s), roman[Mathf.Min(i, roman.Length - 1)], capSt);
                    var box = new Rect(cx - 40 * s, gy + 26 * s, 80 * s, 80 * s);
                    var old = GUI.color;
                    GUI.color = known ? new Color(0.30f, 0.26f, 0.16f, 1f) : new Color(0.14f, 0.15f, 0.18f, 1f);
                    GUI.DrawTexture(box, Texture2D.whiteTexture);
                    GUI.color = old;
                    if (known) DrawGlyph(new Rect(box.x + 6 * s, box.y + 6 * s, box.width - 12 * s, box.height - 12 * s), sealGlyph);
                    else GUI.Label(box, "?", titleSt);
                }
                GUI.Label(new Rect(r.x, gy + 110 * s, r.width, 30 * s), $"Records Vault lock - wheel {roman[Mathf.Min(sealSlot, roman.Length - 1)]}", capSt);
            }
            else if (gl.Count > 0)
            {
                float gy = r.y + 64 * s + bh + ph + 6 * s, cw = 150 * s;
                float gx = r.x + (r.width - cw * gl.Count) / 2f;
                for (int i = 0; i < gl.Count; i++)
                {
                    var box = new Rect(gx + i * cw + (cw - 72 * s) / 2f, gy, 72 * s, 72 * s);
                    DrawGlyph(box, gl[i].d);
                    GUI.Label(new Rect(gx + i * cw, gy + 76 * s, cw, 40 * s), gl[i].cap, capSt);
                }
            }
            GUI.Label(new Rect(r.x, r.yMax - 28 * s, r.width, 22 * s), $"Press {GameInput.Label(GameAction.Interact)} or Esc to close.", footSt);
        }

        private static void DrawSprite(Rect r, Sprite sp, Color tint, Rect? uvPart = null)
        {
            if (sp == null || sp.texture == null) return;
            var tr = sp.textureRect; var tex = sp.texture;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            if (uvPart.HasValue)
            {
                var p = uvPart.Value;
                uv = new Rect(uv.x + p.x * uv.width, uv.y + p.y * uv.height, p.width * uv.width, p.height * uv.height);
            }
            var old = GUI.color; GUI.color = tint;
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = old;
        }

        /// Cửa sổ hoa hồng: 4 mảnh (I trên trái, II trên phải, III dưới trái, IV dưới phải), bấm để xoay 90°.
        private void DrawWindow(Mech m, float s)
        {
            float w = 560 * s, h = 600 * s, img = 380 * s;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f - 10 * s, w, h);
            Frame(r, s);
            GUI.Label(new Rect(r.x + 20 * s, r.y + 14 * s, r.width - 40 * s, 36 * s), TextUtil.Clean(Str(m, "title", "The Rose Window")), titleSt);
            string sub = m.done ? Str(m, "text", "") : Str(m, "hint", "");
            GUI.Label(new Rect(r.x + 30 * s, r.y + 52 * s, r.width - 60 * s, 52 * s), TextUtil.Clean(sub), capSt);
            var area = new Rect(r.x + (r.width - img) / 2f, r.y + 112 * s, img, img);
            pictures.TryGetValue(Str(m, "picture", "-"), out var pic);
            pictures.TryGetValue(Str(m, "truth", "-"), out var truth);
            if (pic == null) { GUI.Label(area, "(missing picture)", capSt); return; }
            float half = img / 2f;
            var e = Event.current;
            bool changed = false;
            for (int i = 0; i < 4; i++)
            {
                int cx = i % 2, cy = i / 2;                                  // cy 0 = hàng trên
                var q = new Rect(area.x + cx * half, area.y + cy * half, half, half);
                var uvq = new Rect(cx * 0.5f, cy == 0 ? 0.5f : 0f, 0.5f, 0.5f); // uv gốc dưới trái
                var oldM = GUI.matrix;
                GUIUtility.RotateAroundPivot(m.wheels[i] * 90f, q.center);
                DrawSprite(q, pic, Color.white, uvq);
                GUI.matrix = oldM;
                if (!m.done && m.wheels[i] == 0)
                {   // mảnh đã đúng chiều: viền chì loé vàng nhẹ
                    var old = GUI.color; GUI.color = new Color(1f, 0.85f, 0.45f, 0.55f);
                    GUI.DrawTexture(new Rect(q.x, q.y, q.width, 2 * s), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(q.x, q.yMax - 2 * s, q.width, 2 * s), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(q.x, q.y, 2 * s, q.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(q.xMax - 2 * s, q.y, 2 * s, q.height), Texture2D.whiteTexture);
                    GUI.color = old;
                }
                if (!m.done && e.type == EventType.MouseDown && q.Contains(e.mousePosition))
                {
                    m.wheels[i] = (m.wheels[i] + (e.button == 1 ? 3 : 1)) % 4;
                    changed = true;
                    e.Use();
                }
            }
            if (m.done && truth != null)
            {   // ánh trăng chiếu qua: màu thật hiện dần
                float a = windowSolvedAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - windowSolvedAt) / 2.5f);
                DrawSprite(area, truth, new Color(1f, 1f, 1f, a));
            }
            string foot = m.done ? $"{GameInput.Label(GameAction.Interact)} or Esc to close."
                                 : $"Click a pane to turn it (right click turns it back).  {GameInput.Label(GameAction.Interact)} or Esc to leave.";
            GUI.Label(new Rect(r.x, r.yMax - 40 * s, r.width, 22 * s), foot, footSt);
            if (!changed) return;
            foreach (var v in m.wheels) if (v != 0) return;
            m.done = true;
            windowSolvedAt = Time.unscaledTime;
            dir.Flags.Set(Str(m, "sets", m.name));
            Hints.Show(Str(m, "solved_hint", "The altar grinds aside. Stairs lead down beneath the chapel."), 5f);
        }

        private void DrawCipher(Mech m, float s)
        {
            var ans = Str(m, "answer", "0,0,0").Split(',');
            var perm = Str(m, "perm", "0,1,2,3,4,5,6,7").Split(',');
            int n = m.wheels.Length;
            float cw = 150 * s, w = Mathf.Max(520 * s, cw * n + 80 * s), h = 430 * s;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f - 20 * s, w, h);
            Frame(r, s);
            GUI.Label(new Rect(r.x + 20 * s, r.y + 14 * s, r.width - 40 * s, 36 * s), Str(m, "title", "Lock"), titleSt);
            GUI.Label(new Rect(r.x + 30 * s, r.y + 52 * s, r.width - 60 * s, 44 * s), Str(m, "hint", ""), capSt);
            string[] roman = { "I", "II", "III", "IV", "V", "VI" };
            float x0 = r.x + (r.width - cw * n) / 2f, y0 = r.y + 104 * s;
            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                float cx = x0 + i * cw + cw / 2f;
                GUI.Label(new Rect(cx - 40 * s, y0, 80 * s, 26 * s), roman[Mathf.Min(i, roman.Length - 1)], capSt);
                var up = new Rect(cx - 30 * s, y0 + 28 * s, 60 * s, 44 * s);
                var gbox = new Rect(cx - 52 * s, y0 + 78 * s, 104 * s, 104 * s);
                var dn = new Rect(cx - 30 * s, y0 + 188 * s, 60 * s, 44 * s);
                var old = GUI.color;
                GUI.color = new Color(0.18f, 0.2f, 0.24f, 1f);
                GUI.DrawTexture(gbox, Texture2D.whiteTexture);
                GUI.color = old;
                int d = int.Parse(perm[m.wheels[i] % perm.Length]);
                DrawGlyph(new Rect(gbox.x + 6 * s, gbox.y + 6 * s, gbox.width - 12 * s, gbox.height - 12 * s), d);
                if (GUI.Button(up, upTex, GUIStyle.none)) { m.wheels[i] = (m.wheels[i] + 1) % perm.Length; changed = true; }
                if (GUI.Button(dn, downTex, GUIStyle.none)) { m.wheels[i] = (m.wheels[i] + perm.Length - 1) % perm.Length; changed = true; }
            }
            GUI.Label(new Rect(r.x, r.yMax - 46 * s, r.width, 22 * s), $"Click the arrows to turn each wheel.  {GameInput.Label(GameAction.Interact)} or Esc to leave.", footSt);
            if (!changed) return;
            bool ok = true;
            for (int i = 0; i < n; i++) if (perm[m.wheels[i] % perm.Length].Trim() != ans[Mathf.Min(i, ans.Length - 1)].Trim()) ok = false;
            if (!ok) return;
            m.done = true;
            openCipher = null;
            Hints.Show(Str(m, "text", "The lock opens."), 3.5f);
            dir.Flags.Set(Str(m, "sets", m.name));
        }

        // ------------------------------------------------------------------ tiện ích
        private Vector3 TilePoint(string s, Vector2 fallback)
        {
            var p = s.Split(',');
            if (p.Length == 2 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float tx) &&
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ty))
                return dir.TileToWorld(tx, ty);
            return fallback;
        }

        /// Cho bộ chiêu boss: các object trong map theo tên Tiled (vd. morrow_pillar_0, isolde_coffin_1_open).
        public List<GameObject> Named(string name) => named.TryGetValue(name, out var l) ? l : new List<GameObject>();
        public void SetVisible(string names, bool on) => Show(names, on);
        public Light2D LightNamed(string name) => lights.TryGetValue(name, out var l) ? l : null;

        private void Show(string names, bool on)
        {
            if (string.IsNullOrEmpty(names)) return;
            foreach (var n in names.Split(','))
                if (named.TryGetValue(n.Trim(), out var list)) foreach (var go in list) if (go != null) go.SetActive(on);
        }

        private IEnumerator Shake(string gateName)
        {
            var parts = new List<Transform>();
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>())
                if (so.m_TiledName == gateName && so.transform.parent != null && so.transform.parent.name == "gates") parts.Add(so.transform);
            var start = new List<Vector3>();
            foreach (var p in parts) start.Add(p.localPosition);
            for (int i = 0; i < 6; i++)
            {
                for (int k = 0; k < parts.Count; k++) if (parts[k] != null) parts[k].localPosition = start[k] + (Vector3)(Random.insideUnitCircle * 0.03f);
                yield return new WaitForSeconds(0.03f);
            }
            for (int k = 0; k < parts.Count; k++) if (parts[k] != null) parts[k].localPosition = start[k];
        }

        /// (Test F5) Bật mọi cờ mà câu đố / vật cản đặt ra (kể cả tay quay).
        public void DebugSolveAll()
        {
            if (dir == null) return;
            dir.Flags.Set("lever_handle");
            foreach (var m in mechs)
            {
                string sets = Str(m, "sets", "");
                if (string.IsNullOrEmpty(sets) || m.done) continue;
                m.done = true;
                dir.Flags.Set(sets);
            }
            Hints.Show("[TEST] All puzzles and barricades solved", 2f);
        }

        // ------------------------------------------------------------------ tương tác bằng phím T (InteractionSystem)
        private const float CloseRange = 1.1f;   // bảng đọc / khoá tự đóng khi đi xa hơn khoảng này

        private static float DistToRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private bool Usable(Mech m)
        {
            switch (m.type)
            {
                case "bell": return true;                                       // chuông đã xong vẫn ngân cho vui
                case "rosewindow": return true;                                 // giải xong vẫn xem lại được
                case "note":
                    if (m.marker != null && !m.shown) return false;            // chưa dọn phòng -> chưa xuất hiện
                    string req = Str(m, "requires", "");
                    return string.IsNullOrEmpty(req) || (dir != null && dir.Flags.HasAll(req));
                default: return !m.done;
            }
        }

        private void RegisterInteract(Mech m)
        {
            string verb;
            bool clue = false;
            switch (m.type)
            {
                case "bell": verb = "Ring"; break;
                case "lever": verb = "Pull"; break;
                case "dial": case "valve": verb = "Turn"; break;
                case "console": verb = "Use"; break;
                case "note": verb = "Read"; clue = m.marker == null; break;     // ghi chú có vòng sáng riêng (show_when) thì khỏi đốm
                case "cipher": verb = "Examine"; clue = true; break;
                case "rosewindow": verb = "Examine"; clue = true; break;
                default: return;
            }
            verb = Str(m, "verb", verb);
            var mm = m;
            var r = m.rect;
            InteractionSystem.Add(new Interactable
            {
                id = m.name, verb = verb, rect = r, clue = clue,
                range = Float(m, "range", 0.55f),                               // vd then cổng: chỉ với tới từ đúng phía
                anchor = new Vector2(r.center.x, Mathf.Min(r.yMax, r.center.y + 0.45f)),
                verbNow = (m.type == "note" || m.type == "cipher" || m.type == "rosewindow")
                    ? (System.Func<string>)(() => (openNote == mm || openCipher == mm || openWindow == mm) ? "Close" : verb) : null,
                available = () => Usable(mm),
                use = () => UseMech(mm),
            });
        }

        private void UseMech(Mech m)
        {
            if (dir == null || activeConsole != null) return;
            switch (m.type)
            {
                case "note":
                    if (openNote == m) { openNote = null; break; }
                    openNote = m; openCipher = null; openWindow = null;
                    if (!m.read)
                    {
                        m.read = true;
                        if (m.marker != null) m.marker.SetActive(false);
                        string nst = Str(m, "sets", "");
                        if (!string.IsNullOrEmpty(nst)) dir.Flags.Set(nst);
                    }
                    break;
                case "cipher":
                    openCipher = openCipher == m ? null : m;
                    if (openCipher != null) { openNote = null; openWindow = null; }
                    break;
                case "rosewindow":
                    openWindow = openWindow == m ? null : m;
                    if (openWindow != null) { openNote = null; openCipher = null; }
                    break;
                case "bell":
                    RingBell(m);
                    break;
                default:
                    if (!m.done) Activate(m);
                    break;
            }
        }

        // (gọi về TiledProps — nơi duy nhất đọc thuộc tính Tiled)
        private static string Str(Mech m, string name, string fallback) => m.props.GetString(name, fallback);
        private static int Int(Mech m, string name, int fallback) => m.props.GetInt(name, fallback);
        private static float Float(Mech m, string name, float fallback) => m.props.GetFloat(name, fallback);
    }
}
