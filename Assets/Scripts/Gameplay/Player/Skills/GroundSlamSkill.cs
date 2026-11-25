using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Earthshatter" — Ren's ground stomp skill:
    /// Stomp animation; on impact deals damage + knockback around Ren, VFX ground_impact, camera kick, hit-stop on hit.
    /// Map mechanisms still listen to PlayerCombat.SkillCast (burning Ichor...).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Ground Slam", fileName = "GroundSlam")]
    public class GroundSlamSkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Area - Melee";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Radius", Tiles(radius));
            Stat(o, "Knockback", knockback.ToString("0.#"));
            Stat(o, "Strikes after", Secs(impactTime));
            Stat(o, "Rooted for", Secs(lockTime));
        }

        public float damage = 40f;
        public float radius = 1.1f;
        public float knockback = 3f;
        [Tooltip("Seconds from the press to the impact frame (frame 4 of the Stomp animation).")]
        public float impactTime = 0.31f;
        [Tooltip("Seconds standing still while stomping.")]
        public float lockTime = 0.5f;
        public string vfx = "ground_impact";
        [Tooltip("VFX scale; 1.4 gives a wave ring of ~1.1 radius.")]
        public float vfxScale = 1.4f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            if (p.PlaySkillAnimation())
            {
                p.LockActions(lockTime);
                yield return new WaitForSeconds(impactTime);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            Vector2 c = p.transform.position;
            int n = p.HitEnemies(c, radius, damage, knockback);
            p.RaiseSkillCast(c, radius);
            // the shockwave spreads from the SWORD TIP planted in the ground (stomp frame 3), not from between the feet
            p.SpawnGroundVfx(vfx, c + BladeTip(p), vfxScale * radius / 1.1f, new Color(0.6f, 0.85f, 1f, 0.5f), radius);
            p.KickCamera(Vector2.down, 1.4f, 0.22f);
            if (n > 0) p.HitStopNow();
        }

        // sword tip position relative to the feet (px on stomp frame 3, measured automatically; +x right, +y down) for 8 directions
        // order E, NE, N, NW, W, SW, S, SE (angles 0°, 45°, ... like PlayerMelee)
        private static readonly Vector2[] TipPx =
        {
            new Vector2(29, 2), new Vector2(18, -10), new Vector2(0, -8), new Vector2(-8, -7),
            new Vector2(-24, 2), new Vector2(-19, 6), new Vector2(5, 2), new Vector2(24, 5),
        };

        private static Vector2 BladeTip(PlayerCombat p)
        {
            Vector2 f = p.Facing.sqrMagnitude > 0.0001f ? p.Facing : Vector2.down;
            int i = ((Mathf.RoundToInt(Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg / 45f) % 8) + 8) % 8;
            var sr = p.GetComponentInChildren<SpriteRenderer>();
            float k = (sr != null ? Mathf.Abs(sr.transform.lossyScale.x) : 1f) / 100f;   // Ren sprite 100 px / unit
            return new Vector2(TipPx[i].x, -TipPx[i].y) * k;
        }
    }
}
