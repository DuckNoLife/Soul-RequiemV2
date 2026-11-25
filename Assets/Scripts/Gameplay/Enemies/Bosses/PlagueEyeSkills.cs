using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Skill set of the Plague Eye boss (Map 2) - the Eye pinned above a pit, never moves. EnemyAI (kind = Beholder) calls Act() every tick.
    ///   1. Eye Volley  (atk2): 0.5 s wind-up, then 2 rings of eye bolts spreading out (phase 2: 3 denser spiral rings). Bolts break on pillars / walls.
    ///   2. Death Ray   (atk1): ~1.3 s wind-up (red line shows the start direction + sweep direction), then a beam sweeps 360° around the pit. Pillars / walls block it
    ///                         -> hide behind a pillar. Phase 2: 2 opposite beams, faster sweep.
    ///   3. Spin        (atk3): when the player presses the pit edge. 0.8 s warning ring, then a spinning lash with knockback; stunned ~1.4 s afterwards (counterattack window).
    ///   4. Toxic Brood: every `summonEvery` seconds calls a wave of 3 toxic enemies rising from the poison cloud around the pit - ONLY when the previous wave is dead.
    ///                   Summoned enemies die with the boss.
    ///   - Priority: summon > Death Ray (when off cooldown, even while Ren is close) > Spin > Volley. Spin only after Ren has stayed next to
    ///     the pit for `spinCommit` seconds straight, never twice in a row, cooldown `spinCooldown` (10 s on the prefab).
    ///   - Death Ray hits by SWEPT AREA every frame (a beam passing between 2 frames still hits), checking both Ren's feet and body; each beam can hit
    ///     again after `rayTick` seconds if Ren stays in it. Blocked by pillars / walls.
    ///   - Death Ray cooldown starts when the beam ENDS (phase 2 = `rayCooldown` x `rayCooldownP2`), and there are always at least
    ///     `minActsBetweenRays` other skills (Volley / Spin / summon) between two rays - breathing room for Ren to leave cover and hit back.
    ///     (Counting from the beam start made the ~6 s phase 2 beam come off cooldown the moment it ended -> back-to-back lasers.)
    /// Arena: the camera zooms out (CameraFollow.SetZoom) to show all 4 pillars; restored when the boss dies.
    /// </summary>
    public class PlagueEyeSkills : MonoBehaviour, IEnemySkills
    {
        [Header("Sprites (set by Map2EnemyBuilder)")]
        public Sprite[] beamFrames;          // beam-loop (pivot left-centre, 32 px per frame)
        public Sprite[] beamImpactFrames;    // beam-impact-loop
        public Sprite[] spinFxFrames;        // atk3-loop-FX (same frames & pivot as the body)
        [Tooltip("Eye position relative to the feet (world units, x3 scale included)")]
        public Vector2 eyeOffset = new Vector2(0f, 0.77f);
        [Tooltip("Pit centre relative to the boss feet (feet = sprite pivot, ~2 tiles below the pit centre) - spin centre, beam origin, summon point")]
        public Vector2 groundOffset = new Vector2(0f, 0.6f);
        public Color beamColor = new Color(0.78f, 1f, 0.62f, 1f);

        [Header("Eye Volley")]
        public int volleyCount = 14;
        public int volleyWaves = 2;
        public float volleyGap = 0.45f;
        public float boltSpeed = 2.0f;
        public float boltDamage = 12f;

        [Header("Death Ray")]
        public float rayWindup = 1.3f;
        public float rayDuration = 4.2f;
        public float raySpeed = 80f;          // degrees / second
        public float rayRange = 9f;
        public float rayHalfWidth = 0.16f;
        public float rayDamage = 12f;          // wide enough to hit reliably on every pass
        public float rayTick = 0.25f;
        public float rayThickness = 2.0f;     // Y scale of the beam sprite

        [Header("Spin")]
        public float spinTrigger = 2.3f;      // spins when the player is within this distance (from the pit centre)
        public float spinCommit = 0.8f;       // the player must stay close this many seconds straight before it spins (no spin from just passing by)
        public float spinRadius = 2.0f;
        public float spinWarn = 0.8f;
        public float spinTime = 1.6f;
        public float spinDamage = 14f;
        public float spinTick = 0.3f;
        public float spinPush = 2.5f;      // only the first lash pushes hard, later beats push lightly
        public float dizzyTime = 1.4f;

        [Header("Toxic Brood (summon)")]
        public float summonEvery = 30f;
        public string[] summonWave = { "Plague_Rat", "Plague_Rat", "Vat_Born" };
        [Tooltip("Summon wave in phase 2 (under 50% HP) - adds ranged poison-spitting rats")]
        public string[] summonWaveP2 = { "Plague_Rat", "Vat_Born", "Plague_Rat", "Bloated_Rat" };
        public int summonCap = 6;
        [Tooltip("Spawn points, from the pit centre (world units)")]
        public Vector2[] summonPoints = { new Vector2(-2.24f, 0f), new Vector2(2.24f, 0f), new Vector2(0f, -2.0f), new Vector2(0f, 2.34f),
                                          new Vector2(-2.0f, -1.7f), new Vector2(2.0f, -1.7f) };
        public Sprite[] cloudPop, cloudLoop, cloudFade;

        [Header("Eye bolts")]
        public float boltScale = 2.4f;
        public Color boltColor = new Color(1f, 0.95f, 0.6f, 1f);

        [Header("Timing")]
        public float idleGap = 1.0f;
        public float rayCooldown = 9f;
        public float spinCooldown = 10f;       // 5 -> 10
        public float arenaZoom = 1.45f;

        private EnemyAI ai;
        private bool engaged;
        private float nextRay, nextSpin, nextSummon, closeSince = -1f;
        private bool lastWasSpin;
        [Tooltip("Phase 2 Death Ray cooldown = rayCooldown x this factor (counted from when the beam ends)")]
        [SerializeField] private float rayCooldownP2 = 0.8f;
        [Tooltip("Minimum number of other skills between two Death Rays")]
        [SerializeField] private int minActsBetweenRays = 1;
        private int actsSinceRay = 99;
        private readonly List<EnemyAI> brood = new List<EnemyAI>();
        private readonly HashSet<Collider2D> ignore = new HashSet<Collider2D>();
        private readonly List<GameObject> temp = new List<GameObject>();
        private CameraFollow camFollow;
        private int waterLayer;
        private static Sprite ringSprite, pixelSprite;

        private Vector2 Ground => (Vector2)transform.position + groundOffset;
        private Vector2 Eye => (Vector2)transform.position + eyeOffset;

        private void Awake()
        {
            ai = GetComponent<EnemyAI>();
            waterLayer = LayerMask.NameToLayer("Water");
            var h = GetComponent<Health>();
            if (h != null) h.Died += _ => { Cleanup(true); KillBrood(); };
        }

        private void OnDestroy() => Cleanup(true);

        /// <summary>One action tick (called by EnemyAI.Brain once aggressive).</summary>
        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (!engaged)
            {
                engaged = true;
                CollectIgnore();
                camFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
                if (camFollow != null) camFollow.SetZoom(arenaZoom);
                nextRay = Time.time + 5f;
                nextSummon = Time.time + summonEvery;
                ai.PlayState("Idle");
                yield return ai.WaitFor(1.2f);
            }
            if (ai.Player == null) yield break;
            bool p2 = ai.InPhase2;
            float d = ((Vector2)ai.Player.position - Ground).magnitude;
            bool close = d <= spinTrigger;
            if (!close) closeSince = -1f; else if (closeSince < 0f) closeSince = Time.time;
            bool spinReady = close && !lastWasSpin && Time.time >= nextSpin && Time.time - closeSince >= spinCommit;
            bool spun = false;
            if (Time.time >= nextSummon && BroodCleared()) yield return Summon(p2);
            else if (Time.time >= nextRay && actsSinceRay >= minActsBetweenRays) { actsSinceRay = -1; yield return Ray(p2); }
            else if (spinReady) { yield return Spin(p2); spun = true; closeSince = -1f; }
            else yield return Volley(p2);
            lastWasSpin = spun;
            actsSinceRay++;
            ai.PlayState("Idle");
            yield return ai.WaitFor(p2 ? idleGap * 0.6f : idleGap);
        }

        // ------------------------------------------------------------------ 1. eye bolt volley
        private IEnumerator Volley(bool p2)
        {
            ai.PlayState("VolleyWindup");
            SoulRequiem.Core.Sfx.Play("v_eye_alert", Eye, 0.55f);                       // volley wind-up sound
            yield return ai.Flash(0.5f, new Color(1f, 0.85f, 0.4f));
            if (ai.Player == null) yield break;
            ai.PlayState("VolleyLoop");
            if (ai.projectilePrefab == null) Debug.LogWarning("[Plague Eye] Missing projectilePrefab (Eye_Bolt) - rerun 'Soul Requiem > Enemies > Map 2'.");
            int waves = volleyWaves + (p2 ? 1 : 0);
            int n = volleyCount + (p2 ? 4 : 0);
            Vector2 to = (Vector2)ai.Player.position - Eye;
            float baseA = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            for (int w = 0; w < waves; w++)
            {
                float off = baseA + (w % 2 == 1 ? 180f / n : 0f) + (p2 ? w * 9f : 0f);   // rings offset by half a gap; phase 2 spirals
                foreach (var dir in BulletPatterns.Ring(n, off)) Fire(dir);
                SoulRequiem.Core.Sfx.Play("proj_magic", Eye, 0.8f);                     // one shot sound per ring
                yield return ai.WaitFor(volleyGap);
            }
            ai.PlayState("VolleyEnd");
            yield return ai.WaitFor(0.3f);
        }

        private void Fire(Vector2 dir)
        {
            if (ai.projectilePrefab == null) return;
            var go = Instantiate(ai.projectilePrefab, (Vector3)(Eye + dir * 0.25f), Quaternion.identity);
            var p = go.GetComponent<Projectile>();
            if (p == null) return;
            p.lifetime = 4.5f;
            p.knockback = 1f;
            // the original bolt is only 24 px - scaled up to fit the x3 Eye and stay visible in the dark arena
            go.transform.localScale = Vector3.one * boltScale;
            p.radius *= boltScale * 0.8f;
            var bsr = go.GetComponent<SpriteRenderer>();
            if (bsr != null) { bsr.color = boltColor; ai.MatchSorting(bsr, 4); }
            p.Ignore(ignore);
            p.Launch(dir, boltSpeed, boltDamage);
        }

        // ------------------------------------------------------------------ 2. 360° sweeping beam
        private IEnumerator Ray(bool p2)
        {
            SfxHooks.BossMove(this, "Ray"); // sound
            float cd = p2 ? rayCooldown * rayCooldownP2 : rayCooldown;
            nextRay = Time.time + rayWindup + rayDuration + cd;                  // fallback in case the beam is interrupted midway
            int beams = p2 ? 2 : 1;
            float speed = raySpeed * (p2 ? 1.25f : 1f);
            float sign = Random.value < 0.5f ? 1f : -1f;
            Vector2 to = (Vector2)ai.Player.position - Ground;
            float start = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - sign * 55f;     // start offset from the player by ~0.7 s of sweep

            var warn = new List<SpriteRenderer>();
            var body = new List<SpriteRenderer>();
            var hit = new List<SpriteRenderer>();
            var lights = new List<Light2D>();
            for (int k = 0; k < beams; k++)
            {
                warn.Add(MakeLine("ray_warn", new Color(1f, 0.25f, 0.2f, 0f)));
                body.Add(MakeSprite("ray_beam", beamFrames, beamColor, 6));
                hit.Add(MakeSprite("ray_hit", beamImpactFrames, beamColor, 7));
                hit[k].transform.localScale = Vector3.one * 1.4f;
                var l = hit[k].gameObject.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightOuterRadius = 1.6f;
                l.pointLightInnerRadius = 0.1f;
                l.intensity = 0f;
                l.color = beamColor;
                lights.Add(l);
                body[k].enabled = false;
                hit[k].enabled = false;
            }

            // wind-up: red line blinking at the start direction, creeping in the sweep direction to show which way it turns
            ai.PlayState("BeamWindup");
            float t = 0f;
            while (t < rayWindup && !ai.Health.IsDead)
            {
                float a = start + sign * Mathf.Lerp(-12f, 0f, t / rayWindup);
                for (int k = 0; k < beams; k++)
                {
                    Vector2 dir = Dir(a + k * 180f);
                    float len = Cast(Ground, dir);
                    PlaceLine(warn[k], Eye, Ground + dir * len, 0.05f);
                    warn[k].color = new Color(1f, 0.25f, 0.2f, (0.35f + 0.35f * Mathf.Sin(t * 22f)) * Mathf.Clamp01(t * 3f));
                }
                t += Time.deltaTime;
                yield return null;
            }
            foreach (var w in warn) if (w != null) w.enabled = false;
            if (ai.Health.IsDead) yield break;

            ai.PlayState("BeamLoop");
            SoulRequiem.Core.Sfx.Play("eye_beam", Ground, 1f);                          // beam fires
            float nextHum = Time.time + 1.1f;
            t = 0f;
            var prevDir = new Vector2[beams];
            var nextHit = new float[beams];
            for (int k = 0; k < beams; k++) prevDir[k] = Dir(start + k * 180f);
            while (t < rayDuration && !ai.Health.IsDead)
            {
                if (Time.time >= nextHum) { nextHum = Time.time + 1.1f; SoulRequiem.Core.Sfx.Play("eye_beam", Ground, 0.65f); }   // continuous sweeping beam sound
                float a = start + sign * speed * t;
                bool struck = false;
                for (int k = 0; k < beams; k++)
                {
                    Vector2 dir = Dir(a + k * 180f);
                    float len = Cast(Ground, dir);
                    Vector2 end = Ground + dir * len;
                    body[k].enabled = hit[k].enabled = true;
                    PlaceBeam(body[k], Eye, end, t);
                    hit[k].transform.position = end;
                    Animate(hit[k], beamImpactFrames, t, 12f);
                    lights[k].intensity = Mathf.Min(1.2f, t * 4f);
                    // hit by the area swept from last frame's angle to this frame's (a fast beam cannot skip past the body)
                    if (!struck && Time.time >= nextHit[k] && ai.Player != null && RayTouchesPlayer(prevDir[k], dir, len))
                    {
                        ai.Hurt(rayDamage, Ground, 1.5f);
                        nextHit[k] = Time.time + rayTick;
                        struck = true;
                    }
                    prevDir[k] = dir;
                }
                t += Time.deltaTime;
                yield return null;
            }

            ai.PlayState("BeamEnd");
            float f = 0f;
            while (f < 0.25f)
            {
                f += Time.deltaTime;
                for (int k = 0; k < beams; k++)
                {
                    if (body[k] != null) { var c = beamColor; c.a = 1f - f / 0.25f; body[k].color = c; hit[k].color = c; }
                    if (lights[k] != null) lights[k].intensity *= 0.8f;
                }
                yield return null;
            }
            Cleanup(false);
            nextRay = Time.time + cd;                                            // cooldown counted from when the beam ends
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ 3. spinning lash around the pit
        private IEnumerator Spin(bool p2)
        {
            SfxHooks.BossMove(this, "Spin"); // sound
            nextSpin = Time.time + spinCooldown * (p2 ? 0.7f : 1f);
            var ring = MakeRing();
            ai.PlayState("SpinStart");
            float t = 0f;
            while (t < spinWarn && !ai.Health.IsDead)
            {
                float k = t / spinWarn;
                ring.transform.localScale = Vector3.one * spinRadius * Mathf.Lerp(0.6f, 1f, k);
                ring.color = new Color(1f, 0.35f, 0.25f, (0.5f + 0.3f * Mathf.Sin(t * 20f)) * Mathf.Lerp(0.4f, 1f, k));
                t += Time.deltaTime;
                yield return null;
            }
            if (ai.Health.IsDead) yield break;

            ai.PlayState("Spin");
            var fx = MakeSprite("spin_fx", spinFxFrames, Color.white, 1);
            fx.transform.SetParent(transform, false);
            fx.transform.localPosition = Vector3.zero;
            fx.transform.localScale = Vector3.one;
            t = 0f;
            float nextTick = 0f;
            bool pushed = false;
            while (t < spinTime && !ai.Health.IsDead)
            {
                Animate(fx, spinFxFrames, t, 16f);
                ring.transform.localScale = Vector3.one * spinRadius;
                ring.color = new Color(1f, 0.35f, 0.25f, 0.35f);
                if (t >= nextTick)
                {
                    nextTick += spinTick;
                    if (ai.Player != null && ((Vector2)ai.Player.position - Ground).magnitude <= spinRadius)
                    {
                        ai.Hurt(spinDamage, Ground, pushed ? 0.6f : spinPush);
                        pushed = true;
                    }
                }
                t += Time.deltaTime;
                yield return null;
            }
            Cleanup(false);
            ai.PlayState("SpinEnd");
            yield return ai.WaitFor(0.3f);
            // stunned after the spin: counterattack window (body slightly greyed)
            ai.PlayState("Idle");
            if (ai.Body != null)
            {
                var c0 = ai.Body.color;
                ai.Body.color = c0 * new Color(0.7f, 0.7f, 0.8f, 1f);
                yield return ai.WaitFor(p2 ? dizzyTime * 0.6f : dizzyTime);
                if (ai.Body != null) ai.Body.color = c0;
            }
        }

        // ------------------------------------------------------------------ 4. summon toxic enemies
        private IEnumerator Summon(bool p2)
        {
            SfxHooks.BossMove(this, "Summon"); // sound
            nextSummon = Time.time + summonEvery;
            brood.RemoveAll(e => e == null || e.Health == null || e.Health.IsDead);
            int room = summonCap - brood.Count;
            var dir = MapEncounterDirector.Instance;
            if (room <= 0 || dir == null) yield break;

            ai.PlayState("VolleyWindup");
            // no extra hint - visuals / animation / boss title already tell the player
            // pick random spawn points, skipping blocked ones
            var pts = new List<Vector2>();
            foreach (var o in summonPoints)
            {
                Vector2 at = Ground + o;
                bool blocked = false;
                foreach (var c in Physics2D.OverlapCircleAll(at, 0.18f))
                    if (c != null && !c.isTrigger && !ignore.Contains(c)) { blocked = true; break; }
                if (!blocked) pts.Add(at);
            }
            for (int i = pts.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (pts[i], pts[j]) = (pts[j], pts[i]); }
            var wave = p2 && summonWaveP2 != null && summonWaveP2.Length > 0 ? summonWaveP2 : summonWave;   // add melee rats
            int n = Mathf.Min(room, wave.Length, pts.Count);
            int order = ai.Body != null ? ai.Body.sortingOrder - 1 : 0;
            for (int i = 0; i < n; i++)
                PoisonCloud.Spawn(pts[i], cloudPop, cloudLoop, cloudFade, 1.6f, 0.4f, 0.02f, 3f, order);
            yield return ai.Flash(0.9f, new Color(0.55f, 1f, 0.45f));
            for (int i = 0; i < n; i++)
            {
                var e = dir.SpawnSummon(wave[i], pts[i], ai);
                if (e != null) brood.Add(e);
            }
            ai.PlayState("VolleyEnd");
            yield return ai.WaitFor(0.4f);
        }

        /// Is the previous wave all dead (if not, no new wave - the player must clear the field first).
        private bool BroodCleared()
        {
            brood.RemoveAll(e => e == null || e.Health == null || e.Health.IsDead);
            return brood.Count == 0;
        }

        private void KillBrood()
        {
            foreach (var e in brood)
                if (e != null && e.Health != null && !e.Health.IsDead) { e.Health.Invulnerable = false; e.Health.TakeDamage(99999f); }
            brood.Clear();
        }

        // ------------------------------------------------------------------ helpers
        private void CollectIgnore()
        {
            ignore.Clear();
            foreach (var c in GetComponentsInChildren<Collider2D>()) ignore.Add(c);
            foreach (var c in Physics2D.OverlapPointAll(Ground)) ignore.Add(c);      // solid block of the pit (the Eye floats inside it)
            foreach (var c in Physics2D.OverlapPointAll(Eye)) ignore.Add(c);
        }

        /// How far the beam travels from the pit centre along dir before hitting a pillar / wall (ignores triggers, water, enemies, the player, the pit).
        private float Cast(Vector2 origin, Vector2 dir)
        {
            var hits = Physics2D.RaycastAll(origin, dir, rayRange);
            float best = rayRange;
            foreach (var h in hits)
            {
                var c = h.collider;
                if (c == null || c.isTrigger || ignore.Contains(c) || c.gameObject.layer == waterLayer) continue;
                if (c.GetComponentInParent<EnemyAI>() != null) continue;
                var hp = c.GetComponentInParent<Health>();
                if (hp != null && hp.IsPlayer) continue;
                if (h.distance < best) best = h.distance;
            }
            return Mathf.Max(0.3f, best);
        }

        /// <summary>Does a beam (origin at the pit centre) sweeping from `from` to `to` (length `len`, already cut by pillars / walls) touch Ren's feet or body.</summary>
        private bool RayTouchesPlayer(Vector2 from, Vector2 to, float len)
        {
            Vector2 feet = ai.Player.position;
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = feet + Vector2.up * (i == 0 ? 0.1f : 0.45f);   // feet, mid body
                Vector2 v = p - Ground;
                float r = v.magnitude;
                if (r > len + 0.12f) continue;                             // behind a pillar / out of range
                if (SegmentDistance(p, Ground, Ground + to * len) <= rayHalfWidth + 0.12f) return true;
                float c0 = from.x * v.y - from.y * v.x, c1 = to.x * v.y - to.y * v.x;
                if (c0 * c1 <= 0f && Vector2.Dot(v, to) > 0f && Vector2.Dot(v, from) > 0f) return true;   // beam passed between 2 frames
            }
            return false;
        }

        private static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * k)).magnitude;
        }

        private SpriteRenderer MakeSprite(string name, Sprite[] frames, Color c, int orderAdd)
        {
            var go = new GameObject(name);
            temp.Add(go);
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            r.color = c;
            var body = ai != null ? ai.Body : null;
            if (body != null) { r.sortingLayerID = body.sortingLayerID; r.sortingOrder = body.sortingOrder + orderAdd; }
            return r;
        }

        private SpriteRenderer MakeLine(string name, Color c)
        {
            var r = MakeSprite(name, null, c, 5);
            r.sprite = PixelSprite;
            return r;
        }

        private SpriteRenderer MakeRing()
        {
            var r = MakeSprite("spin_warn", null, new Color(1f, 0.35f, 0.25f, 0f), 0);
            r.sprite = RingSprite;
            r.sortingOrder = -4;                               // above decals, below characters (like GroundStrike)
            r.transform.position = Ground;
            return r;
        }

        private static void PlaceLine(SpriteRenderer r, Vector2 a, Vector2 b, float width)
        {
            Vector2 d = b - a;
            r.transform.position = a;
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude, width, 1f);           // PixelSprite: 1 x 1 unit, pivot left-centre
        }

        private void PlaceBeam(SpriteRenderer r, Vector2 a, Vector2 b, float t)
        {
            Animate(r, beamFrames, t, 12f);
            Vector2 d = b - a;
            float w = r.sprite != null ? r.sprite.bounds.size.x : 0.32f;
            r.transform.position = a;
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude / Mathf.Max(0.01f, w), rayThickness * (1f + 0.1f * Mathf.Sin(t * 30f)), 1f);
        }

        private static void Animate(SpriteRenderer r, Sprite[] frames, float t, float fps)
        {
            if (r == null || frames == null || frames.Length == 0) return;
            r.sprite = frames[Mathf.FloorToInt(t * fps) % frames.Length];
        }

        private void Cleanup(bool final)
        {
            foreach (var g in temp) if (g != null) Destroy(g);
            temp.Clear();
            if (final && camFollow != null) { camFollow.ClearZoom(); camFollow = null; }
        }

        private static Sprite PixelSprite
        {
            get
            {
                if (pixelSprite != null) return pixelSprite;
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
                return pixelSprite;
            }
        }

        /// Warning circle: 2 units diameter at scale 1 (scale = radius).
        private static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null) return ringSprite;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                float c = (n - 1) / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        byte a = r > 1f ? (byte)0 : r > 0.92f ? (byte)235 : (byte)(30 + 60 * r);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
                return ringSprite;
            }
        }
    }
}
