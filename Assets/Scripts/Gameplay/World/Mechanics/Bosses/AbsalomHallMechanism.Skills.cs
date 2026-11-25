using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ABSALOM SKILL KIT — 5 SCHOOLS, each with its own dodge (not "the same bullet in a different colour"):
    ///   Light  (gold)         — Sunlance (light lances along lines), Halo Ring (expanding light ring with a gap), Sunfall (falling light pillars). -> .Light.cs
    ///   Fire   (orange)       — Pyre Lines (3 lines burn into fire walls on the floor), Cinder Rain (meteors leaving fire pools).              -> .Fire.cs
    ///   Frost  (blue)         — Rime Thorns (ice thorns erupting in a chasing path), Glacial Volley (sniping ice lances with aim lines).       -> .Frost.cs
    ///   Storm  (violet-white) — Tempest Lattice (lightning rods linked by bolts — do not cross), Thunderclap (rings struck from inside out).  -> .Storm.cs
    ///   Umbral (violet)       — Umbral Orbs (dark orbs that chase then burst into shards), Abyssal Well (black hole pulling Ren in).         -> .Umbral.cs
    /// This file: school colours / names, the cast (Cast — charge + skill name on the HUD board), bullets (Shot), FLOOR HAZARDS (Hazard: circle / segment /
    /// annulus; standing inside = damage per tick; a roll avoids it; frozen during the time stop), warning rings (RingSprite), jagged lightning,
    /// pulling Ren (black hole). Every skill is TELEGRAPHED (line / ring / charge ≥ 0.5 s). Damage ~8–12% of Ren's HP per hit.
    /// Each skill has 4 beats — WARN (zone / line on the floor) -> CHARGE (cast animation + particles in the school colour) -> BURST (visual 2–3x size,
    /// white-hot core, real LIGHT FLASH on the floor: FlashLight, 3-tier SCREEN SHAKE: Shake) -> AFTERMATH (scorch, smoke…). Visuals standing on the floor
    /// (fire walls…) sort by their feet like characters (Grounded) so Ren walks behind / in front correctly.
    /// Scale rules: bullets 0.5–1.2 u, bursts 0.8–2 u with white-hot cores; big bullets GLOW (HallBullet.Glow); charging = school-colour particles (abs_charge2)
    /// + hand light flash + the floor SUMMON CIRCLE switches to the school colour and flares (SealCast); lightning has 3 layers (glow / body / white core) with lights along it.
    /// Called by: every school / phase file of AbsalomHallMechanism. Related: HallBullet, TimeStop, SpriteVfx.
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private enum School { Light, Fire, Frost, Storm, Umbral }

        private static readonly Color Fire = new Color(1f, 0.55f, 0.28f), Frost = new Color(0.55f, 0.88f, 1f), Storm = new Color(0.8f, 0.76f, 1f),
                                      Sun = new Color(1f, 0.9f, 0.58f), Umbral = new Color(0.66f, 0.42f, 0.98f);

        private static Color SchoolColor(School s)
        {
            switch (s)
            {
                case School.Fire: return Fire;
                case School.Frost: return Frost;
                case School.Storm: return Storm;
                case School.Umbral: return Umbral;
                default: return Sun;
            }
        }

        private static string SchoolName(School s) => s.ToString().ToUpperInvariant();

        private Vector2 Hand => abs.Pos + Vector2.up * (0.6f * abs.Size + abs.Lift);

        private static Vector2 Rot(Vector2 v, float deg) => Quaternion.Euler(0f, 0f, deg) * v;

        private Vector2 RandomDaisPoint() => new Vector2(Random.Range(dais.xMin, dais.xMax), Random.Range(dais.yMin, dais.yMax));

        /// <summary>Clamp a point inside the hall (keeping a margin).</summary>
        private Vector2 InArena(Vector2 p, float margin = 0.3f) =>
            new Vector2(Mathf.Clamp(p.x, A.xMin + margin, A.xMax - margin), Mathf.Clamp(p.y, A.yMin + margin * 0.7f, A.yMax - margin));

        // ------------------------------------------------------------------ bullets
        /// <summary>Small light bullet (rings / time stop).</summary>
        private HallBullet Shot(Vector2 at, Vector2 vel, Color tint, float scale = 0.55f, float dmg = 9f, float radius = 0.1f, float life = 5f)
            => Shot("abs_light_mote", at, vel, tint, scale, dmg, radius, life);

        private HallBullet Shot(string sheet, Vector2 at, Vector2 vel, Color tint, float scale, float dmg, float radius, float life)
            => HallBullet.Spawn(SpriteVfx.Load(sheet), at, vel, dmg, radius, life, tint, scale, Layer, Order + 3);

        // ------------------------------------------------------------------ casting
        /// <summary>Charge a skill: face Ren, raise hands, school-colour flash, particles gather in the hands, SKILL NAME on the HUD board (the player learns name + colour = dodge).</summary>
        private IEnumerator Cast(School s, string skill, float windup, string sfx)
        {
            Color c = SchoolColor(s);
            abs.Face(PlayerFeet - abs.Pos);
            abs.Play("strike", Mathf.Max(6f, 9f / Mathf.Max(0.3f, windup)), false);
            abs.Flash(c, windup + 0.25f);
            // big gathering particles (110 px, 12 frames — fits the charge time exactly, last frame bursts) + school-colour hand light + summon circle flares
            var fx = SpriteVfx.Spawn("abs_charge2", Hand, 1f, Layer, Order + 4, false, 12f / Mathf.Max(0.3f, windup), false);
            if (fx != null) fx.Tint(c);
            FlashLight(Hand, c, 1.6f, 1.1f, windup + 0.3f);
            SealCast(c, windup + 0.6f);
            if (!string.IsNullOrEmpty(skill)) DrillBoard.Say(skill, c);
            NoteSkill(s, skill);                                                        // HUD board teaches how to dodge this skill
            if (!string.IsNullOrEmpty(sfx)) Sfx.Play(sfx, abs.Pos, 0.7f);
            yield return Wait(windup);
        }

        private IEnumerator Glide(Vector2 to, float speed)
        {
            abs.Play("walk", 9f);
            while (!aborted && (abs.Pos - to).sqrMagnitude > 0.0004f)
            {
                abs.Face(to - abs.Pos);
                abs.Pos = Vector2.MoveTowards(abs.Pos, to, speed * Time.deltaTime);
                yield return null;
            }
            abs.Play("idle", 5f);
            abs.Face(PlayerFeet - abs.Pos);
        }

        // ----------------------------------------------------------------- "burst" beat — light flash, shake, sort by feet
        /// <summary>Light flash: goes to `intensity` then fades over `seconds` (really lights the floor + Ren — sells the explosion).</summary>
        private void FlashLight(Vector2 at, Color col, float radius, float intensity, float seconds)
        {
            var go = GlowAt(at, col, radius, intensity, 0f);
            Ctx.Run(FadeLight(go, intensity, seconds));
        }

        private static IEnumerator FadeLight(GameObject go, float i0, float seconds)
        {
            var l = go != null ? go.GetComponent<Light2D>() : null;
            for (float t = 0f; t < seconds && l != null; t += Time.deltaTime)
            {
                float k = 1f - t / seconds;
                l.intensity = i0 * k * k;
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        /// <summary>3-tier screen shake: 1 light (normal hit), 2 medium (big hit), 3 heavy (signature skill).</summary>
        private static void Shake(int tier)
        {
            float amp = tier >= 3 ? 0.16f : tier == 2 ? 0.09f : 0.045f, dur = tier >= 3 ? 0.45f : tier == 2 ? 0.3f : 0.18f;
            CameraFollow.KickMain(Vector2.down, amp, dur);
        }

        /// <summary>Effect sprite STANDING on the floor (fire wall, pillar…): same layer + order as characters, sorted by its feet (pivot).</summary>
        private SpriteVfx Grounded(string sheet, Vector2 at, float scale, float fps, bool loop, float seconds)
        {
            var fx = SpriteVfx.Spawn(sheet, at, scale, Layer, Order, false, fps, false);
            if (fx == null) return null;
            var sr = fx.GetComponent<SpriteRenderer>();
            if (sr != null) sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (loop) fx.Looping(seconds);
            return fx;
        }

        // ------------------------------------------------------------------ damage to Ren (zones / beams — bullets handle themselves in HallBullet)
        private bool HurtRen(float dmg, Vector2 from, float knock = 1f)
        {
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead || hp.Invulnerable || TimeStop.Active || frozenRen) return false;
            Vector2 d = PlayerFeet - from;
            Health.IncomingRanged = true;
            try { return hp.TakeDamage(dmg, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down, knock); }
            finally { Health.IncomingRanged = false; }
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        // ------------------------------------------------------------------ FLOOR HAZARDS
        private enum HazardShape { Circle, Segment, Band }

        private sealed class Hazard
        {
            public HazardShape Shape;
            public Vector2 A, B;
            public float R, W, Dmg, Tick = 0.5f, ArmAt, EndAt, NextHit;
            public readonly List<SpriteVfx> Fx = new List<SpriteVfx>();
            public readonly List<GameObject> Objs = new List<GameObject>();
            public bool Dead;
        }

        private readonly List<Hazard> hazards = new List<Hazard>();

        private Hazard AddHazard(HazardShape shape, Vector2 a, Vector2 b, float r, float w, float dmg, float armIn, float seconds, float tick = 0.5f)
        {
            var h = new Hazard { Shape = shape, A = a, B = b, R = r, W = w, Dmg = dmg, Tick = tick, ArmAt = Time.time + armIn, EndAt = Time.time + armIn + seconds };
            hazards.Add(h);
            return h;
        }

        private static bool InHazard(Hazard h, Vector2 p)
        {
            switch (h.Shape)
            {
                case HazardShape.Circle: return (p - h.A).sqrMagnitude <= h.R * h.R;
                case HazardShape.Segment: return SegDist(p, h.A, h.B) <= h.W;
                default: return Mathf.Abs((p - h.A).magnitude - h.R) <= h.W;
            }
        }

        /// <summary>Every frame (fight): a zone that is "live" with Ren inside -> damage per tick. Time stop: zones freeze (their clocks shift forward).</summary>
        private void TickHazards(float dt)
        {
            bool stopped = TimeStop.Active;
            Vector2 feet = PlayerFeet;
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                var h = hazards[i];
                if (stopped && !h.Dead) { h.ArmAt += dt; h.EndAt += dt; h.NextHit += dt; continue; }
                if (h.Dead || Time.time >= h.EndAt) { KillHazard(h); hazards.RemoveAt(i); continue; }
                if (Time.time < h.ArmAt || h.Dmg <= 0f || Time.time < h.NextHit) continue;
                if (!InHazard(h, feet)) continue;
                Vector2 from = h.Shape == HazardShape.Segment ? (h.A + h.B) * 0.5f : h.A;
                if (HurtRen(h.Dmg, from, 0.6f)) h.NextHit = Time.time + h.Tick;
            }
        }

        private static void KillHazard(Hazard h)
        {
            foreach (var f in h.Fx) if (f != null) f.Stop();
            foreach (var o in h.Objs) if (o != null) Object.Destroy(o);
            h.Fx.Clear(); h.Objs.Clear();
        }

        private void ClearHazards()
        {
            foreach (var h in hazards) KillHazard(h);
            hazards.Clear();
            ClearRods();
            ClearBeams();                                                               // tia Judgement Ray / Void Cannon
            wellPull = false;
        }

        /// <summary>Looping visual (floor fire, pool, black hole…) bound to a zone, removed with the zone.</summary>
        private SpriteVfx HazardFx(Hazard h, string sheet, Vector2 at, float scale, int order, float fps, Color tint)
        {
            var fx = SpriteVfx.Spawn(sheet, at, scale, Layer, order, false, fps, false);
            if (fx == null) return null;
            fx.Looping(999f);                                                          // the zone removes its visuals when it ends (KillHazard)
            fx.Tint(tint);
            h.Fx.Add(fx);
            return fx;
        }

        // ------------------------------------------------------------------ warning rings (annulus) — sprite built at runtime, 50 px / u (double-size pixels, still crisp)
        private static readonly Dictionary<int, Sprite> ringCache = new Dictionary<int, Sprite>();

        private static Sprite RingSprite(float r, float w)
        {
            int key = Mathf.RoundToInt(r * 50f) * 1000 + Mathf.RoundToInt(w * 50f);
            if (ringCache.TryGetValue(key, out var sp) && sp != null) return sp;
            const float Ppu = 50f;
            int n = Mathf.CeilToInt(2f * (r + w) * Ppu) + 2;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f, edge = 1.6f / Ppu;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / Ppu;
                    float o = Mathf.Abs(d - r);
                    byte a = o > w ? (byte)0 : o > w - edge ? (byte)235 : (byte)70;
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), Ppu);
            ringCache[key] = sp;
            return sp;
        }

        /// <summary>Warning ring / disc on the floor (below characters). w ≥ r = solid disc. Destroys itself after `seconds` (0 = caller destroys it).</summary>
        private SpriteRenderer RingMark(Vector2 c, float r, float w, Color col, float seconds)
        {
            var go = new GameObject("Absalom Ring Mark");
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = c;
            var sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sprite = RingSprite(r, w);
            sr.sortingOrder = -4;                                                       // same as the GroundStrike warning ring: above floor decals, below characters
            sr.color = col;
            if (seconds > 0f) Object.Destroy(go, seconds);
            return sr;
        }

        // ------------------------------------------------------------------ jagged lightning (1 px stretched, re-jitters 20 times / s)
        /// <summary>3-LAYER lightning — colour glow (wide, faint), colour body, white core; 6 jagged segments, re-jitters 20 times / s; 2 lights along the bolt.</summary>
        private sealed class Bolt
        {
            public readonly SpriteRenderer[] Seg = new SpriteRenderer[6], Mid = new SpriteRenderer[6], Halo = new SpriteRenderer[6];
            public readonly List<GameObject> Lights = new List<GameObject>();
            public Vector2 A, B;
            public float NextJitter;
        }

        private Bolt MakeBolt(Vector2 a, Vector2 b, Color col)
        {
            var bolt = new Bolt { A = a, B = b };
            for (int i = 0; i < bolt.Seg.Length; i++)
            {
                bolt.Halo[i] = MakeLine("Storm Arc Halo"); bolt.Halo[i].sortingOrder = Order + 1; bolt.Halo[i].color = new Color(col.r * 0.75f, col.g * 0.65f, col.b, 0.35f); bolt.Halo[i].enabled = true;
                bolt.Mid[i] = MakeLine("Storm Arc"); bolt.Mid[i].sortingOrder = Order + 2; bolt.Mid[i].color = new Color(col.r, col.g, col.b, 0.95f); bolt.Mid[i].enabled = true;
                bolt.Seg[i] = MakeLine("Storm Arc Core"); bolt.Seg[i].sortingOrder = Order + 3; bolt.Seg[i].color = Color.white; bolt.Seg[i].enabled = true;
            }
            bolt.Lights.Add(GlowAt(Vector2.Lerp(a, b, 0.3f), col, 1.3f, 1.1f, 0f));
            bolt.Lights.Add(GlowAt(Vector2.Lerp(a, b, 0.7f), col, 1.3f, 1.1f, 0f));
            JitterBolt(bolt, true);
            return bolt;
        }

        private static void JitterBolt(Bolt bolt, bool force)
        {
            if (!force && Time.time < bolt.NextJitter) return;
            bolt.NextJitter = Time.time + 0.05f;
            Vector2 d = bolt.B - bolt.A, n = new Vector2(-d.y, d.x).normalized;
            var pts = new Vector2[bolt.Seg.Length + 1];
            pts[0] = bolt.A; pts[pts.Length - 1] = bolt.B;
            for (int i = 1; i < pts.Length - 1; i++) pts[i] = bolt.A + d * (i / (float)(pts.Length - 1)) + n * Random.Range(-0.13f, 0.13f);
            for (int i = 0; i < bolt.Seg.Length; i++)
            {
                Stretch(bolt.Halo[i], pts[i], pts[i + 1], Random.Range(0.16f, 0.22f));
                Stretch(bolt.Mid[i], pts[i], pts[i + 1], Random.Range(0.06f, 0.08f));
                Stretch(bolt.Seg[i], pts[i], pts[i + 1], Random.Range(0.022f, 0.035f));
            }
            foreach (var l in bolt.Lights)
            {
                var li = l != null ? l.GetComponent<Light2D>() : null;
                if (li != null) li.intensity = Random.Range(0.7f, 1.5f);                // lights flicker with the bolt
            }
        }

        private static void KillBolt(Bolt bolt)
        {
            if (bolt == null) return;
            foreach (var s in bolt.Seg) if (s != null) Object.Destroy(s.gameObject);
            foreach (var s in bolt.Mid) if (s != null) Object.Destroy(s.gameObject);
            foreach (var s in bolt.Halo) if (s != null) Object.Destroy(s.gameObject);
            foreach (var l in bolt.Lights) if (l != null) Object.Destroy(l);
        }

        /// <summary>Big bullet with its own light + break sprite (lances, orbs). Returns the bullet for further tweaks.</summary>
        private HallBullet BigShot(string sheet, Vector2 at, Vector2 vel, Color glow, float scale, float dmg, float radius, float life,
                                   string pop, float popScale, Color popTint)
        {
            var b = Shot(sheet, at, vel, Color.white, scale, dmg, radius, life);
            if (b == null) return null;
            b.Glow(glow, 0.9f, 0.9f);
            b.popFx = pop; b.popScale = popScale; b.popTint = popTint;
            return b;
        }

        /// <summary>One-shot tinted effect (returned for further tweaks).</summary>
        private SpriteVfx Fx(string sheet, Vector2 at, float scale, int order, float fps, Color tint)
        {
            var fx = SpriteVfx.Spawn(sheet, at, scale, Layer, order, false, fps, false);
            if (fx != null) fx.Tint(tint);
            return fx;
        }

        // ------------------------------------------------------------------ black hole pulling Ren
        private Rigidbody2D renRb;
        private bool wellPull;
        private Vector2 wellAt;
        private float wellSpeed;

        /// <summary>Every frame during the fight: an open black hole pulls Ren toward its centre (stronger when close; no pull during the time stop).</summary>
        private void PullRen(float dt)
        {
            if (!wellPull || frozenRen || TimeStop.Active || Ctx.Player == null) return;
            if (renRb == null) renRb = Ctx.Player.GetComponent<Rigidbody2D>();
            Vector2 p = renRb != null ? renRb.position : (Vector2)Ctx.Player.position;
            Vector2 d = wellAt - p;
            float dist = d.magnitude;
            if (dist < 0.06f || dist > 4.2f) return;
            float k = Mathf.Lerp(1.25f, 0.55f, Mathf.Clamp01(dist / 4.2f));
            Vector2 np = p + d / dist * Mathf.Min(dist, wellSpeed * k * dt);
            if (renRb != null) renRb.position = np;
            else Ctx.Player.position = new Vector3(np.x, np.y, Ctx.Player.position.z);
        }

        /// <summary>Loose light (Light2D) for zones / rods.</summary>
        private GameObject GlowAt(Vector2 at, Color col, float radius, float intensity, float seconds)
        {
            var go = new GameObject("Absalom Glow");
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = at;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = col;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * 0.15f;
            l.intensity = intensity;
            if (seconds > 0f) Object.Destroy(go, seconds);
            return go;
        }
    }
}
