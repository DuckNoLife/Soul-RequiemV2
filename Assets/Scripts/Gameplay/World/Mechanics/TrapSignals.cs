using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§196): xung tín hiệu giữa các cơ quan (tấm sàn / cần gạt -> bẫy). Khác cờ WorldFlags: không lưu, phát rồi thôi.
    /// Bẫy HỎI (không đăng ký sự kiện) bằng <see cref="Count"/>: số lần tên đó đã phát — so với số đã thấy lần trước.
    /// Thuộc tính Tiled: bên phát `fires` = "ten1,ten2"; bên nghe `trigger` (chạy một lượt) / `disable_on` (tắt tạm).
    /// </summary>
    public static class TrapSignals
    {
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public static void Fire(string names)
        {
            if (string.IsNullOrEmpty(names)) return;
            foreach (var raw in names.Split(','))
            {
                string n = raw.Trim();
                if (n.Length == 0) continue;
                counts.TryGetValue(n, out int c);
                counts[n] = c + 1;
            }
        }

        /// <summary>Tổng số lần mọi tên trong danh sách "a,b" đã phát.</summary>
        public static int Count(string names)
        {
            if (string.IsNullOrEmpty(names)) return 0;
            int sum = 0;
            foreach (var raw in names.Split(','))
                if (counts.TryGetValue(raw.Trim(), out int c)) sum += c;
            return sum;
        }
    }

    /// <summary>Người nghe một danh sách tín hiệu: <see cref="Poll"/> trả true khi có xung mới kể từ lần hỏi trước.</summary>
    public sealed class TrapListener
    {
        private readonly string names;
        private int seen;

        public TrapListener(string names)
        {
            this.names = names ?? "";
            seen = TrapSignals.Count(this.names);
        }

        public bool Active => names.Length > 0;

        public bool Poll()
        {
            if (names.Length == 0) return false;
            int c = TrapSignals.Count(names);
            if (c == seen) return false;
            seen = c;
            return true;
        }
    }
}
