using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 10/11 (§217): GIÁ KIẾM — những thanh katana Ren đang có và thanh đang đeo.
    ///   • Có một thanh = cờ `weapon_&lt;id&gt;` (cờ của map, GameSession mang qua map như cờ `book_`, SaveGame lưu lại).
    ///   • Thanh đang đeo: <see cref="Equipped"/> (tĩnh, sống suốt phiên chơi) + lưu trong bản lưu.
    ///   • Mọi thanh nằm ở Resources/Weapons/*.asset — nhặt được bằng pickup Tiled `item = weapon`, `weapon_id = &lt;id&gt;`.
    /// Thanh mở màn (Ronin's Katana) luôn có sẵn.
    /// </summary>
    public static class WeaponRing
    {
        public const string Prefix = "weapon_";
        private static WeaponDefinition[] all;

        /// <summary>Mọi thanh katana trong game (thứ tự: thanh mở màn trước, rồi theo tên).</summary>
        public static IReadOnlyList<WeaponDefinition> All
        {
            get
            {
                if (all == null)
                {
                    var list = new List<WeaponDefinition>(Resources.LoadAll<WeaponDefinition>("Weapons"));
                    list.Sort((a, b) => a == null || b == null ? 0
                        : a.id == DefaultWeaponId ? -1 : b.id == DefaultWeaponId ? 1 : string.CompareOrdinal(a.displayName, b.displayName));
                    all = list.ToArray();
                }
                return all;
            }
        }

        public static string DefaultWeaponId
        {
            get { var d = WeaponDefinition.LoadDefault(); return d != null ? d.id : "katana"; }
        }

        public static WeaponDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var w in All) if (w != null && w.id == id) return w;
            return null;
        }

        public static string Flag(string id) => Prefix + id;
        public static bool Has(WorldFlags f, string id) =>
            !string.IsNullOrEmpty(id) && (id == DefaultWeaponId || (f != null && f.Has(Flag(id))));

        /// <summary>Những thanh đang có (theo thứ tự <see cref="All"/>).</summary>
        public static List<WeaponDefinition> Owned(WorldFlags f)
        {
            var list = new List<WeaponDefinition>();
            foreach (var w in All) if (w != null && Has(f, w.id)) list.Add(w);
            return list;
        }

        /// <summary>Id thanh đang đeo (mang qua map, lưu trong bản lưu). Rỗng = thanh mở màn.</summary>
        public static string Equipped { get; private set; } = "";

        public static void SetEquipped(string id) => Equipped = id ?? "";

        /// <summary>Chơi mới: quên thanh đang đeo của lượt trước (dữ liệu tĩnh sống suốt phiên chạy game).</summary>
        public static void Reset() => Equipped = "";

        /// <summary>Nhặt được một thanh: bật cờ + dòng thông báo.</summary>
        /// <summary>16/11 (§269): `announce` = false -> không hiện dòng chữ dưới màn hình (nơi gọi tự hiện, vd chân bảng ghi chú).</summary>
        public static void Give(WorldFlags f, string id, string line, bool announce = true)
        {
            var w = Find(id);
            if (f == null || w == null) { Debug.LogWarning($"[WeaponRing] không có katana id '{id}' trong Resources/Weapons."); return; }
            if (f.Has(Flag(id))) return;
            SoulRequiem.Core.Sfx.Play2D("pickup_weapon");   // 17/11 (§294): âm
            f.Set(Flag(id));
            if (!announce) return;
            string msg = string.IsNullOrEmpty(line) ? $"Obtained: {w.displayName}." : line;
            Hints.Show(msg + "  [Inventory]", Mathf.Max(5f, msg.Length / 15f));
        }

        // ---- số liệu tóm tắt cho bảng túi đồ (0..1 để vẽ thanh) -------------------------------------------------
        /// <summary>Tổng sát thương một combo đủ 3 nhát.</summary>
        public static float ComboDamage(WeaponDefinition w)
        {
            float d = 0f;
            if (w != null && w.combo != null) foreach (var s in w.combo) d += s.damage;
            return d;
        }

        /// <summary>Thời gian một combo đủ 3 nhát (nối sớm ở khung huỷ) + nghỉ cuối combo.</summary>
        public static float ComboTime(WeaponDefinition w)
        {
            if (w == null || w.combo == null || w.combo.Length == 0) return 1f;
            float t = 0f;
            for (int i = 0; i < w.combo.Length; i++) t += i < w.combo.Length - 1 ? w.combo[i].CancelTime : w.combo[i].Length;
            return t + w.finisherRecovery;
        }

        public static float Dps(WeaponDefinition w)
        {
            float t = ComboTime(w);
            return t > 0.01f ? ComboDamage(w) / t : 0f;
        }

        public static float Reach(WeaponDefinition w)
        {
            float r = 0f;
            if (w != null && w.combo != null) foreach (var s in w.combo) r = Mathf.Max(r, s.range);
            return r;
        }

        public static float Impact(WeaponDefinition w)
        {
            float k = 0f;
            if (w != null && w.combo != null) foreach (var s in w.combo) k = Mathf.Max(k, s.knockback);
            return k;
        }

        /// <summary>Nhát / giây khi đánh nối liên tục.</summary>
        public static float Rate(WeaponDefinition w)
        {
            float t = ComboTime(w);
            return t > 0.01f && w != null && w.combo != null ? w.combo.Length / t : 0f;
        }
    }
}
