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
    /// The "Requiem Gothic" UI built with UI Toolkit (the game's only HUD system).
    ///   - One UIDocument / PanelSettings created at runtime, 1920x1080 reference (the HUD size setting scales the reference).
    ///   - Images: Resources/UI/Requiem/*.png (PixelLab + hand-drawn, one palette of ash stone / dull gold / blood red), Point filtering.
    ///   - Fonts: Resources/UI/Requiem/Fonts/RequiemTitle (titles), RequiemBody (body text) — PixelLab pixel fonts.
    ///   - Layers: Hud (ignores the mouse), Fx (low-HP red vignette...), Menu (Pause/Settings/Rest/Death).
    /// Starts by itself in scenes with a PlayerController; the Title scene uses TitleScreen (which also creates its UIDocument through here).
    /// Also: shared build helpers (Img, Txt, Nine, KeyCap) and self-drawn controls (button, slider, checkbox, stepper, cooldown ring).
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

        /// <summary>Is the element (or a parent up to `stop`) CLICKABLE — so the click sound stays silent on empty background.
        /// Clickable = focusable or has the "sr-press" class (section tabs / shortcut rows). Sliders do not count (they tick themselves on value change).</summary>
        public static bool Pressable(VisualElement v, VisualElement stop)
        {
            for (; v != null && v != stop; v = v.parent)
            {
                if (v is SRSlider) return false;
                if (v.focusable || v.ClassListContains("sr-press")) return v.enabledInHierarchy;
            }
            return false;
        }
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
            // the old HUD (OnGUI) steps aside
            var go = new GameObject("RequiemUI");
            go.AddComponent<RequiemUI>();
            go.AddComponent<RequiemHUD>();
            go.AddComponent<RequiemMenus>();
            go.AddComponent<MechanismPanelView>();     // note panels / cipher locks / rose windows
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

        /// Create the UIDocument + PanelSettings (scale with screen, match height) + the Requiem stylesheet.
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
            else Debug.LogWarning("[RequiemUI] Missing Resources/UI/Requiem/Requiem.uss");
            root.AddToClassList("sr-root");
            root.pickingMode = PickingMode.Ignore;
            return doc;
        }

        // ------------------------------------------------------------------ UI build helpers
        public static Texture2D Tex(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (texCache.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(Res + name);
            if (t == null) t = Resources.Load<Texture2D>("UI/" + name);           // old HUD images (hud_pointer, hud_wp_*)
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            else Debug.LogWarning($"[RequiemUI] Missing image {Res}{name}.png");
            texCache[name] = t;
            return t;
        }

        /// Absolutely positioned image at (x, y), size (w, h).
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

        /// Decorative image in flex layout (not absolute), size decided by the USS class.
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

        // the default theme sets -unity-font-definition (Inter) on the root, so "-unity-font" in USS is ignored -> text shows a thin,
        // blurry default font. Assign the pixel font's FontDefinition directly on every label (higher priority than the theme).
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

        /// The pixel font only has basic ASCII: replace unusual characters (—, …, ’, ×, >) with ones the font has.
        public static string Clean(string s) => TextUtil.Clean(s);   // old name kept; logic lives in Core/TextUtil

        /// 9-slice frame from a background image (slices in image px).
        public static void Slice(VisualElement v, string tex, int l, int t, int r, int b)
        {
            var tx = Tex(tex);
            if (tx != null) v.style.backgroundImage = new StyleBackground(tx);
            v.style.unitySliceLeft = l; v.style.unitySliceTop = t; v.style.unitySliceRight = r; v.style.unitySliceBottom = b;
        }

        /// Key cap (keycap image + text) centred at (cx, top).
        public static VisualElement KeyCap(string label, float cx, float top, VisualElement parent)
        {
            var k = Img("keycap", cx - 16, top, 32, 30, parent);
            var l = Text(label, "sr-key", k);
            l.style.position = Position.Absolute; l.style.left = 0; l.style.right = 0; l.style.top = 0; l.style.bottom = 3;
            return k;
        }
    }

    // ====================================================================== self-drawn controls (independent of the default theme)

    /// "Gold-trimmed iron plate" button (9-slice button image). Mouse / Enter / gamepad all press it.
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

    /// Slider 0..1 (track + fill + knob). Mouse drag or left/right keys.
    public class SRSlider : VisualElement
    {
        private readonly VisualElement fill, knob;
        private readonly Label valueLabel;
        private float value;
        private readonly Action<float> changed;
        private readonly Func<float, string> fmt;
        private bool dragging;
        private int tickStep = -1;                 // tick every 5 % step
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
            int step = Mathf.RoundToInt(v * 20f);                                   // drag / left-right keys -> tick every 5 % step
            if (notify && step != tickStep) SoulRequiem.Core.Sfx.Play2D("ui_move", 0.8f);
            tickStep = step;
            if (notify) changed?.Invoke(v);
        }

        private void Layout()
        {
            fill.style.width = Length.Percent(value * 100f);
            knob.style.left = 8f + value * TrackW - 12f;
        }
    }

    /// Checkbox (images chk_off / chk_on).
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

    /// Value picker with two arrows ( &lt;  value  &gt; ).
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
                if (e.direction == NavigationMoveEvent.Direction.Left) { Step(-1); SoulRequiem.Core.Sfx.Play2D("ui_move"); e.StopPropagation(); }   // sound
                else if (e.direction == NavigationMoveEvent.Direction.Right) { Step(1); SoulRequiem.Core.Sfx.Play2D("ui_move"); e.StopPropagation(); }
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

    /// Cooldown ring: dark pie overlay (`Progress` share remaining), sweeping clockwise from 12 o'clock.
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
