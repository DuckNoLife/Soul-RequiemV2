using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Plain text menu entry (no button frame) — selected: gold text, a small flame (PixelLab, 8-frame loop) + a faint red glow strip behind.
    /// Mouse hover = select; Enter / click = press. Up/down navigation uses UI Toolkit focus.
    /// PC layout: LEFT-ALIGNED — one flame on the left, the left edges of all entries line up. Optional sub-line (Continue: chapter / region).
    /// `AutoTick` = runs its effects on real time by itself (used in Pause / confirm dialogs, where no TitleScreen calls Tick).
    /// </summary>
    public class TitleMenuItem : VisualElement
    {
        public readonly Label Label;
        public readonly Label Sub;
        private readonly Action onClick;
        private readonly VisualElement flame, band;
        private static Texture2D[] flames;
        public float Appear = 1f;                       // 0..1 driven by TitleScreen during the opening
        public bool AutoTick;

        public TitleMenuItem(string text, Action click, string sub = null)
        {
            onClick = click;
            AddToClassList("sr-tm-item");
            focusable = true; tabIndex = 0;
            band = new VisualElement { pickingMode = PickingMode.Ignore };
            band.AddToClassList("sr-tm-band");
            var g = RequiemUI.Tex("title_glow");
            if (g != null) { g.filterMode = FilterMode.Bilinear; band.style.backgroundImage = new StyleBackground(g); }
            Add(band);
            flame = new VisualElement { pickingMode = PickingMode.Ignore };
            flame.AddToClassList("sr-tm-flame");
            Add(flame);
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.AddToClassList("sr-tm-txt");
            Add(col);
            Label = RequiemUI.Text(text, "sr-title sr-tm-label", col);
            if (!string.IsNullOrEmpty(sub)) Sub = RequiemUI.Text(sub, "sr-tm-sub2", col);
            style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            this.AddManipulator(new Clickable(() => { if (enabledSelf) onClick?.Invoke(); }));
            RegisterCallback<NavigationSubmitEvent>(_ => { if (enabledSelf) onClick?.Invoke(); });
            RegisterCallback<PointerEnterEvent>(_ => { if (enabledSelf) Focus(); });
            schedule.Execute(() => { if (AutoTick) Tick(Time.unscaledTime); }).Every(16);
        }

        public bool Selected => focusController != null && focusController.focusedElement == this;

        /// Called every frame (TitleScreen, or itself with AutoTick): animate the flame, text / glow strip by selection state.
        public void Tick(float t)
        {
            if (flames == null)
            {
                var list = new System.Collections.Generic.List<Texture2D>();
                for (int i = 0; i < 16; i++) { var f = Resources.Load<Texture2D>("UI/Requiem/title_flame_" + i); if (f == null) break; f.filterMode = FilterMode.Point; list.Add(f); }
                flames = list.ToArray();
            }
            bool on = Selected;
            if (flames.Length > 0)
            {
                var a = flames[(int)(t * 12f) % flames.Length];
                if (flame.style.backgroundImage.value.texture != a) flame.style.backgroundImage = new StyleBackground(a);
            }
            flame.style.opacity = on ? 1f : 0f;
            band.style.opacity = on ? 0.5f + 0.1f * Mathf.Sin(t * 3f) : 0f;
            Label.style.color = on ? new Color(0.965f, 0.82f, 0.47f) : new Color(0.8f, 0.75f, 0.67f, 0.86f);
            if (Sub != null) Sub.style.color = on ? new Color(0.78f, 0.72f, 0.62f) : new Color(0.6f, 0.56f, 0.5f, 0.8f);
            style.opacity = Appear;
            style.translate = new Translate(0f, (1f - Appear) * 18f);
            float sc = on ? 1.04f : 1f;
            style.scale = new Scale(new Vector3(sc, sc, 1f));
        }
    }
}
