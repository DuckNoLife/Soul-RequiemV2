using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoulRequiem.UI
{
    /// <summary>
    /// 07/11: mục menu của màn tiêu đề — chữ trơn (không khung nút), đang chọn thì chữ vàng, hai ngọn lửa nhỏ (PixelLab, lặp 8 khung)
    /// hai bên + dải sáng đỏ mờ phía sau. Chuột rê vào = chọn; Enter / click = bấm. Điều hướng lên/xuống dùng focus của UI Toolkit.
    /// </summary>
    public class TitleMenuItem : VisualElement
    {
        public readonly Label Label;
        private readonly Action onClick;
        private readonly VisualElement flameL, flameR, band;
        private static Texture2D[] flames;
        public float Appear = 1f;                       // 0..1 do TitleScreen điều khiển lúc mở màn

        public TitleMenuItem(string text, Action click)
        {
            onClick = click;
            AddToClassList("sr-tm-item");
            focusable = true; tabIndex = 0;
            band = new VisualElement { pickingMode = PickingMode.Ignore };
            band.AddToClassList("sr-tm-band");
            var g = RequiemUI.Tex("title_glow");
            if (g != null) { g.filterMode = FilterMode.Bilinear; band.style.backgroundImage = new StyleBackground(g); }
            Add(band);
            flameL = Flame(); Add(flameL);
            Label = RequiemUI.Text(text, "sr-title sr-tm-label", this);
            flameR = Flame(); Add(flameR);
            this.AddManipulator(new Clickable(() => { if (enabledSelf) onClick?.Invoke(); }));
            RegisterCallback<NavigationSubmitEvent>(_ => { if (enabledSelf) onClick?.Invoke(); });
            RegisterCallback<PointerEnterEvent>(_ => { if (enabledSelf) Focus(); });
        }

        private static VisualElement Flame()
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList("sr-tm-flame");
            return v;
        }

        public bool Selected => focusController != null && focusController.focusedElement == this;

        /// Gọi mỗi khung từ TitleScreen: lửa chạy khung, chữ / dải sáng theo trạng thái chọn.
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
                var b = flames[(int)(t * 12f + 3) % flames.Length];      // lệch nhịp để hai ngọn không giống hệt nhau
                if (flameL.style.backgroundImage.value.texture != a) flameL.style.backgroundImage = new StyleBackground(a);
                if (flameR.style.backgroundImage.value.texture != b) flameR.style.backgroundImage = new StyleBackground(b);
            }
            float k = on ? 1f : 0f;
            flameL.style.opacity = k; flameR.style.opacity = k;
            band.style.opacity = on ? 0.55f + 0.1f * Mathf.Sin(t * 3f) : 0f;
            Label.style.color = on ? new Color(0.965f, 0.82f, 0.47f) : new Color(0.8f, 0.75f, 0.67f, 0.82f);
            style.opacity = Appear;
            style.translate = new Translate(0f, (1f - Appear) * 18f);
            float sc = on ? 1.06f : 1f;
            style.scale = new Scale(new Vector3(sc, sc, 1f));
        }
    }
}
