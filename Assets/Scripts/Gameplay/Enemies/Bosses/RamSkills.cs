using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE ETHER RAM (mini-boss of the Boiler Room - the cult's old holding pen).
    ///
    /// Passive "Ether Hide": takes only `armorDamageMul` (50%) damage by default. Hit by the sluice water (SluiceMechanism) -> stunned
    ///   `quenchStun` seconds (takes `quenchDamageMul`), then "Soaked" for `soakedTime` seconds: LOSES the damage reduction + slowed by `soakedSlow`.
    ///   The valve on the north wall stays marked (arrow + glow, <see cref="SluiceMechanism.Alert"/>) for the whole fight.
    ///
    /// Phase 1: faster than Ren (moveSpeed in EnemyDefinition), BUTTS when close (short wind-up, 0.7 unit lunge). Now and then (every
    ///   `chargeEveryMin..Max` seconds, when Ren is far) it winds up a straight CHARGE: hits a wall -> stunned `wallStunTime`, rocks fall from the ceiling.
    /// Phase 2 "Rampage": charges NONSTOP like a spring - hitting a wall drops rocks and it bounces off along the reflection (random offset), aiming at nobody
    ///   but fast (`rampageSpeed`) and sometimes bouncing toward Ren (`rampageAimChance`). Walls do not stun it - only water stops it.
    ///   Every `rampageBreathEvery` seconds it pants for `rampageBreath` seconds (rest window for the player).
    /// Anti-pin: the Ram does not physically push into Ren while walking (stops close, then butts); while charging / butting / rampaging
    ///   its collision with Ren is off (deals damage + knockback, then passes through).
    /// Also: poison trail while charging (washed away by water), re-absorbing the trail -> overload (faster charge, shorter wind-up).
    /// Class files: RamSkills.cs (settings, setup, skill choice, helpers) · RamSkills.Attacks.cs (each skill as a coroutine).
    /// </summary>
    public partial class RamSkills : MonoBehaviour, IEnemySkills
    {
        public Sprite[] rubbleSprites;                         // rocks falling when the Ram hits a wall
        public int rubbleCount = 3;
        public float windupTime = 0.85f, chargeSpeed = 3.6f, maxRun = 1.8f, impactRadius = 1.1f;
        public float chargeEveryMin = 5f, chargeEveryMax = 8f, chargeMinDistance = 1.2f;
        [Header("Butt (phase 1)")]
        public float goreRange = 0.32f, goreWindup = 0.4f, goreSpeed = 3.2f, goreTime = 0.22f, goreCooldown = 1.3f;
        [Header("Rampage (pha 2)")]
        public float rampageSpeed = 4.2f, rampageAimChance = 0.35f, rampageJitter = 35f;
        public float rampageBreathEvery = 10f, rampageBreath = 1.4f;
        [Tooltip("A bounce direction must have at least this many units of clear path (prevents corner traps)")]
        public float rampageMinRun = 1.2f;
        public int rampageRubble = 2;
        [Header("Ether hide / water")]
        public float armorDamageMul = 0.5f;
        public float wallStunTime = 1.2f;
        public float quenchStun = 2.5f, quenchStunP2 = 2.0f, quenchDamageMul = 1.5f;
        public float soakedTime = 10f, soakedSlow = 0.2f;
        [Header("Poison trail / overload")]
        public float trailLife = 4f, trailRadius = 0.24f, trailEvery = 0.09f;
        public int overchargeAbsorb = 8;
        public float overchargeTime = 6f, overchargeSpeedMul = 1.25f, overchargeWindupMul = 0.7f;

        private struct Trail { public Vector2 p; public float born; public SpriteRenderer sr; }
        private readonly List<Trail> trails = new List<Trail>();
        private float nextTrail, nextTrailHurt, nextDrip;
        private int absorbed;
        private float overchargedUntil, soakedUntil, lastQuench, nextNudge, nextCharge, nextGore, nextBodyHit;
        private bool engaged, stunnedByWater, rampHinted, quenchHinted, soakHinted, armorHinted, overHinted, wallHinted;
        private SpriteRenderer aura, chargeLine;
        private float lastBounceAt = -9f;
        private Collider2D lastWall;                                   // object just hit by the probe (diagnostic log in the Editor)
        private Rect arena; private bool hasArena;                      // boss arena box: ArenaSeal tethers the boss feet 0.3 units inside the edge
        private EnemyAI ai;
        private bool ignoringPlayer;
        private static Sprite pixel;

        private bool Overcharged => Time.time < overchargedUntil;
        private bool Soaked => Time.time < soakedUntil;
        private float SpeedMul => (Soaked ? 1f - soakedSlow : 1f) * (Overcharged ? overchargeSpeedMul : 1f);
        private float Windup(float w) => w * (Overcharged ? overchargeWindupMul : 1f);

        private void Engage()
        {
            engaged = true;
            nextCharge = Time.time + 2.5f;
            lastQuench = Time.time;
            nextNudge = Time.time + 25f;
            SluiceMechanism.Alert = true;
            var med = MapEncounterDirector.Instance;
            hasArena = med != null && med.TryGetZoneAt(transform.position, out _, out arena);
            ai.Health.Damaged += OnDamaged;
            ai.Health.Died += _ => { SluiceMechanism.Alert = false; IgnorePlayer(false); };
            StartCoroutine(Tutorial());
        }

        private void OnDamaged(Health h, float amount)
        {
            if (armorHinted || Soaked || stunnedByWater || h.IsDead) return;
            armorHinted = true;
            Hints.Show("Your blade barely bites. Soak it with a sluice valve first.", 3f);
        }

        /// <summary>Ram not quenched for a long time -> repeat how to win (at most once every 25 seconds).</summary>
        private void Nudge()
        {
            if (Time.time < nextNudge || Time.time - lastQuench < 25f) return;
            nextNudge = Time.time + 25f;
            Hints.Show("Lure it across a channel, then strike the glowing valve!", 4f);
        }

        // ------------------------------------------------------------------ wall probe / bounce direction
        /// <summary>Centre + radius of the probe shape (from the Ram body collider).</summary>
        private Vector2 CastOrigin => ai.BodyCollider != null ? (Vector2)ai.BodyCollider.bounds.center : (Vector2)transform.position;
        private float CastRadius => ai.BodyCollider != null ? Mathf.Clamp(Mathf.Min(ai.BodyCollider.bounds.extents.x, ai.BodyCollider.bounds.extents.y * 2.5f), 0.1f, 0.4f) : 0.2f;

        /// <summary>Things the Ram bounces off: ONLY static objects (walls, pillars, fences, water edges). Ignores every dynamic body (Ren, enemies, pushed objects)
        /// and Ren's whole object tree - a Ren collider slipping past the `Health` filter made the Ram treat Ren as a wall and spin in place next to him.</summary>
        private bool Solid(Collider2D c)
        {
            if (c == null || c.isTrigger) return false;
            var rb = c.attachedRigidbody;
            if (rb != null && (rb == ai.Rb || rb.bodyType == RigidbodyType2D.Dynamic)) return false;
            if (ai.Player != null && (c.transform == ai.Player || c.transform.IsChildOf(ai.Player) || ai.Player.IsChildOf(c.transform))) return false;
            if (c.GetComponentInParent<EnemyAI>() != null) return false;                 // other enemies: no bounce
            var hp = c.GetComponentInParent<Health>();
            return hp == null || !hp.IsPlayer;                                          // Ren: charge through
        }

        /// <summary>Clear distance along `dir` (at most `max`) before the Ram body shape touches something solid. Only surfaces facing against the motion count
        /// (ignores the wall it just left - the probe starts overlapping it).</summary>
        private float FreeDistance(Vector2 dir, float max, out Vector2 normal, out Vector2 point)
        {
            Vector2 o = CastOrigin;
            normal = -dir; point = o + dir * max;
            float best = max;
            foreach (var h in Physics2D.CircleCastAll(o, CastRadius, dir, max))
            {
                if (!Solid(h.collider)) continue;
                Vector2 n = h.normal;
                if (h.distance <= 0.001f)
                {
                    // the probe OVERLAPS an object (Ram body over an arena barrier / wall): the real normal = from the closest point
                    // on the object to the centre. Moving into the object -> blocked at once; moving out -> ignored (ignoring all overlaps made the Ram pick a direction into the wall and spin in place).
                    Vector2 cp = h.collider.ClosestPoint(o);
                    n = o - cp;
                    if (n.sqrMagnitude < 1e-6f) n = hasArena ? arena.center - o : -dir;   // centre fully inside the object: escape direction = toward the arena middle
                    n.Normalize();
                }
                if (Vector2.Dot(n, dir) > -0.1f) continue;                 // surface facing the same way / moving away from it: not blocking
                if (h.distance < best) { best = h.distance; normal = n; point = h.point; lastWall = h.collider; }
            }
            // the arena tether box is also a wall (there is no collider exactly where the boss feet are held)
            if (hasArena)
            {
                Vector2 f = transform.position;
                const float m = 0.34f;                                      // ArenaSeal holds the boss feet 0.3 units inside the edge
                if (dir.x > 0.0001f) Edge((arena.xMax - m - f.x) / dir.x, Vector2.left, f, dir, ref best, ref normal, ref point);
                if (dir.x < -0.0001f) Edge((arena.xMin + m - f.x) / dir.x, Vector2.right, f, dir, ref best, ref normal, ref point);
                if (dir.y > 0.0001f) Edge((arena.yMax - m - f.y) / dir.y, Vector2.down, f, dir, ref best, ref normal, ref point);
                if (dir.y < -0.0001f) Edge((arena.yMin + m - f.y) / dir.y, Vector2.up, f, dir, ref best, ref normal, ref point);
            }
            return best;
        }

        private void Edge(float t, Vector2 n, Vector2 from, Vector2 dir, ref float best, ref Vector2 normal, ref Vector2 point)
        {
            t = Mathf.Max(0f, t);                                           // already past the edge and still moving out -> blocked at once
            if (t >= best) return;
            best = t; normal = n; point = from + dir * t; lastWall = null;
        }

        /// <summary>Bounce direction: sometimes toward Ren, otherwise a randomly offset reflection; only accepts directions with ≥ `rampageMinRun`
        /// clear path that do not dive into the surface just hit. No direction works (dead corner) -> scan 16 directions and take the clearest.</summary>
        private Vector2 ChooseBounce(Vector2 dir, Vector2 normal)
        {
            bool hasNormal = normal.sqrMagnitude > 0.01f;
            Vector2 baseDir = hasNormal ? Vector2.Reflect(dir, normal) : dir;
            var tries = new List<Vector2>(10);
            if (ai.Player != null && (Random.value < rampageAimChance || !hasNormal))
                tries.Add(Rotate(((Vector2)ai.Player.position - (Vector2)transform.position).normalized, Random.Range(-15f, 15f)));
            float j = Random.Range(-rampageJitter, rampageJitter);
            foreach (float off in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f }) tries.Add(Rotate(baseDir, j + off));
            foreach (var t in tries)
            {
                var d = t.normalized;
                if (hasNormal && Vector2.Dot(d, normal) < 0.2f) continue;
                if (FreeDistance(d, rampageMinRun, out _, out _) >= rampageMinRun - 0.001f) return d;
            }
            Vector2 open = -dir; float far = -1f;
            for (int k = 0; k < 16; k++)
            {
                var d = Rotate(Vector2.right, k * 22.5f + Random.Range(-5f, 5f));
                float f = FreeDistance(d, 6f, out _, out _);
                if (f > far) { far = f; open = d; }
            }
            return open;
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Charging but barely moving = hit a wall.</summary>
        private bool Stalled(ref Vector2 last, ref float stall, float run, float speed)
        {
            Vector2 now = transform.position;
            float moved = (now - last).magnitude;
            last = now;
            stall = (run > 0.15f && moved < speed * Time.deltaTime * 0.25f) ? stall + Time.deltaTime : 0f;
            return stall > 0.06f;
        }

        private void Impact(Vector2 at, Vector2 dir, int rocks, bool aimOne)
        {
            var mm = MapMechanics.Instance;
            if (mm != null) mm.RamImpact(at, impactRadius);
            Rubble(at, rocks, aimOne);
            CameraFollow.KickMain(dir, 0.08f, 0.25f);
            SoulRequiem.Core.Sfx.Play("lg_wall_crash", at); // sound: wall hit -> ceiling cracks, rocks about to fall
            if (rocks > 0) SoulRequiem.Core.Sfx.Play("ceiling_crack", at);
        }

        /// <summary>Small water drops falling off the body while soaked (shows "Ether hide lost").</summary>
        private void Drip()
        {
            if (Time.time < nextDrip || ai == null || ai.Body == null) return;
            nextDrip = Time.time + 0.12f;
            Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 0.45f));
            var sr = EtherVfx.Spawn("RamDrip", EtherVfx.Orb, p, new Color(0.6f, 0.85f, 1f, 0.85f), ai.Body.sortingLayerID, ai.Body.sortingOrder + 1);
            float k = 0.05f / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            sr.transform.localScale = new Vector3(k, k * 1.4f, 1f);
            StartCoroutine(Fall(sr, p));
        }

        // ------------------------------------------------------------------ every frame: Ether hide, poison trail, overload
        private void Update()
        {
            if (engaged && ai != null && ai.Health != null && !ai.Health.IsDead)
            {
                ai.Health.DamageTakenMul = stunnedByWater ? quenchDamageMul : Soaked ? 1f : armorDamageMul;
                if (Soaked) Drip();
            }
            UpdateAura();
            if (trails.Count == 0) return;
            float now = Time.time;
            Vector2 pp = ai != null && ai.Player != null ? (Vector2)ai.Player.position : new Vector2(9999f, 9999f);
            bool inTrail = false;
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                var t = trails[i];
                float age = now - t.born;
                bool washed = SluiceMechanism.Wet(t.p);                       // sluice water washes the poison trail away
                if (age >= trailLife || washed || t.sr == null) { if (t.sr != null) Destroy(t.sr.gameObject); trails.RemoveAt(i); continue; }
                float a = age < 0.15f ? age / 0.15f : age > trailLife - 1f ? (trailLife - age) : 1f;
                t.sr.color = new Color(0.55f, 1f, 0.35f, 0.55f * a);
                Vector2 d = pp - t.p;
                if (new Vector2(d.x, d.y / 0.6f).magnitude < trailRadius) inTrail = true;
            }
            if (inTrail && now >= nextTrailHurt && ai != null && !ai.Health.IsDead)
            {
                nextTrailHurt = now + 0.5f;
                ai.Hurt(ai.damage * 0.22f, pp, 0f);
            }
        }

        /// <summary>The Ram walks over its own poison trail -> absorbs it; enough spots -> overload.</summary>
        private void Absorb(Vector2 pos)
        {
            if (Overcharged) return;
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                if ((trails[i].p - pos).sqrMagnitude > 0.3f * 0.3f) continue;
                if (trails[i].sr != null) Destroy(trails[i].sr.gameObject);
                trails.RemoveAt(i);
                absorbed++;
            }
            if (absorbed < overchargeAbsorb) return;
            absorbed = 0;
            overchargedUntil = Time.time + overchargeTime;
            if (!overHinted) { overHinted = true; Hints.Show("It drinks its own ichor and surges! Wash the trails away with the sluices.", 3.5f); }
            StartCoroutine(ai.Flash(0.35f, new Color(0.6f, 1f, 0.4f)));
        }

        private void UpdateAura()
        {
            bool on = ai != null && !ai.Health.IsDead && Overcharged;
            if (!on) { if (aura != null) aura.enabled = false; return; }
            if (aura == null)
            {
                aura = EtherVfx.Spawn("RamOvercharge", EtherVfx.Orb, transform.position, new Color(0.55f, 1f, 0.35f, 0.4f),
                                      ai.Body != null ? ai.Body.sortingLayerID : 0, ai.Body != null ? ai.Body.sortingOrder - 1 : 0);
            }
            aura.enabled = true;
            aura.transform.position = transform.position;
            float k = (0.9f + 0.12f * Mathf.Sin(Time.time * 9f)) / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            aura.transform.localScale = new Vector3(k, k * 0.55f, 1f);
            aura.color = new Color(0.55f, 1f, 0.35f, 0.3f + 0.15f * Mathf.Sin(Time.time * 9f));
        }

        private void DropTrail(Vector2 p)
        {
            var sr = EtherVfx.Spawn("RamTrail", EtherVfx.Orb, p, new Color(0.55f, 1f, 0.35f, 0f),
                                    ai.Body != null ? ai.Body.sortingLayerID : 0, ai.Body != null ? ai.Body.sortingOrder - 3 : 0);
            float k = trailRadius * 2.4f / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            sr.transform.localScale = new Vector3(k, k * 0.6f, 1f);
            trails.Add(new Trail { p = p, born = Time.time, sr = sr });
        }

        private void ClearTrails()
        {
            foreach (var t in trails) if (t.sr != null) Destroy(t.sr.gameObject);
            trails.Clear();
        }

        // ------------------------------------------------------------------ collision with Ren
        /// <summary>Turn physical collision between the Ram and Ren on / off (charging: passes through after dealing damage -> does not pin Ren against walls).</summary>
        private void IgnorePlayer(bool on)
        {
            if (ignoringPlayer == on || ai == null || ai.Player == null) return;
            ignoringPlayer = on;
            var mine = GetComponentsInChildren<Collider2D>();
            foreach (var pc in ai.Player.GetComponentsInChildren<Collider2D>())
                foreach (var c in mine)
                    if (pc != null && c != null) Physics2D.IgnoreCollision(c, pc, on);
        }

        private void LateUpdate()
        {
            if (chargeLine != null && (ai == null || ai.Health == null || ai.Health.IsDead)) DropLine();
        }

        private void OnDisable()
        {
            DropLine();
            IgnorePlayer(false);
            SluiceMechanism.Alert = false;
            if (ai != null && ai.Health != null) ai.Health.DamageTakenMul = 1f;
            if (aura != null) aura.enabled = false;
            ClearTrails();
        }

        private void OnDestroy()
        {
            ClearTrails();
            if (aura != null) Destroy(aura.gameObject);
        }

        /// Ceiling rocks: `aimOne` = one rock aimed where Ren stands (with a warning ring), the rest scattered around the impact.
        private void Rubble(Vector2 impact, int n, bool aimOne)
        {
            if (rubbleSprites == null || rubbleSprites.Length == 0 || n <= 0) return;
            int order = ai.Body != null ? ai.Body.sortingOrder : 2;
            Vector2 p0 = ai.Player != null ? (Vector2)ai.Player.position : impact;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = i == 0 && aimOne ? p0 : impact + Random.insideUnitCircle * 1.4f;
                var g = GroundStrike.Spawn(p, 0.9f + 0.12f * i, 0.34f, ai.damage * 0.8f, null, 0, 14f, order,
                                           new Color(0.85f, 0.75f, 0.55f));
                g.shell = rubbleSprites[Random.Range(0, rubbleSprites.Length)];
                g.shellFrom = p + Vector2.up * 3.2f;
                g.shellArc = 0f;
                g.leaveSprite = g.shell;
                g.leaveSeconds = 2.5f;
            }
        }

        // ------------------------------------------------------------------ warning lines
        private SpriteRenderer MakeLine()
        {
            if (pixel == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            }
            var go = new GameObject("RamChargeLine");
            var s = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(s);
            s.sprite = pixel;
            s.sortingOrder = ai.Body != null ? ai.Body.sortingOrder - 1 : 1;
            return s;
        }

        /// <summary>Warning lane colour (red; green when overloaded).</summary>
        private Color RamWarn => Overcharged ? new Color(0.55f, 1f, 0.35f) : new Color(1f, 0.3f, 0.24f);

        private void PlaceLine(SpriteRenderer line, Vector2 dir, float length)
        {
            if (line == null) return;
            line.transform.position = transform.position;
            line.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(length, 0.12f, 1f);
            line.color = Overcharged ? new Color(0.55f, 1f, 0.35f, Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? 0.6f : 0.25f)
                                     : new Color(1f, 0.25f, 0.2f, Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? 0.55f : 0.25f);
        }

        private void DropLine()
        {
            if (chargeLine != null) Destroy(chargeLine.gameObject);
            chargeLine = null;
        }
    }
}
