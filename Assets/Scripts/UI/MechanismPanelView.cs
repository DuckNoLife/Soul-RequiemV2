using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Vẽ bảng giữa màn hình của cơ quan (UI Toolkit, cùng khung đá với menu): tờ ghi chú, khoá ký tự, cửa sổ hoa hồng.
    /// Dữ liệu lấy từ <see cref="MechanismPanels.Current"/> (assembly Gameplay); bấm chuột gọi ngược lại model.
    /// Dựng lại toàn bộ khi <see cref="MechanismPanels.Version"/> đổi (mở / đóng / giải xong).
    /// Kích thước theo khung tham chiếu 1920x1080 (như HUD).
    /// </summary>
    public class MechanismPanelView : MonoBehaviour
    {
        private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI" };
        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f, 0.55f);

        private VisualElement layer;
        private int shownVersion = -1;

        // phần cần cập nhật mà không dựng lại
        private readonly List<VisualElement> wheelGlyphs = new List<VisualElement>();
        private readonly List<VisualElement> paneQuads = new List<VisualElement>();
        private VisualElement truthImage;
        private RoseWindowPanel rose;
        private Texture2D upArrow, downArrow;

        private void Start()
        {
            var ui = RequiemUI.Instance;
            if (ui == null) { enabled = false; return; }
            layer = new VisualElement { pickingMode = PickingMode.Position };
            layer.AddToClassList("sr-layer");
            layer.AddToClassList("sr-fill");
            layer.AddToClassList("sr-center-box");
            layer.style.display = DisplayStyle.None;
            int idx = ui.Root.IndexOf(ui.Menu);                     // dưới menu Pause, trên HUD
            ui.Root.Insert(idx < 0 ? ui.Root.childCount : idx, layer);
        }

        private void Update()
        {
            if (layer == null) return;
            if (MechanismPanels.Version != shownVersion) Rebuild();
            if (rose != null && truthImage != null && rose.Solved)
                truthImage.style.opacity = rose.SolvedAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - rose.SolvedAt) / 2.5f);
        }

        private void Rebuild()
        {
            shownVersion = MechanismPanels.Version;
            layer.Clear();
            wheelGlyphs.Clear(); paneQuads.Clear();
            truthImage = null; rose = null;
            var p = MechanismPanels.Current;
            layer.style.display = p != null ? DisplayStyle.Flex : DisplayStyle.None;
            switch (p)
            {
                case NotePanel n: layer.Add(BuildNote(n)); break;
                case CipherPanel c: layer.Add(BuildCipher(c)); break;
                case RoseWindowPanel w: layer.Add(BuildRoseWindow(w)); break;
            }
        }

        // ------------------------------------------------------------------ ghi chú
        private VisualElement BuildNote(NotePanel n)
        {
            var p = RequiemMenus.Panel(n.Title, null, 1080);
            var body = RequiemUI.Text(n.Body, "sr-small", p);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.unityTextAlign = TextAnchor.UpperLeft;
            body.style.alignSelf = Align.Stretch;
            if (n.Picture != null)
            {
                var pic = SpriteBox(n.Picture, 336, 336, p);
                pic.style.marginTop = 12;
            }
            if (n.SealSlot >= 0)
            {
                var row = Row(p);
                for (int i = 0; i < n.SealSlots; i++)
                {
                    var col = Column(row, 156);
                    RequiemUI.Text(Roman[Mathf.Min(i, Roman.Length - 1)], "sr-small sr-gold sr-center", col);
                    bool known = i == n.SealSlot;
                    var box = Box(col, 120, known ? new Color(0.30f, 0.26f, 0.16f) : new Color(0.14f, 0.15f, 0.18f));
                    if (known) Inset(SpriteBox(n.SealGlyph, 102, 102, box), 9);
                    else
                    {
                        var q = RequiemUI.Text("?", "sr-title sr-center", box);
                        q.style.flexGrow = 1;
                    }
                }
                RequiemUI.Text($"Records Vault lock - wheel {Roman[Mathf.Min(n.SealSlot, Roman.Length - 1)]}", "sr-small sr-center", p);
            }
            else if (n.Glyphs.Count > 0)
            {
                var row = Row(p);
                foreach (var (glyph, caption) in n.Glyphs)
                {
                    var col = Column(row, 225);
                    SpriteBox(glyph, 108, 108, col);
                    var cap = RequiemUI.Text(caption, "sr-tiny sr-center", col);
                    cap.style.whiteSpace = WhiteSpace.Normal;
                    cap.style.width = 220;
                }
            }
            RequiemUI.Text($"Press {GameInput.Label(GameAction.Interact)} or Esc to close.", "sr-tiny sr-gold sr-panel-foot", p);
            return p;
        }

        // ------------------------------------------------------------------ khoá ký tự
        private VisualElement BuildCipher(CipherPanel c)
        {
            int n = c.WheelCount;
            var p = RequiemMenus.Panel(c.Title, c.Hint, Mathf.Max(780, 225 * n + 120));
            if (upArrow == null) upArrow = Arrow(true);
            if (downArrow == null) downArrow = Arrow(false);
            var row = Row(p);
            for (int i = 0; i < n; i++)
            {
                int wheel = i;
                var col = Column(row, 225);
                RequiemUI.Text(Roman[Mathf.Min(i, Roman.Length - 1)], "sr-small sr-gold sr-center", col);
                col.Add(ArrowButton(upArrow, () => TurnWheel(c, wheel, +1)));
                var box = Box(col, 156, new Color(0.18f, 0.2f, 0.24f));
                var g = SpriteBox(c.WheelGlyph(i), 138, 138, box);
                Inset(g, 9);
                wheelGlyphs.Add(g);
                col.Add(ArrowButton(downArrow, () => TurnWheel(c, wheel, -1)));
            }
            RequiemUI.Text($"Click the arrows to turn each wheel.  {GameInput.Label(GameAction.Interact)} or Esc to leave.", "sr-tiny sr-gold sr-panel-foot", p);
            return p;
        }

        private void TurnWheel(CipherPanel c, int i, int delta)
        {
            c.Turn(i, delta);
            if (MechanismPanels.Current != c) return;           // vừa giải xong -> bảng đã đóng
            for (int k = 0; k < wheelGlyphs.Count; k++) SetSprite(wheelGlyphs[k], c.WheelGlyph(k));
        }

        private static Button ArrowButton(Texture2D tex, System.Action click)
        {
            var b = new Button(click) { text = "" };
            b.style.width = 90; b.style.height = 66;
            b.style.marginTop = 4; b.style.marginBottom = 4;
            b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0;
            b.style.borderLeftWidth = b.style.borderRightWidth = b.style.borderTopWidth = b.style.borderBottomWidth = 0;
            b.style.backgroundColor = Color.clear;
            b.style.backgroundImage = new StyleBackground(tex);
            return b;
        }

        // ------------------------------------------------------------------ cửa sổ hoa hồng
        private VisualElement BuildRoseWindow(RoseWindowPanel w)
        {
            rose = w;
            var p = RequiemMenus.Panel(w.Title, w.Subtitle, 840);
            const float img = 570f, half = img / 2f;
            var area = new VisualElement();
            area.style.width = img; area.style.height = img;
            area.style.marginTop = 6;
            p.Add(area);
            if (w.Picture == null)
            {
                RequiemUI.Text("(missing picture)", "sr-small sr-center", area);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    int pane = i, cx = i % 2, cy = i / 2;                  // cy 0 = hàng trên
                    var quad = new VisualElement();
                    RequiemUI.Abs(quad, cx * half, cy * half, half, half);
                    quad.style.overflow = Overflow.Hidden;
                    var pic = SpriteBox(w.Picture, img, img, quad);
                    RequiemUI.Abs(pic, -cx * half, -cy * half, img, img);
                    pic.pickingMode = PickingMode.Ignore;
                    quad.RegisterCallback<PointerDownEvent>(e =>
                    {
                        if (rose == null || rose.Solved) return;
                        rose.Rotate(pane, e.button != 1);              // chuột phải xoay ngược
                        RefreshPanes();
                        e.StopPropagation();
                    });
                    area.Add(quad);
                    paneQuads.Add(quad);
                }
                if (w.Truth != null && w.Solved)
                {
                    truthImage = SpriteBox(w.Truth, img, img, area);
                    RequiemUI.Abs(truthImage, 0, 0, img, img);
                    truthImage.pickingMode = PickingMode.Ignore;
                    truthImage.style.opacity = 0f;
                }
                RefreshPanes();
            }
            string key = GameInput.Label(GameAction.Interact);
            RequiemUI.Text(w.Solved ? $"{key} or Esc to close." : $"Click a pane to turn it (right click turns it back).  {key} or Esc to leave.",
                           "sr-tiny sr-gold sr-panel-foot", p);
            return p;
        }

        private void RefreshPanes()
        {
            if (rose == null) return;
            for (int i = 0; i < paneQuads.Count; i++)
            {
                var q = paneQuads[i];
                q.style.rotate = new Rotate(rose.Rotation(i) * 90f);
                // mảnh đã đúng chiều (chưa giải xong): viền chì loé vàng nhẹ
                bool right = !rose.Solved && rose.Rotation(i) == 0;
                float bw = right ? 3f : 0f;
                q.style.borderLeftWidth = q.style.borderRightWidth = q.style.borderTopWidth = q.style.borderBottomWidth = bw;
                q.style.borderLeftColor = q.style.borderRightColor = q.style.borderTopColor = q.style.borderBottomColor = Gold;
            }
        }

        // ------------------------------------------------------------------ tiện ích dựng
        private static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            r.style.justifyContent = Justify.Center;
            r.style.marginTop = 14;
            parent.Add(r);
            return r;
        }

        private static VisualElement Column(VisualElement parent, float width)
        {
            var c = new VisualElement { pickingMode = PickingMode.Ignore };
            c.style.width = width;
            c.style.alignItems = Align.Center;
            parent.Add(c);
            return c;
        }

        private static VisualElement Box(VisualElement parent, float size, Color bg)
        {
            var b = new VisualElement { pickingMode = PickingMode.Ignore };
            b.style.width = size; b.style.height = size;
            b.style.backgroundColor = bg;
            b.style.marginTop = 4;
            b.style.justifyContent = Justify.Center;
            b.style.alignItems = Align.Center;
            parent.Add(b);
            return b;
        }

        private static VisualElement SpriteBox(Sprite sp, float w, float h, VisualElement parent)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.width = w; v.style.height = h;
            v.style.flexShrink = 0;
            SetSprite(v, sp);
            parent.Add(v);
            return v;
        }

        private static void SetSprite(VisualElement v, Sprite sp)
        {
            v.style.backgroundImage = sp != null ? new StyleBackground(sp) : new StyleBackground(StyleKeyword.None);
        }

        private static void Inset(VisualElement v, float px)
        {
            v.style.marginLeft = v.style.marginRight = v.style.marginTop = v.style.marginBottom = 0;
            v.style.width = new Length(100, LengthUnit.Percent);
            v.style.height = new Length(100, LengthUnit.Percent);
            v.style.flexShrink = 1;
            var parent = v.parent;
            if (parent != null) parent.style.paddingLeft = parent.style.paddingRight = parent.style.paddingTop = parent.style.paddingBottom = px;
        }

        /// <summary>Mũi tên vàng pixel (đáy rộng, đỉnh nhọn) — vẽ bằng code.</summary>
        private static Texture2D Arrow(bool up)
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int row = up ? y : N - 1 - y;
                    float halfW = (N - 1 - row) * 0.5f;
                    bool inside = row >= 4 && row <= N - 6 && Mathf.Abs(x - (N - 1) * 0.5f) <= halfW - 2f;
                    t.SetPixel(x, y, inside ? new Color(0.95f, 0.82f, 0.45f, 1f) : new Color(0, 0, 0, 0));
                }
            t.Apply();
            return t;
        }
    }
}
