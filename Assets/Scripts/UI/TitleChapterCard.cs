using System;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Chapter CARD of the CHAPTERS screen (Title) — 2 x 2 grid. A 512x288 chapter image in a bordered frame, a name strip below:
    /// Roman numeral + chapter name + status line (hidden blades / not reached), a "CURRENT" tag top right if the save is in this chapter.
    /// Selected: bright gold border + faint red glow behind + slight zoom; not reached: fully dark image + "???" + "NOT YET REACHED".
    /// A locked chapter reveals nothing but its number.
    /// </summary>
    public class TitleChapterCard : VisualElement
    {
        public readonly bool Open;
        public readonly ChapterBook.Chapter Chapter;
        private readonly VisualElement frame, glow;
        private readonly Label num, title, stat;
        private readonly Action onClick;

        private static readonly Color Gold = new Color(0.965f, 0.82f, 0.47f);
        private static readonly Color Bone = new Color(0.86f, 0.8f, 0.7f);
        private static readonly Color Dim = new Color(0.5f, 0.46f, 0.52f);
        private static readonly Color EdgeOn = new Color(0.96f, 0.8f, 0.45f);
        private static readonly Color EdgeOff = new Color(0.36f, 0.27f, 0.2f);

        public TitleChapterCard(ChapterBook.Chapter ch, bool open, string status, bool current, Action click)
        {
            Chapter = ch;
            Open = open;
            onClick = click;
            AddToClassList("sr-cc");
            focusable = true; tabIndex = 0;

            glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.AddToClassList("sr-cc-glow");
            var g = RequiemUI.Tex("title_glow");
            if (g != null) { g.filterMode = FilterMode.Bilinear; glow.style.backgroundImage = new StyleBackground(g); }
            Add(glow);

            frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("sr-cc-frame");
            Add(frame);
            var img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.AddToClassList("sr-cc-img");
            // a chapter not yet reached does NOT reveal its image / name / tagline — dark striped background + padlock + "LOCKED"
            var tx = open && !string.IsNullOrEmpty(ch.thumb) ? RequiemUI.Tex(ch.thumb) : null;
            if (tx != null) img.style.backgroundImage = new StyleBackground(tx);
            else img.AddToClassList("sr-cc-locked");
            frame.Add(img);
            if (!open)
            {
                var veil = new VisualElement { pickingMode = PickingMode.Ignore };
                veil.AddToClassList("sr-cc-veil");
                frame.Add(veil);
                var lk = new VisualElement { pickingMode = PickingMode.Ignore };
                lk.AddToClassList("sr-cc-lock");
                var lt = RequiemUI.Tex("ic_lock");
                if (lt != null) lk.style.backgroundImage = new StyleBackground(lt);
                veil.Add(lk);
                RequiemUI.Text("LOCKED", "sr-cc-locktext", veil);
            }
            if (current)
            {
                var tag = RequiemUI.Text("CURRENT", "sr-cc-tag", frame);
                tag.pickingMode = PickingMode.Ignore;
            }

            var cap = new VisualElement { pickingMode = PickingMode.Ignore };
            cap.AddToClassList("sr-cc-cap");
            Add(cap);
            num = RequiemUI.Text(ch.roman, "sr-title sr-cc-num", cap);
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.AddToClassList("sr-cc-txt");
            cap.Add(col);
            title = RequiemUI.Text(open ? ch.name : "???", "sr-title sr-cc-name", col);
            stat = RequiemUI.Text(status, "sr-cc-stat", col);

            this.AddManipulator(new Clickable(() => { if (Open) onClick?.Invoke(); }));
            RegisterCallback<NavigationSubmitEvent>(_ => { if (Open) onClick?.Invoke(); });
            RegisterCallback<PointerEnterEvent>(_ => Focus());
        }

        public bool Selected => focusController != null && focusController.focusedElement == this;

        /// Called every frame by TitleScreen.
        public void Tick(float t)
        {
            bool on = Selected;
            glow.style.opacity = on ? 0.5f + 0.1f * Mathf.Sin(t * 3f) : 0f;
            var e = on ? EdgeOn : EdgeOff;
            frame.style.borderTopColor = e; frame.style.borderBottomColor = e; frame.style.borderLeftColor = e; frame.style.borderRightColor = e;
            var c = Open ? (on ? Gold : Bone) : Dim;
            title.style.color = c;
            num.style.color = Open ? Gold : Dim;
            stat.style.color = Open ? new Color(0.74f, 0.69f, 0.6f) : new Color(0.5f, 0.47f, 0.52f);
            float sc = on ? 1.025f : 1f;
            style.scale = new Scale(new Vector3(sc, sc, 1f));
        }
    }
}
