using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Tầm nhìn / vật cản cho chiến đấu và di chuyển.
    ///   "Vật cản" = collider KHÔNG phải trigger và KHÔNG thuộc nhân vật (người chơi, quái — có Health) hay viên đạn:
    ///   đá, tường, cổng, đồ vật có va chạm, mép map (layer "obstacle colliders", "blockers", "map bounds", props...).
    ///   Nước (layer Water) chặn ĐI nhưng KHÔNG chặn tầm nhìn / đòn đánh.
    ///   Vật cản bao quanh một trong hai bên (boss đứng trong hố) không tính là chắn giữa hai bên.
    /// </summary>
    public static class LineOfSight
    {
        private static readonly RaycastHit2D[] buffer = new RaycastHit2D[24];
        private static readonly Dictionary<int, bool> environment = new Dictionary<int, bool>();
        private static ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
        private static int waterLayer = -2;

        private static int WaterLayer => waterLayer == -2 ? (waterLayer = LayerMask.NameToLayer("Water")) : waterLayer;

        /// <summary>Collider này có phải vật cản không. <paramref name="includeWater"/> = tính cả nước (cho việc đi lại).</summary>
        public static bool IsSolid(Collider2D c, bool includeWater)
        {
            if (c == null || c.isTrigger) return false;
            if (!includeWater && c.gameObject.layer == WaterLayer) return false;
            int id = c.GetInstanceID();
            if (!environment.TryGetValue(id, out bool env))
            {
                if (environment.Count > 4096) environment.Clear();
                env = c.GetComponentInParent<Health>() == null && c.GetComponentInParent<Projectile>() == null;
                environment[id] = env;
            }
            return env;
        }

        /// <summary>Đoạn thẳng from → to bị vật cản chắn (không tính nước).</summary>
        public static bool Blocked(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.001f) return false;
            int n = Physics2D.Raycast(from, d / dist, filter, buffer, dist);
            for (int i = 0; i < n; i++)
            {
                var c = buffer[i].collider;
                if (!IsSolid(c, false)) continue;
                // Collider bao quanh chính một đầu mút (vd hố của Plague Eye bao quanh boss) không phải bức tường
                // chắn GIỮA hai bên -> bỏ qua, nếu không đòn đánh vào boss đứng trong hố bị huỷ hết.
                if (c.OverlapPoint(from) || c.OverlapPoint(to)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Từ <paramref name="from"/> thấy được thân <paramref name="target"/> (điểm gần nhất HOẶC tâm collider). target null = coi như thấy.</summary>
        public static bool CanSee(Vector2 from, Collider2D target)
        {
            if (target == null) return true;
            if (!Blocked(from, target.ClosestPoint(from))) return true;
            return !Blocked(from, target.bounds.center);
        }

        /// <summary>
        /// Đi theo hướng <paramref name="dir"/> (đã chuẩn hoá) với thân bán kính <paramref name="radius"/>: còn bao xa thì đụng vật cản
        /// (tính cả nước). Trả về <paramref name="maxDistance"/> nếu thông.
        /// </summary>
        public static float Clearance(Vector2 origin, float radius, Vector2 dir, float maxDistance, Collider2D self)
        {
            int n = Physics2D.CircleCast(origin, radius, dir, filter, buffer, maxDistance);
            float best = maxDistance;
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i];
                if (h.collider == self || !IsSolid(h.collider, true)) continue;
                if (h.distance < best) best = h.distance;
            }
            return best;
        }
    }
}
