using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Grasping Roots" — "Mantras of the Vessel" set (Specimen Zero relic), slot I. Ren stomps: violet flesh roots (Specimen Zero's own
    /// PixelLab root art, Resources/VFX/specimen/roots{a..d}_*) erupt one after another along a line <see cref="length"/> u long toward
    /// the aim (walls stop it). Each enemy on the line: <see cref="damage"/> + rooted (stunned) <see cref="rootTime"/> s.
    /// COMBO OPENER: every enemy hit by the roots (bosses too) carries the BOUND mark for <see cref="boundTime"/> s (SkillKit.Bind, PixelLab sk_bound)
    /// — Hand of the Vessel hits Bound targets harder. Normal enemies are still rooted <see cref="rootTime"/> s.
    /// Balance: 30 / 9 s ≈ 3.3 DPS per enemy + roots a whole line + opens the Hand combo (+50% of 75).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Grasping Roots", fileName = "VesselRoots")]
    public class GraspingRootsSkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Line - Control";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Length", Tiles(length));
            Stat(o, "Rooted", Secs(rootTime));
            Stat(o, "Bound", Secs(boundTime));
        }

        public float length = 2.4f;
        public float spacing = 0.36f;
        public float stepDelay = 0.06f;
        [Tooltip("Seconds from the press until the first root erupts (the stomp). Rolling before that = cancel, cooldown refunded.")]
        public float startDelay = 0.28f;
        public float damage = 24f;
        public float hitRadius = 0.36f;
        public float rootTime = 1.4f;
        [Tooltip("Duration of the Bound mark (bosses too).")]
        public float boundTime = 2f;
        public float castLock = 0.5f;
        public float rootScale = 0.75f;
        public Color rootTint = new Color(0.86f, 0.62f, 1f);

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            Vector2 dir = SkillKit.AimDir(p, p.Facing.sqrMagnitude > 0.01f ? p.Facing : Vector2.down);
            p.SetFacing(dir);
            p.LockActions(castLock);
            if (p.PlaySkillAnimation() && startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            Vector2 feet = p.transform.position;
            float len = SkillKit.WallDistance(feet, dir, length, 0.08f);
            int layer = p.VfxLayer, order = p.PlayerOrder;
            var kinds = new[] { SkillKit.Frames("VFX/specimen/rootsa_", 8), SkillKit.Frames("VFX/specimen/rootsb_", 8),
                                SkillKit.Frames("VFX/specimen/rootsc_", 8), SkillKit.Frames("VFX/specimen/rootsd_", 8) };
            var done = new HashSet<EnemyAI>();
            Vector2 side = new Vector2(-dir.y, dir.x);
            int k = 0;
            p.KickCamera(Vector2.down, 0.7f, 0.15f);
            for (float d = spacing * 0.9f; d <= len + 0.01f; d += spacing, k++)
            {
                Vector2 at = feet + dir * d + side * ((k % 2 == 0 ? 1f : -1f) * 0.06f);
                var fr = kinds[k % kinds.Length];
                if (fr.Length > 0) p.StartCoroutine(SkillKit.PlayFrames(fr, at, rootScale, 16f, rootTint, layer, order + 1, k % 2 == 1));
                foreach (var ai in SkillKit.EnemiesIn(at, hitRadius))
                {
                    if (!done.Add(ai)) continue;
                    if (!ai.Health.TakeDamage(damage, dir, 0.3f)) continue;
                    SkillKit.HitFx(p, ai, dir);
                    SkillKit.Control(ai, rootTime, 0f, 0f, layer, order);
                    SkillKit.Bind(ai, boundTime, layer, order);
                }
                PlayerCombat.RaiseStruck(at, hitRadius);
                yield return new WaitForSeconds(stepDelay);
            }
            if (done.Count > 0) p.HitStopNow();
        }
    }
}
