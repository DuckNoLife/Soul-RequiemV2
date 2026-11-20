using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Lái quái tới đích mà né vật cản ("context steering"), không cần lưới tìm đường:
    ///   - Đường thẳng tới đích thông -> đi thẳng.
    ///   - Bị chắn -> thử 16 hướng (thân tròn quét trước LookAhead), chấm điểm: gần hướng đích + đường trống + giữ hướng cũ
    ///     + bám một phía mép vật cản (không lưỡng lự trái/phải) + tránh chen vào quái khác. Chọn hướng điểm cao nhất.
    ///   - Theo dõi tiến độ thật (FixedUpdate): đẩy mà không nhúc nhích ~0,5 s = KẸT -> đổi phía bám mép, chọn lại hướng ngay.
    /// Mỗi quái một bộ (EnemyAI tạo). Tính lại hướng mỗi 0,1 s cho nhẹ máy.
    /// </summary>
    public sealed class EnemySteering
    {
        private const int Directions = 16;
        private const float LookAhead = 0.7f;        // đơn vị Unity (~2 ô)
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
        private int wallSide;                       // +1 / -1: đang bám mép phía nào; 0 = chưa
        private float wallSideUntil;
        private Vector2 lastPos;
        private float lastExpected, stuckT;

        /// <summary>Số lần bị kẹt gần đây (giảm dần khi đi lại được). EnemyAI dùng để biết "không tới được".</summary>
        public int StuckCount { get; private set; }
        public bool IsStuck => stuckT > StuckTime * 0.5f;

        public EnemySteering(Rigidbody2D rb, Collider2D body)
        {
            this.rb = rb;
            this.body = body;
            // bán kính quét = nửa bề ngang thân (collider quái là vệt chân nằm ngang), thu nhỏ chút để mép chạm không tính là chắn
            radius = body != null ? Mathf.Clamp(body.bounds.extents.x * 0.85f, 0.05f, 0.25f) : 0.1f;
            lastPos = rb.position;
        }

        private Vector2 Feet => body != null ? (Vector2)body.bounds.center : rb.position;

        /// <summary>Vận tốc mong muốn để tới <paramref name="target"/> với tốc độ <paramref name="speed"/> (0 nếu không có lối).</summary>
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
                if (clear < 0.25f) continue;                                                          // chắn ngay trước mặt
                float score = Vector2.Dot(d, want) + clear * 0.6f + Vector2.Dot(d, sep) * 0.5f;
                if (lastDir != Vector2.zero) score += Vector2.Dot(d, lastDir) * 0.3f;
                if (wallSide != 0) score += Vector2.Dot(d, side * wallSide) * 0.35f;
                if (score > bestScore) { bestScore = score; best = d; }
            }
            if (best == Vector2.zero) { lastDir = Vector2.zero; return Vector2.zero; }   // bị vây kín: đứng chờ
            // phải đi chệch nhiều để vòng qua vật cản: nhớ phía đang bám trong 1,5 s
            if (wallSide == 0 && Vector2.Dot(best, want) < 0.7f)
            {
                wallSide = Vector2.Dot(best, side) >= 0f ? 1 : -1;
                wallSideUntil = Time.time + 1.5f;
            }
            lastDir = best;
            return best * speed;
        }

        /// <summary>Gọi đầu mỗi FixedUpdate với vận tốc dự định của bước trước: phát hiện kẹt.</summary>
        public void TrackProgress(Vector2 intendedVelocity, float dt)
        {
            float moved = (rb.position - lastPos).magnitude;
            if (lastExpected > 0.0005f && moved < lastExpected * 0.3f) stuckT += dt;
            else stuckT = Mathf.Max(0f, stuckT - dt * 2f);
            if (stuckT >= StuckTime)
            {
                stuckT = 0f;
                StuckCount++;
                wallSide = wallSide == 0 ? (Random.value < 0.5f ? 1 : -1) : -wallSide;   // phía này không qua được: thử phía kia
                wallSideUntil = Time.time + 2f;
                nextEval = 0f;
            }
            else if (moved >= lastExpected * 0.8f && StuckCount > 0 && Random.value < dt) StuckCount--;   // đi lại bình thường: quên dần
            lastPos = rb.position;
            lastExpected = intendedVelocity.magnitude * dt;
        }

        /// <summary>Đẩy nhẹ ra khỏi quái khác đứng quá sát (không chen chúc thành một cục).</summary>
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
