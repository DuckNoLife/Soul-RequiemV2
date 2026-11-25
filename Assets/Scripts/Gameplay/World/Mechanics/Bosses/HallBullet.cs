using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;                                   // FxLighting

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Absalom's bullet (Hall of Naming). Moves by velocity (+ acceleration / light homing); touching Ren -> damage (a roll dodges it);
    /// breaks on walls. <see cref="held"/> = hangs still in mid-air (placed during the TIME STOP, with an arrow TRAIL showing its future path);
    /// <see cref="TimeStop.Active"/> -> every bullet freezes and its age clock stops too. onExpire: end of life (Umbral Orb bursts into shards).
    /// Release(thaw) — when the time stop ends the bullet MOVES IMMEDIATELY as colour returns (it must not wait extra seconds),
    /// its speed "thaws" from 15% to full over `thaw` seconds (time flowing again gives Ren a moment to react).
    /// Spawned by: AbsalomHallMechanism.Shot (.Skills.cs, used by every school and the time stop) and VesselDomainMechanism (.Fire, .Hands).
    /// Reflect (BulletWard / Sanctuary Bell): turns it into Ren's bullet that only hits enemies.
    /// </summary>
    public sealed class HallBullet : MonoBehaviour
    {
        public Vector2 velocity;
        public float accel, homing, damage = 9f, radius = 0.12f, life = 6f, knock = 1.4f, spinDeg;
        public bool held, stopOnWalls = true, faceVelocity = true;
        public Sprite[] frames;
        public float fps = 12f;
        public System.Action<HallBullet> onExpire;
        /// <summary>Own break sprite on hit / wall (empty = shared "hit"), size + colour; harmless = decorative particle (no damage).</summary>
        public string popFx = "";
        public float popScale = 1f;
        public Color popTint = Color.white;
        public bool harmless;
        /// <summary>Bursts (calls onExpire) when Ren comes within this radius — a homing dark orb explodes on arrival; expireOnPop: wall / hit also calls onExpire.</summary>
        public float fuseRadius;
        public bool expireOnPop;
        private bool expired;
        public static readonly List<HallBullet> Live = new List<HallBullet>();

        private SpriteRenderer sr;
        private float age, releasedAt = -99f, thaw;
        private Vector2 last;
        private Color tint = Color.white;
        private readonly List<SpriteRenderer> path = new List<SpriteRenderer>();
        private static Transform player;
        private static Health playerHp;

        public SpriteRenderer Sr => sr;
        public float Age => age;

        public static HallBullet Spawn(Sprite[] frames, Vector2 pos, Vector2 vel, float damage, float radius, float life,
                                       Color tint, float scale, int layer, int order)
        {
            var go = new GameObject("HallBullet");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var b = go.AddComponent<HallBullet>();
            b.sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(b.sr);
            b.sr.sortingLayerID = layer; b.sr.sortingOrder = order;
            b.frames = frames;
            if (frames != null && frames.Length > 0) b.sr.sprite = frames[0];
            b.tint = tint; b.sr.color = tint;
            b.velocity = vel; b.damage = damage; b.radius = radius; b.life = life;
            b.last = pos;
            b.Orient();
            return b;
        }

        private void OnEnable() => Live.Add(this);
        private void OnDisable() { Live.Remove(this); ClearPath(); }

        /// <summary>Faint arrow trail along the path it will travel in `seconds` (bullets placed during the time stop).</summary>
        public void ShowPath(float seconds)
        {
            ClearPath();
            var arrow = SpriteVfx.Load("drill/fx_arrow");
            if (arrow.Length == 0 || sr == null) return;
            Vector2 p = transform.position, v = velocity;
            float len = Mathf.Min(v.magnitude * seconds + 0.5f * accel * seconds * seconds, 6f);
            if (len < 0.2f || v.sqrMagnitude < 0.0001f) return;
            Vector2 d = v.normalized;
            int n = Mathf.Clamp(Mathf.FloorToInt(len / 0.3f), 1, 18);
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            for (int i = 1; i <= n; i++)
            {
                var go = new GameObject("HallBulletPath");
                go.transform.position = p + d * (i * 0.3f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var s = go.AddComponent<SpriteRenderer>();
                FxLighting.MakeUnlit(s);
                s.sprite = arrow[0];
                s.sortingLayerID = sr.sortingLayerID; s.sortingOrder = sr.sortingOrder - 1;
                float a = Mathf.Lerp(0.6f, 0.2f, i / (float)n);
                s.color = new Color(Mathf.Lerp(tint.r, 1f, 0.4f), Mathf.Lerp(tint.g, 1f, 0.4f), Mathf.Lerp(tint.b, 1f, 0.4f), a);
                path.Add(s);
            }
        }

        public void ClearPath()
        {
            foreach (var s in path) if (s != null) Destroy(s.gameObject);
            path.Clear();
        }

        /// <summary>Release a held bullet (time stop ended).</summary>
        public void Release(float thawSeconds = 0f) { held = false; ClearPath(); releasedAt = Time.time; thaw = Mathf.Max(0f, thawSeconds); }

        /// <summary>Reflected by a ward (BulletWard): flies along `newVel`, `damageMul` times stronger, hits only enemies (no homing, no burst).</summary>
        public bool Reflected { get; private set; }
        public void Reflect(Vector2 newVel, float damageMul)
        {
            if (Reflected || harmless || held) return;
            Reflected = true;
            velocity = newVel; accel = 0f; homing = 0f; fuseRadius = 0f;
            onExpire = null; expireOnPop = false;
            damage *= damageMul;
            age = 0f; life = 2f;
            tint = new Color(1f, 0.92f, 0.55f, tint.a);
            Orient();
        }

        public void Pop()
        {
            if (expireOnPop && !expired) { expired = true; onExpire?.Invoke(this); }
            if (sr != null)
            {
                if (string.IsNullOrEmpty(popFx)) SpriteVfx.Spawn("hit", transform.position, 0.5f, sr.sortingLayerID, sr.sortingOrder + 1, false, 18f, false);
                else if (popFx != "none")
                {
                    var fx = SpriteVfx.Spawn(popFx, transform.position, popScale, sr.sortingLayerID, sr.sortingOrder + 1, false, 18f, false);
                    if (fx != null) fx.Tint(popTint);
                }
            }
            Destroy(gameObject);
        }

        /// <summary>The bullet GLOWS onto the floor + Ren (child Light2D) — big bullets (lances, orbs) stay readable in the dark hall.</summary>
        public HallBullet Glow(Color c, float radius, float intensity)
        {
            var go = new GameObject("HallBullet Glow");
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            l.color = c;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * 0.15f;
            l.intensity = intensity;
            return this;
        }

        private void Orient()
        {
            if (faceVelocity && velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }

        private void Update()
        {
            if (held || TimeStop.Active)
            {
                // frozen: only a faint "breath" shows the bullet is still alive
                if (sr != null) sr.color = new Color(tint.r, tint.g, tint.b, tint.a * (0.75f + 0.25f * Mathf.Sin(Time.time * 8f)));
                return;
            }
            if (sr != null) sr.color = tint;
            age += Time.deltaTime;
            if (age >= life)
            {
                if (!expired) { expired = true; onExpire?.Invoke(this); }
                Destroy(gameObject);
                return;
            }
            Vector2 p = transform.position;
            if (homing > 0f && Target() != null)
            {
                Vector2 want = ((Vector2)player.position + Vector2.up * 0.2f - p);
                float a = Vector2.SignedAngle(velocity, want);
                velocity = Quaternion.Euler(0f, 0f, Mathf.Clamp(a, -homing * Time.deltaTime, homing * Time.deltaTime)) * velocity;
            }
            if (accel != 0f && velocity.sqrMagnitude > 0.0001f) velocity += velocity.normalized * accel * Time.deltaTime;
            float pace = thaw > 0f ? Mathf.SmoothStep(0.15f, 1f, (Time.time - releasedAt) / thaw) : 1f;   // thawing after the time stop
            p += velocity * (pace * Time.deltaTime);
            if (stopOnWalls && age > 0.08f && LineOfSight.Blocked(last, p)) { if (!harmless) Sfx.Play("bullet_pop", p, 0.2f); Pop(); return; }
            transform.position = p;
            last = p;
            if (frames != null && frames.Length > 1 && sr != null) sr.sprite = frames[(int)(age * fps) % frames.Length];
            if (spinDeg != 0f) transform.Rotate(0f, 0f, spinDeg * Time.deltaTime);
            else Orient();

            if (harmless) return;
            if (Reflected)
            {
                foreach (var ai in SkillKit.EnemiesIn(p, radius + 0.18f))
                    if (ai.Health.TakeDamage(damage, velocity.normalized, knock)) { Pop(); return; }
                return;
            }
            var pt = Target();
            if (fuseRadius > 0f && pt != null && ((Vector2)pt.position + Vector2.up * 0.2f - p).sqrMagnitude <= fuseRadius * fuseRadius)
            {
                if (!expired) { expired = true; onExpire?.Invoke(this); }                   // bursts when close
                Destroy(gameObject);
                return;
            }
            if (pt == null || playerHp == null || playerHp.IsDead) return;
            Vector2 body = (Vector2)pt.position + Vector2.up * 0.2f;
            float r = radius + 0.16f;
            if ((body - p).sqrMagnitude > r * r) return;
            if (playerHp.Invulnerable) return;                                         // rolling: passes through
            Health.IncomingRanged = true;
            bool hit;
            try { hit = playerHp.TakeDamage(damage, (body - p).normalized, knock); }
            finally { Health.IncomingRanged = false; }
            if (hit) Pop();
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

        /// <summary>Remove every remaining bullet (fight reset / phase cleared).</summary>
        public static void ClearAll()
        {
            for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] != null) Destroy(Live[i].gameObject);
            Live.Clear();
        }
    }
}
