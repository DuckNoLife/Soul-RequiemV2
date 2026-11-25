using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Fire directions for common bullet patterns (shared by normal enemies and bosses). Only computes DIRECTIONS - spawning the bullets
    /// (prefab, speed, colour) is done by each skill set.
    ///
    ///   foreach (var d in BulletPatterns.Ring(12, BulletPatterns.RandomRingOffset(12))) Fire(d);   // ring of 12, random offset
    ///   foreach (var d in BulletPatterns.Fan(toPlayer, 5, 60f)) Fire(d);                             // fan of 5, 60° wide
    /// </summary>
    public static class BulletPatterns
    {
        /// <summary>Unit vector for an angle (degrees, 0 = right, counter-clockwise).</summary>
        public static Vector2 Direction(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>Angle (degrees) of a direction.</summary>
        public static float Angle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        /// <summary>Direction i of an evenly spaced ring of <paramref name="count"/>, bullet 0 at <paramref name="offsetDegrees"/>.</summary>
        public static Vector2 RingDirection(int i, int count, float offsetDegrees = 0f) =>
            Direction(offsetDegrees + i * 360f / Mathf.Max(1, count));

        /// <summary>The whole ring of <paramref name="count"/> evenly spaced directions.</summary>
        public static IEnumerable<Vector2> Ring(int count, float offsetDegrees = 0f)
        {
            for (int i = 0; i < count; i++) yield return RingDirection(i, count, offsetDegrees);
        }

        /// <summary>Random offset inside one ring gap (so consecutive rings do not leave the same gaps).</summary>
        public static float RandomRingOffset(int count) => Random.Range(0f, 360f / Mathf.Max(1, count));

        /// <summary>Fan of <paramref name="count"/> directions, <paramref name="spreadDegrees"/> wide, centred on <paramref name="aim"/>.</summary>
        public static IEnumerable<Vector2> Fan(Vector2 aim, int count, float spreadDegrees)
        {
            float a0 = Angle(aim);
            if (count <= 1) { yield return Direction(a0); yield break; }
            for (int i = 0; i < count; i++) yield return Direction(a0 - spreadDegrees * 0.5f + spreadDegrees * i / (count - 1));
        }
    }
}
