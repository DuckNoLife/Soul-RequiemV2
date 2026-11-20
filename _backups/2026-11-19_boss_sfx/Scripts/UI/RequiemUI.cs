using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Giao diện "Requiem Gothic" dựng bằng UI Toolkit (HUD duy nhất của game).
    ///   - Một UIDocument / PanelSettings tạo lúc chạy, tham chiếu 1920x1080 (cỡ HUD trong Settings co giãn tham chiếu).
    ///   - Ảnh: Resources/UI/Requiem/*.png (PixelLab + vẽ tay, cùng bảng màu đá tro / vàng xỉn / đỏ máu), lọc Point.
    ///   - Phông: Resources/UI/Requiem/Fonts/RequiemTitle (tiêu đề), RequiemBody (chữ thường) — phông pixel PixelLab.
    ///   - Lớp: Hud (không bắt chuột), Fx (viền đỏ máu thấp...), Menu (Pause/Settings/Rest/Chết).
    /// Tự khởi động ở scene có PlayerController; scene Title dùng TitleScreen (cũng tạo UIDocument qua đây).
    /// </summary>
    public class RequiemUI : MonoBehaviour
    {
        public const string Res = "UI/Requiem/";
        public static readonly Color Bone = new Color(0.91f, 0.86f, 0.75f);
        public static readonly Color Gold = new Color(0.93f, 0.78f, 0.44f);
        public static readonly Color Ink = new Color(0.055f, 0.03f, 0.08f);
        public static readonly Color Blood = new Color(0.77f, 0.14f, 0.2f);

        public static RequiemUI Instance { get; private set; }
        public UIDocument Doc { get; private set; }
        public VisualElement Root { get; private set; }
        public VisualElement Hud { get; private set; }
        public VisualElement Fx { get; private set; }
        public VisualElement Menu { get; private set; }
        public PanelSettings Panel { get; private set; }

        private static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnScene(Scene s, LoadSceneMode m)
        {
            if (FindFirstObjectByType<PlayerController>() == null) return;
            if (FindFirstObjectByType<RequiemUI>() != null) return;
            // HUD cũ (OnGUI) nhường chỗ
            var go = new GameObject("RequiemUI");
            go.AddComponent<RequiemUI>();
            go.AddComponent<RequiemHUD>();
            go.AddComponent<RequiemMenus>();
            go.AddComponent<MechanismPanelView>();     // bảng ghi chú / khoá ký tự / cửa sổ hoa hồng
            if (s.name.StartsWith("Map")) { PlayerPrefs.SetString("last_scene", s.name); PlayerPrefs.Save(); }
        }

        private void Awake()
        {
            Instance = this;
            Doc = CreateDocument(gameObject, out var ps, 10);
            Panel = ps;
            Root = Doc.rootVisualElement;
            Hud = Layer("sr-hud", PickingMode.Ignore);
            Fx = Layer("sr-fx", PickingMode.Ignore);
            Menu = Layer("sr-menu", PickingMode.Position);
            Menu.style.display = DisplayStyle.None;
            GameSettings.Changed += ApplyScale;
            ApplyScale();
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyScale;
            if (Instance == this) Instance = null;
            GameInput.Blocked = false;
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        private VisualElement Layer(string cls, PickingMode pick)
        {
            var v = new VisualElement { pickingMode = pick };
            v.AddToClassList("sr-layer"); v.AddToClassList(cls);
            Root.Add(v);
            return v;
        }

        private void ApplyScale()
        {
            if (Panel == null) return;
            float k = Mathf.Clamp(GameSettings.HudScale, 0.6f, 1.5f);
            Panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(1920 / k), Mathf.RoundToInt(1080 / k));
        }

        /// Tạo UIDocument + PanelSettings (scale theo màn hình, khớp chiều cao) + stylesheet Requiem.
        public static UIDocument CreateDocument(GameObject host, out PanelSettings ps, int sortingOrder)
        {
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "Requiem Panel";
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 1f;
            ps.sortingOrder = sortingOrder;
            var theme = Resources.Load<ThemeStyleSheet>(Res + "RequiemTheme");
            if (theme != null) ps.themeStyleSheet = theme;
            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            var root = doc.rootVisualElement;
            var uss = Resources.Load<StyleSheet>(Res + "Requiem");
            if (uss != null) root.styleSheets.Add(uss);
            else Debug.LogWarning("[RequiemUI] Thiếu Resources/UI/Requiem/Requiem.uss");
            root.AddToClassList("sr-root");
            root.pickingMode = PickingMode.Ignore;
            return doc;
        }

        // ------------------------------------------------------------------ tiện ích dựng giao diện
        public static Texture2D Tex(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (texCache.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(Res + name);
            if (t == null) t = Resources.Load<Texture2D>("UI/" + name);           // ảnh HUD cũ (hud_pointer, hud_wp_*)
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            else Debug.LogWarning($"[RequiemUI] Thiếu ảnh {Res}{name}.png");
            texCache[name] = t;
            return t;
        }

        /// Ảnh tuyệt đối tại (x, y) cỡ (w, h).
        public static VisualElement Img(string tex, float x, float y, float w, float h, VisualElement parent = null)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = x; v.style.top = y; v.style.width = w; v.style.height = h;
            var t = Tex(tex);
            if (t != null) v.style.backgroundImage = new StyleBackground(t);
            parent?.Add(v);
            return v;
        }

        /// Ảnh trang trí theo bố cục flex (không tuyệt đối), kích thước do lớp USS quyết định.
        public static VisualElement Deco(string tex, string cls, VisualElement parent)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            var t = Tex(tex);
            if (t != null) v.style.backgroundImage = new StyleBackground(t);
            if (!string.IsNullOrEmpty(cls)) foreach (var c in cls.Split(' ')) v.AddToClassList(c);
            parent?.Add(v);
            return v;
        }

        public static Label Text(string text, string cls, VisualElement parent = null)
        {
            var l = new Label(Clean(text)) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("sr-text");
            if (!string.IsNullOrEmpty(cls)) foreach (var c in cls.Split(' ')) l.AddToClassList(c);
            bool title = cls != null && (cls.Contains("sr-title") || cls.Contains("sr-cd") || cls.Contains("sr-count"));
            ApplyFont(l, title);
            parent?.Add(l);
            return l;
        }

        // 28/10: theme mặc định đặt -unity-font-definition (Inter) cho gốc, nên "-unity-font" trong USS bị bỏ qua -> chữ hiện phông
        // mặc định mảnh và mờ. Gán thẳng FontDefinition từ phông pixel cho từng chữ (ưu tiên cao hơn theme).
        private static Font bodyFont, titleFont;
        public static void ApplyFont(TextElement t, bool title)
        {
            if (bodyFont == null) bodyFont = Resources.Load<Font>(Res + "Fonts/RequiemBody");
            if (titleFont == null) titleFont = Resources.Load<Font>(Res + "Fonts/RequiemTitle");
            var f = title ? titleFont : bodyFont;
            if (f != null) t.style.unityFontDefinition = FontDefinition.FromFont(f);
        }

        public static void Abs(VisualElement v, float x, float y, float w = -1, float h = -1)
        {
            v.style.position = Position.Absolute;
            v.style.left = x; v.style.top = y;
            if (w >= 0) v.style.width = w;
            if (h >= 0) v.style.height = h;
        }

        /// Phông pixel chỉ có ASCII cơ bản: đổi ký tự lạ (—, …, ’, ×, >) sang ký tự có trong phông.
        public static string Clean(string s) => TextUtil.Clean(s);   // giữ tên cũ; logic ở Core/TextUtil

        /// Khung 9 lát bằng ảnh nền (slice tính theo px ảnh).
        public static void Slice(VisualElement v, string tex, int l, int t, int r, int b)
        {
            var tx = Tex(tex);
            if (tx != null) v.style.backgroundImage = new StyleBackground(tx);
            v.style.unitySliceLeft = l; v.style.unitySliceTop = t; v.style.unitySliceRight = r; v.style.unitySliceBottom = b;
        }

        /// Nắp phím (ảnh keycap + chữ) có tâm ở (cx, top).
        public static VisualElement KeyCap(string label, float cx, float top, VisualElement parent)
        {
            var k = Img("keycap", cx - 16, top, 32, 30, parent);
            var l = Text(label, "sr-key", k);
            l.style.position = Position.Absolute; l.style.left = 0; l.style.right = 0; l.style.top = 0; l.style.bottom = 3;
            return k;
        }
    }

    // ====================================================================== điều khiển tự vẽ (không phụ thuộc theme mặc định)

    /// Nút "tấm sắt viền vàng" (ảnh button 9 lát). Chuột / Enter / tay cầm đều bấm được.
    public class SRButton : VisualElement
    {
        public readonly Label Label;
        private readonly Action onClick;

        public SRButton(string text, Action click, float width = 380, float height = 64)
        {
            onClick = click;
            AddToClassList("sr-btn");
            RequiemUI.Slice(this, "button", 44, 22, 44, 22);
            style.width = width; style.height = height;
            focusable = true; tabIndex = 0;
            Label = RequiemUI.Text(text, "sr-btn-label", this);
            this.AddManipulator(new Clickable(() => { if (enabledSelf) onClick?.Invoke(); }));
            RegisterCallback<NavigationSubmitEvent>(_ => { if (enabledSelf) onClick?.Invoke(); });
            RegisterCallback<PointerEnterEvent>(_ => { if (enabledSelf) Focus(); });
        }

        public SRButton SetText(string t) { Label.text = RequiemUI.Clean(t); return this; }
    }

    /// Thanh trượt 0..1 (rãnh + phần lấp + núm). Kéo chuột hoặc phím trái/phải.
    public class SRSlider : VisualElement
    {
        private readonly VisualElement fill, knob;
        private readonly Label valueLabel;
        private float value;
        private readonly Action<float> changed;
        private readonly Func<float, string> fmt;
        private bool dragging;
        public float Value { get => value; set => Set(value, false); }

        public SRSlider(float v, Action<float> onChanged, Func<float, string> format = null, float width = 300)
        {
            changed = onChanged; fmt = format ?? (x => Mathf.RoundToInt(x * 100) + "%");
            AddToClassList("sr-slider");
            style.width = width; style.height = 32;
            focusable = true; tabIndex = 0;
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("sr-slider-track");
            RequiemUI.Slice(track, "track", 8, 4, 8, 4);
            Add(track);
            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("sr-slider-fill");
            track.Add(fill);
            knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.AddToClassList("sr-slider-knob");
            var kt = RequiemUI.Tex("btn_round");
            if (kt != null) knob.style.backgroundImage = new StyleBackground(kt);
            Add(knob);
            valueLabel = RequiemUI.Text("", "sr-slider-value", this);
            RegisterCallback<PointerDownEvent>(e => { dragging = true; this.CapturePointer(e.pointerId); FromPointer(e.localPosition.x); });
            RegisterCallback<PointerMoveEvent>(e => { if (dragging) FromPointer(e.localPosition.x); });
            RegisterCallback<PointerUpEvent>(e => { dragging = false; this.ReleasePointer(e.pointerId); });
            RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction == NavigationMoveEvent.Direction.Left) { Set(value - 0.05f, true); e.StopPropagation(); }
                else if (e.direction == NavigationMoveEvent.Direction.Right) { Set(value + 0.05f, true); e.StopPropagation(); }
            });
            RegisterCallback<GeometryChangedEvent>(_ => Layout());
            Set(v, false);
        }

        private float TrackW => Mathf.Max(1f, resolvedStyle.width - 70f);

        private void FromPointer(float x) => Set((x - 8f) / TrackW, true);

        private void Set(float v, bool notify)
        {
            v = Mathf.Clamp01(v);
            value = v;
            valueLabel.text = fmt(v);
            Layout();
            if (notify) changed?.Invoke(v);
        }

        private void Layout()
        {
            fill.style.width = Length.Percent(value * 100f);
            knob.style.left = 8f + value * TrackW - 12f;
        }
    }

    /// Ô tích (ảnh chk_off / chk_on).
    public class SRToggle : VisualElement
    {
        private bool on;
        private readonly VisualElement box;
        private readonly Action<bool> changed;

        public SRToggle(bool v, Action<bool> onChanged)
        {
            changed = onChanged;
            AddToClassList("sr-toggle");
            focusable = true; tabIndex = 0;
            box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("sr-toggle-box");
            Add(box);
            on = v; Refresh();
            this.AddManipulator(new Clickable(() => { on = !on; Refresh(); changed?.Invoke(on); }));
            RegisterCallback<NavigationSubmitEvent>(_ => { on = !on; Refresh(); changed?.Invoke(on); });
        }

        private void Refresh()
        {
            var t = RequiemUI.Tex(on ? "chk_on" : "chk_off");
            if (t != null) box.style.backgroundImage = new StyleBackground(t);
            EnableInClassList("sr-on", on);
        }
    }

    /// Chọn giá trị bằng hai mũi tên ( &lt;  giá trị  &gt; ).
    public class SRStepper : VisualElement
    {
        private readonly Label label;
        private readonly string[] options;
        private int index;
        private readonly Action<int> changed;

        public SRStepper(string[] opts, int start, Action<int> onChanged, float width = 300)
        {
            options = opts; changed = onChanged; index = Mathf.Clamp(start, 0, Mathf.Max(0, opts.Length - 1));
            AddToClassList("sr-stepper");
            style.width = width;
            focusable = true; tabIndex = 0;
            var l = Arrow("ic_arrow_l", -1); Add(l);
            label = RequiemUI.Text(opts.Length > 0 ? opts[index] : "-", "sr-stepper-label", this);
            var r = Arrow("ic_arrow_r", 1); Add(r);
            RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction == NavigationMoveEvent.Direction.Left) { Step(-1); e.StopPropagation(); }
                else if (e.direction == NavigationMoveEvent.Direction.Right) { Step(1); e.StopPropagation(); }
            });
        }

        private VisualElement Arrow(string tex, int dir)
        {
            var a = new VisualElement();
            a.AddToClassList("sr-stepper-arrow");
            var t = RequiemUI.Tex(tex);
            if (t != null) a.style.backgroundImage = new StyleBackground(t);
            a.AddManipulator(new Clickable(() => Step(dir)));
            return a;
        }

        private void Step(int d)
        {
            if (options.Length == 0) return;
            index = (index + d + options.Length) % options.Length;
            label.text = RequiemUI.Clean(options[index]);
            changed?.Invoke(index);
        }
    }

    /// Vòng hồi chiêu: phủ tối hình quạt (còn lại `Progress` phần), quét theo chiều kim đồng hồ từ 12 giờ.
    public class CooldownRing : VisualElement
    {
        private float progress;
        public Color Shade = new Color(0.02f, 0.01f, 0.04f, 0.68f);

        public float Progress
        {
            get => progress;
            set { value = Mathf.Clamp01(value); if (Mathf.Abs(value - progress) > 0.002f) { progress = value; MarkDirtyRepaint(); } }
        }

        public CooldownRing()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext mgc)
        {
            if (progress <= 0.001f) return;
            var r = contentRect;
            var c = r.center;
            float rad = Mathf.Sqrt(r.width * r.width + r.height * r.height) * 0.5f + 1f;
            var p = mgc.painter2D;
            p.fillColor = Shade;
            p.BeginPath();
            p.MoveTo(c);
            if (progress >= 0.999f) p.Arc(c, rad, 0f, 360f);
            else p.Arc(c, rad, -90f, -90f + 360f * progress);
            p.ClosePath();
            p.Fill();
        }
    }
}
