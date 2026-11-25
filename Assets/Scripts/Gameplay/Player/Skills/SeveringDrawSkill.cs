using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Severing Draw" — single skill, found in the Archive (Map 2). Ren dashes <see cref="distance"/> u toward the aim (walls stop him),
    /// invulnerable during the dash, drawing his sword to cut EVERY enemy on the path (<see cref="damage"/> each, once). At the end he locks for
    /// <see cref="recover"/> s (sheathing) — dashing into the middle of a pack has a price. The PixelLab sk_draw slash trail stretches exactly along the dash.
    /// Balance: 32 / 6 s ≈ 5.3 DPS per enemy + a dodge that deals damage; does not replace the Roll (0.7 s cooldown).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Severing Draw", fileName = "SeveringDraw")]
    public class SeveringDrawSkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Dash - Strike";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Dash distance", Tiles(distance));
            Stat(o, "Untouchable", Secs(dashTime) + " mid-dash");
            Stat(o, "Knockback", knockback.ToString("0.#"));
            Stat(o, "Sheathe pause", Secs(recover));
        }

        [Tooltip("Max dash distance (Unity units; 1 tile = 0.32).")]
        public float distance = 1.6f;
        public float dashTime = 0.16f;
        public float damage = 32f;
        [Tooltip("Width of the slash path.")]
        public float hitWidth = 0.5f;
        public float knockback = 1.2f;
        [Tooltip("Lock at the end of the path (sheathing).")]
        public float recover = 0.3f;
        [Tooltip("Ren's body radius for the wall check.")]
        public float bodyRadius = 0.13f;
        public string vfx = "sk_draw";
        [Tooltip("Length of the slash trail in the VFX image (px, PPU 100) — to stretch it exactly over the dash.")]
        public float vfxLength = 113f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            p.BeginAction();
            Vector2 dir = SkillKit.AimDir(p, p.Facing.sqrMagnitude > 0.01f ? p.Facing : Vector2.down);
            Vector2 from = p.transform.position;
            float len = Mathf.Max(0f, SkillKit.WallDistance(from, dir, distance, bodyRadius) - 0.02f);
            p.SetFacing(dir);
            p.PlayDash(dir);
            p.LockActions(dashTime + recover);
            p.Health.Shield(dashTime + 0.08f);

            var targets = SkillKit.EnemiesAlong(from, dir, len + 0.1f, hitWidth);
            var done = new HashSet<EnemyAI>();
            var rb = p.GetComponent<Rigidbody2D>();
            bool sim = rb == null || rb.simulated;
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }

            if (p.showVfx)
            {
                var v = SpriteVfx.Spawn(vfx, from + new Vector2(0f, 0.18f), 1f, p.VfxLayer, p.PlayerOrder + 2, false, 22f);
                if (v != null)
                {
                    v.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    v.transform.localScale = new Vector3(Mathf.Max(0.35f, (len + 0.2f) / (vfxLength / 100f)), 0.9f, 1f);
                }
            }

            int hits = 0;
            for (float t = 0f; t < dashTime; t += Time.deltaTime)
            {
                float u = 1f - (1f - t / dashTime) * (1f - t / dashTime);
                Vector2 pos = from + dir * len * u;
                p.transform.position = pos;
                if (rb != null) rb.position = pos;
                hits += Strike(p, targets, done, len * u + 0.15f, dir);
                yield return null;
            }
            p.transform.position = from + dir * len;
            if (rb != null) { rb.position = from + dir * len; rb.simulated = sim; }
            hits += Strike(p, targets, done, len + 0.3f, dir);
            PlayerCombat.RaiseStruck(from + dir * len * 0.5f, len * 0.5f + 0.2f);   // mechanisms (lamps, barrels...) on the slash path
            p.PlaySlashPose(dir);                                   // slash pose (combo hit 1)
            p.KickCamera(dir, 0.9f, 0.16f);
            if (hits > 0) p.HitStopNow();
        }

        private int Strike(PlayerCombat p, List<KeyValuePair<EnemyAI, float>> targets, HashSet<EnemyAI> done, float reached, Vector2 dir)
        {
            int n = 0;
            foreach (var kv in targets)
            {
                var ai = kv.Key;
                if (kv.Value > reached || ai == null || ai.Health == null || ai.Health.IsDead || !done.Add(ai)) continue;
                Vector2 side = new Vector2(-dir.y, dir.x);
                if (Vector2.Dot((Vector2)ai.transform.position - (Vector2)p.transform.position, side) < 0f) side = -side;
                if (ai.Health.TakeDamage(damage, side, knockback)) { SkillKit.HitFx(p, ai, dir); n++; }
            }
            return n;
        }
    }
}
