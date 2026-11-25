using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoulRequiem.UI
{
    /// <summary>
    /// DRAG AND DROP in the Mantra panel (UI Toolkit). Attach to a row / slot:
    ///   - press and release in place (moved &lt; 8 px) = a normal click;
    ///   - press and drag = a "ghost" icon follows the cursor, target slots get a glowing outline, the hovered one glows stronger; release on a target = drop(slot), outside = drop(null).
    /// Not a MonoBehaviour. Targets are fetched again on every drag (`targets`) because the panel is rebuilt after every change.
    /// Used by: RequiemMenus.Rest (Mantra panel).
    /// </summary>
    public sealed class DragToSlot : PointerManipulator
    {
        private readonly Func<Texture2D> icon;
        private readonly Action click;
        private readonly Action<VisualElement> drop;
        private readonly Func<IList<VisualElement>> targets;
        private readonly Func<VisualElement> overlay;
        private readonly Action<VisualElement, int> mark;     // 0 = normal, 1 = can drop, 2 = hovered

        private bool pressed, dragging;
        private int pid = -1;
        private Vector2 start;
        private VisualElement ghost, hover;
        private IList<VisualElement> live;

        public DragToSlot(Func<Texture2D> icon, Action click, Action<VisualElement> drop, Func<IList<VisualElement>> targets,
                          Func<VisualElement> overlay, Action<VisualElement, int> mark)
        {
            this.icon = icon; this.click = click; this.drop = drop; this.targets = targets; this.overlay = overlay; this.mark = mark;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnLost);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnLost);
        }

        private void OnDown(PointerDownEvent e)
        {
            if (!CanStartManipulation(e)) return;
            pressed = true; dragging = false; pid = e.pointerId; start = e.position;
            target.CapturePointer(pid);
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (!pressed || e.pointerId != pid) return;
            Vector2 p = e.position;
            if (!dragging)
            {
                if ((p - start).sqrMagnitude < 64f) return;
                BeginDrag();
            }
            var root = overlay != null ? overlay() : null;
            if (ghost != null && root != null)
            {
                Vector2 lp = root.WorldToLocal(p);
                ghost.style.left = lp.x - 36f; ghost.style.top = lp.y - 36f;
            }
            VisualElement over = null;
            if (live != null) foreach (var t in live) if (t != null && t.worldBound.Contains(p)) { over = t; break; }
            if (over != hover)
            {
                if (hover != null) mark?.Invoke(hover, 1);
                hover = over;
                if (hover != null) mark?.Invoke(hover, 2);
            }
            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            if (!pressed || e.pointerId != pid) return;
            bool was = dragging;
            var over = hover;
            End();
            if (target.HasPointerCapture(e.pointerId)) target.ReleasePointer(e.pointerId);
            e.StopPropagation();
            if (was) drop?.Invoke(over);
            else click?.Invoke();
        }

        private void OnLost(PointerCaptureOutEvent e) { if (pressed) End(); }

        private void BeginDrag()
        {
            dragging = true;
            live = targets != null ? targets() : null;
            if (live != null) foreach (var t in live) if (t != null) mark?.Invoke(t, 1);
            var root = overlay != null ? overlay() : null;
            if (root == null) return;
            ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            ghost.style.position = Position.Absolute; ghost.style.width = 72; ghost.style.height = 72;
            ghost.style.opacity = 0.85f;
            var tex = icon != null ? icon() : null;
            if (tex != null) ghost.style.backgroundImage = new StyleBackground(tex);
            ghost.style.borderTopWidth = ghost.style.borderBottomWidth = ghost.style.borderLeftWidth = ghost.style.borderRightWidth = 2;
            var gold = new Color(0.93f, 0.78f, 0.44f, 1f);
            ghost.style.borderTopColor = ghost.style.borderBottomColor = ghost.style.borderLeftColor = ghost.style.borderRightColor = gold;
            ghost.style.backgroundColor = new Color(0.03f, 0.02f, 0.04f, 0.9f);
            root.Add(ghost);
            Vector2 lp = root.WorldToLocal(start);
            ghost.style.left = lp.x - 36f; ghost.style.top = lp.y - 36f;
        }

        private void End()
        {
            pressed = false;
            if (ghost != null) { ghost.RemoveFromHierarchy(); ghost = null; }
            if (live != null) foreach (var t in live) if (t != null) mark?.Invoke(t, 0);
            live = null; hover = null;
            dragging = false;
        }
    }
}
