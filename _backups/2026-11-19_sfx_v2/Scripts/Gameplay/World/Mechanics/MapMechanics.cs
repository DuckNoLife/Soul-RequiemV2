using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Chủ quản các cơ quan trên map (layer "mechanics" của Tiled, do Catalog/v2/maps/*.py sinh). Mỗi scene map có một.
    ///
    /// Việc của component này chỉ là:
    ///   1. đọc map: tạo một <see cref="Mechanism"/> cho mỗi object (loại theo Type, xem <see cref="MechanismRegistry"/>),
    ///      gom object theo tên, đèn "Light &lt;tên&gt;", bảng ảnh ẩn (glyphpal_*, picpal_*, pipepal_*) vào <see cref="MechanismContext"/>;
    ///   2. mỗi khung hình gọi Animate / Tick, chuyển đòn chém, cờ mới, phím T tới đúng cơ quan;
    ///   3. đóng bảng giữa màn hình khi Esc / đi xa.
    /// Hành vi từng loại nằm trong class riêng: Mechanics/Hazards, Mechanics/Effects, Mechanics/Puzzles.
    /// Bảng ghi chú / khoá / cửa sổ hoa hồng: dữ liệu ở <see cref="MechanismPanels"/>, giao diện vẽ ở UI/MechanismPanelView.
    /// </summary>
    public partial class MapMechanics : MonoBehaviour
    {
        public static MapMechanics Instance { get; private set; }
        /// <summary>Đang đứng ở bàn điều khiển (camera đã lia đi): PlayerController / PlayerCombat không nhận phím.</summary>
        public static bool ConsoleActive { get; internal set; }
        /// <summary>Bảng đọc / khoá đang mở (Esc đóng bảng thay vì mở menu Pause; chuột trái không chém).</summary>
        public static bool PanelOpen => MechanismPanels.BlocksInput;
        /// <summary>Bảng tự đóng khi người chơi đi xa vùng cơ quan hơn khoảng này.</summary>
        public const float CloseRange = 1.1f;

        [Tooltip("Nhịp gây sát thương khi đứng trong vùng phun / vùng rát (giây)")]
        public float tick = 0.5f;

        private readonly List<Mechanism> mechs = new List<Mechanism>();
        private MechanismContext ctx;

        private void Awake()
        {
            Instance = this;
            ConsoleActive = false;
            MechanismPanels.Reset();
        }

        private void OnEnable() => PlayerCombat.Struck += OnStruck;

        private void OnDisable()
        {
            PlayerCombat.Struck -= OnStruck;
            ConsoleActive = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            MechanismPanels.Reset();
        }

        private IEnumerator Start()
        {
            yield return null;                                  // đợi Director đọc map xong
            var dir = MapEncounterDirector.Instance;
            var pc = FindFirstObjectByType<PlayerController>();
            if (dir == null || dir.mapRoot == null || pc == null) { enabled = false; yield break; }
            ctx = new MechanismContext(this, dir, pc.transform, pc.GetComponent<Health>(), FindFirstObjectByType<CameraFollow>());

            ReadMap(dir.mapRoot);
            foreach (var m in mechs) m.Init();
            dir.Flags.Changed += _ => RefreshFlags();
            RefreshFlags();
            foreach (var m in mechs) RegisterInteraction(m);
            Debug.Log($"[MapMechanics] {mechs.Count} cơ quan, {ctx.PipeSprites.Count} sprite ống.");
        }

        // ------------------------------------------------------------------ đọc map
        /// <summary>Lớp hình của cơ quan (06/11): tile object nhìn thấy được trong Tiled, đọc như lớp "mechanics".</summary>
        public const string ArtLayer = "mechanics_art";

        private void ReadMap(Transform mapRoot)
        {
            var pieces = new List<MechanismArt>();                 // mảnh hình `art_for` — gắn vào cơ quan sau khi đọc hết
            foreach (var so in mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                string layer = so.transform.parent != null ? so.transform.parent.name : "";
                if (layer == "mechanics" || layer == ArtLayer)
                {
                    var art = MechanismArt.From(so);               // tile object -> khung hình (tắt ảnh ST2U dựng sẵn)
                    var props = so.GetComponent<SuperCustomProperties>();
                    if (art != null && props != null && props.GetString("art_for", "").Length > 0) { pieces.Add(art); continue; }
                    var col = so.GetComponent<Collider2D>();
                    var rect = col != null ? new Rect(col.bounds.min, col.bounds.size)
                             : art != null ? art.Box
                             : new Rect(so.transform.position, Vector2.one * 0.3f);
                    var m = MechanismRegistry.Create(so.m_Type, so.m_TiledName);
                    m.Bind(so.m_Type, so.m_TiledName, rect, props, ctx);
                    m.AddArt(art);
                    mechs.Add(m);
                    ctx.All.Add(m);
                    continue;
                }
                string n = so.m_TiledName;
                if (string.IsNullOrEmpty(n)) continue;
                // bảng ảnh ẩn trong map: lấy sprite rồi tắt object
                if (TakePalette(so, n, "glyphpal_", s => { if (int.TryParse(n.Substring(9), out int d)) ctx.Glyphs[d] = s; })) continue;   // ký tự Vael
                if (TakePalette(so, n, "picpal_", s => ctx.Pictures[n.Substring(7)] = s)) continue;                                      // tranh
                if (TakePalette(so, n, "pipepal_", s => ctx.PipeSprites[n.Substring(8)] = s)) continue;                                  // sprite ống
                if (!ctx.Named.TryGetValue(n, out var list)) ctx.Named[n] = list = new List<GameObject>();
                list.Add(so.gameObject);
            }
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.name.StartsWith("Light ")) ctx.Lights[l.name.Substring(6)] = l;
            foreach (var a in pieces)
            {
                string owner = a.Props.GetString("art_for", "");
                var m = mechs.Find(x => x.Name == owner);
                if (m != null) m.AddArt(a);
                else Debug.LogWarning("[MapMechanics] mảnh hình '" + a.Name + "': không có cơ quan tên '" + owner + "' (art_for)");
            }
        }

        private static bool TakePalette(SuperObject so, string name, string prefix, System.Action<Sprite> store)
        {
            if (!name.StartsWith(prefix)) return false;
            var sr = so.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) store(sr.sprite);
            so.gameObject.SetActive(false);
            return true;
        }

        private void RefreshFlags()
        {
            foreach (var m in mechs) m.OnFlagsChanged();
        }

        // ------------------------------------------------------------------ vòng lặp
        private void Update()
        {
            if (ctx == null) return;
            foreach (var m in mechs) m.Animate();
            if (ctx.ActiveConsole != null) { ctx.ActiveConsole.ControlUpdate(); return; }

            Vector2 p = ctx.Player.position;
            var open = MechanismPanels.Current;
            if (open != null)
            {
                var kb = Keyboard.current;
                if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (open.Owner != null && !open.Owner.KeepPanelOpen(p)))
                    MechanismPanels.Close();
            }
            foreach (var m in mechs)
                if (!m.Done) m.Tick(p, m.Rect.Contains(p));
        }

        // ------------------------------------------------------------------ đòn chém / phím T
        /// <summary>16/11 (§255): bán kính của đòn đang xét (chém ~0,45; Earthshatter 1,1) — vật cần đòn nặng (min_radius) đọc giá trị này.</summary>
        public static float StrikeRadius { get; private set; }

        private void OnStruck(Vector2 c, float r)
        {
            if (ctx == null || ctx.ActiveConsole != null) return;
            StrikeRadius = r;
            // 28/10: đòn chém chỉ còn tác dụng với van gỉ và vật cản gỗ (chuông / cần gạt / vòng số / bàn điều khiển dùng phím T)
            foreach (var m in mechs)
                if (!m.Done && m.ReactsToStrike && m.Overlaps(c, r)) m.OnStruck();
        }

        private void RegisterInteraction(Mechanism m)
        {
            if (m.DefaultVerb == null) return;
            string verb = m.Props.GetString("verb", m.DefaultVerb);
            var r = m.Rect;
            InteractionSystem.Add(new Interactable
            {
                id = m.Name, verb = verb, rect = r, clue = m.Clue,
                calmOnly = m.DefaultVerb == "Read" || m.DefaultVerb == "Examine",   // 04/11: ghi chú / bảng giải đố khoá khi giao chiến
                range = m.Props.GetFloat("range", 0.55f),                          // vd then cổng: chỉ với tới từ đúng phía
                anchor = new Vector2(r.center.x, Mathf.Min(r.yMax, r.center.y + 0.45f)),
                verbNow = () => m.PanelIsOpen ? "Close" : verb,
                available = () => m.Usable,
                use = () => { if (ctx.ActiveConsole == null) { SfxHooks.MechanismUsed(m); m.Use(); } },   // 17/11 (§294): âm
            });
        }

        // ------------------------------------------------------------------ API cho boss / quái
        /// <summary>Object trong map theo tên Tiled (vd morrow_pillar_0, isolde_coffin_1_open).</summary>
        public List<GameObject> Named(string name) => ctx != null ? ctx.NamedObjects(name) : new List<GameObject>();
        public void SetVisible(string names, bool on) => ctx?.Show(names, on);
        public Light2D LightNamed(string name) => ctx?.Light(name);

        /// <summary>Ichor Ram lao đâm vào tường: van gỉ trong bán kính r (đơn vị Unity) bị gõ vỡ lớp gỉ.
        /// (06/11 b: bỏ cột ram_pillar — người dùng: quá nhiều boss dùng cột; Ram giờ dùng van xả nước SluiceMechanism.)</summary>
        public bool RamImpact(Vector2 at, float r)
        {
            bool any = false;
            foreach (var m in mechs) if (m is ValveMechanism v && v.TryCrackRust(at, r)) any = true;
            if (any) Hints.Show("The ram's charge cracks the rust off a valve!", 2.5f);
            return any;
        }

        /// <summary>03/11: tâm các vũng Ichor của bể kính ĐÃ vỡ trong bán kính r quanh `near` (chỗ cóc con chui lên).</summary>
        public System.Collections.Generic.List<Vector2> SpillPoints(Vector2 near, float r)
        {
            var list = new System.Collections.Generic.List<Vector2>();
            foreach (var m in mechs)
                if (m is TankMechanism t && t.Done)
                {
                    var p = t.SpillPoint;
                    if ((p - near).sqrMagnitude <= r * r) list.Add(p);
                }
            return list;
        }

        /// <summary>Cóc Gorgemaw đập xuống: bể kính trong bán kính r vỡ. Trả về true nếu có bể vỡ (cóc bị choáng).</summary>
        public bool Shatter(Vector2 at, float r)
        {
            bool any = false;
            foreach (var m in mechs) if (m is TankMechanism t && t.TryShatter(at, r)) any = true;
            return any;
        }
    }
}
