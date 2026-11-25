using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Reflects every hostile bullet inside a circle, whatever system fired it:
    ///   Projectile (ranged enemies, traps), HallBullet (Absalom / the Vessel / the Choir acolytes), DuoBullet (Reliquary duo).
    /// A reflected bullet flies straight back the way it came (or outward when it had no direction), is `damageMul` times stronger
    /// and only hits enemies. Bullets that are already reflected, harmless, held in a time stop or thrown by Ren are skipped.
    /// Used by: SanctuaryBellSkill (every frame while the dome stands).
    /// </summary>
    public static class BulletWard
    {
        /// <summary>Reflect the bullets inside (c, r). `onReflect` gets each bullet's position (for the hit flash). Returns how many were reflected.</summary>
        public static int Reflect(Vector2 c, float r, float damageMul, Action<Vector2> onReflect = null)
        {
            int n = 0;
            float rr = r * r;
            for (int i = Projectile.Live.Count - 1; i >= 0; i--)
            {
                var b = Projectile.Live[i];
                if (b == null || b.Reflected || b.Exploding) continue;
                Vector2 at = b.transform.position;
                if ((at - c).sqrMagnitude > rr) continue;
                b.Reflect(Back(b.Direction, at, c), damageMul);
                onReflect?.Invoke(at); n++;
            }
            for (int i = HallBullet.Live.Count - 1; i >= 0; i--)
            {
                var b = HallBullet.Live[i];
                if (b == null || b.Reflected || b.harmless || b.held) continue;
                Vector2 at = b.transform.position;
                if ((at - c).sqrMagnitude > rr) continue;
                b.Reflect(Back(b.velocity, at, c) * Mathf.Max(2.5f, b.velocity.magnitude) * 1.4f, damageMul);
                onReflect?.Invoke(at); n++;
            }
            for (int i = DuoBullet.Live.Count - 1; i >= 0; i--)
            {
                var b = DuoBullet.Live[i];
                if (b == null || b.Reflected || b.hitEnemy != null) continue;
                Vector2 at = b.transform.position;
                if ((at - c).sqrMagnitude > rr) continue;
                Vector2 v = b.path != null ? Vector2.zero : b.velocity;
                b.Reflect(Back(v, at, c) * Mathf.Max(3f, v.magnitude) * 1.4f, damageMul);
                onReflect?.Invoke(at); n++;
            }
            return n;
        }

        private static Vector2 Back(Vector2 incoming, Vector2 at, Vector2 c)
        {
            if (incoming.sqrMagnitude > 0.0001f) return -incoming.normalized;
            Vector2 o = at - c;
            return o.sqrMagnitude > 0.0001f ? o.normalized : Vector2.up;
        }
    }
}
