using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Cờ trạng thái của map đang chơi: "z3_cleared", "valve_w", "bells_done", "book_purifier"...
    /// Câu đố, cổng, nhặt đồ, nhiệm vụ đều giao tiếp qua cờ — không gọi thẳng nhau.
    ///
    ///   Đặt  : flags.Set("valve_w");
    ///   Hỏi  : flags.Has("valve_w")   |   flags.HasAll("valve_w,valve_e")   (danh sách rỗng = true)
    ///   Nghe : flags.Changed += f => ...;
    ///
    /// Cờ khu tự động: "&lt;zone_id&gt;_started" khi trận đấu trong khu bắt đầu, "&lt;zone_id&gt;_cleared" khi dọn xong.
    /// (Sau này hệ thống lưu game chỉ cần lưu <see cref="All"/>.)
    /// </summary>
    public sealed class WorldFlags
    {
        private readonly HashSet<string> flags = new HashSet<string>();
        private readonly Action<bool> afterSet;

        /// <summary>Một cờ MỚI vừa được bật (tên đã cắt khoảng trắng).</summary>
        public event Action<string> Changed;

        /// <param name="afterSet">Gọi sau <see cref="Changed"/> mỗi khi có cờ mới (tham số = announce). Director dùng để mở cổng.</param>
        public WorldFlags(Action<bool> afterSet = null)
        {
            this.afterSet = afterSet;
        }

        /// <summary>Mọi cờ đang bật.</summary>
        public IReadOnlyCollection<string> All => flags;

        public bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag.Trim());

        /// <summary>Tất cả cờ trong danh sách "a,b,c" đã bật. Danh sách rỗng = true.</summary>
        public bool HasAll(string commaList)
        {
            if (string.IsNullOrEmpty(commaList)) return true;
            foreach (var f in commaList.Split(','))
            {
                // 07/11: "a|b" = có MỘT trong các cờ là đủ (vd dải chặn mở khi đã đọc trang nhật ký HOẶC bản lưu cũ đã lấy chìa)
                if (f.IndexOf('|') < 0) { if (!Has(f.Trim())) return false; continue; }
                bool any = false;
                foreach (var g in f.Split('|')) if (Has(g.Trim())) { any = true; break; }
                if (!any) return false;
            }
            return true;
        }

        /// <summary>19/11 (§343): một cờ vừa bị GỠ (người chơi gục giữa trận có `reset_on_death`).</summary>
        public event Action<string> Removed;

        /// <summary>19/11 (§343): gỡ cờ. Không có thì không làm gì.</summary>
        public void Unset(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Remove(flag.Trim())) return;
            Debug.Log($"[Map] Gỡ cờ: {flag}");
            Removed?.Invoke(flag.Trim());
        }

        /// <summary>Bật cờ. Bật lại cờ đã có thì không làm gì.</summary>
        /// <param name="announce">true = nếu có cổng mở nhờ cờ này thì hiện "Somewhere, a way opens."</param>
        public void Set(string flag, bool announce = false)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Add(flag.Trim())) return;
            Debug.Log($"[Map] Cờ: {flag}");
            Changed?.Invoke(flag.Trim());
            afterSet?.Invoke(announce);
        }
    }
}
