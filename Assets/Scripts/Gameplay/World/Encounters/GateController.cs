using System.Collections.Generic;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Đóng / mở cổng của map. Ba kiểu cổng (thuộc tính Tiled trên object type "gate"):
    ///   close_on_enter = true : cổng khoá đấu trường — đóng khi trận trong khu bắt đầu, mở khi dọn xong.
    ///   unlock_when = "a,b"   : mở khi đủ cờ.
    ///   (không có gì)         : mở khi khu zone_id dọn xong.
    /// starts_closed = false -> mở sẵn từ đầu.
    /// </summary>
    internal sealed class GateController
    {
        private readonly List<MapGate> gates;

        public GateController(List<MapGate> gates)
        {
            this.gates = gates;
        }

        public IReadOnlyList<MapGate> All => gates;

        /// <summary>Đặt trạng thái ban đầu: cổng starts_closed = false thì mở sẵn.</summary>
        public void ApplyStartState()
        {
            foreach (var g in gates)
                if (!g.startsClosed) SetClosed(g, false);
        }

        public void SetClosed(MapGate g, bool closed)
        {
            foreach (var part in g.parts)
            {
                if (part == null) continue;
                // 08/11 (§196): hình cổng có hoạt ảnh -> luôn hiện, chạy khung đóng / mở thay vì bật / tắt
                var anim = part.GetComponent<GateAnimator>();
                if (anim != null) { part.SetActive(true); anim.SetClosed(closed); }
                else part.SetActive(closed);
            }
        }

        public void Open(MapGate g)
        {
            g.opened = true;
            SetClosed(g, false);
        }

        /// <summary>Khu có cổng khoá đấu trường không.</summary>
        public bool HasArenaLock(string zoneId)
        {
            foreach (var g in gates) if (g.closeOnEnter && g.zoneId == zoneId) return true;
            return false;
        }

        /// <summary>Đóng (true) / mở (false) mọi cổng khoá đấu trường của khu.</summary>
        public void SetArenaClosed(string zoneId, bool closed)
        {
            foreach (var g in gates)
                if (g.closeOnEnter && g.zoneId == zoneId) SetClosed(g, closed);
        }

        /// <summary>08/11 (ải): khu làm lại từ đầu -> cổng khoá đấu trường mở ra và được đóng lại khi trận bắt đầu lại
        /// (trước đây cổng đã "opened" thì OpenForClearedZone bỏ qua -> đánh lại xong cổng kẹt đóng).</summary>
        public void RearmArena(string zoneId)
        {
            foreach (var g in gates)
                if (g.closeOnEnter && g.zoneId == zoneId) { g.opened = false; SetClosed(g, false); }
        }

        /// <summary>Khu vừa dọn xong: mở cổng khoá đấu trường và cổng không ghi điều kiện của khu. Trả về true nếu có cổng mở.</summary>
        public bool OpenForClearedZone(string zoneId)
        {
            bool opened = false;
            foreach (var g in gates)
            {
                if (g.zoneId != zoneId || g.opened) continue;
                if (g.closeOnEnter || string.IsNullOrEmpty(g.unlockWhen)) { Open(g); opened = true; }
            }
            return opened;
        }

        /// <summary>Mở mọi cổng unlock_when đã đủ cờ. Trả về true nếu có cổng mở.</summary>
        public bool OpenUnlocked(WorldFlags flags)
        {
            bool any = false;
            foreach (var g in gates)
            {
                if (g.opened || g.closeOnEnter || string.IsNullOrEmpty(g.unlockWhen)) continue;
                if (!flags.HasAll(g.unlockWhen)) continue;
                Open(g);
                any = true;
            }
            return any;
        }
    }
}
