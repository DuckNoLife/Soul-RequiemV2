using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Per-enemy effects (Map 3): images in Assets/Resources/VFX (made with PixelLab VFX Workbench), played by SpriteVfx.
    /// EnemyAI / LungeSkills / HealerSkills call the functions below at the right attack moment. Leave a field empty = no such effect.
    /// Directional swing images: &lt;swing&gt;_E (flipped on X when facing left), _S (swing down), _N (swing up) - anchored at the CHEST.
    /// Other images are anchored at the FEET (death, spawn, dash, heal) or at the centre (windup, cast, hurt).
    /// </summary>
    public class EnemyVfx : MonoBehaviour
    {
        [Tooltip("Base name of the swing trail, plus _E/_S/_N (e.g. m3_slash_steel, m3_thrust, m3_claw)")]
        public string swing = "";
        [Tooltip("Telegraph flash before the attack (at the start of the swing)")]
        public string windup = "";
        [Tooltip("Play the windup at the feet instead of the chest (worm bursting up)")]
        public bool windupAtFeet = false;
        [Tooltip("Effect on the player when a melee hit lands")]
        public string hurt = "m3_hurt";
        public string dash = "";
        public string cast = "";
        [Tooltip("Effect on an ally being healed (Monk)")]
        public string heal = "";
        public string death = "";
        [Tooltip("Effect on appear (zombie crawling out of a grave)")]
        public string spawn = "";
        [Tooltip("VFX image scale (0.7 = same pixel density as the PixelLab characters)")]
        public float scale = 0.7f;
        [Tooltip("Chest height above the feet (Unity units)")]
        public float chestY = 0.34f;

        private SpriteRenderer sr;

        private void Awake() { sr = GetComponentInChildren<SpriteRenderer>(); }

        private int Layer => sr != null ? sr.sortingLayerID : 0;
        private int Order => sr != null ? sr.sortingOrder : 2;
        private Vector2 Feet => transform.position;
        private Vector2 Chest => (Vector2)transform.position + Vector2.up * chestY;

        private static bool Has(string s) => !string.IsNullOrEmpty(s);

        public void Spawned()
        {
            if (Has(spawn)) SpriteVfx.Spawn(spawn, Feet, scale, Layer, Order - 1, false, 12f);
        }

        public void Windup(Vector2 dir)
        {
            if (!Has(windup)) return;
            if (windupAtFeet) { SpriteVfx.Spawn(windup, Feet, scale, Layer, Order - 1, false, 12f); return; }
            Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            SpriteVfx.Spawn(windup, Chest + d * 0.14f + Vector2.up * 0.05f, scale, Layer, Order + 2, false, 14f);
        }

        /// Swing trail toward the player on the dominant axis (vertical when |y| > 1.3|x|, same rule EnemyAI uses to pick a facing).
        public void Swing(Vector2 dir)
        {
            // the swing sound moved to SfxHooks.EnemyState (every enemy, by voice family)
            if (!Has(swing)) return;
            bool vertical = Mathf.Abs(dir.y) > Mathf.Abs(dir.x) * 1.3f;
            string suf = vertical ? (dir.y < 0f ? "_S" : "_N") : "_E";
            int order = vertical && dir.y > 0f ? Order - 1 : Order + 1;      // swing up (back turned): trail on the far side, behind the body
            SpriteVfx.Spawn(swing + suf, Chest, scale, Layer, order, !vertical && dir.x < 0f, 16f);
        }

        public void HurtPlayer(Vector2 playerFeet)
        {
            if (Has(hurt)) SpriteVfx.Spawn(hurt, playerFeet + Vector2.up * 0.3f, scale, Layer, Order + 3, false, 18f);
        }

        public void Dash(Vector2 dir)
        {
            if (Has(dash)) SpriteVfx.Spawn(dash, Feet, scale, Layer, Order - 1, dir.x < 0f, 14f);
        }

        public void Cast()
        {
            if (Has(cast)) SpriteVfx.Spawn(cast, Chest + Vector2.up * 0.04f, scale, Layer, Order + 2, false, 14f);
        }

        public void HealOn(Transform target)
        {
            if (!Has(heal) || target == null) return;
            var tsr = target.GetComponentInChildren<SpriteRenderer>();
            int o = tsr != null ? tsr.sortingOrder : Order;
            var v = SpriteVfx.Spawn(heal, target.position, scale, Layer, o - 1, false, 14f);
            if (v != null) v.Follow(target);
        }

        public void Death()
        {
            if (Has(death)) SpriteVfx.Spawn(death, Feet, scale, Layer, Order + 1, false, 12f);
        }
    }
}
