using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Steers an enemy to its target around obstacles ("context steering"), no pathfinding grid needed:
    ///   - Straight line to the target is clear -> go straight.
    ///   - Blocked -> try 16 directions (circle cast LookAhead ahead) and score them: close to the target direction + clear path + keep the old heading
    ///     + hug one side of the obstacle (no left/right dithering) + avoid squeezing into other enemies. Take the highest score.
    ///   - Tracks real progress (FixedUpdate): pushing without moving for ~0.5 s = STUCK -> switch the hug side and re-pick at once.
    /// One instance per enemy (created by EnemyAI). Re-picks the direction every 0.1 s to stay cheap.
    /// </summary>
    public sealed class EnemySteering
    {
        private const int Directions = 16;
        private const float LookAhead = 0.7f;        // Unity units (~2 tiles)
        private const float Reevaluate = 0.1f;
        private const float StuckTime = 0.5f;
        private const float SeparationRadius = 0.4f;

        private readonly Rigidbody2D rb;
        private readonly Collider2D body;
        private readonly float radius;
        private readonly List<Collider2D> near = new List<Collider2D>();
        private ContactFilter2D allFilter = new ContactFilter2D { useTriggers = false, useLayerMask = false };

        private Vector2 lastDir;
        private float nextEval;
        private int wallSide;                       // +1 / -1: which side we are hugging; 0 = none
        private float wallSideUntil;
        private Vector2 lastPos;
        private float lastExpected, stuckT;

        /// <summary>Recent stuck count (decays while moving freely). EnemyAI uses it to know the target is "unreachable".</summary>
        public int StuckCount { get; private set; }
        public bool IsStuck => stuckT > StuckTime * 0.5f;

        public EnemySteering(Rigidbody2D rb, Collider2D body)
        {
            this.rb = rb;
            this.body = body;
            // cast radius = half the body width (the enemy collider is a flat foot strip), shrunk a little so grazing an edge does not count as blocked
            radius = body != null ? Mathf.Clamp(body.bounds.extents.x * 0.85f, 0.05f, 0.25f) : 0.1f;
            lastPos = rb.position;
        }

        private Vector2 Feet => body != null ? (Vector2)body.bounds.center : rb.position;

        /// <summary>Desired velocity to reach <paramref name="target"/> at <paramref name="speed"/> (zero when there is no way through).</summary>
        public Vector2 Steer(Vector2 target, float speed)
        {
            if (Time.time < nextEval) return lastDir * speed;
            nextEval = Time.time + Reevaluate;

            Vector2 origin = Feet;
            Vector2 to = target - origin;
            float dist = to.magnitude;
            if (dist < 0.02f) { lastDir = Vector2.zero; return Vector2.zero; }
            Vector2 want = to / dist;
            Vector2 sep = Separation(origin);
            if (Time.time >= wallSideUntil) wallSide = 0;

            float ahead = Mathf.Min(LookAhead, dist);
            if (!IsStuck && LineOfSight.Clearance(origin, radius, want, ahead, body) >= ahead - 0.01f)
            {
                lastDir = (want + sep * 0.6f).normalized;
                return lastDir * speed;
            }

            Vector2 side = Vector2.Perpendicular(want);
            Vector2 best = Vector2.zero;
            float bestScore = float.MinValue;
            for (int k = 0; k < Directions; k++)
            {
                Vector2 d = BulletPatterns.Direction(k * 360f / Directions);
                float clear = LineOfSight.Clearance(origin, radius, d, LookAhead, body) / LookAhead;   // 0..1
                if (clear < 0.25f) continue;                                                          // blocked right in front
                float score = Vector2.Dot(d, want) + clear * 0.6f + Vector2.Dot(d, sep) * 0.5f;
                if (lastDir != Vector2.zero) score += Vector2.Dot(d, lastDir) * 0.3f;
                if (wallSide != 0) score += Vector2.Dot(d, side * wallSide) * 0.35f;
                if (score > bestScore) { bestScore = score; best = d; }
            }
            if (best == Vector2.zero) { lastDir = Vector2.zero; return Vector2.zero; }   // fully surrounded: wait
            // had to turn far to go around the obstacle: remember the hug side for 1.5 s
            if (wallSide == 0 && Vector2.Dot(best, want) < 0.7f)
            {
                wallSide = Vector2.Dot(best, side) >= 0f ? 1 : -1;
                wallSideUntil = Time.time + 1.5f;
            }
            lastDir = best;
            return best * speed;
        }

        /// <summary>Call at the start of every FixedUpdate with the velocity planned last step: detects being stuck.</summary>
        public void TrackProgress(Vector2 intendedVelocity, float dt)
        {
            float moved = (rb.position - lastPos).magnitude;
            if (lastExpected > 0.0005f && moved < lastExpected * 0.3f) stuckT += dt;
            else stuckT = Mathf.Max(0f, stuckT - dt * 2f);
            if (stuckT >= StuckTime)
            {
                stuckT = 0f;
                StuckCount++;
                wallSide = wallSide == 0 ? (Random.value < 0.5f ? 1 : -1) : -wallSide;   // this side is blocked: try the other side
                wallSideUntil = Time.time + 2f;
                nextEval = 0f;
            }
            else if (moved >= lastExpected * 0.8f && StuckCount > 0 && Random.value < dt) StuckCount--;   // moving normally: slowly forget
            lastPos = rb.position;
            lastExpected = intendedVelocity.magnitude * dt;
        }

        /// <summary>Gently push away from other enemies that are too close (so they do not clump into one blob).</summary>
        private Vector2 Separation(Vector2 origin)
        {
            near.Clear();
            Physics2D.OverlapCircle(origin, SeparationRadius, allFilter, near);
            Vector2 push = Vector2.zero;
            foreach (var c in near)
            {
                if (c == null || c == body || c.isTrigger || c.GetComponentInParent<EnemyAI>() == null) continue;
                Vector2 away = origin - (Vector2)c.bounds.center;
                float m = away.magnitude;
                if (m < 0.001f) continue;
                push += away / m * (1f - Mathf.Clamp01(m / SeparationRadius));
            }
            return Vector2.ClampMagnitude(push, 1f);
        }
    }
}
