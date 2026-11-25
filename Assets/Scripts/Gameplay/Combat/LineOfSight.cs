using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Line of sight / obstacles for combat and movement.
    ///   "Obstacle" = a collider that is NOT a trigger and does NOT belong to a character (player, enemy — has Health) or a bullet:
    ///   rocks, walls, gates, props with collision, map edges (layers "obstacle colliders", "blockers", "map bounds", props...).
    ///   Water (layer Water) blocks WALKING but NOT sight / attacks.
    ///   An obstacle enclosing one of the two ends (a boss standing in a pit) does not count as blocking between them.
    /// </summary>
    public static class LineOfSight
    {
        private static readonly RaycastHit2D[] buffer = new RaycastHit2D[24];
        private static readonly Dictionary<int, bool> environment = new Dictionary<int, bool>();
        private static ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
        private static int waterLayer = -2;

        private static int WaterLayer => waterLayer == -2 ? (waterLayer = LayerMask.NameToLayer("Water")) : waterLayer;

        /// <summary>Is this collider an obstacle. <paramref name="includeWater"/> = also count water (for walking).</summary>
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

        /// <summary>The segment from → to is blocked by an obstacle (water not counted).</summary>
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
                // A collider enclosing one endpoint itself (e.g. the Plague Eye pit around the boss) is not a wall
                // BETWEEN the two -> skip it, otherwise every attack on a boss standing in a pit would be cancelled.
                if (c.OverlapPoint(from) || c.OverlapPoint(to)) continue;
                return true;
            }
            return false;
        }

        /// <summary>From <paramref name="from"/> the body of <paramref name="target"/> is visible (closest point OR collider centre). target null = visible.</summary>
        public static bool CanSee(Vector2 from, Collider2D target)
        {
            if (target == null) return true;
            if (!Blocked(from, target.ClosestPoint(from))) return true;
            return !Blocked(from, target.bounds.center);
        }

        /// <summary>
        /// Moving along <paramref name="dir"/> (normalized) with a body of radius <paramref name="radius"/>: how far until hitting an obstacle
        /// (water included). Returns <paramref name="maxDistance"/> if clear.
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
