using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Ember ring of the Mantra "Seeker Embers": up to <c>max</c> blue Ether orbs circle the player for <c>duration</c> seconds.
    /// An orb touching an enemy -> damages it + small burst (damage + Burn to nearby enemies), the orb is used up. Bursts = orbs remaining.
    /// Recasting: refills to <c>max</c> orbs and refreshes the timer (no stacking).
    /// Collision is checked at GROUND level (enemy colliders are foot strips); the orbs are drawn a little higher, at body height.
    /// Attached to the player automatically (<see cref="For"/>).
    /// </summary>
    public class EmberOrbit : MonoBehaviour
    {
        private SeekerEmbersSkill def;
        private PlayerCombat owner;
        private readonly SpriteRenderer[] orbs = new SpriteRenderer[8];
        private int count;
        private float endTime, angle;
        private readonly List<Collider2D> found = new List<Collider2D>();
        private readonly HashSet<Health> done = new HashSet<Health>();

        public int Alive { get { int n = 0; for (int i = 0; i < count; i++) if (orbs[i] != null) n++; return n; } }

        public static EmberOrbit For(PlayerCombat p)
        {
            var o = p.GetComponent<EmberOrbit>();
            if (o == null) o = p.gameObject.AddComponent<EmberOrbit>();
            o.owner = p;
            return o;
        }

        /// <summary>Refill the orbs, refresh the timer.</summary>
        public void Refill(SeekerEmbersSkill skill)
        {
            def = skill;
            int max = Mathf.Clamp(skill.maxEmbers, 1, orbs.Length);
            if (count != max) { Clear(); count = max; }
            for (int i = 0; i < count; i++)
            {
                if (orbs[i] != null) continue;
                var r = EtherVfx.Spawn("Ember", EtherVfx.Orb, transform.position, EtherVfx.Blue, owner.VfxLayer, owner.PlayerOrder + 3);
                var core = EtherVfx.Spawn("Core", EtherVfx.Orb, transform.position, EtherVfx.Core, owner.VfxLayer, owner.PlayerOrder + 4, r.transform);
                core.transform.localScale = Vector3.one * 0.45f;
                orbs[i] = r;
            }
            endTime = Time.time + skill.duration;
        }

        private void Update()
        {
            if (def == null || count == 0) return;
            if (owner == null || owner.Health == null || owner.Health.IsDead || Time.time >= endTime) { Clear(); return; }

            angle += def.spinSpeed * Time.deltaTime;
            Vector2 feet = transform.position;
            float left = endTime - Time.time;
            for (int i = 0; i < count; i++)
            {
                var r = orbs[i];
                if (r == null) continue;
                float a = (angle + i * 360f / count) * Mathf.Deg2Rad;
                Vector2 ground = feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * def.orbitRadius;   // slightly flattened ring (angled view)
                r.transform.position = new Vector3(ground.x, ground.y + def.lift, 0f);
                // back half (above the body) draws behind, front half in front
                bool front = Mathf.Sin(a) < 0f;
                r.sortingOrder = owner.PlayerOrder + (front ? 3 : -1);
                var core = r.transform.childCount > 0 ? r.transform.GetChild(0).GetComponent<SpriteRenderer>() : null;
                if (core != null) core.sortingOrder = r.sortingOrder + 1;
                float pulse = 0.6f + 0.06f * Mathf.Sin(Time.time * 25f + i);
                if (left < 1f) pulse *= 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 16f));   // about to expire -> blink
                r.transform.localScale = Vector3.one * pulse;

                var hit = Touching(ground);
                if (hit != null) { Burst(ground, hit); Destroy(r.gameObject); orbs[i] = null; }
            }
            if (Alive == 0) Clear();
        }

        private EnemyAI Touching(Vector2 p)
        {
            found.Clear();
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            Physics2D.OverlapCircle(p, def.contactRadius, filter, found);
            foreach (var c in found)
            {
                var ai = c.GetComponentInParent<EnemyAI>();
                if (ai != null && ai.Health != null && !ai.Health.IsDead) return ai;
            }
            return null;
        }

        // Small burst: the touched enemy takes a hit + every enemy within blastRadius (visible from the burst centre) takes burst damage + Burn
        private void Burst(Vector2 at, EnemyAI first)
        {
            Vector2 d0 = (Vector2)first.transform.position - (Vector2)transform.position;
            first.Health.TakeDamage(def.hitDamage, d0, 0.5f);
            done.Clear();
            found.Clear();
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            Physics2D.OverlapCircle(at, def.blastRadius, filter, found);
            foreach (var c in found)
            {
                var ai = c.GetComponentInParent<EnemyAI>();
                if (ai == null || ai.Health == null || ai.Health.IsDead || done.Contains(ai.Health)) continue;
                if (!LineOfSight.CanSee(at, c)) continue;
                done.Add(ai.Health);
                Vector2 d = (Vector2)ai.transform.position - at;
                ai.Health.TakeDamage(def.blastDamage, d, 0.8f);
                ai.Health.ApplyBurn(def.burnDps, def.burnTime);
            }
            PlayerCombat.RaiseStruck(at, def.blastRadius);
            Vector2 fx = at + new Vector2(0f, def.lift);
            SpriteVfx.Spawn("hit", fx, 0.6f, owner.VfxLayer, owner.PlayerOrder + 5, d0.x < 0f);
            var ring = EtherVfx.Spawn("Ember Burst", EtherVfx.Ring, fx, EtherVfx.Blue, owner.VfxLayer, owner.PlayerOrder + 4);
            StartCoroutine(Expand(ring, def.blastRadius * 2f / 0.64f));
        }

        private static System.Collections.IEnumerator Expand(SpriteRenderer r, float toScale)
        {
            float t = 0f;
            while (r != null && t < 0.25f)
            {
                t += Time.deltaTime;
                float k = t / 0.25f;
                r.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, toScale, k);
                Color c = EtherVfx.Blue; c.a = 1f - k; r.color = c;
                yield return null;
            }
            if (r != null) Destroy(r.gameObject);
        }

        private void Clear()
        {
            for (int i = 0; i < orbs.Length; i++) { if (orbs[i] != null) Destroy(orbs[i].gameObject); orbs[i] = null; }
            count = 0;
        }

        private void OnDisable() => Clear();
    }
}
