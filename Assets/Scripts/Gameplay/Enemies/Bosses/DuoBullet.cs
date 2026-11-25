using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Shared bullet of the Reliquary Wardens duo fight (coins, ghost skulls, flying axes, thrown maces...). Any image; moves by velocity
    /// or a custom PATH (`path`: time -> position), optionally HOMING on Ren (`homing` degrees/second). Touching Ren -> damage (can be rolled through);
    /// `killable` -> Ren's slash destroys it; `stopOnWalls` -> breaks on walls. `hitEnemy` -> Ren's own projectile (axe thrown at the Champion).
    /// Reflect (BulletWard / Sanctuary Bell): turns it into Ren's bullet that only hits enemies.
    /// </summary>
    public sealed class DuoBullet : MonoBehaviour
    {
        public Vector2 velocity;
        public float damage = 10f, radius = 0.2f, knock = 1.5f, life = 4f, spinDeg = 0f, homing = 0f, maxSpeed = 3f;
        public bool killable, stopOnWalls = true;
        public System.Func<float, Vector2> path;               // if set: position over time (velocity / homing ignored)
        public Transform hitEnemy;                             // Ren's projectile: hitting this enemy -> onHitEnemy
        public System.Action<DuoBullet> onHitEnemy, onEnd;
        /// <summary>On touching Ren, call this instead of / together with damage (damage = 0 -> effect only; passes through Ren while he rolls).</summary>
        public System.Action<Health> onHitPlayer;
        public Sprite[] frames;                                // animated bullet (the gold Champion's light orb)
        public float fps = 12f;
        private SpriteRenderer sr;
        private float born;
        private Vector2 last;
        private static Transform player;
        private static Health playerHp;
        public static readonly List<DuoBullet> Live = new List<DuoBullet>();

        public static DuoBullet Spawn(Sprite sprite, Vector2 pos, Vector2 vel, float damage, float radius, float life, int layer, int order, float scale = 1f)
        {
            var go = new GameObject("DuoBullet");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var b = go.AddComponent<DuoBullet>();
            b.sr = go.AddComponent<SpriteRenderer>();
            b.sr.sprite = sprite;
            b.sr.sortingLayerID = layer; b.sr.sortingOrder = order;
            b.velocity = vel; b.damage = damage; b.radius = radius; b.life = life;
            b.born = Time.time; b.last = pos;
            return b;
        }

        public float Age => Time.time - born;
        public SpriteRenderer Sprite => sr;

        private void OnEnable() { Live.Add(this); PlayerCombat.Struck += OnStruck; }
        private void OnDisable() { Live.Remove(this); PlayerCombat.Struck -= OnStruck; }

        private void OnStruck(Vector2 c, float r)
        {
            if (!killable || this == null) return;
            if (((Vector2)transform.position - c).sqrMagnitude <= (r + radius) * (r + radius))
            {
                SoulRequiem.Core.Sfx.Play("bullet_pop", transform.position, 0.55f);   // slashed apart
                Pop();
            }
        }

        /// <summary>Reflected by a ward (BulletWard): flies along `newVel`, `damageMul` times stronger, hits only enemies.</summary>
        public bool Reflected { get; private set; }
        public void Reflect(Vector2 newVel, float damageMul)
        {
            if (Reflected || hitEnemy != null) return;
            Reflected = true;
            path = null; homing = 0f; killable = false; onHitPlayer = null;
            velocity = newVel;
            damage *= damageMul;
            born = Time.time; life = 2f;
            if (sr != null) sr.color = new Color(1f, 0.92f, 0.55f, sr.color.a);
        }

        public void Pop()
        {
            SpriteVfx.Spawn("hit", transform.position, 0.6f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 1, false, 18f, false);
            End();
        }

        private void End()
        {
            onEnd?.Invoke(this);
            Destroy(gameObject);
        }

        private void Update()
        {
            float age = Time.time - born;
            if (age >= life) { End(); return; }
            Vector2 p = transform.position;
            if (path != null) p = path(age);
            else
            {
                if (homing > 0f && hitEnemy == null)
                {
                    var t = Target();
                    if (t != null)
                    {
                        Vector2 want = ((Vector2)t.position - p).normalized * Mathf.Max(velocity.magnitude, 0.5f);
                        float a = Vector2.SignedAngle(velocity, want);
                        velocity = Quaternion.Euler(0f, 0f, Mathf.Clamp(a, -homing * Time.deltaTime, homing * Time.deltaTime)) * velocity;
                        velocity = velocity.normalized * Mathf.Min(maxSpeed, velocity.magnitude + Time.deltaTime * 0.8f);
                    }
                }
                else if (hitEnemy != null)
                {
                    Vector2 want = ((Vector2)hitEnemy.position + Vector2.up * 0.3f - p).normalized * Mathf.Max(velocity.magnitude, 4f);
                    velocity = Vector2.Lerp(velocity, want, Time.deltaTime * 6f);
                }
                p += velocity * Time.deltaTime;
            }
            if (stopOnWalls && path == null && age > 0.1f && LineOfSight.Blocked(last, p)) { Pop(); return; }
            transform.position = p;
            last = p;
            if (frames != null && frames.Length > 0 && sr != null) sr.sprite = frames[(int)(age * fps) % frames.Length];
            if (spinDeg != 0f) transform.Rotate(0f, 0f, spinDeg * Time.deltaTime);
            if (hitEnemy != null)
            {
                if (((Vector2)hitEnemy.position + Vector2.up * 0.3f - p).sqrMagnitude < 0.35f * 0.35f) { onHitEnemy?.Invoke(this); End(); }
                return;
            }
            if (Reflected)
            {
                foreach (var ai in SkillKit.EnemiesIn(p, radius + 0.2f))
                    if (ai.Health.TakeDamage(damage, velocity.normalized, knock)) { Pop(); return; }
                return;
            }
            var pt = Target();
            if (pt == null || playerHp == null || playerHp.IsDead) return;
            Vector2 body = (Vector2)pt.position + Vector2.up * 0.2f;
            if ((body - p).sqrMagnitude > (radius + 0.22f) * (radius + 0.22f)) return;
            if (damage <= 0f)
            {
                if (playerHp.Invulnerable) return;                     // rolling / shielded: fly through
                onHitPlayer?.Invoke(playerHp);
                End(); return;
            }
            Health.IncomingRanged = true;
            bool hit;
            try { hit = playerHp.TakeDamage(damage, (body - p).normalized, knock); }
            finally { Health.IncomingRanged = false; }
            if (hit) { onHitPlayer?.Invoke(playerHp); End(); }                                            // Ren rolling (invulnerable) -> the bullet flies through
        }

        private static Transform Target()
        {
            if (player == null)
            {
                var pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null) { player = pc.transform; playerHp = pc.GetComponent<Health>(); }
            }
            return player;
        }

        /// <summary>Clear every bullet still in flight (boss down / fight reset).</summary>
        public static void ClearAll()
        {
            for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] != null) Destroy(Live[i].gameObject);
            Live.Clear();
        }
    }
}
