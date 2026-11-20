using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§196): tiện ích chung cho bẫy / cổng có hoạt ảnh: nạp dải khung Resources/VFX/&lt;tên&gt; (cắt bằng
    /// Catalog/v2/tools/traps196/slice_traps.py, pivot dưới-trái, khung 0 = trạng thái nghỉ / đóng), tạo SpriteRenderer cùng lớp
    /// với nhân vật, tìm quái đứng trong một vùng.
    /// </summary>
    public static class TrapArt
    {
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        /// <summary>Các khung theo đúng thứ tự số đuôi (&lt;tên&gt;_0, _1, ...). Rỗng nếu không có ảnh.</summary>
        public static Sprite[] Frames(string name)
        {
            if (string.IsNullOrEmpty(name)) return new Sprite[0];
            if (cache.TryGetValue(name, out var f) && f != null && f.Length > 0 && f[0] != null) return f;
            var all = Resources.LoadAll<Sprite>("VFX/" + name);
            var list = new List<Sprite>(all);
            list.Sort((a, b) => Suffix(a.name).CompareTo(Suffix(b.name)));
            f = list.ToArray();
            if (f.Length == 0) Debug.LogWarning("[TrapArt] không thấy khung Resources/VFX/" + name);
            cache[name] = f;
            return f;
        }

        private static int Suffix(string n)
        {
            int i = n.LastIndexOf('_');
            return i >= 0 && int.TryParse(n.Substring(i + 1), out int v) ? v : 0;
        }

        /// <summary>SpriteRenderer mới, cùng sorting layer + material với nhân vật; orderOffset âm = nằm dưới chân (bẫy sàn).</summary>
        public static SpriteRenderer MakeRenderer(Transform parent, string name, Vector3 bottomLeft, Transform player, int orderOffset)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = bottomLeft;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var psr = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null)
            {
                sr.sortingLayerID = psr.sortingLayerID;
                sr.sortingOrder = psr.sortingOrder + orderOffset;
                sr.sharedMaterial = psr.sharedMaterial;
            }
            return sr;
        }

        private static readonly List<Collider2D> buf = new List<Collider2D>();
        private static ContactFilter2D filter;
        private static bool filterReady;

        /// <summary>Quái (Health không phải người chơi, còn sống) có collider chạm vùng.</summary>
        public static void EnemiesIn(Rect r, List<Health> into)
        {
            into.Clear();
            if (!filterReady) { filter = new ContactFilter2D(); filter.useTriggers = true; filter.useLayerMask = false; filterReady = true; }
            buf.Clear();
            Physics2D.OverlapBox(r.center, r.size, 0f, filter, buf);
            for (int i = 0; i < buf.Count; i++)
            {
                var h = buf[i] != null ? buf[i].GetComponentInParent<Health>() : null;
                if (h == null || h.IsPlayer || h.IsDead || into.Contains(h)) continue;
                into.Add(h);
            }
        }

        /// <summary>Gây sát thương bẫy cho người chơi: coi như đòn từ xa (Riposte không đỡ được bẫy, Veil vẫn giảm).</summary>
        public static void HurtPlayer(MechanismContext ctx, float amount, Vector2 dir, float knock)
        {
            Health.IncomingRanged = true;
            try { ctx.HurtPlayer(amount, dir, knock); }
            finally { Health.IncomingRanged = false; }
        }

        /// <summary>16/11 (§284): bẫy CƠ KHÍ (gai sàn, trụ lăn, lưỡi cưa, phụt nước) — như HurtPlayer nhưng lăn không né được
        /// (bất tử khi lăn bị bỏ qua; khiên / cảnh diễn vẫn chặn). Tên bắn vẫn dùng HurtPlayer (lăn né tên là hợp lý).</summary>
        public static void HurtPlayerCrush(MechanismContext ctx, float amount, Vector2 dir, float knock)
        {
            Health.IncomingRanged = true; Health.TrapIgnoresRoll = true;
            try { ctx.HurtPlayer(amount, dir, knock); }
            finally { Health.IncomingRanged = false; Health.TrapIgnoresRoll = false; }
        }
    }
}
