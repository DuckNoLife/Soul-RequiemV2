using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// console — cần gạt điều khiển phòng ống: cần cờ `requires` (thiếu -> liệt kê theo `needs` "cờ=mô tả|..." hoặc `locked_text`).
    /// Nhấn T -> camera lia tới lưới ống `grid`, khoá điều khiển nhân vật; chuột trái/phải xoay ống, R đặt lại, Esc / Q thoát.
    /// `prompt` = dòng hướng dẫn khi đang điều khiển.
    /// </summary>
    public sealed class ConsoleMechanism : Mechanism
    {
        private bool announced;
        private PipeGridMechanism grid;
        // 03/11: vòng sáng + đốm lấp lánh dưới cần gạt (`marker_item` = tên hình cần gạt) tới khi có cờ `marker_off`
        // (chạy thử: cần gạt 10x18 px nằm lẫn trong máy bơm, người chơi không nhận ra đó là công tắc giải đố)
        private GameObject marker;

        // 04/11 (người dùng): tay quay đang nằm trong túi đồ thì cần gạt KHÔNG được hiện sẵn trên máy. `fit_object` = tên hình cần gạt:
        // ẩn tới khi Ren lắp tay quay (lần đầu bấm T khi đủ `requires`) -> cờ `fit_flag` (mặc định <tên>_fitted), lưu theo map.
        private string FitFlag => Str("fit_flag", Name + "_fitted");
        private bool Fitted => Str("fit_object", "").Length == 0 || Ctx.Flags.Has(FitFlag) || Ctx.Flags.HasAll(Str("marker_off", "-"));

        public override void Init()
        {
            string item = Str("marker_item", "");
            if (item.Length > 0) marker = NoteMarker.Create(Name, Rect, Ctx, item);
            OnFlagsChanged();
        }

        public override void OnFlagsChanged()
        {
            string fit = Str("fit_object", "");
            if (fit.Length > 0) Ctx.Show(fit, Fitted);
            if (marker == null) return;
            string off = Str("marker_off", "");
            marker.SetActive(off.Length == 0 || !Ctx.Flags.HasAll(off));
        }

        public override void Animate()
        {
            if (marker != null && marker.activeSelf) NoteMarker.Animate(marker);
        }

        public override string DefaultVerb => "Use";

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (inside && !announced)
            {
                announced = true;
                Hints.Show(Ctx.Flags.HasAll(Str("requires", "")) ? $"The pump lever. Press {GameInput.Label(GameAction.Interact)} to take the controls."
                                                                 : LockedText(), 4f);
            }
            if (!inside) announced = false;
        }

        public override void Use()
        {
            if (Done) return;
            if (!Ctx.Flags.HasAll(Str("requires", ""))) { Hints.Show(LockedText(), 4f); return; }
            if (!Fitted)
            {
                Ctx.Flags.Set(FitFlag);                                       // -> OnFlagsChanged hiện cần gạt
                string item = Str("fit_item", "");
                if (item.Length > 0) Ctx.Flags.Set(KeyRing.UsedFlag(item));
                SfxHooks.MechanismEvent(this, "fit", "fit_part"); // 19/11 (§335): âm
                Hints.Show(Str("fit_text", "You fit the crank onto the console. It locks into place."), 2.5f);
                CameraFollow.KickMain(Vector2.down, 0.02f, 0.12f);
            }
            Enter();
        }

        private void Enter()
        {
            grid = null;
            foreach (var m in Ctx.All) if (m is PipeGridMechanism g && g.Name == Str("grid", "")) { grid = g; break; }
            if (grid == null) { Debug.LogWarning("[MapMechanics] Không thấy lưới ống " + Str("grid", "")); return; }
            if (grid.Done) { Hints.Show("The flow is already turned to the drain.", 2.5f); return; }
            Ctx.ActiveConsole = this;
            grid.BeginControl(this);
            Ctx.SetPlayerControl(false);
            // khung nhìn: cả lưới + ống cấp phía trên + cống phía dưới; dời tâm xuống chút để dòng chữ hướng dẫn không che hàng ống cuối
            Vector3 focus = (Vector3)grid.Rect.center + new Vector3(0f, -0.22f, 0f);
            var cam = UnityEngine.Camera.main;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float size = Mathf.Max(grid.Rect.height * 0.5f + 0.95f, grid.Rect.width * 0.5f / aspect + 0.5f);
            if (Ctx.Camera != null) Ctx.Camera.Focus(focus, size);
            Hints.Show(Str("prompt", "Click a pipe to turn it. Esc to step back."), 9999f);
        }

        /// <summary>Rời bàn điều khiển: trả camera và điều khiển nhân vật.</summary>
        internal void Exit()
        {
            if (Ctx.ActiveConsole == this) Ctx.ActiveConsole = null;
            grid = null;
            if (Ctx.Camera != null) Ctx.Camera.Release();
            Ctx.SetPlayerControl(true);
            Hints.Clear();
        }

        /// <summary>Gọi mỗi khung hình khi đang điều khiển (thay cho Tick của mọi cơ quan).</summary>
        internal void ControlUpdate()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)) { Exit(); return; }
            if (Ctx.PlayerHealth != null && Ctx.PlayerHealth.IsDead) { Exit(); return; }
            if (grid != null) grid.ControlUpdate(kb, Mouse.current);
        }

        /// <summary>Chưa đủ điều kiện: liệt kê đúng những thứ CÒN THIẾU theo `needs`; không có `needs` thì dùng `locked_text`.</summary>
        private string LockedText()
        {
            string needs = Str("needs", "");
            if (string.IsNullOrEmpty(needs)) return Str("locked_text", "The lever will not move.");
            var missing = new System.Collections.Generic.List<string>();
            foreach (var part in needs.Split('|'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (!Ctx.Flags.HasAll(part.Substring(0, eq))) missing.Add(part.Substring(eq + 1).Trim());
            }
            return missing.Count == 0 ? Str("locked_text", "The lever will not move.")
                                      : "The console will not wake. Still missing: " + string.Join("; ", missing) + ".";
        }
    }
}
