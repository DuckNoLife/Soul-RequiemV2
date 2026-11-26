using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Shared frame for the Map 3 bosses (Morrow, Isolde). Fixes the old problem "ranged boss keeps backing straight away -> stuck in a corner -> easy to hit":
    ///   1. ARENA: the boss remembers the arena ellipse centre + radii (taken at its spawn point). Every destination is clamped inside the ellipse.
    ///   2. NO STRAIGHT RETREAT: while waiting for a skill, the boss strafes around the player at its preferred range, drifting toward the centre.
    ///   3. ANTI-STUCK / ANTI-PIN: too long near the ellipse edge, stuck without moving, or pressed by the player constantly
    ///      -> the boss's own reposition skill (Morrow: Vespers Step; Isolde: Mist Step) to a point far from the player.
    ///   4. MELEE: player within close range -> the boss's own melee attack (Bell Sweep / Blood Lash) instead of backing off.
    ///   5. RHYTHM: always has a skill ready; if none is ready it strafes at most 0.7 s and picks again -> never stands idle.
    ///   6. BREAK: when the player solves the arena mechanic, the boss kneels (Stagger) and takes x2 damage for a few seconds.
    /// Subclasses implement Think() (pick a skill) and Reposition() (how to escape a corner).
    /// </summary>
    public abstract class BossSkillsBase : MonoBehaviour, IEnemySkills
    {
        [Header("Arena (Unity units, from the spawn point)")]
        public Vector2 arenaRadii = new Vector2(2.9f, 1.9f);
        public Vector2 arenaOffset = Vector2.zero;
        [Header("Range")]
        public float meleeRange = 0.55f;          // body-to-body gap for a melee attack
        public float preferredRange = 1.7f;       // preferred range while strafing
        [Header("Anti-stuck")]
        public float edgeLimit = 0.86f, edgeTimeMax = 1.2f, pinnedTimeMax = 2.2f, stuckTimeMax = 0.7f, repositionCooldown = 3f;

        protected EnemyAI ai;
        protected Health hp;
        protected Vector2 center;
        protected bool engaged;
        protected bool busy;                       // currently casting (not counted as stuck)
        protected int phase = 1;
        protected float breakUntil;
        protected readonly Dictionary<string, float> ready = new Dictionary<string, float>();

        private float edgeT, pinT, stuckT, nextReposition;
        private float nextGlobal;                  // shared rest between two skills (melee not included)
        private bool usedSkill;
        private Vector2 lastPos;
        private int strafeSign = 1;
        private float strafeFlipAt;

        protected int Layer => ai != null ? ai.SortingLayer : 0;
        protected int Order => ai != null ? ai.SortingOrder : 2;
        protected Vector2 Pos => transform.position;
        protected Vector2 PlayerPos => ai != null && ai.Player != null ? (Vector2)ai.Player.position : Pos;
        protected bool Broken => Time.time < breakUntil;

        // ------------------------------------------------------------------ lifecycle
        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (hp == null) hp = ai.Health;
            if (!engaged)
            {
                engaged = true;
                center = (Vector2)transform.position + arenaOffset;
                lastPos = Pos;
                OnEngage();
            }
            if (ai.Player == null) yield break;
            int newPhase = PhaseFor(hp.Percent);
            if (newPhase > phase) { phase = newPhase; busy = true; yield return OnPhase(phase); busy = false; yield break; }
            if (!Broken && NeedsReposition())
            {
                nextReposition = Time.time + repositionCooldown;
                busy = true; yield return Reposition(); busy = false;
                nextGlobal = Time.time + Gcd();
                ResetTrackers();
                yield break;
            }
            busy = true;
            usedSkill = false;
            yield return Think();
            busy = false;
            if (usedSkill) nextGlobal = Time.time + Gcd();       // after each skill the boss only strafes / melees for one beat -> counterattack window
        }

        protected abstract IEnumerator Think();
        protected abstract IEnumerator Reposition();
        protected virtual void OnEngage() { }
        protected virtual int PhaseFor(float hpPercent) => 1;
        protected virtual IEnumerator OnPhase(int p) { yield break; }

        protected virtual void Update()
        {
            if (ai == null || !engaged || hp == null || hp.IsDead) return;
            float dt = Time.deltaTime;
            // near the arena edge
            edgeT = EllipseR(Pos) > edgeLimit ? edgeT + dt : Mathf.Max(0f, edgeT - dt * 2f);
            // pressed constantly (the player is "pinning" the boss)
            pinT = ai.GapToPlayer() < meleeRange * 0.8f ? pinT + dt : Mathf.Max(0f, pinT - dt * 1.5f);
            // wants to move but does not (stuck on an obstacle)
            float moved = (Pos - lastPos).magnitude;
            lastPos = Pos;
            bool wants = ai.Desired.sqrMagnitude > 0.04f;
            stuckT = wants && moved < ai.Desired.magnitude * dt * 0.25f ? stuckT + dt : 0f;
        }

        private bool NeedsReposition() =>
            Time.time >= nextReposition && (edgeT > edgeTimeMax || pinT > pinnedTimeMax || stuckT > stuckTimeMax);

        protected void ResetTrackers() { edgeT = pinT = stuckT = 0f; lastPos = Pos; }

        // ------------------------------------------------------------------ arena
        /// 0 = centre, 1 = ellipse edge.
        protected float EllipseR(Vector2 p)
        {
            Vector2 d = p - center;
            return Mathf.Sqrt((d.x * d.x) / (arenaRadii.x * arenaRadii.x) + (d.y * d.y) / (arenaRadii.y * arenaRadii.y));
        }

        protected Vector2 ClampArena(Vector2 p, float k = 0.8f)
        {
            float r = EllipseR(p);
            return r <= k || r < 0.0001f ? p : center + (p - center) * (k / r);
        }

        /// Arena point farthest from the player among n candidates (corner escape, teleport).
        protected Vector2 FarPoint(float k = 0.65f, int n = 12, IList<Vector2> extra = null)
        {
            Vector2 best = center; float bestD = -1f;
            var cands = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                cands.Add(center + new Vector2(Mathf.Cos(a) * arenaRadii.x, Mathf.Sin(a) * arenaRadii.y) * k);
            }
            cands.Add(center);
            if (extra != null) cands.AddRange(extra);
            foreach (var c in cands)
            {
                float d = (c - PlayerPos).magnitude;
                if (d > bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// Strafe around the player at the preferred range (no straight retreat), pulled toward the centre when far out.
        protected IEnumerator Stalk(float seconds, float speedMul = 1f)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !hp.IsDead && ai.Player != null)
            {
                if (Time.time > strafeFlipAt) { strafeSign = Random.value < 0.5f ? -1 : 1; strafeFlipAt = Time.time + Random.Range(1.2f, 2.4f); }
                Vector2 toP = PlayerPos - Pos;
                float d = toP.magnitude;
                Vector2 tangent = Vector2.Perpendicular(toP.normalized) * strafeSign;
                Vector2 radial = d > preferredRange * 1.15f ? toP.normalized : d < preferredRange * 0.7f ? tangent * 0.3f : Vector2.zero;
                Vector2 toCenter = (center - Pos) * Mathf.Clamp01(EllipseR(Pos) - 0.5f) * 1.6f;
                Vector2 v = (tangent * 0.8f + radial + toCenter);
                Vector2 next = ClampArena(Pos + v.normalized * 0.4f, 0.82f);
                ai.Desired = (next - Pos).normalized * ai.moveSpeed * speedMul;
                ai.FaceX(toP.x);
                ai.PlayState("Move", false);
                yield return null;
            }
            ai.Desired = Vector2.zero;
        }

        // ------------------------------------------------------------------ skills: helpers
        protected bool Ready(string key) => !ready.TryGetValue(key, out float t) || Time.time >= t;
        /// Ready + shared rest elapsed (used by every skill except melee).
        protected bool ReadyG(string key) => Ready(key) && Time.time >= nextGlobal;
        protected void Use(string key, float cd)
        {
            ready[key] = Time.time + cd * CooldownMul();
            if (cd > 0.5f) usedSkill = true;
        }
        protected virtual float CooldownMul() => 1f;
        /// Rest after each skill (seconds): long in phase 1, short in phase 3.
        protected virtual float Gcd() => phase == 1 ? 1.7f : phase == 2 ? 1.35f : 1.05f;

        /// Player status effects (slow, skill seal, heal block, stun).
        protected PlayerStatus PS => ai != null && ai.Player != null ? PlayerStatus.Of(ai.Player) : null;

        protected void Hint(string text, float s = 2.5f)
        {
            Hints.Show(text, s);
        }

        protected static void Kick(Vector2 dir, float amp, float dur) => CameraFollow.KickMain(dir, amp, dur);

        /// Break: kneel, take x2 damage for `seconds`. Called when the player solves the arena mechanic.
        protected IEnumerator BreakRoutine(float seconds, string text)
        {
            breakUntil = Time.time + seconds;
            ai.Desired = Vector2.zero;
            Hint(text, 2.5f);
            SpriteVfx.Spawn("m3_break", Pos + Vector2.up * 0.5f, 0.7f, Layer, Order + 3, false, 14f);
            Kick(Vector2.down, 0.05f, 0.25f);
            hp.DamageTakenMul = 2f;
            ai.PlayState(ai.HasAnimState("Stagger") ? "Stagger" : "Idle");
            yield return ai.Flash(seconds, new Color(1f, 1f, 0.6f));
            hp.DamageTakenMul = 1f;
            ai.PlayState("Idle");
        }

        /// Fade the boss body to alpha `target` (vanish / reappear).
        protected IEnumerator Fade(float target, float seconds)
        {
            var sr = ai.Body;
            if (sr == null) yield break;
            float a0 = sr.color.a, t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var c = sr.color; c.a = Mathf.Lerp(a0, target, t / Mathf.Max(0.0001f, seconds)); sr.color = c;
                yield return null;
            }
            var cc = sr.color; cc.a = target; sr.color = cc;
        }

        /// Hide the boss completely while it travels as bats or hides in a coffin: body renderer off, colliders off, invulnerable.
        /// The renderer is switched off rather than made transparent because tint flashes (Health.TintPulse from katana statuses,
        /// burn, hit flash) write the full body colour back and would make a transparent body visible again.
        /// The blob shadow (DepthEffects.BlobShadowFollow) and the status icon (EnemyStatus.UpdateIcon) follow the renderer state.
        /// Called from IsoldeSkills.Attacks.cs (Bat Dive, Night Flight).
        protected void SetHidden(bool hidden)
        {
            hp.Invulnerable = hidden;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = !hidden;
            var body = ai.Body;
            if (body == null) return;
            body.enabled = !hidden;
            var col = body.color; col.a = 1f; body.color = col;
        }

        /// Short "white lock" beat before a big attack: the player can read exactly when to dodge.
        /// telegraph = boss body flashes colour + GATHERING PARTICLES (tinted abs_charge2) at the chest + light grows, then bursts.
        protected IEnumerator Telegraph(float seconds, Color c)
        {
            ChargeFx(c, seconds);
            yield return ai.Flash(seconds, c);
        }

        // ------------------------------------------------------------------ "burst" kit (BossJuice) for the Map 3 bosses
        /// Charge point (boss chest).
        protected virtual Vector2 CastPoint => Pos + Vector2.up * 0.45f;

        /// Gathering particles + light growing over `seconds`, flash at the end (the player reads "about to fire").
        protected void ChargeFx(Color c, float seconds, float scale = 0.75f)
        {
            if (seconds <= 0.05f) return;
            var fx = SpriteVfx.Spawn("abs_charge2", CastPoint, scale, Layer, Order + 2, false, 12f / Mathf.Max(0.3f, seconds), false);
            if (fx != null) { fx.Tint(c); fx.Follow(transform); }
            var l = BossJuice.Light(CastPoint, c, 1.4f, 0.15f, 0f, transform);
            BossJuice.Run(RampLight(l, 1.3f, seconds));
        }

        private static IEnumerator RampLight(UnityEngine.Rendering.Universal.Light2D l, float peak, float seconds)
        {
            for (float t = 0f; t < seconds && l != null; t += Time.deltaTime)
            {
                float k = t / seconds;
                l.intensity = Mathf.Lerp(0.15f, peak, k * k);
                l.pointLightOuterRadius = Mathf.Lerp(1.4f, 1.9f, k);
                yield return null;
            }
            for (float t = 0f; t < 0.22f && l != null; t += Time.deltaTime)
            {
                l.intensity = peak * 1.4f * (1f - t / 0.22f);
                yield return null;
            }
            if (l != null) Destroy(l.gameObject);
        }

        /// Skill name on the HUD (cast bar) - windup = wind-up time, 0 = flash the name at once.
        protected void Announce(string skill, Color c, float windup) => BossJuice.Cast(hp, skill, c, windup);
        /// Skill just fired: HUD flashes the name.
        protected void Fired() => BossJuice.Fire(hp);
        /// "Burst" beat at a point (core flash + light + shake by level 1-3).
        protected void Boom(Vector2 at, Color c, float size, int tier) => BossJuice.Impact(at, c, size, tier, Layer, Order + 2);

        // ------------------------------------------------------------------ shared PixelLab VFX
        /// Ring wave with a PixelLab shell (m3_wave_hymn / m3_wave_blood pieces laid around the ring).
        protected Shockwave Wave(Vector2 at, float r, float dur, float dmg, string crestName, float alpha = 1f)
        {
            var w = Shockwave.Spawn(at, r, dur, dmg, Order - 1, new Color(1f, 1f, 1f, alpha));
            // the old 12x20 px wave pieces were too small -> 24x36 crescent pieces with a hot white core (m3x_crest_*), laid denser,
            // + a wave-coloured light flash at the centre (the wave "bursts" out instead of just drifting)
            bool blood = crestName == "m3_wave_blood", hymn = crestName == "m3_wave_hymn";
            var f = SpriteVfx.Load(blood ? "m3x_crest_blood" : hymn ? "m3x_crest_hymn" : crestName);
            if (f.Length == 0) f = SpriteVfx.Load(crestName);
            if (f.Length > 0) w.crest = f;
            if (blood || hymn)
            {
                w.crestScale = Mathf.Lerp(0.75f, 1.05f, Mathf.Clamp01((r - 1f) / 3f));
                w.crestSpacing = 0.17f;
                var wc = blood ? new Color(1f, 0.25f, 0.32f) : new Color(0.55f, 0.8f, 1f);
                BossJuice.Flash(at, wc, Mathf.Min(3.2f, r * 0.6f + 0.6f), 1.2f, 0.4f);
            }
            return w;
        }

        /// Beam effect (PixelLab image drawn horizontally, origin on the left) stretched from a to b. nativeLen = image length (units).
        protected SpriteVfx Ray(string name, Vector2 a, Vector2 b, float nativeLen, float thick, int order, float fps, float loopSeconds = 0f)
        {
            var v = SpriteVfx.Spawn(name, a, 1f, Layer, order, false, fps);
            if (v == null) return null;
            if (loopSeconds > 0f) v.Looping(loopSeconds);
            PlaceRay(v, a, b, nativeLen, thick);
            return v;
        }

        protected static void PlaceRay(SpriteVfx v, Vector2 a, Vector2 b, float nativeLen, float thick)
        {
            if (v == null) return;
            Vector2 d = b - a;
            v.transform.position = a;
            v.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            v.transform.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude / nativeLen), thick, 1f);
        }

        /// One particle (blood drop...) curving lightly from `from` to object `to`, then fading - a stream of drained blood.
        protected IEnumerator FlyMote(string name, Vector2 from, Transform to, Vector2 toOffset, float seconds, float scale)
        {
            bool faces = name.StartsWith("m3x_");                        // directional bullet image (drawn flying right) -> rotate along the flight path
            var v = SpriteVfx.Spawn(name, from, scale, Layer, Order + 1, false, 12f);
            if (v == null) yield break;
            v.Looping(seconds + 0.3f);
            float t = 0f;
            Vector2 prev = from;
            while (t < seconds && v != null && to != null)
            {
                t += Time.deltaTime;
                float k = t / seconds;
                Vector2 p = Vector2.Lerp(from, (Vector2)to.position + toOffset, k * k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 0.2f;
                if (faces && (p - prev).sqrMagnitude > 1e-6f) v.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.y - prev.y, p.x - prev.x) * Mathf.Rad2Deg);
                prev = p;
                v.transform.position = p;
                yield return null;
            }
            if (v != null) v.Stop();
        }

        /// Distance from p to segment ab.
        protected static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        protected static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        protected virtual void OnDisable()
        {
            if (hp != null) BossJuice.Clear(hp);                         // turn off the cast bar
            if (hp != null) { hp.DamageTakenMul = 1f; hp.Invulnerable = false; }
        }
    }
}
