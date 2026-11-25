using System;
using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Seeker Embers" — Mantras of the Eye set, slot I: <see cref="maxEmbers"/> blue Ether orbs circle Ren for <see cref="duration"/> seconds.
    /// An orb touching an enemy: damages it + small burst (damage + Burn to nearby enemies), then the orb is gone.
    /// Recasting while orbs remain: refills to <see cref="maxEmbers"/> and refreshes the timer (no stacking). Handled by <see cref="EmberOrbit"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Seeker Embers", fileName = "EyeOrbs")]
    public class SeekerEmbersSkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Summon - Burn";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Embers", maxEmbers.ToString());
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Touch damage", hitDamage.ToString("0"));
            Stat(o, "Burst", blastDamage.ToString("0") + " in " + Tiles(blastRadius));
            Stat(o, "Burn", burnDps.ToString("0.#") + " /s for " + Secs(burnTime));
        }

        [Tooltip("Max orbs around the body (no stacking).")]
        public int maxEmbers = 5;
        [Tooltip("Seconds the orbs last.")]
        public float duration = 10f;
        [Tooltip("Orbit radius around the feet (Unity units; 1 tile = 0.32).")]
        public float orbitRadius = 0.55f;
        [Tooltip("Degrees / second.")]
        public float spinSpeed = 200f;
        [Tooltip("How far above the ground the orbs are drawn (at body height).")]
        public float lift = 0.2f;
        [Tooltip("Enemy contact radius of each orb.")]
        public float contactRadius = 0.15f;
        [Tooltip("Damage to the enemy an orb touches.")]
        public float hitDamage = 8f;
        [Tooltip("Burst damage to every enemy within the burst radius (the touched one included).")]
        public float blastDamage = 6f;
        public float blastRadius = 0.6f;
        public float burnDps = 4f;
        public float burnTime = 3f;
        [Tooltip("Seconds standing still while summoning the orbs.")]
        public float castLock = 0.35f;
        [Tooltip("Seconds from the press until the orbs appear (the hand raise in the animation). Rolling before that = cancel, cooldown refunded.")]
        public float spawnDelay = 0.3f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(castLock);
            if (p.PlayEmbersCast() && spawnDelay > 0f)
            {
                yield return new WaitForSeconds(spawnDelay);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            EmberOrbit.For(p).Refill(this);
        }
    }
}
