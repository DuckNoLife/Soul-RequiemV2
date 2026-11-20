using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Lớp nền của MỘT cơ quan trên map (một object trong layer "mechanics" của Tiled).
    /// Mỗi loại (Tiled Type) là một class con, đăng ký trong <see cref="MechanismRegistry"/>.
    ///
    /// Vòng đời (MapMechanics gọi):
    ///   Init()            một lần sau khi đọc map
    ///   OnFlagsChanged()  mỗi khi có cờ mới (và một lần sau Init)
    ///   Animate()         mỗi khung hình, kể cả khi đã Done (hiệu ứng, vòng sáng)
    ///   Tick(pos, inside) mỗi khung hình khi CHƯA Done (inside = người chơi trong vùng object)
    ///   OnStruck()        người chơi chém trúng (chỉ khi ReactsToStrike)
    ///   Use()             người chơi nhấn T (chỉ khi DefaultVerb != null)
    ///
    /// Thêm loại cơ quan mới: tạo class con + một dòng trong MechanismRegistry. Không cần sửa MapMechanics.
    /// </summary>
    public abstract class Mechanism
    {
        public string Type { get; private set; }
        public string Name { get; private set; }
        public Rect Rect { get; private set; }
        public SuperCustomProperties Props { get; private set; }
        /// <summary>Đã xong (giải xong / bị phá / đã dừng). Cơ quan Done không còn được Tick.</summary>
        public bool Done { get; internal set; }
        /// <summary>06/11: hình đặt trong Tiled bằng tile object (<see cref="MechanismArt"/>): chính object của cơ quan (nếu là
        /// tile object) đứng đầu, rồi các mảnh có `art_for` = tên cơ quan. Rỗng = cơ quan kiểu cũ (hình chữ nhật + `sprite`).</summary>
        public System.Collections.Generic.IReadOnlyList<MechanismArt> Art => art;
        private readonly System.Collections.Generic.List<MechanismArt> art = new System.Collections.Generic.List<MechanismArt>();

        protected MechanismContext Ctx { get; private set; }

        internal void Bind(string type, string name, Rect rect, SuperCustomProperties props, MechanismContext ctx)
        {
            Type = type; Name = name; Rect = rect; Props = props; Ctx = ctx;
        }

        internal void AddArt(MechanismArt a) { if (a != null) art.Add(a); }

        public virtual void Init() { }
        public virtual void OnFlagsChanged() { }
        /// <summary>19/11 (§343): có cờ vừa bị GỠ (người chơi gục giữa trận `reset_on_death`) — cơ quan đã xong nhờ cờ đó thì mở lại.</summary>
        public virtual void OnFlagsReset() { }
        public virtual void Animate() { }
        public virtual void Tick(Vector2 playerPos, bool inside) { }

        // ---- đòn chém
        public virtual bool ReactsToStrike => false;
        public virtual void OnStruck() { }

        // ---- phím T (InteractionSystem)
        /// <summary>Chữ trên nhắc phím ("Ring", "Read"...). null = không tương tác bằng T. Tiled có thể ghi đè bằng thuộc tính `verb`.</summary>
        public virtual string DefaultVerb => null;
        /// <summary>Vật dẫn truyện: có đốm sáng lấp lánh tới khi xem lần đầu.</summary>
        public virtual bool Clue => false;
        /// <summary>Lúc này có nhấn T được không.</summary>
        public virtual bool Usable => !Done;
        public virtual void Use() { }

        // ---- bảng giữa màn hình (ghi chú, khoá ký tự, cửa sổ hoa hồng)
        /// <summary>Bảng của cơ quan này đang mở (nhắc phím đổi thành "Close").</summary>
        public bool PanelIsOpen => MechanismPanels.Current != null && MechanismPanels.Current.Owner == this;
        /// <summary>Bảng đang mở còn hợp lệ không (vd người chơi đi xa thì đóng).</summary>
        public virtual bool KeepPanelOpen(Vector2 playerPos) => true;

        // ---- công cụ test (F5)
        /// <summary>Coi như đã giải: bật cờ `sets`. Mặc định cho mọi loại có thuộc tính sets.</summary>
        internal virtual void DebugSolve()
        {
            string sets = Str("sets", "");
            if (string.IsNullOrEmpty(sets) || Done) return;
            Done = true;
            Ctx.Flags.Set(sets);
        }

        // ---- đọc thuộc tính Tiled
        protected string Str(string key, string fallback) => Props.GetString(key, fallback);
        protected int Int(string key, int fallback) => Props.GetInt(key, fallback);
        protected float Float(string key, float fallback) => Props.GetFloat(key, fallback);

        /// <summary>Khoảng cách từ điểm tới mép vùng của cơ quan (0 nếu ở trong).</summary>
        protected float DistanceTo(Vector2 p)
        {
            var r = Rect;
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Vùng cơ quan chạm hình tròn (tâm c, bán kính r).</summary>
        internal bool Overlaps(Vector2 c, float r)
        {
            var rc = Rect;
            float dx = Mathf.Max(rc.xMin - c.x, 0f, c.x - rc.xMax);
            float dy = Mathf.Max(rc.yMin - c.y, 0f, c.y - rc.yMax);
            return dx * dx + dy * dy <= r * r;
        }
    }

    /// <summary>Loại không có hành vi riêng (vd ritual_pillar: chỉ là mốc vị trí cho boss).</summary>
    public sealed class PassiveMechanism : Mechanism { }
}
