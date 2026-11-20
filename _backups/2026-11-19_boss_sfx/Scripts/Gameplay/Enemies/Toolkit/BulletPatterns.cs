using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Hướng bắn cho các kiểu đạn hay gặp (dùng chung cho quái thường và boss). Chỉ tính HƯỚNG — việc tạo đạn
    /// (prefab, tốc độ, màu) do từng bộ chiêu tự làm.
    ///
    ///   foreach (var d in BulletPatterns.Ring(12, BulletPatterns.RandomRingOffset(12))) Fire(d);   // vòng 12 viên, lệch ngẫu nhiên
    ///   foreach (var d in BulletPatterns.Fan(toPlayer, 5, 60f)) Fire(d);                             // quạt 5 viên rộng 60°
    /// </summary>
    public static class BulletPatterns
    {
        /// <summary>Vector đơn vị theo góc (độ, 0 = phải, ngược chiều kim đồng hồ).</summary>
        public static Vector2 Direction(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>Góc (độ) của một hướng.</summary>
        public static float Angle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        /// <summary>Hướng thứ i của vòng <paramref name="count"/> viên đều nhau, viên 0 ở góc <paramref name="offsetDegrees"/>.</summary>
        public static Vector2 RingDirection(int i, int count, float offsetDegrees = 0f) =>
            Direction(offsetDegrees + i * 360f / Mathf.Max(1, count));

        /// <summary>Cả vòng <paramref name="count"/> hướng đều nhau.</summary>
        public static IEnumerable<Vector2> Ring(int count, float offsetDegrees = 0f)
        {
            for (int i = 0; i < count; i++) yield return RingDirection(i, count, offsetDegrees);
        }

        /// <summary>Lệch ngẫu nhiên trong một khe của vòng (để các vòng liên tiếp không trùng chỗ hở).</summary>
        public static float RandomRingOffset(int count) => Random.Range(0f, 360f / Mathf.Max(1, count));

        /// <summary>Quạt <paramref name="count"/> hướng, rộng <paramref name="spreadDegrees"/>, giữa là <paramref name="aim"/>.</summary>
        public static IEnumerable<Vector2> Fan(Vector2 aim, int count, float spreadDegrees)
        {
            float a0 = Angle(aim);
            if (count <= 1) { yield return Direction(a0); yield break; }
            for (int i = 0; i < count; i++) yield return Direction(a0 - spreadDegrees * 0.5f + spreadDegrees * i / (count - 1));
        }
    }
}
