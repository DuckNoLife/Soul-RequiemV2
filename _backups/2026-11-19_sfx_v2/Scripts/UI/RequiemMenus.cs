using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Các màn ngoài trận (UI Toolkit, cùng bộ ảnh Requiem):
    ///   - PAUSE (Esc): Resume / Skills (chỉ ở điểm nghỉ) / Settings / Quit to Title / Quit Game. Dừng thời gian, chặn điều khiển.
    ///   - SETTINGS: Audio / Display / Gameplay / Controls (lưu PlayerPrefs, áp ngay). Dùng chung cho Title (BuildSettings).
    ///   - REST / SKILLS (T ở điểm nghỉ): chọn ổ chiêu rồi chọn chiêu để gắn; tháo chiêu.
    ///   - CHẾT: lớp phủ "YOU HAVE FALLEN" trong lúc gục -> hồi sinh ở điểm nghỉ cuối.
    /// Esc khi đang trong menu con = quay lại.
    /// </summary>
    public class RequiemMenus : MonoBehaviour
    {
        public static bool Open => instance != null && instance.stack.Count > 0;
        private static RequiemMenus instance;

        private readonly List<VisualElement> stack = new List<VisualElement>();
        private VisualElement layer, dim, death;
        private Label deathTitle, deathSub;
        private float deathT, savedScale = 1f;
        private Health hp;
        private PlayerLoadout loadout;

        private void Awake() => instance = this;

        private void Start()
        {
            var ui = RequiemUI.Instance;
            if (ui == null) { enabled = false; return; }
            layer = ui.Menu;
            dim = new VisualElement();
            dim.AddToClassList("sr-fill"); dim.AddToClassList("sr-dim");
            layer.Add(dim);
            layer.RegisterCallback<NavigationMoveEvent>(_ => SoulRequiem.Core.Sfx.Play2D("ui_move"), TrickleDown.TrickleDown);   // 17/11 (§294): âm
            layer.RegisterCallback<NavigationSubmitEvent>(_ => SoulRequiem.Core.Sfx.Play2D("ui_confirm"), TrickleDown.TrickleDown);
            layer.RegisterCallback<ClickEvent>(_ => SoulRequiem.Core.Sfx.Play2D("ui_confirm"), TrickleDown.TrickleDown);
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) { hp = pc.GetComponent<Health>(); loadout = pc.GetComponent<PlayerLoadout>(); }
            BuildDeath(ui.Fx);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (stack.Count > 0) { Time.timeScale = savedScale; GameInput.Blocked = false; }
            GameInput.MenuOpen = false;
        }

        private void Update()
        {
            // Unity nạp lại code giữa lúc đang Play (domain reload) -> các tham chiếu giao diện (không serialize) thành null
            // và Start() không chạy lại. Bỏ qua thay vì ném NullReferenceException mỗi lần bấm Esc; dừng Play rồi chạy lại là hết.
            // Tránh hẳn: Edit > Preferences > General > Script Changes While Playing = Recompile After Finished Playing.
            if (layer == null) return;
            bool dead = hp != null && hp.IsDead;
            var dir = MapEncounterDirector.Instance;
            // 04/11: phím B (tay cầm LT) mở / đóng túi đồ (bỏ qua Blocked để đóng được khi túi đang mở)
            if (GameInput.PressedRaw(GameAction.Inventory) && !dead && !MapMechanics.ConsoleActive && !MapMechanics.PanelOpen
                && !CutsceneState.Active && !TutorialPrompt.Active && !DialogueBox.Active)
            {
                if (stack.Count == 0 && !GameInput.Blocked) { Push(BuildInventory()); return; }
                if (stack.Count > 0 && inventoryPanel != null && stack[stack.Count - 1] == inventoryPanel) { Pop(); return; }
            }
            // 18/11 (§329): phím L (tay cầm R3) mở / đóng bảng lắp Mantra ở BẤT CỨ ĐÂU (bỏ điều kiện phải đứng ở điểm nghỉ).
            // Mở bảng = dừng thời gian như túi đồ. Không mở khi đang diễn cảnh / hội thoại / đọc bảng; Mantra bị phong theo map -> báo chữ.
            if (GameInput.PressedRaw(GameAction.Mantras) && !dead && !MapMechanics.ConsoleActive && !MapMechanics.PanelOpen
                && !CutsceneState.Active && !TutorialPrompt.Active && !DialogueBox.Active)
            {
                if (stack.Count == 0 && !GameInput.Blocked) { OpenMantras(); return; }
                if (stack.Count > 0 && restPanel != null && stack[stack.Count - 1] == restPanel) { Pop(); return; }
            }
            if (stack.Count > 0 && inventoryPanel != null && stack[stack.Count - 1] == inventoryPanel) InvTick();   // 11/11 (§221)
            if (stack.Count > 0 && restPanel != null && stack[stack.Count - 1] == restPanel)                       // 11/11 (§228)
            {
                InvTick();
                var kb2 = UnityEngine.InputSystem.Keyboard.current; var gp2 = UnityEngine.InputSystem.Gamepad.current;
                if ((kb2 != null && (kb2.deleteKey.wasPressedThisFrame || kb2.backspaceKey.wasPressedThisFrame)) || (gp2 != null && gp2.buttonWest.wasPressedThisFrame)) restClear?.Invoke();
            }
            if (GameInput.Pressed(GameAction.Pause))
            {
                if (stack.Count > 0) Pop();
                else if (!dead && !MapMechanics.ConsoleActive && !MapMechanics.PanelOpen) Push(BuildPause());
            }
            else if (stack.Count == 0 && !dead && dir != null && dir.AtCheckpoint && InteractionSystem.Focus == null && GameInput.Pressed(GameAction.Interact))
            {
                // 02/11: nghỉ bên lửa = đầy máu, hết độc, nạp lại bình tối đa (kiểu Souls: chết chỉ mất đường đi, không mất tài nguyên)
                if (hp != null) { hp.SetCurrent(hp.Max); hp.CurePoison(); }
                var kit = hp != null ? hp.GetComponent<PlayerCombat>() : null;
                if (kit != null) kit.RefillPotions();
                dir.SaveProgress();                                   // 02/11: nghỉ = lưu game
                Hints.Show("You rest by the fire. Health and flasks restored - progress saved.", 3f);
                Push(BuildRest(true));
            }
            UpdateDeath(dead);
        }

        // ------------------------------------------------------------------ ngăn xếp menu
        private void Push(VisualElement panel)
        {
            if (stack.Count == 0)
            {
                savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                GameInput.Blocked = true;
                layer.style.display = DisplayStyle.Flex;
            }
            else stack[stack.Count - 1].style.display = DisplayStyle.None;
            stack.Add(panel);
            GameInput.MenuOpen = true;
            layer.Add(panel);
            FocusFirst(panel);
            SoulRequiem.Core.Sfx.Play2D(stack.Count <= 1 ? "ui_open" : "ui_confirm");   // 17/11 (§294): âm
        }

        private void Pop()
        {
            if (stack.Count == 0) return;
            SoulRequiem.Core.Sfx.Play2D("ui_back");   // 17/11 (§294): âm
            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            top.RemoveFromHierarchy();
            if (stack.Count > 0)
            {
                var prev = stack[stack.Count - 1];
                prev.style.display = DisplayStyle.Flex;
                FocusFirst(prev);
                return;
            }
            layer.style.display = DisplayStyle.None;
            Time.timeScale = savedScale;
            GameInput.Blocked = false;
            GameInput.MenuOpen = false;
        }

        private void CloseAll() { while (stack.Count > 0) Pop(); }

        private static void FocusFirst(VisualElement panel)
        {
            panel.schedule.Execute(() =>
            {
                var f = panel.Query<VisualElement>().Where(v => v.focusable && v.enabledInHierarchy && v.resolvedStyle.display != DisplayStyle.None).First();
                f?.Focus();
            });
        }

        // ------------------------------------------------------------------ khung chung
        public static VisualElement Panel(string title, string subtitle, float width)
        {
            var p = new VisualElement();
            p.AddToClassList("sr-panel");
            RequiemUI.Slice(p, "panel_stone", 16, 18, 16, 16);
            p.style.width = width;
            var t = RequiemUI.Text(title, "sr-title sr-panel-title", p);
            if (!string.IsNullOrEmpty(subtitle)) RequiemUI.Text(subtitle, "sr-panel-sub", p);
            RequiemUI.Deco("hint_line", "sr-rule", p);
            return p;
        }

        private static VisualElement Center(VisualElement panel)
        {
            var c = new VisualElement();
            c.AddToClassList("sr-fill"); c.AddToClassList("sr-center-box");
            c.Add(panel);
            return c;
        }

        /// <summary>18/11 (§329): mở bảng Mantra không cần điểm nghỉ (phím L / nút Mantras trong Pause). Mantra bị phong -> chỉ báo chữ.</summary>
        private bool OpenMantras()
        {
            var st = hp != null ? PlayerStatus.Of(hp) : null;
            if (st != null && st.MantrasBound) { Hints.Show(st.MantraSealText, 2.5f); return false; }
            Push(BuildRest(false));
            return true;
        }

        // ------------------------------------------------------------------ PAUSE
        private VisualElement BuildPause()
        {
            var p = Panel("PAUSED", RequiemUI.Clean(MapEncounterDirector.Instance != null ? MapEncounterDirector.Instance.CurrentZoneTitle : ""), 520);
            p.Add(new SRButton("Resume", Pop));
            p.Add(new SRButton($"Inventory  ({GameInput.Label(GameAction.Inventory)})", () => Push(BuildInventory())));   // 04/11: túi đồ
            p.Add(new SRButton($"Mantras  ({GameInput.Label(GameAction.Mantras)})", () => OpenMantras()));   // 18/11 (§329): mọi nơi
            p.Add(new SRButton("Settings", () => Push(Center(BuildSettings(Pop)))));
            if (Application.CanStreamedLevelBeLoaded("Title"))
                p.Add(new SRButton("Quit to Title", () => { CloseAll(); SceneManager.LoadScene("Title"); }));
            p.Add(new SRButton("Quit Game", Quit));
            RequiemUI.Text($"{GameInput.Label(GameAction.Pause)} to resume", "sr-small sr-gold sr-panel-foot", p);
            return Center(p);
        }

        // ------------------------------------------------------------------ 04/11: INVENTORY
        // 04/11 (người dùng): túi đồ kiểu ô chứa đồ — bấm (chạm) một ô -> bảng thông tin bên phải (tên, loại, mở được gì, mô tả,
        // nội tại / chỉ số cho vũ khí sau này, đọc lại tài liệu). Ô to 132 px, nút to, dùng tốt trên điện thoại / tay cầm.
        // ------------------------------------------------------------------ 11/11 (§221): TÚI ĐỒ — làm lại lần 2
        // Người dùng: không tab "All", không thanh cuộn kiểu Windows, bỏ nền đá tím và chữ thừa.
        //   • 4 mục riêng: Weapons / Keys / Tools / Notes (Q / E hoặc LB / RB để đổi; mục rỗng mờ đi nhưng giữ chỗ -> thanh mục không xê dịch).
        //   • Trái: DANH SÁCH (icon + tên + một dòng phụ) — đọc được tên mà không phải rê chuột. Phải: thẻ chi tiết.
        //   • Một bảng màu: nền mực gần đen, mặt thẻ tối hơn một bậc, đường kẻ đồng mảnh; VÀNG chỉ cho thứ đang chọn / tiêu đề / số liệu.
        //   • Chữ dài cuộn bằng bánh xe chuột / cần phải, KHÔNG hiện thanh cuộn — chỉ một dải mờ ở đáy báo "còn nữa".
        //   • Chân bảng = nhắc phím (keycap) bấm được: [Enter] Draw this blade · [B] Close.
        private VisualElement inventoryPanel;
        private string invSel = "", invTab = "Weapon";
        private Action<int> invCycle;
        private ScrollView invReader;
        // 11/11 (§222): 3 mục — chìa + dụng cụ giải đố gộp thành KEY ITEMS
        private static readonly string[] InvTabs = { "Weapon", "KeyItem", "Document" };
        private static string TabLabel(string t) => t == "KeyItem" ? "Key Items" : t == "Document" ? "Notes" : "Weapons";
        private static string KindLabel(string k) => k == "Weapon" ? "Katana" : k == "Document" ? "Note" : k;
        private static string EmptyText(string t) => t == "KeyItem" ? "Nothing that opens or works anything yet." : t == "Document" ? "Nothing written down yet." : "No blades.";

        private const float InvW = 1840f, InvH = 1000f, ListW = 460f;
        private static readonly Color InvBg = new Color(0.050f, 0.036f, 0.064f, 0.97f);
        private static readonly Color InvSurface = new Color(0.074f, 0.054f, 0.092f, 1f);
        private static readonly Color InvRowSel = new Color(0.135f, 0.098f, 0.066f, 1f);
        private static readonly Color InvLine = new Color(0.27f, 0.155f, 0.135f, 1f);    // đồng đỏ tối (khớp khung PixelLab §221)
        private static readonly Color InvFrame = new Color(0.47f, 0.286f, 0.224f, 1f);   // đồng đỏ
        private const string ArtFrame = "inv/inv_frame", ArtDivider = "inv/inv_divider", ArtSlot = "inv/inv_slot";
        private static string CatIcon(string tab) => "inv/cat_" + (tab == "Weapon" ? "weapon" : tab == "KeyItem" ? "key" : "note");
        /// <summary>Ảnh UI có thì dùng (9-slice), thiếu thì vẽ phẳng bằng màu — giao diện không vỡ khi chưa nhập art.</summary>
        private static bool Art(VisualElement v, string tex, int l, int t, int r, int b)
        {
            var tx = RequiemUI.Tex(tex);
            if (tx == null) return false;
            v.style.backgroundImage = new StyleBackground(tx);
            v.style.unitySliceLeft = l; v.style.unitySliceTop = t; v.style.unitySliceRight = r; v.style.unitySliceBottom = b;
            return true;
        }
        private static readonly Color InvMuted = new Color(0.62f, 0.58f, 0.51f);
        private static readonly Color InvFaint = new Color(0.38f, 0.35f, 0.31f);
        private static readonly Color InvBody = new Color(0.80f, 0.75f, 0.65f);
        private static readonly Color InvGood = new Color(0.56f, 0.86f, 0.50f), InvBad = new Color(0.93f, 0.46f, 0.40f);
        private static readonly Color Clear0 = new Color(0f, 0f, 0f, 0f);

        private sealed class InvEntry
        {
            public KeyItem key;
            public WeaponDefinition weapon;
            public string Id => weapon != null ? weapon.id : key.id;
            public string Kind => weapon != null ? "Weapon" : key.kind == "Document" ? "Document" : "KeyItem";
            public string Name => weapon != null ? weapon.displayName : key.name;
            public Texture2D Icon => weapon != null
                ? RequiemUI.Tex("weapon_" + weapon.id)
                : (string.IsNullOrEmpty(key.icon) ? null : RequiemUI.Tex(key.icon));
            public string Sub
            {
                get
                {
                    if (weapon != null)
                        return weapon.trait != null && weapon.trait.Any
                            ? "Innate - " + (string.IsNullOrEmpty(weapon.trait.title) ? weapon.trait.kind.ToString() : weapon.trait.title) : "Katana";
                    return key.kind == "Document" ? "" : (key.opens ?? "");
                }
            }
        }

        // ---- tiện ích dựng
        private static void Border(VisualElement v, float w, Color c)
        {
            v.style.borderLeftWidth = v.style.borderRightWidth = v.style.borderTopWidth = v.style.borderBottomWidth = w;
            v.style.borderLeftColor = v.style.borderRightColor = v.style.borderTopColor = v.style.borderBottomColor = c;
        }
        private static void Pad(VisualElement v, float h, float vert)
        {
            v.style.paddingLeft = v.style.paddingRight = h;
            v.style.paddingTop = v.style.paddingBottom = vert;
        }
        private static Label Txt(string s, string cls, VisualElement parent, Color col, float size = 0f)
        {
            var l = RequiemUI.Text(s, cls, parent);
            l.style.color = col;
            if (size > 0f) l.style.fontSize = size;
            return l;
        }
        private static void NoWrap(Label l)
        {
            l.style.whiteSpace = WhiteSpace.NoWrap; l.style.overflow = Overflow.Hidden; l.style.textOverflow = TextOverflow.Ellipsis;
        }
        private static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row;
            parent.Add(r);
            return r;
        }
        private static void Hairline(VisualElement parent, float top, float bottom)
        {
            var h = new VisualElement(); h.style.height = 1; h.style.flexShrink = 0;
            h.style.backgroundColor = InvLine; h.style.marginTop = top; h.style.marginBottom = bottom;
            parent.Add(h);
        }
        private static Label Header(string text, VisualElement parent, float top = 0f)
        {
            var h = Txt(text, "sr-tiny", parent, RequiemUI.Gold);
            h.style.marginTop = top; h.style.marginBottom = 8; h.style.letterSpacing = 2;
            return h;
        }
        private static VisualElement Chip(string text, Color c, VisualElement parent)
        {
            var chip = new VisualElement();
            chip.style.flexShrink = 0; chip.style.alignSelf = Align.FlexStart;
            Pad(chip, 12, 4); Border(chip, 1, c);
            Txt(text, "sr-tiny", chip, c, 16).style.letterSpacing = 2;
            parent.Add(chip);
            return chip;
        }
        private static VisualElement KeyCap(string key, VisualElement parent)
        {
            var k = new VisualElement { pickingMode = PickingMode.Ignore };
            k.style.flexShrink = 0; k.style.minWidth = 30; k.style.alignItems = Align.Center;
            Pad(k, 8, 1); Border(k, 2, InvMuted); k.style.borderBottomWidth = 3;
            k.style.backgroundColor = new Color(0.10f, 0.08f, 0.11f);
            Txt(key, "sr-tiny", k, RequiemUI.Bone, 16).pickingMode = PickingMode.Ignore;
            parent.Add(k);
            return k;
        }
        private static void Prompt(string key, string label, VisualElement parent, Action click)
        {
            var p = Row(parent); p.style.alignItems = Align.Center; p.style.marginLeft = 32;
            KeyCap(key, p);
            var l = Txt(label, "sr-tiny", p, InvMuted); l.style.marginLeft = 10;
            if (click == null) return;
            p.AddManipulator(new Clickable(click));
            p.RegisterCallback<PointerEnterEvent>(_ => l.style.color = RequiemUI.Bone);
            p.RegisterCallback<PointerLeaveEvent>(_ => l.style.color = InvMuted);
        }
        private static VisualElement ActionButton(string label, string key, Action click)
        {
            var idle = new Color(0.15f, 0.11f, 0.062f, 1f); var hot = new Color(0.22f, 0.16f, 0.09f, 1f);
            var b = new VisualElement { focusable = true, tabIndex = 0 };
            b.style.flexDirection = FlexDirection.Row; b.style.alignItems = Align.Center; b.style.alignSelf = Align.FlexStart;
            Pad(b, 16, 9); Border(b, 2, InvFrame); b.style.backgroundColor = idle;
            KeyCap(key, b);
            var l = Txt(label.ToUpperInvariant(), "sr-small", b, RequiemUI.Gold); l.style.marginLeft = 12; l.style.letterSpacing = 2;
            b.AddManipulator(new Clickable(click));
            b.RegisterCallback<NavigationSubmitEvent>(_ => click());
            b.RegisterCallback<PointerEnterEvent>(_ => { Border(b, 2, RequiemUI.Gold); b.style.backgroundColor = hot; });
            b.RegisterCallback<PointerLeaveEvent>(_ => { Border(b, 2, InvFrame); b.style.backgroundColor = idle; });
            return b;
        }

        // ---- khung chữ cuộn KHÔNG có thanh cuộn: bánh xe chuột / cần phải; dải mờ ở đáy khi còn chữ bên dưới
        private static Texture2D fadeTex;
        private static Texture2D FadeTex()
        {
            if (fadeTex != null) return fadeTex;
            fadeTex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 32; y++) fadeTex.SetPixel(0, y, new Color(InvSurface.r, InvSurface.g, InvSurface.b, 1f - y / 31f));
            fadeTex.Apply();
            return fadeTex;
        }
        private ScrollView Reader(VisualElement parent)
        {
            var wrap = new VisualElement(); wrap.style.flexGrow = 1; wrap.style.flexShrink = 1;
            parent.Add(wrap);
            var sc = new ScrollView(ScrollViewMode.Vertical);
            sc.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sc.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            sc.mouseWheelScrollSize = 60f;
            sc.style.flexGrow = 1;
            wrap.Add(sc);
            var fade = new VisualElement { pickingMode = PickingMode.Ignore };
            fade.style.position = Position.Absolute; fade.style.left = 0; fade.style.right = 0; fade.style.bottom = 0;
            fade.style.height = 48; fade.style.backgroundImage = new StyleBackground(FadeTex());
            fade.style.display = DisplayStyle.None;
            wrap.Add(fade);
            Action upd = () =>
            {
                float over = sc.contentContainer.layout.height - sc.contentViewport.layout.height;
                fade.style.display = over > 4f && sc.scrollOffset.y < over - 4f ? DisplayStyle.Flex : DisplayStyle.None;
            };
            sc.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => upd());
            sc.RegisterCallback<GeometryChangedEvent>(_ => upd());
            sc.verticalScroller.valueChanged += _ => upd();
            invReader = sc;
            return sc;
        }

        /// <summary>Q / E (LB / RB) đổi mục, cần phải cuộn chữ — gọi mỗi khung khi túi đồ đang mở.</summary>
        private void InvTick()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            int d = 0;
            if (kb != null) { if (kb.qKey.wasPressedThisFrame) d = -1; else if (kb.eKey.wasPressedThisFrame) d = 1; }
            if (gp != null) { if (gp.leftShoulder.wasPressedThisFrame) d = -1; else if (gp.rightShoulder.wasPressedThisFrame) d = 1; }
            if (d != 0) invCycle?.Invoke(d);
            if (invReader != null && gp != null)
            {
                float y = gp.rightStick.ReadValue().y;
                if (Mathf.Abs(y) > 0.2f) invReader.scrollOffset += new Vector2(0f, -y * 900f * Time.unscaledDeltaTime);
            }
        }

        private static VisualElement Tab(string label, string icon, int n, bool on, Action click)
        {
            var t = new VisualElement();
            t.style.flexDirection = FlexDirection.Row; t.style.alignItems = Align.Center;
            Pad(t, 16, 6); t.style.marginLeft = t.style.marginRight = 2;
            t.style.borderBottomWidth = 3; t.style.borderBottomColor = on ? RequiemUI.Gold : Clear0;
            Color idle = n > 0 ? InvMuted : InvFaint;
            var itx = RequiemUI.Tex(icon);
            if (itx != null)
            {
                var ic = new VisualElement { pickingMode = PickingMode.Ignore };
                ic.style.width = 40; ic.style.height = 40; ic.style.marginRight = 10; ic.style.flexShrink = 0;
                ic.style.backgroundImage = new StyleBackground(itx);
                ic.style.opacity = on ? 1f : (n > 0 ? 0.6f : 0.3f);
                t.Add(ic);
            }
            var l = Txt(label.ToUpperInvariant(), "sr-small", t, on ? RequiemUI.Gold : idle); l.style.letterSpacing = 2;
            if (n > 0) { var c = Txt(n.ToString(), "sr-tiny", t, on ? RequiemUI.Bone : InvMuted, 17); c.style.marginLeft = 8; }
            t.AddManipulator(new Clickable(click));
            t.RegisterCallback<PointerEnterEvent>(_ => { if (!on) l.style.color = RequiemUI.Bone; });
            t.RegisterCallback<PointerLeaveEvent>(_ => { if (!on) l.style.color = idle; });
            return t;
        }

        private static VisualElement ListRow(InvEntry it, bool equipped, bool used)
        {
            var r = new VisualElement { focusable = true, tabIndex = 0 };
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
            r.style.height = 84; r.style.marginBottom = 4; r.style.paddingLeft = 12; r.style.paddingRight = 14;
            r.style.borderLeftWidth = 4; r.style.borderLeftColor = Clear0;
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.width = 72; box.style.height = 72; box.style.flexShrink = 0;
            box.style.alignItems = Align.Center; box.style.justifyContent = Justify.Center;
            if (!Art(box, ArtSlot, 16, 16, 16, 16)) { box.style.backgroundColor = new Color(0.028f, 0.02f, 0.036f, 1f); Border(box, 1, InvLine); }
            var ic = new VisualElement { pickingMode = PickingMode.Ignore };
            ic.style.width = 56; ic.style.height = 56;
            var tex = it.Icon;
            if (tex != null) ic.style.backgroundImage = new StyleBackground(tex);
            if (used) ic.style.opacity = 0.45f;
            box.Add(ic); r.Add(box);
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.marginLeft = 16; col.style.overflow = Overflow.Hidden;
            r.Add(col);
            var nm = Txt(it.Name, "sr-small", col, used ? InvMuted : RequiemUI.Bone); nm.pickingMode = PickingMode.Ignore; NoWrap(nm);
            string sub = used ? "Used" : it.Sub;
            if (!string.IsNullOrEmpty(sub))
            {
                var sl = Txt(sub, "sr-tiny", col, InvMuted, 17); sl.pickingMode = PickingMode.Ignore; NoWrap(sl); sl.style.marginTop = 2;
            }
            if (equipped)
            {
                var e = Txt("EQUIPPED", "sr-tiny", r, RequiemUI.Gold, 15);
                e.style.letterSpacing = 2; e.style.marginLeft = 10; e.style.flexShrink = 0; e.pickingMode = PickingMode.Ignore;
            }
            return r;
        }
        private static void StyleRow(VisualElement r, bool sel)
        {
            r.style.backgroundColor = sel ? InvRowSel : Clear0;
            r.style.borderLeftColor = sel ? RequiemUI.Gold : Clear0;
        }

        private VisualElement BuildInventory()
        {
            var dir = MapEncounterDirector.Instance;
            var flags = dir != null ? dir.Flags : null;
            if (Array.IndexOf(InvTabs, invTab) < 0) invTab = "Weapon";

            var p = new VisualElement();
            p.style.width = InvW; p.style.maxWidth = Length.Percent(96);
            p.style.height = InvH; p.style.maxHeight = Length.Percent(96);
            if (!Art(p, ArtFrame, 44, 44, 44, 44)) { p.style.backgroundColor = InvBg; Border(p, 2, InvFrame); }
            Pad(p, 58, 46);

            // đầu bảng: tiêu đề trái | mục phải (Q ‹ … › E)
            var head = Row(p); head.style.alignItems = Align.Center; head.style.flexShrink = 0;
            var title = Txt("INVENTORY", "sr-title", head, RequiemUI.Gold, 40); title.style.flexGrow = 1; title.style.letterSpacing = 3;
            var tabsWrap = Row(head); tabsWrap.style.alignItems = Align.Center;
            KeyCap("Q", tabsWrap).style.marginRight = 12;
            var tabs = Row(tabsWrap);
            KeyCap("E", tabsWrap).style.marginLeft = 12;
            var rule = new VisualElement(); rule.style.flexShrink = 0; rule.style.marginTop = 12;
            if (Art(rule, ArtDivider, 44, 0, 44, 0)) rule.style.height = 36;
            else { rule.style.height = 1; rule.style.backgroundColor = InvLine; }
            p.Add(rule);

            var body = new VisualElement(); body.style.flexGrow = 1; body.style.flexShrink = 1; body.style.marginTop = 22;
            p.Add(body);
            VisualElement card = null;

            var foot = Row(p); foot.style.flexShrink = 0; foot.style.marginTop = 18;
            foot.style.justifyContent = Justify.FlexEnd; foot.style.alignItems = Align.Center; foot.style.height = 40;

            var shown = new List<InvEntry>();
            var items = new List<KeyValuePair<InvEntry, Action<bool>>>();      // món + cách tô "đang chọn"
            Action rebuild = null;
            Action<string> select = null;
            Action<WeaponDefinition> equip = w =>
            {
                var kit = hp != null ? hp.GetComponent<PlayerCombat>() : null;
                if (kit == null || w == null || kit.Weapon == w) return;
                kit.Equip(w);
                // 16/11 (§269): bỏ thông báo thừa — hình / hoạt ảnh / bảng tên boss đã thể hiện (bảng kiếm đã đổi)
                rebuild();
            };

            select = id =>
            {
                invSel = id;
                foreach (var kv in items) kv.Value(kv.Key.Id == id);
                var cur = shown.FirstOrDefault(k => k.Id == id);
                card.Clear(); card.style.flexDirection = FlexDirection.Column; invReader = null;
                foot.Clear();
                bool canEquip = cur != null && cur.weapon != null && EquippedWeapon() != cur.weapon;
                if (canEquip) { var cw = cur.weapon; Prompt("Enter", "Draw this blade", foot, () => equip(cw)); }
                Prompt(GameInput.Label(GameAction.Inventory), "Close", foot, Pop);
                if (cur == null)
                {
                    var e = Txt(EmptyText(invTab), "sr-small", card, InvFaint);
                    e.style.alignSelf = Align.Center; e.style.marginTop = 160;
                    return;
                }
                if (cur.weapon != null) WeaponDetail(card, cur.weapon, equip);
                else if (cur.Kind == "Document") NoteCard(card, cur.key);
                else KeyCard(card, cur.key, flags);
            };

            rebuild = () =>
            {
                var owned = new List<InvEntry>();
                foreach (var w in WeaponRing.Owned(flags)) owned.Add(new InvEntry { weapon = w });
                if (flags != null) foreach (var k in KeyRing.Owned(flags)) owned.Add(new InvEntry { key = k });

                tabs.Clear();
                foreach (var t in InvTabs)
                {
                    string tab = t;
                    tabs.Add(Tab(TabLabel(tab), CatIcon(tab), owned.Count(k => k.Kind == tab), invTab == tab, () => { invTab = tab; rebuild(); }));
                }
                shown.Clear();
                shown.AddRange(owned.Where(k => k.Kind == invTab));
                if (shown.Count > 0 && shown.All(k => k.Id != invSel)) invSel = shown[0].Id;

                body.Clear(); items.Clear();
                bool rack = invTab == "Weapon";
                body.style.flexDirection = rack ? FlexDirection.Column : FlexDirection.Row;
                // 11/11 (§223): giá kiếm = trang 3 ô. Nhiều hơn 3 thanh -> mũi tên + chấm trang; chọn sang thanh ở trang khác thì tự lật.
                const float Gap = 24f; const int PerPage = 3;
                VisualElement list;
                var cards = new List<VisualElement>();
                int page = 0, pages = Mathf.Max(1, Mathf.CeilToInt(shown.Count / (float)PerPage));
                float cardW = 0f;
                Action<int> setPage = null;
                if (rack)
                {
                    var rackRoot = new VisualElement(); rackRoot.style.flexShrink = 0;
                    body.Add(rackRoot);
                    var line = Row(rackRoot); line.style.height = 276; line.style.flexShrink = 0;
                    bool paged = shown.Count > PerPage;
                    VisualElement chevL = null, chevR = null, dots = null;
                    if (paged) chevL = Chevron(line, true, () => setPage(page - 1));
                    var view = new VisualElement(); view.style.flexGrow = 1; view.style.overflow = Overflow.Hidden;
                    line.Add(view);
                    if (paged) chevR = Chevron(line, false, () => setPage(page + 1));
                    var strip = new VisualElement();
                    strip.style.position = Position.Absolute; strip.style.top = 0; strip.style.bottom = 0; strip.style.left = 0;
                    strip.style.flexDirection = FlexDirection.Row;
                    strip.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("left") };
                    strip.style.transitionDuration = new List<TimeValue> { new TimeValue(0.22f, TimeUnit.Second) };
                    strip.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                    view.Add(strip);
                    if (paged) { dots = Row(rackRoot); dots.style.justifyContent = Justify.Center; dots.style.marginTop = 12; dots.style.flexShrink = 0; }
                    setPage = pg =>
                    {
                        page = Mathf.Clamp(pg, 0, pages - 1);
                        if (chevL != null) chevL.style.opacity = page > 0 ? 1f : 0.2f;
                        if (chevR != null) chevR.style.opacity = page < pages - 1 ? 1f : 0.2f;
                        if (dots != null)
                        {
                            dots.Clear();
                            for (int k = 0; k < pages; k++)
                            {
                                var dt = new VisualElement();
                                dt.style.width = k == page ? 26 : 10; dt.style.height = 6; dt.style.marginLeft = dt.style.marginRight = 4;
                                dt.style.backgroundColor = k == page ? RequiemUI.Gold : InvLine;
                                dots.Add(dt);
                            }
                        }
                        float vw = view.layout.width;
                        if (float.IsNaN(vw) || vw <= 0f || cardW <= 0f) return;
                        int n = shown.Count;
                        float total = n * cardW + Mathf.Max(0, n - 1) * Gap;
                        // trang cuối luôn đủ 3 ô (lùi lại cho khỏi trống); 1-2 thanh thì căn giữa
                        int first = Mathf.Clamp(page * PerPage, 0, Mathf.Max(0, n - PerPage));
                        strip.style.left = n <= PerPage ? (vw - total) / 2f : -first * (cardW + Gap);
                    };
                    view.RegisterCallback<GeometryChangedEvent>(e =>
                    {
                        cardW = Mathf.Floor((e.newRect.width - (PerPage - 1) * Gap) / PerPage);
                        foreach (var c in cards) c.style.width = cardW;
                        setPage(page);
                    });
                    view.RegisterCallback<WheelEvent>(e =>
                    {
                        if (!paged) return;
                        setPage(page + (e.delta.y > 0f ? 1 : -1));
                        e.StopPropagation();
                    });
                    list = strip;
                }
                else
                {
                    list = new VisualElement(); list.style.flexShrink = 0; list.style.width = ListW;
                    body.Add(list);
                }
                card = new VisualElement();
                card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = 0;
                if (rack) card.style.marginTop = 20; else card.style.marginLeft = 28;
                card.style.backgroundColor = InvSurface; Border(card, 1, InvLine); card.style.overflow = Overflow.Hidden;
                body.Add(card);

                var eqW = EquippedWeapon();
                VisualElement focusMe = null;
                int selIndex = 0;
                for (int i = 0; i < shown.Count; i++)
                {
                    var it = shown[i];
                    VisualElement el; Action<bool> sty;
                    if (rack)
                    {
                        el = RackCard(it.weapon, it.weapon == eqW, out sty);
                        el.style.flexGrow = 0; el.style.flexShrink = 0; el.style.flexBasis = StyleKeyword.Auto;
                        if (cardW > 0f) el.style.width = cardW;
                        if (i < shown.Count - 1) el.style.marginRight = Gap;
                        cards.Add(el);
                        int pi = i / PerPage;
                        el.RegisterCallback<FocusInEvent>(_ => { if (pi != page) setPage(pi); });   // phím / tay cầm sang trang kế
                    }
                    else
                    {
                        bool used = it.key != null && KeyRing.Used(flags, it.key.id);
                        el = ListRow(it, false, used);
                        var er = el; sty = on => StyleRow(er, on);
                    }
                    list.Add(el);
                    items.Add(new KeyValuePair<InvEntry, Action<bool>>(it, sty));
                    string id = it.Id; var entry = it; var rv = el;
                    if (id == invSel) { focusMe = el; selIndex = i; }
                    el.RegisterCallback<PointerEnterEvent>(_ => rv.Focus());                  // rê chuột = xem
                    el.RegisterCallback<FocusInEvent>(_ => { if (invSel != id) select(id); });
                    el.AddManipulator(new Clickable(() => { rv.Focus(); if (invSel != id) select(id); }));
                    el.RegisterCallback<NavigationSubmitEvent>(_ => { if (entry.weapon != null) equip(entry.weapon); });
                }
                if (setPage != null) setPage(selIndex / PerPage);                             // mở túi: trang có thanh đang chọn
                select(shown.Count > 0 ? invSel : "");
                if (focusMe != null) { var f = focusMe; f.schedule.Execute(() => f.Focus()); }
            };
            invCycle = d =>
            {
                int k = Array.IndexOf(InvTabs, invTab);
                invTab = InvTabs[(k + d + InvTabs.Length) % InvTabs.Length];
                rebuild();
            };
            rebuild();

            inventoryPanel = Center(p);
            return inventoryPanel;
        }

        private WeaponDefinition EquippedWeapon()
        {
            var kit = hp != null ? hp.GetComponent<PlayerCombat>() : null;
            return kit != null ? kit.Weapon : null;
        }

        // ------------------------------------------------------------------ 11/11 (§222): GIÁ KIẾM + bảng thông số
        /// <summary>Tô vàng các con số trong luật nội tại (+15%, 1.2s ...). Lọc chữ TRƯỚC rồi mới gắn thẻ màu (Clean xoá dấu &lt; &gt; #).</summary>
        private static string Rich(string s)
        {
            s = TextUtil.Clean(s ?? "");
            return System.Text.RegularExpressions.Regex.Replace(s, @"(\+?\d+(?:\.\d+)?%|\d+(?:\.\d+)?s\b)", "<color=#EDC770>$1</color>");
        }

        private static string TraitTitle(WeaponDefinition w) =>
            w.trait == null || !w.trait.Any ? "" : string.IsNullOrEmpty(w.trait.title) ? w.trait.kind.ToString() : w.trait.title;

        /// <summary>11/11 (§223): mũi tên lật trang giá kiếm (vẽ bằng 2 cạnh viền của ô vuông xoay 45°, không dùng ký tự).</summary>
        private static VisualElement Chevron(VisualElement parent, bool left, Action click)
        {
            var b = new VisualElement();
            b.style.width = 44; b.style.flexShrink = 0; b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            if (left) b.style.marginRight = 12; else b.style.marginLeft = 12;
            var a = new VisualElement { pickingMode = PickingMode.Ignore };
            a.style.width = 16; a.style.height = 16;
            if (left) { a.style.borderLeftWidth = 3; a.style.borderBottomWidth = 3; }
            else { a.style.borderRightWidth = 3; a.style.borderTopWidth = 3; }
            a.style.borderLeftColor = a.style.borderBottomColor = a.style.borderRightColor = a.style.borderTopColor = RequiemUI.Gold;
            a.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            b.Add(a);
            b.AddManipulator(new Clickable(click));
            b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = InvRowSel);
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = Clear0);
            parent.Add(b);
            return b;
        }

        /// <summary>Một ô trên giá kiếm: tranh trưng bày (kiếm + bao) + tên + nội tại. Chọn = viền vàng, ô khác tranh mờ đi.</summary>
        private static VisualElement RackCard(WeaponDefinition w, bool equipped, out Action<bool> style)
        {
            var c = new VisualElement { focusable = true, tabIndex = 0 };
            c.style.flexGrow = 1; c.style.flexShrink = 1; c.style.flexBasis = 0; c.style.height = 276;
            c.style.alignItems = Align.Center; c.style.overflow = Overflow.Hidden;
            var tint = new Color(w.trailColor.r, w.trailColor.g, w.trailColor.b, 1f);
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.style.alignSelf = Align.Stretch; art.style.height = 200; art.style.flexShrink = 0;
            art.style.alignItems = Align.Center; art.style.justifyContent = Justify.Center;
            art.style.backgroundColor = Color.Lerp(new Color(0.025f, 0.018f, 0.032f, 1f), tint, 0.10f);
            art.style.borderBottomWidth = 1; art.style.borderBottomColor = InvLine;
            var pic = new VisualElement { pickingMode = PickingMode.Ignore };
            pic.style.width = 512; pic.style.height = 192;                        // 256x96 x2
            var tex = w.showcase != null ? w.showcase.texture : null;
            if (tex != null) { tex.filterMode = FilterMode.Point; pic.style.backgroundImage = new StyleBackground(tex); }
            art.Add(pic);
            c.Add(art);
            // màn hẹp (ô < 540): tranh x1,5 cho khỏi tràn
            c.RegisterCallback<GeometryChangedEvent>(e =>
            {
                bool big = e.newRect.width >= 520f;
                pic.style.width = big ? 512 : 384; pic.style.height = big ? 192 : 144;
            });
            var nm = Txt(w.displayName, "sr-small", c, RequiemUI.Bone, 24); nm.style.marginTop = 12;
            string tt = TraitTitle(w);
            var sub = Txt(tt.Length > 0 ? "Innate - " + tt : "Katana", "sr-tiny", c, InvMuted, 17); sub.style.marginTop = 2;
            if (equipped)
            {
                var chip = Chip("EQUIPPED", RequiemUI.Gold, c);
                chip.style.position = Position.Absolute; chip.style.top = 10; chip.style.right = 10;
                chip.style.backgroundColor = new Color(0.05f, 0.035f, 0.06f, 0.9f);
            }
            style = sel =>
            {
                Border(c, 2, sel ? RequiemUI.Gold : InvLine);
                c.style.backgroundColor = sel ? InvRowSel : InvSurface;
                pic.style.opacity = sel ? 1f : 0.7f;
                nm.style.color = sel ? RequiemUI.Gold : RequiemUI.Bone;
            };
            style(false);
            return c;
        }

        /// <summary>Hàng chỉ số: nhãn | 10 nấc (mốc CỐ ĐỊNH) | số | chênh lệch với thanh đang đeo.</summary>
        /// <summary>16/11 (§259): hiệu ứng đặc biệt của thanh kiếm (nội tại) thành các dòng "TÊN — con số" cho bảng SPECIAL.
        /// Thêm loại sát thương mới (đốt / chảy máu…) thì thêm nhánh ở đây.</summary>
        private static List<KeyValuePair<string, string>> SpecialEffects(WeaponDefinition w)
        {
            var o = new List<KeyValuePair<string, string>>();
            var t = w != null ? w.trait : null;
            if (t == null || !t.Any) return o;
            float dps = WeaponRing.Dps(w);
            string P(float f) => Mathf.RoundToInt(f * 100f) + "%";
            void Add(string k, string v) => o.Add(new KeyValuePair<string, string>(k, v));
            switch (t.kind)
            {
                case WeaponTraitKind.Counterstroke: Add("FIRST CUT AFTER A ROLL", "+" + P(t.bonus)); break;
                case WeaponTraitKind.Vigil: Add("DPS WHILE UNTOUCHED", (dps * (1f + t.bonus)).ToString("0")); break;
                case WeaponTraitKind.BloodOath:
                    Add("LIFESTEAL", P(t.leech) + " of damage");
                    Add("DPS BELOW " + P(t.threshold) + " HEALTH", (dps * (1f + t.bonus)).ToString("0")); break;
                case WeaponTraitKind.Iaijutsu: Add("DRAW CUT", "+" + P(t.bonus) + " to +" + P(Mathf.Max(t.bonus, t.bonusMax)) + ", stun " + t.threshold.ToString("0.#") + "s"); break;   // §273: tăng dần theo thời gian giữ
                case WeaponTraitKind.LastStand: Add("DAMAGE TAKEN BELOW " + P(t.threshold), "-" + P(t.bonus)); break;
                case WeaponTraitKind.Hunger:
                    Add("HEAL ON KILL", P(t.leech) + " max health");
                    Add("FLASK HEALING", "-" + P(t.bonus)); break;
                case WeaponTraitKind.Accord:
                    Add("FINISHER", "+" + P(t.bonus));
                    Add("GUARD AFTER FINISHER", "-" + P(t.leech) + " for " + t.window.ToString("0.#") + "s"); break;
                case WeaponTraitKind.Namebound: Add("MANTRA COOLDOWN", "-" + P(t.bonus)); break;
                case WeaponTraitKind.Penance: Add("PER HIT TAKEN", "+" + P(t.bonus) + " (max " + Mathf.Max(1, Mathf.RoundToInt(t.threshold * 10f)) + ")"); break;
            }
            // 16/11 (§275): hiệu ứng lên quái
            string when = t.statusOn == StatusTrigger.Finisher ? " (finisher)" : t.statusOn == StatusTrigger.WhileActive ? " (while lit)" : "";
            switch (t.status)
            {
                case StatusKind.Bleed: Add("BLEED" + when, t.statusPower.ToString("0.#") + "/s per stack, " + t.statusSeconds.ToString("0.#") + "s, max " + t.statusStacks); break;
                case StatusKind.Burn: Add("HOLY FIRE" + when, t.statusPower.ToString("0.#") + "/s for " + t.statusSeconds.ToString("0.#") + "s"); break;
                case StatusKind.Bind: Add("ROOT BIND" + when, "slow " + P(t.statusPower) + " for " + t.statusSeconds.ToString("0.#") + "s"); break;
                case StatusKind.Chill: Add("FROST" + when, "slow " + P(t.statusPower) + " for " + t.statusSeconds.ToString("0.#") + "s"); break;
                case StatusKind.Mark: Add("BLOOD MARK" + when, t.statusSeconds.ToString("0.#") + "s - marked kills heal"); break;
            }
            return o;
        }

        private static void StatRow(VisualElement parent, string label, float v, float cap, string shown, float? other, string fmt)
        {
            var row = Row(parent); row.style.alignItems = Align.Center; row.style.height = 30; row.style.flexShrink = 0;
            var l = Txt(label, "sr-tiny", row, InvMuted); l.style.width = 136; l.style.flexShrink = 0; l.style.letterSpacing = 1;
            var pips = Row(row); pips.style.flexShrink = 0;
            int n = Mathf.Clamp(Mathf.RoundToInt(v / Mathf.Max(0.0001f, cap) * 10f), 1, 10);
            for (int i = 0; i < 10; i++)
            {
                var pip = new VisualElement();
                pip.style.width = 13; pip.style.height = 10; pip.style.marginRight = 3;
                pip.style.backgroundColor = i < n ? RequiemUI.Gold : new Color(0.19f, 0.15f, 0.12f);
                pips.Add(pip);
            }
            var val = Txt(shown, "sr-tiny", row, RequiemUI.Bone); val.style.width = 84; val.style.flexShrink = 0;
            val.style.unityTextAlign = TextAnchor.MiddleRight; val.style.marginLeft = 6;
            if (!other.HasValue) return;
            float d = v - other.Value;
            if (Mathf.Abs(d) <= 0.004f) return;
            var dl = Txt((d > 0f ? "+" : "-") + Mathf.Abs(d).ToString(fmt), "sr-tiny", row, d > 0f ? InvGood : InvBad);
            dl.style.marginLeft = 12;
        }

        /// <summary>Bảng thông số dưới giá kiếm, 3 cột: DANH TÍNH (tên, hành động, nội tại) | SỨC CHIẾN (chỉ số, ba nhát) | THE TELLING.</summary>
        private void WeaponDetail(VisualElement card, WeaponDefinition w, Action<WeaponDefinition> equip)
        {
            var eq = EquippedWeapon();
            bool equipped = eq == w;
            var cmp = !equipped && eq != null ? eq : null;
            card.style.flexDirection = FlexDirection.Row;
            Func<float, bool, VisualElement> col = (grow, sep) =>
            {
                var v = new VisualElement(); v.style.flexGrow = grow; v.style.flexShrink = 1; v.style.flexBasis = 0;
                Pad(v, 32, 26);
                if (sep) { v.style.borderLeftWidth = 1; v.style.borderLeftColor = InvLine; }
                card.Add(v);
                return v;
            };

            // --- 1. danh tính
            var c1 = col(1f, false);
            Txt(w.displayName, "sr-title", c1, RequiemUI.Bone, 36);
            if (!string.IsNullOrEmpty(w.epithet))
            {
                var ep = Txt(w.epithet, "sr-tiny", c1, RequiemUI.Gold); ep.style.whiteSpace = WhiteSpace.Normal; ep.style.marginTop = 4;
            }
            var act = new VisualElement(); act.style.marginTop = 14; act.style.height = 50; act.style.flexShrink = 0;
            c1.Add(act);
            if (equipped) Chip("EQUIPPED", RequiemUI.Gold, act).style.marginTop = 10;
            else act.Add(ActionButton("Draw this blade", "Enter", () => equip(w)));
            string tt = TraitTitle(w);
            if (tt.Length > 0)
            {
                var box = new VisualElement(); box.style.flexShrink = 0; box.style.marginTop = 18;
                Pad(box, 18, 14);
                box.style.backgroundColor = new Color(0.115f, 0.082f, 0.048f, 1f);
                box.style.borderLeftWidth = 3; box.style.borderLeftColor = RequiemUI.Gold;
                Txt("INNATE", "sr-tiny", box, InvMuted, 16).style.letterSpacing = 3;
                Txt(tt, "sr-title", box, RequiemUI.Gold, 26).style.marginTop = 2;
                var td = Txt("", "sr-card-desc", box, RequiemUI.Bone);
                td.enableRichText = true; td.text = Rich(w.trait.Describe()); td.style.marginTop = 8;
                c1.Add(box);
            }

            // --- 2. sức chiến
            var c2 = col(1f, true);
            Header(cmp != null ? $"ATTRIBUTES  -  vs {cmp.displayName}" : "ATTRIBUTES", c2);
            // 16/11 (§259): sát thương = MỘT con số DPS (người chơi không cần sát thương từng nhát)
            StatRow(c2, "DPS", WeaponRing.Dps(w), 45f, WeaponRing.Dps(w).ToString("0"),
                cmp != null ? WeaponRing.Dps(cmp) : (float?)null, "0");
            StatRow(c2, "SPEED", WeaponRing.Rate(w), 2.2f, WeaponRing.Rate(w).ToString("0.0") + "/s",
                cmp != null ? WeaponRing.Rate(cmp) : (float?)null, "0.0");
            StatRow(c2, "REACH", WeaponRing.Reach(w), 1.2f, WeaponRing.Reach(w).ToString("0.00"),
                cmp != null ? WeaponRing.Reach(cmp) : (float?)null, "0.00");
            StatRow(c2, "STAGGER", WeaponRing.Impact(w), 5f, WeaponRing.Impact(w).ToString("0.0"),
                cmp != null ? WeaponRing.Impact(cmp) : (float?)null, "0.0");
            // 16/11 (§259): bỏ bảng "THE THREE CUTS" — thanh có hiệu ứng đặc biệt thì ghi thẳng hiệu ứng + con số
            var fx = SpecialEffects(w);
            if (fx.Count > 0)
            {
                Header("SPECIAL", c2, 16);
                foreach (var kv in fx)
                {
                    var r = Row(c2); r.style.height = 26; r.style.flexShrink = 0; r.style.alignItems = Align.Center;
                    var l = Txt(kv.Key, "sr-tiny", r, InvMuted); l.style.flexGrow = 1; l.style.letterSpacing = 1;
                    var v = Txt(kv.Value, "sr-tiny", r, RequiemUI.Gold); v.style.unityTextAlign = TextAnchor.MiddleRight;
                }
            }

            // --- 3. the telling
            var c3 = col(1.15f, true);
            Header("ORIGIN", c3);                                              // 16/11 (§260): người dùng đổi tên mục
            var sc = Reader(c3);
            string lore = !string.IsNullOrEmpty(w.lore) ? w.lore : w.description;
            if (!string.IsNullOrEmpty(lore)) Txt(lore, "sr-card-desc", sc, InvBody, 20);
            if (!string.IsNullOrEmpty(w.foundAt)) Txt("Found: " + w.foundAt, "sr-tiny", sc, InvFaint, 17).style.marginTop = 14;
        }

        // ------------------------------------------------------------------ thẻ chìa / dụng cụ
        private void KeyCard(VisualElement card, KeyItem cur, WorldFlags flags)
        {
            var top = Row(card); top.style.alignItems = Align.Center; top.style.flexShrink = 0; Pad(top, 40, 30);
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.width = 140; box.style.height = 140; box.style.flexShrink = 0;
            box.style.alignItems = Align.Center; box.style.justifyContent = Justify.Center;
            box.style.backgroundColor = new Color(0.028f, 0.02f, 0.036f, 1f); Border(box, 1, InvLine);
            var big = new VisualElement { pickingMode = PickingMode.Ignore };
            big.style.width = 112; big.style.height = 112;
            var bt = string.IsNullOrEmpty(cur.icon) ? null : RequiemUI.Tex(cur.icon);
            if (bt != null) big.style.backgroundImage = new StyleBackground(bt);
            box.Add(big); top.Add(box);
            var col = new VisualElement(); col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.marginLeft = 28;
            top.Add(col);
            var nm = Txt(cur.name, "sr-title", col, RequiemUI.Bone, 38); nm.style.whiteSpace = WhiteSpace.Normal;
            bool used = KeyRing.Used(flags, cur.id);
            Chip((KindLabel(cur.kind) + (used ? "  -  USED" : "")).ToUpperInvariant(), used ? InvMuted : RequiemUI.Gold, col).style.marginTop = 10;

            var inner = new VisualElement(); inner.style.flexGrow = 1; inner.style.flexShrink = 1; Pad(inner, 40, 0);
            inner.style.paddingBottom = 28;
            card.Add(inner);
            Hairline(inner, 0, 22);
            var sc = Reader(inner);
            sc.style.maxWidth = 860;
            if (!string.IsNullOrEmpty(cur.opens))
            {
                Header("OPENS", sc);
                Txt(cur.opens, "sr-card-desc", sc, RequiemUI.Bone).style.marginBottom = 18;
            }
            if (!string.IsNullOrEmpty(cur.desc)) Txt(cur.desc, "sr-card-desc", sc, InvBody);
            if (cur.stats.Count > 0)
            {
                Header("PROPERTIES", sc, 20);
                foreach (var kv in cur.stats)
                {
                    var r = Row(sc); r.style.justifyContent = Justify.SpaceBetween; r.style.marginBottom = 2;
                    Txt(kv.Key, "sr-card-desc", r, InvMuted);
                    Txt(kv.Value, "sr-card-desc", r, RequiemUI.Gold);
                }
            }
            if (!string.IsNullOrEmpty(cur.text))
            {
                Header("WRITTEN ON IT", sc, 20);
                Txt(cur.text, "sr-card-desc", sc, InvBody);
            }
        }

        // ------------------------------------------------------------------ thẻ ghi chép: trang đọc (không có icon to — tờ giấy nào cũng giống nhau)
        private void NoteCard(VisualElement card, KeyItem cur)
        {
            var inner = new VisualElement(); inner.style.flexGrow = 1; inner.style.flexShrink = 1;
            Pad(inner, 56, 40);
            card.Add(inner);
            Txt("NOTE", "sr-tiny", inner, InvMuted, 16).style.letterSpacing = 3;
            var nm = Txt(cur.name, "sr-title", inner, RequiemUI.Bone, 38); nm.style.whiteSpace = WhiteSpace.Normal; nm.style.marginTop = 4;
            if (!string.IsNullOrEmpty(cur.desc))
            {
                var dsc = Txt(cur.desc, "sr-tiny", inner, RequiemUI.Gold); dsc.style.whiteSpace = WhiteSpace.Normal; dsc.style.marginTop = 6;
            }
            Hairline(inner, 20, 24);
            var sc = Reader(inner);
            sc.style.maxWidth = 860;
            string body = !string.IsNullOrEmpty(cur.text) ? cur.text : cur.desc;
            if (!string.IsNullOrEmpty(body)) Txt(body, "sr-card-desc", sc, InvBody, 22);
            if (!string.IsNullOrEmpty(cur.opens)) Txt("Concerns: " + cur.opens, "sr-tiny", sc, InvFaint, 17).style.marginTop = 16;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ SETTINGS (dùng chung với Title)
        public static VisualElement BuildSettings(Action back)
        {
            var p = Panel("SETTINGS", null, 860);
            var tabs = new VisualElement(); tabs.AddToClassList("sr-tabs");
            p.Add(tabs);
            var body = new VisualElement(); body.AddToClassList("sr-settings-body");
            p.Add(body);
            string[] names = { "Audio", "Display", "Gameplay", "Controls" };
            var tabBtns = new List<SRButton>();
            Action<int> show = null;
            show = i =>
            {
                body.Clear();
                for (int k = 0; k < tabBtns.Count; k++) tabBtns[k].EnableInClassList("sr-tab-on", k == i);
                if (i == 0) AudioTab(body);
                else if (i == 1) DisplayTab(body);
                else if (i == 2) GameplayTab(body);
                else ControlsTab(body);
            };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var b = new SRButton(names[i], () => show(idx), 190, 52);
                b.AddToClassList("sr-tab");
                tabs.Add(b); tabBtns.Add(b);
            }
            show(0);
            var foot = new VisualElement(); foot.AddToClassList("sr-row-buttons");
            foot.Add(new SRButton("Defaults", () => { GameSettings.ResetDefaults(); show(0); }, 240, 56));
            foot.Add(new SRButton("Back", () => back?.Invoke(), 240, 56));
            p.Add(foot);
            return p;
        }

        private static VisualElement Row(VisualElement body, string label, VisualElement control)
        {
            var r = new VisualElement(); r.AddToClassList("sr-row");
            RequiemUI.Text(label, "sr-row-label", r);
            r.Add(control);
            body.Add(r);
            return r;
        }

        private static void Changed() { GameSettings.Save(); GameSettings.Apply(); }

        private static void AudioTab(VisualElement b)
        {
            Row(b, "Master volume", new SRSlider(GameSettings.Master, v => { GameSettings.Master = v; Changed(); }));
            Row(b, "Music", new SRSlider(GameSettings.Music, v => { GameSettings.Music = v; Changed(); }));
            Row(b, "Sound effects", new SRSlider(GameSettings.Sfx, v => { GameSettings.Sfx = v; Changed(); }));
        }

        private static void DisplayTab(VisualElement b)
        {
            var res = Screen.resolutions.Select(r => $"{r.width} x {r.height}").Distinct().ToList();
            var resIdx = new List<int>();
            var all = Screen.resolutions;
            foreach (var label in res) resIdx.Add(Array.FindLastIndex(all, r => $"{r.width} x {r.height}" == label));
            if (res.Count == 0) { res.Add($"{Screen.width} x {Screen.height}"); resIdx.Add(-1); }
            int cur = Mathf.Max(0, resIdx.IndexOf(GameSettings.ResolutionIndex));
            if (GameSettings.ResolutionIndex < 0) cur = Mathf.Max(0, res.IndexOf($"{Screen.currentResolution.width} x {Screen.currentResolution.height}"));
            Row(b, "Resolution", new SRStepper(res.ToArray(), cur, i => { GameSettings.ResolutionIndex = resIdx[i]; Changed(); }));
            Row(b, "Fullscreen", new SRToggle(GameSettings.Fullscreen, v => { GameSettings.Fullscreen = v; Changed(); }));
            Row(b, "V-Sync", new SRToggle(GameSettings.VSync, v => { GameSettings.VSync = v; Changed(); }));
            float[] scales = { 0.8f, 0.9f, 1f, 1.1f, 1.25f };
            int si = Array.FindIndex(scales, x => Mathf.Abs(x - GameSettings.HudScale) < 0.01f);
            Row(b, "Interface size", new SRStepper(scales.Select(x => Mathf.RoundToInt(x * 100) + "%").ToArray(), si < 0 ? 2 : si,
                i => { GameSettings.HudScale = scales[i]; Changed(); }));
        }

        private static void GameplayTab(VisualElement b)
        {
            Row(b, "Enemy health numbers", new SRToggle(GameSettings.EnemyHpNumbers, v => { GameSettings.EnemyHpNumbers = v; Changed(); }));
            Row(b, "Screen shake", new SRSlider(GameSettings.ScreenShake, v => { GameSettings.ScreenShake = v; Changed(); }));
            Row(b, "Low health warning", new SRToggle(GameSettings.LowHpVignette, v => { GameSettings.LowHpVignette = v; Changed(); }));
            Row(b, "Reduce flashing", new SRToggle(GameSettings.ReduceFlashing, v => { GameSettings.ReduceFlashing = v; Changed(); }));
        }

        private static void ControlsTab(VisualElement b)
        {
            foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
            {
                var k = new VisualElement(); k.AddToClassList("sr-keycap-big");
                var kt = RequiemUI.Tex("keycap");
                if (kt != null) k.style.backgroundImage = new StyleBackground(kt);
                RequiemUI.Text(GameInput.Label(a), "sr-key", k);
                var r = Row(b, GameInput.ActionName(a), k);
                r.AddToClassList("sr-row-compact");
            }
            RequiemUI.Text("Move: W A S D / arrow keys / left stick    Attack also: left mouse button", "sr-small sr-gold sr-panel-foot", b);
        }

        // ------------------------------------------------------------------ REST / SKILLS
        // 11/11 (§228): làm lại bảng nghỉ theo cùng ngôn ngữ với túi đồ (§221-§223): khung PixelLab, không thanh cuộn mặc định,
        // 3 mục Q/E (Mantras | Mantra Sets | Passives), cột ô chiêu bên trái, danh sách giữa, thẻ chi tiết đầy đủ bên phải.
        private int selSlot;
        private VisualElement restPanel;
        private string restTab = "Mantra", restSel = "";
        private Action restClear;
        private static readonly string[] RestTabs = { "Mantra", "Set", "Passive" };
        private static string RestTabLabel(string t) => t == "Set" ? "Mantra Sets" : t == "Passive" ? "Passives" : "Mantras";
        private static string RestTabIcon(string t) => "inv/cat_" + (t == "Set" ? "set" : t == "Passive" ? "passive" : "mantra");
        private static string SlotKey(int i) => GameInput.KeyLabel(GameInput.SkillKey(i));

        private sealed class RestEntry
        {
            public SkillDefinition skill;
            public List<SkillDefinition> set;
            public PassiveCatalog.Entry passive;
            public bool isPassive;
            public string Id => isPassive ? "p:" + passive.flag : set != null ? "s:" + set[0].setId : "m:" + skill.id;
            public string Name => isPassive ? passive.name : set != null ? PlayerLoadout.SetName(set[0]) : skill.displayName;
            public Texture2D Icon => isPassive ? RequiemUI.Tex(passive.icon) : set != null ? set[0].Icon : skill.Icon;
        }

        /// <summary>Cuộn dọc KHÔNG thanh cuộn (bánh xe / cần phải), dải mờ đáy khi còn nội dung.</summary>
        private static ScrollView QuietScroll(VisualElement parent)
        {
            var wrap = new VisualElement(); wrap.style.flexGrow = 1; wrap.style.flexShrink = 1;
            parent.Add(wrap);
            var sc = new ScrollView(ScrollViewMode.Vertical);
            sc.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sc.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            sc.mouseWheelScrollSize = 60f; sc.style.flexGrow = 1;
            wrap.Add(sc);
            return sc;
        }

        private static VisualElement SlotBox(Texture2D tex, float box, float icon, float alpha = 1f)
        {
            var b = new VisualElement { pickingMode = PickingMode.Ignore };
            b.style.width = box; b.style.height = box; b.style.flexShrink = 0;
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            if (!Art(b, ArtSlot, 16, 16, 16, 16)) { b.style.backgroundColor = new Color(0.028f, 0.02f, 0.036f, 1f); Border(b, 1, InvLine); }
            var ic = new VisualElement { pickingMode = PickingMode.Ignore };
            ic.style.width = icon; ic.style.height = icon; ic.style.opacity = alpha;
            if (tex != null) ic.style.backgroundImage = new StyleBackground(tex);
            b.Add(ic);
            return b;
        }

        /// <param name="atRest">18/11 (§329): true = mở khi nghỉ ở lửa (tiêu đề REST…, đã hồi máu); false = phím L ở bất cứ đâu.</param>
        private VisualElement BuildRest(bool atRest)
        {
            var dir = MapEncounterDirector.Instance;
            if (Array.IndexOf(RestTabs, restTab) < 0) restTab = "Mantra";
            var p = new VisualElement();
            p.style.width = InvW; p.style.maxWidth = Length.Percent(96);
            p.style.height = InvH; p.style.maxHeight = Length.Percent(96);
            if (!Art(p, ArtFrame, 44, 44, 44, 44)) { p.style.backgroundColor = InvBg; Border(p, 2, InvFrame); }
            Pad(p, 58, 46);

            var head = Row(p); head.style.alignItems = Align.Center; head.style.flexShrink = 0;
            var tcol = new VisualElement(); tcol.style.flexGrow = 1; head.Add(tcol);
            string ttl = !atRest ? "MANTRAS" : ShrineMarker.Noun == "shrine" ? "SHRINE OF REST" : "REST BY THE " + ShrineMarker.Noun.ToUpperInvariant();
            Txt(ttl, "sr-title", tcol, RequiemUI.Gold, 40).style.letterSpacing = 3;
            Txt(atRest ? "Health and flasks restored - progress saved. Pick a socket, then the mantra to attune to it."
                       : "Pick a socket, then the mantra to attune to it.", "sr-tiny", tcol, InvMuted, 17).style.marginTop = 2;
            var tabsWrap = Row(head); tabsWrap.style.alignItems = Align.Center;
            KeyCap("Q", tabsWrap).style.marginRight = 12;
            var tabs = Row(tabsWrap);
            KeyCap("E", tabsWrap).style.marginLeft = 12;
            var rule = new VisualElement(); rule.style.flexShrink = 0; rule.style.marginTop = 12;
            if (Art(rule, ArtDivider, 44, 0, 44, 0)) rule.style.height = 36; else { rule.style.height = 1; rule.style.backgroundColor = InvLine; }
            p.Add(rule);

            var body = Row(p); body.style.flexGrow = 1; body.style.flexShrink = 1; body.style.marginTop = 22;
            var foot = Row(p); foot.style.flexShrink = 0; foot.style.marginTop = 18;
            foot.style.justifyContent = Justify.FlexEnd; foot.style.alignItems = Align.Center; foot.style.height = 40;

            Action rebuild = null;
            Action<RestEntry> attune = e =>
            {
                if (loadout == null || e == null || e.isPassive) return;
                if (e.set != null) loadout.Equip(0, PlayerLoadout.SetLeader(e.set[0]) ?? e.set[0]);
                else loadout.Equip(selSlot, e.skill);
                // 16/11 (§269): bỏ thông báo thừa — hình / hoạt ảnh / bảng tên boss đã thể hiện (bảng chiêu đã đổi)
                rebuild();
            };
            restClear = () => { if (loadout != null && loadout.SkillAt(selSlot) != null) { loadout.Equip(selSlot, null); rebuild(); } };

            rebuild = () =>
            {
                var flags = dir != null ? dir.Flags : null;
                var all = new List<RestEntry>();
                var seen = new HashSet<string>();
                if (loadout != null)
                    foreach (var sk in loadout.owned)
                    {
                        if (PlayerLoadout.InSet(sk))
                        {
                            if (!seen.Add(sk.setId)) continue;
                            var mem = PlayerLoadout.SetOf(sk); mem.Sort((a, b) => a.setSlot.CompareTo(b.setSlot));
                            all.Add(new RestEntry { set = mem });
                        }
                        else all.Add(new RestEntry { skill = sk });
                    }
                foreach (var pe in PassiveCatalog.Learned(flags)) all.Add(new RestEntry { passive = pe, isPassive = true });
                Func<RestEntry, string> kindOf = e => e.isPassive ? "Passive" : e.set != null ? "Set" : "Mantra";

                tabs.Clear();
                foreach (var t in RestTabs)
                {
                    string tab = t;
                    tabs.Add(Tab(RestTabLabel(tab), RestTabIcon(tab), all.Count(x => kindOf(x) == tab), restTab == tab, () => { restTab = tab; rebuild(); }));
                }
                var shown = all.Where(x => kindOf(x) == restTab).ToList();
                if (shown.Count > 0 && shown.All(x => x.Id != restSel)) restSel = shown[0].Id;
                body.Clear();

                // ---- cột ô chiêu
                var sockCol = new VisualElement(); sockCol.style.width = 330; sockCol.style.flexShrink = 0;
                body.Add(sockCol);
                Header("SOCKETS", sockCol);
                VisualElement focusSock = null;
                for (int i = 0; i < PlayerLoadout.SlotCount; i++)
                {
                    int idx = i;
                    var sk = loadout != null ? loadout.SkillAt(i) : null;
                    var r = new VisualElement { focusable = true, tabIndex = 0 };
                    r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
                    r.style.height = 108; r.style.marginBottom = 8; r.style.paddingLeft = 12; r.style.paddingRight = 12;
                    r.style.borderLeftWidth = 4;
                    StyleRow(r, i == selSlot);
                    var bx = SlotBox(sk != null ? sk.Icon : RequiemUI.Tex("ic_slot_empty"), 88, 64, sk != null ? 1f : 0.5f);
                    r.Add(bx);
                    var kc = KeyCap(SlotKey(i), bx); kc.style.position = Position.Absolute; kc.style.right = -8; kc.style.bottom = -8;
                    var c = new VisualElement { pickingMode = PickingMode.Ignore }; c.style.marginLeft = 18; c.style.flexShrink = 1; c.style.overflow = Overflow.Hidden;
                    r.Add(c);
                    var nm = Txt(sk != null ? sk.displayName : "Empty", "sr-small", c, sk != null ? RequiemUI.Bone : InvFaint); NoWrap(nm);
                    var sub = Txt(sk != null ? $"{sk.Kind}  -  {sk.cooldown:0.#}s" : "No mantra attuned", "sr-tiny", c, InvMuted, 16); NoWrap(sub); sub.style.marginTop = 2;
                    r.AddManipulator(new Clickable(() => { selSlot = idx; rebuild(); }));
                    r.RegisterCallback<NavigationSubmitEvent>(_ => { selSlot = idx; rebuild(); });
                    sockCol.Add(r);
                    if (i == selSlot) focusSock = r;
                }
                SkillDefinition s0 = loadout != null ? loadout.SkillAt(0) : null;
                if (s0 != null && PlayerLoadout.InSet(s0))
                {
                    var note = Txt($"Set attuned: {PlayerLoadout.SetName(s0)}. Attuning any single mantra removes the whole set.", "sr-tiny", sockCol, InvMuted, 16);
                    note.style.whiteSpace = WhiteSpace.Normal; note.style.marginTop = 6;
                }

                // ---- danh sách
                var listWrap = new VisualElement(); listWrap.style.width = ListW; listWrap.style.flexShrink = 0; listWrap.style.marginLeft = 28;
                body.Add(listWrap);
                Header(RestTabLabel(restTab).ToUpperInvariant(), listWrap);
                var list = QuietScroll(listWrap);
                var card = new VisualElement();
                card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = 0; card.style.marginLeft = 28;
                card.style.backgroundColor = InvSurface; Border(card, 1, InvLine); card.style.overflow = Overflow.Hidden;
                body.Add(card);

                var rows = new List<KeyValuePair<RestEntry, VisualElement>>();
                Action<string> select = null;
                select = id =>
                {
                    restSel = id;
                    foreach (var kv in rows) StyleRow(kv.Value, kv.Key.Id == id);
                    var cur = shown.FirstOrDefault(x => x.Id == id);
                    card.Clear(); invReader = null; foot.Clear();
                    if (cur == null)
                    {
                        var em = Txt(restTab == "Passive" ? "No passive gifts learned yet." : restTab == "Set" ? "No mantra sets yet." : "You know no single mantras yet.", "sr-small", card, InvFaint);
                        em.style.alignSelf = Align.Center; em.style.marginTop = 160;
                    }
                    else RestDetail(card, cur, attune);
                    if (cur != null && !cur.isPassive) Prompt("Enter", cur.set != null ? "Attune set" : $"Attune to {SlotKey(selSlot)}", foot, () => attune(cur));
                    if (loadout != null && loadout.SkillAt(selSlot) != null) Prompt("Del", $"Clear socket {SlotKey(selSlot)}", foot, restClear);
                    Prompt("Esc", "Leave", foot, Pop);
                };
                VisualElement focusRow = null;
                foreach (var e in shown)
                {
                    var en = e;
                    var r = new VisualElement { focusable = true, tabIndex = 0 };
                    r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
                    r.style.height = 84; r.style.marginBottom = 4; r.style.paddingLeft = 12; r.style.paddingRight = 14;
                    r.style.borderLeftWidth = 4; r.style.borderLeftColor = Clear0;
                    if (en.set != null)
                    {
                        var stack3 = new VisualElement { pickingMode = PickingMode.Ignore }; stack3.style.width = 72; stack3.style.height = 72; stack3.style.flexShrink = 0;
                        if (!Art(stack3, ArtSlot, 16, 16, 16, 16)) stack3.style.backgroundColor = new Color(0.028f, 0.02f, 0.036f, 1f);
                        for (int k = 0; k < en.set.Count && k < 3; k++)
                        {
                            var ic = new VisualElement { pickingMode = PickingMode.Ignore };
                            ic.style.position = Position.Absolute; ic.style.width = 34; ic.style.height = 34;
                            ic.style.left = k == 0 ? 6 : k == 1 ? 32 : 19; ic.style.top = k == 2 ? 32 : 6;
                            if (en.set[k].Icon != null) ic.style.backgroundImage = new StyleBackground(en.set[k].Icon);
                            stack3.Add(ic);
                        }
                        r.Add(stack3);
                    }
                    else r.Add(SlotBox(en.Icon, 72, 56));
                    var c = new VisualElement { pickingMode = PickingMode.Ignore };
                    c.style.flexGrow = 1; c.style.flexShrink = 1; c.style.marginLeft = 16; c.style.overflow = Overflow.Hidden;
                    r.Add(c);
                    var nm = Txt(en.Name, "sr-small", c, RequiemUI.Bone); NoWrap(nm);
                    string sub = en.isPassive ? "Always active" : en.set != null ? $"{en.set.Count} mantras  -  takes every socket" : $"{en.skill.Kind}  -  {en.skill.cooldown:0.#}s";
                    var sl = Txt(sub, "sr-tiny", c, InvMuted, 16); NoWrap(sl); sl.style.marginTop = 2;
                    string badge = null;
                    if (loadout != null && !en.isPassive)
                    {
                        if (en.set != null) { if (en.set.All(m => Array.IndexOf(loadout.slots, m) >= 0)) badge = "SET"; }
                        else { int at = Array.IndexOf(loadout.slots, en.skill); if (at >= 0) badge = SlotKey(at); }
                    }
                    if (badge != null) { var kb = KeyCap(badge, r); kb.style.marginLeft = 10; kb.style.borderLeftColor = kb.style.borderRightColor = kb.style.borderTopColor = kb.style.borderBottomColor = RequiemUI.Gold; }
                    list.Add(r);
                    rows.Add(new KeyValuePair<RestEntry, VisualElement>(en, r));
                    string id = en.Id; var rv = r;
                    if (id == restSel) focusRow = r;
                    r.RegisterCallback<PointerEnterEvent>(_ => rv.Focus());
                    r.RegisterCallback<FocusInEvent>(_ => { if (restSel != id) select(id); });
                    r.AddManipulator(new Clickable(() => { rv.Focus(); if (restSel != id) select(id); }));
                    r.RegisterCallback<NavigationSubmitEvent>(_ => attune(en));
                }
                select(shown.Count > 0 ? restSel : "");
                var f = focusRow ?? focusSock;
                if (f != null) f.schedule.Execute(() => f.Focus());
            };
            invCycle = d =>
            {
                int k = Array.IndexOf(RestTabs, restTab);
                restTab = RestTabs[(k + d + RestTabs.Length) % RestTabs.Length];
                rebuild();
            };
            rebuild();
            restPanel = Center(p);
            return restPanel;
        }

        /// <summary>Thẻ chi tiết: đầu thẻ (biểu tượng lớn, tên, loại, hồi chiêu, nút gắn) | HIỆU ỨNG | THÔNG SỐ; bộ chiêu: từng chiêu trong bộ.</summary>
        private void RestDetail(VisualElement card, RestEntry e, Action<RestEntry> attune)
        {
            card.style.flexDirection = FlexDirection.Column;
            var top = Row(card); top.style.alignItems = Align.Center; top.style.flexShrink = 0; Pad(top, 36, 28);
            top.style.borderBottomWidth = 1; top.style.borderBottomColor = InvLine;
            top.Add(SlotBox(e.Icon, 128, 96));
            var tc = new VisualElement(); tc.style.marginLeft = 28; tc.style.flexGrow = 1; tc.style.flexShrink = 1; top.Add(tc);
            Txt(e.Name, "sr-title", tc, RequiemUI.Bone, 36);
            string kind = e.isPassive ? "PASSIVE GIFT  -  always active, takes no socket"
                : e.set != null ? $"MANTRA SET  -  {e.set.Count} mantras, takes every socket" : (e.skill.Kind.ToUpperInvariant() + $"  -  COOLDOWN {e.skill.cooldown:0.#}s");
            var kl = Txt(kind, "sr-tiny", tc, RequiemUI.Gold, 17); kl.style.letterSpacing = 2; kl.style.marginTop = 4;
            var act = Row(tc); act.style.marginTop = 14; act.style.height = 50; act.style.alignItems = Align.Center;
            if (!e.isPassive && loadout != null)
            {
                bool on;
                if (e.set != null) on = e.set.All(m => Array.IndexOf(loadout.slots, m) >= 0);
                else on = loadout.SkillAt(selSlot) == e.skill;
                if (on) Chip(e.set != null ? "ATTUNED  -  ALL SOCKETS" : $"ATTUNED  -  {SlotKey(selSlot)}", RequiemUI.Gold, act);
                else act.Add(ActionButton(e.set != null ? "Attune set" : $"Attune to {SlotKey(selSlot)}", "Enter", () => attune(e)));
                if (e.set == null)
                {
                    int at = Array.IndexOf(loadout.slots, e.skill);
                    if (at >= 0 && at != selSlot) { var mv = Txt($"Now in {SlotKey(at)} - attuning moves it.", "sr-tiny", act, InvMuted, 16); mv.style.marginLeft = 16; }
                }
            }
            else if (e.isPassive) Chip("ALWAYS ACTIVE", RequiemUI.Gold, act);

            var sc = Reader(card);
            sc.contentContainer.style.paddingLeft = sc.contentContainer.style.paddingRight = 36;
            sc.contentContainer.style.paddingTop = 24; sc.contentContainer.style.paddingBottom = 28;
            if (e.isPassive)
            {
                Header("EFFECT", sc);
                var d = Txt("", "sr-card-desc", sc, InvBody, 21); d.enableRichText = true; d.text = Rich(e.passive.description);
                return;
            }
            if (e.set == null) { SkillBlock(sc, e.skill, false); return; }
            var lead = PlayerLoadout.SetLeader(e.set[0]);
            string lore = lead != null && !string.IsNullOrEmpty(lead.setDescription) ? lead.setDescription : "";
            if (lore.Length > 0)
            {
                Header("THE SET", sc);
                Txt(lore, "sr-card-desc", sc, InvBody, 20).style.marginBottom = 10;
            }
            foreach (var m in e.set)
            {
                Hairline(sc, 14, 18);
                SkillBlock(sc, m, true);
            }
        }

        /// <summary>Một chiêu: (trong bộ: phím + biểu tượng + tên) | HIỆU ỨNG (mô tả) | THÔNG SỐ (bảng nhãn / giá trị).</summary>
        private void SkillBlock(VisualElement parent, SkillDefinition s, bool member)
        {
            if (member)
            {
                var h = Row(parent); h.style.alignItems = Align.Center; h.style.marginBottom = 12;
                if (s.setSlot >= 0 && s.setSlot < PlayerLoadout.SlotCount) KeyCap(SlotKey(s.setSlot), h).style.marginRight = 14;
                h.Add(SlotBox(s.Icon, 60, 46));
                var hc = new VisualElement(); hc.style.marginLeft = 14; h.Add(hc);
                Txt(s.displayName, "sr-small", hc, RequiemUI.Bone, 26);
                var k = Txt($"{s.Kind.ToUpperInvariant()}  -  COOLDOWN {s.cooldown:0.#}s", "sr-tiny", hc, RequiemUI.Gold, 15); k.style.letterSpacing = 2;
            }
            var cols = Row(parent); cols.style.alignItems = Align.FlexStart;
            var c1 = new VisualElement(); c1.style.flexGrow = 1.25f; c1.style.flexBasis = 0; c1.style.paddingRight = 28; cols.Add(c1);
            var c2 = new VisualElement(); c2.style.flexGrow = 1f; c2.style.flexBasis = 0; c2.style.paddingLeft = 28;
            c2.style.borderLeftWidth = 1; c2.style.borderLeftColor = InvLine; cols.Add(c2);
            Header("EFFECT", c1);
            var d = Txt("", "sr-card-desc", c1, InvBody, 21); d.enableRichText = true; d.text = Rich(s.description);
            Header("DETAILS", c2);
            var st = new List<KeyValuePair<string, string>>();
            st.Add(new KeyValuePair<string, string>("Cooldown", $"{s.cooldown:0.#}s"));
            s.Stats(st);
            foreach (var kv in st)
            {
                var r = Row(c2); r.style.minHeight = 30; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
                r.style.borderBottomWidth = 1; r.style.borderBottomColor = new Color(InvLine.r, InvLine.g, InvLine.b, 0.45f);
                var l = Txt(kv.Key, "sr-tiny", r, InvMuted, 17); l.style.width = 170; l.style.flexShrink = 0;
                var v = Txt("", "sr-tiny", r, RequiemUI.Bone, 17); v.enableRichText = true; v.text = Rich(kv.Value);
                v.style.flexGrow = 1; v.style.whiteSpace = WhiteSpace.Normal; v.style.unityTextAlign = TextAnchor.MiddleRight;
            }
        }

        // ------------------------------------------------------------------ CHẾT
        private void BuildDeath(VisualElement fx)
        {
            death = new VisualElement { pickingMode = PickingMode.Ignore };
            death.AddToClassList("sr-fill"); death.AddToClassList("sr-death");
            deathTitle = RequiemUI.Text("YOU HAVE FALLEN", "sr-title sr-death-title", death);
            deathSub = RequiemUI.Text("The bells toll. You rise again at the last shrine...", "sr-death-sub", death);
            death.style.opacity = 0f;
            death.style.display = DisplayStyle.None;
            fx.Add(death);
        }

        private void UpdateDeath(bool dead)
        {
            if (death == null) return;
            string sub = $"The bells toll. You rise again at the last {ShrineMarker.Noun}...";
            if (deathSub.text != sub) deathSub.text = sub;
            deathT = Mathf.MoveTowards(deathT, dead ? 1f : 0f, Time.unscaledDeltaTime * (dead ? 1.4f : 3f));
            death.style.display = deathT > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            death.style.opacity = deathT;
            deathTitle.style.scale = new Scale(new Vector3(1f + 0.06f * (1f - deathT), 1f + 0.06f * (1f - deathT), 1f));
            if (dead && stack.Count > 0) CloseAll();
        }
    }
}
