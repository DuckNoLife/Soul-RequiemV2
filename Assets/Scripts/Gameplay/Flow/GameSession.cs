using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Dữ liệu mang theo khi chuyển map (scene): máu, bình máu, map vừa rời đi, tên vùng sẽ tới.
    /// Tĩnh, sống suốt phiên chơi (không cần DontDestroyOnLoad).
    /// </summary>
    public static class GameSession
    {
        public static bool HasCarry { get; private set; }
        public static float HpPercent { get; private set; } = 1f;
        public static int Potions { get; private set; }
        /// <summary>11/11 (§229): số bình TỐI ĐA (rương / bình đồng +1) — trước đây không mang theo: sang map mới về lại 3, chết ở map sau
        /// cũng chỉ nạp lại 3, bản lưu ở map sau ghi 3.</summary>
        public static int MaxPotions { get; private set; }
        public static string FromMap { get; private set; } = "";
        public static string ArrivalTitle { get; private set; } = "";
        public static string ArrivalSubtitle { get; private set; } = "";

        public static void Capture(GameObject player, string fromMap, string arrivalTitle, string arrivalSubtitle)
        {
            HasCarry = true;
            FromMap = fromMap ?? "";
            ArrivalTitle = arrivalTitle ?? "";
            ArrivalSubtitle = arrivalSubtitle ?? "";
            var hp = player != null ? player.GetComponent<Health>() : null;
            HpPercent = hp != null ? Mathf.Clamp(hp.Percent, 0.35f, 1f) : 1f;      // tới map mới không bao giờ với dưới 35% máu
            var kit = player != null ? player.GetComponent<PlayerCombat>() : null;
            Potions = kit != null ? kit.Potions : 0;
            MaxPotions = kit != null ? kit.maxPotions : 0;
            // 04/11: sách đã học (cờ book_*) đi theo sang map mới (trước đây cờ của map nào ở lại map đó -> sang Map 3 mất Purifier / Turned Blade)
            var dir = MapEncounterDirector.Instance;
            if (dir != null && dir.Flags != null)
                foreach (var f in dir.Flags.All)
                    if (f.StartsWith("book_") || f.StartsWith(WeaponRing.Prefix)) Books.Add(f);   // 10/11 (§217): katana đã có cũng đi theo
            // 07/11: nhớ toàn bộ cờ của nhóm map đang rời (quay lại map chính từ hầm phụ -> khu đã dọn / đồ đã nhặt giữ nguyên)
            if (dir != null && dir.Flags != null && !string.IsNullOrEmpty(dir.FlagGroup))
                GroupFlags[dir.FlagGroup] = new System.Collections.Generic.HashSet<string>(dir.Flags.All);
            var lo = player != null ? player.GetComponent<PlayerLoadout>() : null;
            if (lo != null)
            {
                HasLoadout = true;
                Owned.Clear(); foreach (var o in lo.owned) if (o != null) Owned.Add(o.id);
                for (int i = 0; i < Equipped.Length; i++) Equipped[i] = i < PlayerLoadout.SlotCount && lo.slots[i] != null ? lo.slots[i].id : "";
            }
        }

        /// <summary>04/11: sách đã học (cờ book_*), mang qua map và đặt lại vào cờ của map mới (MapEncounterDirector.Start).</summary>
        public static readonly System.Collections.Generic.HashSet<string> Books = new System.Collections.Generic.HashSet<string>();

        // ---- 07/11: tiến độ theo nhóm map (thuộc tính map `flag_group`) + chỗ tới (thuộc tính lối ra `arrive_at`)
        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> GroupFlags =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>();
        private static bool hasArrive;
        private static Vector2 arriveTile;

        /// <summary>Cờ đã nhớ của nhóm map (null nếu chưa từng rời nhóm này trong phiên chơi).</summary>
        public static System.Collections.Generic.IEnumerable<string> RecallGroup(string group) =>
            !string.IsNullOrEmpty(group) && GroupFlags.TryGetValue(group, out var f) ? f : null;

        /// <summary>Lối ra đặt chỗ tới (ô Tiled của map đích); Director của map đích lấy một lần.</summary>
        public static void SetArrival(bool has, Vector2 tile) { hasArrive = has; arriveTile = tile; }

        public static bool TakeArrival(out Vector2 tile)
        {
            tile = arriveTile; bool h = hasArrive; hasArrive = false; return h;
        }

        /// <summary>07/11: Continue — quên tiến độ nhóm map của lượt chơi trước trong cùng phiên (chỉ tin bản lưu).</summary>
        public static void ForgetMaps() { GroupFlags.Clear(); hasArrive = false; }

        /// <summary>04/11: chơi mới — xoá mọi thứ mang theo của lượt trước (dữ liệu tĩnh sống suốt phiên chạy game).</summary>
        public static void NewGame()
        {
            HasCarry = false; HasLoadout = false; MaxPotions = 0;
            GroupFlags.Clear(); hasArrive = false;
            Owned.Clear();
            for (int i = 0; i < Equipped.Length; i++) Equipped[i] = "";
            Books.Clear();
            WeaponRing.Reset();                                                  // 10/11 (§217)
        }

        /// <summary>04/11: đặt bộ chiêu mang theo từ bản lưu (Continue).</summary>
        public static void SetLoadout(string[] owned, string[] equipped)
        {
            HasLoadout = true;
            Owned.Clear(); if (owned != null) Owned.AddRange(owned);
            for (int i = 0; i < Equipped.Length; i++) Equipped[i] = equipped != null && i < equipped.Length ? equipped[i] ?? "" : "";
        }

        // ---- bộ chiêu mang theo qua map (id)
        public static bool HasLoadout { get; private set; }
        public static readonly System.Collections.Generic.List<string> Owned = new System.Collections.Generic.List<string>();
        public static readonly string[] Equipped = new string[PlayerLoadout.SlotCount];

        /// Áp bộ chiêu mang theo (PlayerLoadout.Start gọi).
        public static void ApplyLoadout(PlayerLoadout lo)
        {
            if (!HasLoadout || lo == null) return;
            foreach (var id in Owned) { var s = PlayerLoadout.Find(id); if (s != null && !lo.owned.Contains(s)) lo.owned.Add(s); }
            for (int i = 0; i < PlayerLoadout.SlotCount && i < Equipped.Length; i++)
                lo.slots[i] = string.IsNullOrEmpty(Equipped[i]) ? null : PlayerLoadout.Find(Equipped[i]);
            lo.NotifyChanged();
        }

        /// <summary>Áp dữ liệu mang theo lên nhân vật ở scene mới (gọi một lần sau khi scene tải xong).</summary>
        public static void ApplyTo(GameObject player)
        {
            if (!HasCarry || player == null) return;
            var hp = player.GetComponent<Health>();
            if (hp != null) hp.SetCurrent(hp.Max * HpPercent);
            var kit = player.GetComponent<PlayerCombat>();
            if (kit != null)
            {
                if (MaxPotions > 0) kit.maxPotions = Mathf.Max(kit.maxPotions, MaxPotions);   // 11/11 (§229)
                kit.SetPotions(Potions);
            }
            HasCarry = false;
        }
    }
}
