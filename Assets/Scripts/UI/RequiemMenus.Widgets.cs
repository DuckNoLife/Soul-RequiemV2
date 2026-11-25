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
    /// SHARED UI BUILD HELPERS for the menu panels (borders, padding, text, rows, keycaps, buttons, section tabs, bar-less scrolling, skill slots).
    /// </summary>
    public partial class RequiemMenus
    {
        // ---- build helpers
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
            p.AddToClassList("sr-press");                                                                  // clickable -> plays a click sound
            p.RegisterCallback<PointerEnterEvent>(_ => { l.style.color = RequiemUI.Bone; SoulRequiem.Core.Sfx.Play2D("ui_move"); });
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

        // ---- text frame scrolling WITHOUT a scroll bar: mouse wheel / right stick; a faint strip at the bottom while more text is below
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
            t.AddToClassList("sr-press");
            t.RegisterCallback<PointerEnterEvent>(_ => { if (!on) { l.style.color = RequiemUI.Bone; SoulRequiem.Core.Sfx.Play2D("ui_move"); } });
            t.RegisterCallback<PointerLeaveEvent>(_ => { if (!on) l.style.color = idle; });
            return t;
        }

        private static VisualElement Row(VisualElement body, string label, VisualElement control, string help = null)
        {
            var r = new VisualElement(); r.AddToClassList("sr-row"); r.AddToClassList("sr-set-row");
            RequiemUI.Text(label, "sr-row-label", r);
            r.Add(control);
            if (!string.IsNullOrEmpty(help))
            {
                r.RegisterCallback<PointerEnterEvent>(_ => { if (setDesc != null) setDesc.text = help; });
                r.RegisterCallback<FocusInEvent>(_ => { if (setDesc != null) setDesc.text = help; });
            }
            body.Add(r);
            return r;
        }

        /// <summary>Vertical scrolling WITHOUT a scroll bar (wheel / right stick), faint bottom strip while more content remains.</summary>
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
    }
}
