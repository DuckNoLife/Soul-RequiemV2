using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Một món đồ then chốt trong túi (chìa khoá, tay quay...). Định nghĩa bằng thuộc tính Tiled, xem <see cref="KeyRing"/>.</summary>
    public sealed class KeyItem
    {
        public string id, name, desc, opens, icon;
        /// <summary>04/11: loại (lọc trong túi đồ): "Key", "Tool", "Document" — sau này "Weapon". Thuộc tính Tiled `key_kind`.</summary>
        public string kind = "Key";
        /// <summary>Nội dung đầy đủ (tài liệu nhặt được: đọc lại trong túi đồ).</summary>
        public string text = "";
        /// <summary>Bảng chỉ số / nội tại (vũ khí sau này): `key_stats` = "Nhãn:Giá trị;Nhãn:Giá trị".</summary>
        public readonly List<KeyValuePair<string, string>> stats = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// 04/11: TÚI ĐỒ — chìa khoá và đồ then chốt của map hiện tại.
    ///   - Định nghĩa: object Tiled có `key_id` + `key_name`, `key_desc`, `key_opens` (cửa / nơi nó mở), `key_icon` (ảnh trong
    ///     Resources/UI/Requiem). Đặt trên pickup `item = key` hoặc trên rương (`chest`) chứa chìa.
    ///   - Có chìa = cờ `key_&lt;id&gt;` (lưu cùng cờ của map). Đã dùng ít nhất một lần = cờ `key_&lt;id&gt;_used`.
    ///   - Cửa khoá bằng chìa: thuộc tính `requires_key` trên object cổng -> chỉ mở khi Ren tới sát cửa và có chìa
    ///     (MapEncounterDirector.TickGateLocks), không tự mở khi vừa nhặt.
    ///   - Giao diện: menu Pause ▸ Inventory (RequiemMenus).
    /// Đồ là của từng map: KeyRing.Clear() khi nạp map mới.
    /// </summary>
    public static class KeyRing
    {
        private static readonly List<KeyItem> known = new List<KeyItem>();

        public static IReadOnlyList<KeyItem> Known => known;
        public static void Clear() => known.Clear();
        public static string Flag(string id) => "key_" + id;
        public static string UsedFlag(string id) => "key_" + id + "_used";

        public static KeyItem Find(string id)
        {
            foreach (var k in known) if (k.id == id) return k;
            return null;
        }

        public static string NameOf(string id)
        {
            var k = Find(id);
            return k != null && !string.IsNullOrEmpty(k.name) ? k.name : id;
        }

        /// <summary>Đọc định nghĩa đồ từ thuộc tính Tiled. Trả về id (rỗng nếu object không định nghĩa đồ nào).</summary>
        public static string Register(SuperCustomProperties p)
        {
            if (p == null) return "";
            string id = p.GetString("key_id", "");
            if (id.Length == 0) return "";
            var k = Find(id);
            if (k == null) { k = new KeyItem { id = id }; known.Add(k); }
            k.name = p.GetString("key_name", string.IsNullOrEmpty(k.name) ? id : k.name);
            k.desc = p.GetString("key_desc", k.desc ?? "");
            k.opens = p.GetString("key_opens", k.opens ?? "");
            k.icon = p.GetString("key_icon", k.icon ?? "");
            string body = p.GetString("text", "");
            if (body.Length > 0) k.text = TextUtil.Clean(body.Replace("\\n", "\n"));
            k.kind = p.GetString("key_kind", body.Length > 0 ? "Document" : k.kind);
            k.stats.Clear();
            foreach (var part in p.GetString("key_stats", "").Split(';'))
            {
                int c = part.IndexOf(':');
                if (c > 0) k.stats.Add(new KeyValuePair<string, string>(part.Substring(0, c).Trim(), part.Substring(c + 1).Trim()));
            }
            return id;
        }

        public static bool Has(WorldFlags f, string id) => f != null && !string.IsNullOrEmpty(id) && f.Has(Flag(id));
        public static bool Used(WorldFlags f, string id) => f != null && f.Has(UsedFlag(id));

        /// <summary>Đồ đang có trong túi (theo thứ tự định nghĩa trên map).</summary>
        public static List<KeyItem> Owned(WorldFlags f)
        {
            var list = new List<KeyItem>();
            foreach (var k in known) if (Has(f, k.id)) list.Add(k);
            return list;
        }

        /// <summary>Cho Ren một món đồ: bật cờ + dòng thông báo (kèm chữ riêng `text` nếu có).</summary>
        public static void Give(WorldFlags f, string id, string text)
        {
            if (f == null || string.IsNullOrEmpty(id)) return;
            f.Set(Flag(id));
            string msg = string.IsNullOrEmpty(text) ? $"Obtained: {NameOf(id)}." : text;
            Hints.Show(msg + "  [Inventory]", Mathf.Max(4f, msg.Length / 16f));
        }

        /// <summary>Ren nói một câu (cửa / rương khoá...): bóng thoại trên đầu.</summary>
        public static void Say(Transform player, string line, float seconds = 2.4f)
        {
            if (player == null || string.IsNullOrEmpty(line)) return;
            SpeechBubble.Show(player.position + new Vector3(0f, 0.8f, 0f), line, seconds).Follow(player);
        }
    }
}
