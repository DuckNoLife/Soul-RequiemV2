using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Deals damage to enemies + hit feel (short hit-stop, camera kick) + hit / ground VFX.
    /// Shared by the slash (<see cref="PlayerMelee"/>) and the equippable skills (SkillDefinition through the PlayerCombat API).
    /// </summary>
    internal sealed class PlayerStrikes
    {
        private readonly PlayerCombat owner;
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
        private readonly ContactFilter2D triggerFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
        private CameraFollow camFollow;
        private static bool hitStopping;

        public PlayerStrikes(PlayerCombat owner) { this.owner = owner; }

        // ------------------------------------------------------------------ damage
        /// <summary>Every enemy with a collider in the circle (and accepted by <paramref name="accept"/>) takes damage; returns the number hit.</summary>
        public int Damage(Vector2 center, float radius, float dmg, float knock, Func<Collider2D, bool> accept, bool melee = false, bool finisher = false)
        {
            hits.Clear();
            Physics2D.OverlapCircle(center, radius, filter, hits);
            var done = new HashSet<Health>();
            int landedCount = 0;
            foreach (var c in hits)
            {
                if (accept != null && !accept(c)) continue;
                var ai = c.GetComponentInParent<EnemyAI>();
                if (ai == null || ai.Health == null || done.Contains(ai.Health)) continue;
                // rock / wall / prop between the player's FEET and the enemy -> no slashing through (feet, because the hip may be inside a rock collider)
                if (!LineOfSight.CanSee(owner.transform.position, c)) continue;
                done.Add(ai.Health);                                                          // each enemy checked once per hit
                Vector2 dir = (Vector2)ai.transform.position - (Vector2)owner.transform.position;
                float hpBefore = ai.Health.Current;
                bool landed = ai.Health.TakeDamage(dmg, dir, knock);
                // only counts as a HIT when HP is really lost. A touch on an invulnerable enemy (turtle shell, stunned on the floor, fleeing...)
                // must not spray blood + hit-stop + camera kick -> the player would see "hit but no damage".
                if (owner.debugHitbox && !landed)
                    Debug.Log($"[Strike] {ai.name}: touched but took NO hit (invulnerable={ai.Health.Invulnerable}, dead={ai.Health.IsDead}, HP {hpBefore:0.#}/{ai.Health.Max:0})");
                if (!landed) continue;
                landedCount++;
                if (melee && !ai.Health.IsDead) WeaponTraits.ApplyHitStatus(owner, ai, finisher);   // bleed / burn / slow / mark
                if (melee && ai.Health.IsDead) WeaponTraits.OnMeleeKill(owner);                   // Hunger
                // hit VFX on the enemy body (enemy colliders are only foot strips -> lift toward the middle of the sprite);
                // blood sprays along the hit direction (the source image sprays to the right)
                if (owner.showVfx)
                {
                    Vector2 p = c.bounds.center;
                    var body = ai.GetComponentInChildren<SpriteRenderer>();
                    if (body != null) p.y = Mathf.Lerp(p.y, body.bounds.center.y, 0.6f);
                    SpriteVfx.Spawn("hit", p, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder + 2, dir.x < 0f);
                }
            }
            // strikable objects that are NOT enemies (ink creatures drawn by The Registrar...): TRIGGER collider + IStrikeTarget
            hits.Clear();
            Physics2D.OverlapCircle(center, radius, triggerFilter, hits);
            var doneTargets = new HashSet<IStrikeTarget>();
            foreach (var c in hits)
            {
                if (c == null || !c.isTrigger) continue;
                var t = c.GetComponentInParent<IStrikeTarget>();
                if (t == null || doneTargets.Contains(t)) continue;
                if (accept != null && !accept(c)) continue;
                doneTargets.Add(t);
                Vector2 tdir = (Vector2)c.bounds.center - (Vector2)owner.transform.position;
                if (!t.TakeStrike(dmg, tdir)) continue;
                landedCount++;
                if (owner.showVfx) SpriteVfx.Spawn("hit", c.bounds.center, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder + 2, tdir.x < 0f);
            }
            return landedCount;                                                               // only hits that really dealt damage
        }

        // ------------------------------------------------------------------ hit feel
        /// <summary>Slash landed: hit-stop + camera kick along the slash.</summary>
        public void Impact(Vector2 d) => Impact(d, 1f);

        /// <summary>Hit with a force factor (the final combo cleave has a stronger hit-stop / camera kick).</summary>
        public void Impact(Vector2 d, float mul)
        {
            if (owner.hitStop > 0f) owner.StartCoroutine(HitStopRoutine(owner.hitStop * Mathf.Max(0.2f, mul)));
            Kick(d, Mathf.Max(0.2f, mul), 0.16f);
        }

        /// <summary>Camera kick (multiplies the Inspector cameraKick and the screen shake setting).</summary>
        public void Kick(Vector2 dir, float mul, float duration)
        {
            if (owner.cameraKick <= 0f) return;
            if (camFollow == null && Camera.main != null) camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow != null) camFollow.Kick(dir, owner.cameraKick * mul, duration);
        }

        /// <summary>Very short hit-stop (hitStop seconds of real time). Never stacks on pause / another hit-stop.</summary>
        public void HitStop()
        {
            if (owner.hitStop > 0f) owner.StartCoroutine(HitStopRoutine(owner.hitStop));
        }

        private static IEnumerator HitStopRoutine(float seconds)
        {
            if (hitStopping || Time.timeScale < 0.99f) yield break;
            hitStopping = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
            hitStopping = false;
        }

        /// <summary>Player disabled during a hit-stop: restore the time scale.</summary>
        public static void ReleaseHitStop()
        {
            if (!hitStopping) return;
            hitStopping = false;
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ ground VFX
        /// <summary>Ground VFX (Resources/VFX/&lt;name&gt;); with VFX off, draws a faint range circle.</summary>
        public void GroundVfx(string name, Vector2 c, float scale, Color fallback, float radius)
        {
            if (owner.showVfx) SpriteVfx.Spawn(name, c, scale, owner.VfxLayer, owner.PlayerOrder - 1);
            else owner.StartCoroutine(Swing(c, radius, fallback));
        }

        private static IEnumerator Swing(Vector2 center, float radius, Color color)
        {
            var go = new GameObject("fx_swing");
            go.transform.position = center;
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sprite = CircleSprite;
            r.color = color;
            r.sortingOrder = 50;
            float t = 0f;
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                float s = radius * 2f * Mathf.Lerp(0.7f, 1f, t / 0.12f);
                go.transform.localScale = new Vector3(s, s, 1f);
                r.color = new Color(color.r, color.g, color.b, color.a * (1f - t / 0.12f));
                yield return null;
            }
            UnityEngine.Object.Destroy(go);
        }

        private static Sprite circle;
        private static Sprite CircleSprite
        {
            get
            {
                if (circle != null) return circle;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        byte a = (byte)(d > 1f ? 0 : d > 0.85f ? 255 : 90);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 unit diameter
                return circle;
            }
        }
    }
}
