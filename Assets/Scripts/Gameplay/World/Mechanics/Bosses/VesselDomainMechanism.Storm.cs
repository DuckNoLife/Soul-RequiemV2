using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE 4 TEMPEST (yellow lightning). Same recipe as Phases 2–3: a PHASE RULE + a COUNTER + second-half ESCALATION + subtle VFX.
    ///   Phase rule  — LIGHTNING RODS: rods planted by the Vessel are both an electric fence and a LIGHTNING ATTRACTOR (bolts landing near a rod hit the rod, not Ren)
    ///                 -> standing near a rod is shelter from bolts, but the wires between rods are deadly. STUN only comes from Live Wires, with 3 s stun immunity after each stun.
    ///   Counter     — STRIKE A ROD: the rod dumps all its charge into the nearest LYING HAND = 2 hits on the Vessel (ignores armour) + the hand stays numb 0.8 s longer ("OVERLOAD");
    ///                 with no lying hand the rod discharges into the sky and breaks (opens the fence). Keep rods to punish Slams, or break them to escape — player's choice.
    ///   STORM RODS    — a hand plants 3 rods in a triangle around Ren (each with a lightning strike), wires link the rods: ON 2.2 s / OFF 1 s (blinking warning
    ///                   0.5 s before ON), wire touch = 10. Rods live 12 s.
    ///   THUNDERSTORM  — the Vessel throws its head back and roars, 12–16 bolts strike (yellow warning rings 0.85 s, 55 % around Ren); hit = 14. Bolts near a rod are attracted.
    ///   THUNDER DRUM  — 3 fist slams (TripleSlam), each one struck by lightning + a wider electric ring; the last hand lies down (window).
    ///   THUNDER GAZE  — eye move, real forked lightning from the eye sockets (.Rework.cs). WatcherEyes (laser from the eye sockets) is the older version, unused.
    ///   BALL LIGHTNING — mouth move (.Gaze.cs).
    ///   LIVE WIRES    — pairs of small rods on two arena edges, horizontal / vertical wires: 1 s warning (faint flickering wire) -> live 1.3 s; touch = 8 + 2 s STUN.
    ///                   3 waves: horizontal, vertical, cross grid.
    ///   SLAM          — like Phase 1 (+ lightning strikes the fist, electric ring).
    ///   EYE OF THE STORM (phase HP &lt; 50 %, about every 25 s) — the arena goes dark, bolts strike everywhere; only AROUND THE 3 LIGHTNING RODS is calm (phase rule);
    ///                   away from every rod for more than 0.6 s: a bolt aims straight at Ren every 0.6 s. 9 s.
    /// Background: distant sky flashes every 5–9 s while the map has the TEMPEST tint.
    /// Assets: _tools/s427/make_tempest.py (PixelLab 'Vessel Storm Strike', 'Vessel Volt Impact'; recoloured rods / eyes).
    /// Called by: PhaseLoop (move table), TickFight (TickStorm), OnStruckHand (StrikeRods).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static readonly Color StormCol = new Color(1f, 0.86f, 0.3f);
        private static readonly Color StormWarn = new Color(1f, 0.9f, 0.45f);
        private float stunImmuneUntil, nextEyeStorm, nextSkyFlash, stormDim;

        // ------------------------------------------------------------------ lightning drawn in code (3 layers: glow / body / core, jagged, re-jitters 20 times / s)
        private sealed class VArc
        {
            public SpriteRenderer[] Halo, Mid, Core;
            public Vector2 A, B;
            public float NextJitter, Alpha = 1f, Jag = 0.13f;
            public Light2D L;
        }

        private VArc MakeArc(Vector2 a, Vector2 b, int segs = 6, bool light = true)
        {
            var arc = new VArc { A = a, B = b, Halo = new SpriteRenderer[segs], Mid = new SpriteRenderer[segs], Core = new SpriteRenderer[segs] };
            for (int i = 0; i < segs; i++)
            {
                arc.Halo[i] = NewLine("storm arc halo", order + 1);
                arc.Mid[i] = NewLine("storm arc", order + 2);
                arc.Core[i] = NewLine("storm arc core", order + 3);
            }
            if (light) arc.L = BossJuice.Light((a + b) * 0.5f, StormCol, Mathf.Max(1.2f, (b - a).magnitude * 0.45f), 0.8f, 0f);
            JitterArc(arc, true);
            return arc;
        }

        private static void JitterArc(VArc arc, bool force)
        {
            if (arc == null || arc.Halo == null) return;
            if (!force && Time.time < arc.NextJitter) { Fade(arc); return; }
            arc.NextJitter = Time.time + 0.05f;
            int n = arc.Halo.Length;
            Vector2 d = arc.B - arc.A, nrm = new Vector2(-d.y, d.x).normalized;
            var pts = new Vector2[n + 1];
            pts[0] = arc.A; pts[n] = arc.B;
            for (int i = 1; i < n; i++) pts[i] = arc.A + d * (i / (float)n) + nrm * Random.Range(-arc.Jag, arc.Jag);
            for (int i = 0; i < n; i++)
            {
                StretchLine(arc.Halo[i], pts[i], pts[i + 1], Random.Range(0.16f, 0.22f));
                StretchLine(arc.Mid[i], pts[i], pts[i + 1], Random.Range(0.055f, 0.075f));
                StretchLine(arc.Core[i], pts[i], pts[i + 1], Random.Range(0.02f, 0.032f));
            }
            Fade(arc);
            if (arc.L != null) { arc.L.transform.position = (arc.A + arc.B) * 0.5f; arc.L.intensity = Random.Range(0.6f, 1.3f) * arc.Alpha; }
        }

        private static void Fade(VArc arc)
        {
            float a = arc.Alpha;
            for (int i = 0; i < arc.Halo.Length; i++)
            {
                if (arc.Halo[i] != null) arc.Halo[i].color = new Color(1f, 0.72f, 0.18f, 0.3f * a);
                if (arc.Mid[i] != null) arc.Mid[i].color = new Color(1f, 0.88f, 0.4f, 0.95f * a);
                if (arc.Core[i] != null) arc.Core[i].color = new Color(1f, 1f, 0.92f, a);
            }
        }

        private void KillArc(VArc arc)
        {
            if (arc == null || arc.Halo == null) return;
            for (int i = 0; i < arc.Halo.Length; i++) { DestroyObj(arc.Halo[i]); DestroyObj(arc.Mid[i]); DestroyObj(arc.Core[i]); }
            if (arc.L != null) Object.Destroy(arc.L.gameObject);
            arc.Halo = null;
        }

        /// <summary>Lightning strikes `at` (PixelLab VFX + electric burst + light flash + light shake).</summary>
        private void SkyBolt(Vector2 at, float scale)
        {
            SpriteVfx.Spawn("vc_storm_strike", at, scale, layer, at.y < Feet.y ? order + 3 : order - 1, Random.value < 0.5f, 16f, false);
            SpriteVfx.Spawn("vc_volt_impact", at + Vector2.up * 0.1f, 0.7f * scale, layer, order + 2, false, 22f, false);
            BossJuice.Flash(at, StormCol, 2.2f * scale, 2.4f, 0.18f);
            Sfx.Play("bolt_strike", at, 0.55f);
        }

        /// <summary>Electric hit: on hit -> (optional) stun for `stun` s, with 3 s stun immunity after each stun.</summary>
        private bool Shock(float dmg, Vector2 from, float knock, float stun)
        {
            if (!Hurt(dmg, from, knock)) return false;
            SpriteVfx.Spawn("vc_volt_impact", Feet + Vector2.up * 0.3f, 0.6f, layer, order + 3, false, 22f, false);
            var hp = Ctx.PlayerHealth;
            if (stun > 0f && hp != null && Time.time >= stunImmuneUntil)
            {
                hp.Stun(stun);
                stunImmuneUntil = Time.time + stun + 3f;
                DrillBoard.Say("STUNNED", StormCol);
            }
            return true;
        }

        private Move[] StormMoves()
        {
            rods.RemoveAll(r => r.dead);
            return new[]
            {
                new Move { name = "SLAM", w = 1.5f, slam = true, run = pr => Slam(Feet.x < chest.x ? -1 : 1, pr) },
                new Move { name = "STORM RODS", w = rods.Count > 0 ? 0.05f : 1.4f, run = StormRods },
                new Move { name = "THUNDERSTORM", w = 1.5f, run = Thunderstorm },
                new Move { name = "THUNDER DRUM", w = 1.2f, slam = true, run = TripleSlam },
                new Move { name = "THUNDER GAZE", w = 1.3f, run = ThunderGaze },            // Phase 4 EYE move: forked lightning from the eyes (replaced the eye-socket laser)
                new Move { name = "BALL LIGHTNING", w = 1.3f, run = BallLightning },       // Phase 4 MOUTH move
                new Move { name = "LIVE WIRES", w = 1.3f, run = LiveWires },
                new Move { name = "EYE OF THE STORM", w = vhp < 0.5f && Time.time >= nextEyeStorm ? 2.4f : 0f, run = EyeOfTheStorm },
            };
        }

        /// <summary>Every frame (TickFight): rod lifetimes / rod wires, distant sky flashes.</summary>
        private void TickStorm(float dt)
        {
            float now = Time.time;
            if (toneP == 3 && actState == ActState.Playing && now >= nextSkyFlash)
            {
                nextSkyFlash = now + Random.Range(5f, 9f);
                Vector2 p = Floor.center + new Vector2(Random.Range(-9f, 9f), Random.Range(3f, 7f));
                BossJuice.Flash(p, new Color(1f, 0.95f, 0.75f), 9f, 1.1f, 0.12f);
                Sfx.Play("bolt_strike", p, 0.18f);
            }
        }

        private void KillStorm()
        {
            foreach (var r in rods) r.dead = true;
            rods.Clear();
            KillBalls();
            stormDim = 0f;
        }

        // ------------------------------------------------------------------ LIGHTNING RODS
        private sealed class Rod
        {
            public Vector2 p;
            public SpriteRenderer sr;
            public Light2D l;
            public bool dead;
            public float until;
        }
        private readonly List<Rod> rods = new List<Rod>();

        private Rod PlantRod(Vector2 at, float life, float scale)
        {
            var g = new GameObject("storm rod");
            fieldObjs.Add(g);
            g.transform.position = at;
            g.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("VFX/vc_storm_rod");
            if (sr.sprite == null) sr.sprite = Resources.Load<Sprite>("VFX/abs_storm_rod");
            sr.sortingLayerID = layer; sr.sortingOrder = at.y < Feet.y ? order + 1 : order - 1;
            var r = new Rod { p = at, sr = sr, until = Time.time + life };
            r.l = BossJuice.Light(at + Vector2.up * 0.5f, StormCol, 1.1f, 0.7f, 0f);
            return r;
        }

        private Vector2 RodTip(Rod r) => r.p + Vector2.up * 0.62f;

        private void BreakRod(Rod r, bool fx)
        {
            if (r.dead) return;
            r.dead = true;
            if (fx)
            {
                SpriteVfx.Spawn("vc_volt_impact", RodTip(r), 0.8f, layer, order + 3, false, 22f, false);
                Sfx.Play("glass_break", r.p, 0.5f);
            }
            if (r.sr != null) DestroyObj(r.sr.gameObject);
            if (r.l != null) Object.Destroy(r.l.gameObject);
        }

        private IEnumerator StormRods(float pressure)
        {
            int side = FreeSide(Feet.x < chest.x ? -1 : 1);
            if (side == 0) yield break;
            int hi = Idx(side);
            handBusy[hi] = true;
            DrillBoard.Say("STORM RODS", StormCol);
            DrillBoard.Yell("Stand between my rods, little conductor.", 1.8f);
            float rad = Float("rod_radius", 2.1f), a0 = Random.Range(0f, 360f);
            var planted = new List<Rod>();
            for (int k = 0; k < 3 && !aborted; k++)
            {
                float a = (a0 + k * 120f) * Mathf.Deg2Rad;
                Vector2 at = ClampFloor(Feet + new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.7f), 0.5f);
                var ring = NewRing(at, 0.5f);
                for (float t = 0f; t < 0.38f && !aborted; t += Time.deltaTime)
                {
                    rig.SetHand(side, at + Vector2.up * 1.5f, 12f, "hand_point");
                    ring.color = new Color(StormWarn.r, StormWarn.g, StormWarn.b, 0.3f + 0.3f * Mathf.Sin(t * 30f));
                    yield return null;
                }
                DestroyObj(ring);
                if (aborted) break;
                rig.SetHand(side, at + Vector2.up * 0.6f, 45f, "hand_point");
                var rod = PlantRod(at, Float("rod_life", 12f), 1.6f);
                planted.Add(rod); rods.Add(rod);
                SkyBolt(at, 1f);
                if ((Feet - at).magnitude < 0.5f) Shock(10f, at, 1f, 0f);
                yield return Wait(0.12f);
            }
            rig.RestHand(side, 6f);
            handBusy[hi] = false;
            if (aborted) yield break;
            if (planted.Count >= 2) StartR(RodField(planted));
            if (!rodHinted) { rodHinted = true; Hints.Show("Strike a storm rod to hurl its charge at a fallen hand!", 3f); }
            yield return Wait(0.4f);
        }
        private bool rodHinted;

        /// <summary>Wires linking the rods (closed loop): ON 2.2 s / OFF 1 s, blinking warning 0.5 s before ON. A broken rod loses its wires.</summary>
        private IEnumerator RodField(List<Rod> set)
        {
            int n = set.Count;
            var arcs = new VArc[n];
            float t0 = Time.time, nextHurt = 0f;
            const float On = 2.2f, Off = 1f;
            while (!aborted)
            {
                float now = Time.time;
                bool alive = false;
                foreach (var r in set) if (!r.dead && now < r.until) alive = true;
                if (!alive) break;
                float cyc = (now - t0 + Off) % (On + Off);                               // start in the OFF state (lets Ren get out)
                bool on = cyc >= Off;
                bool warn = !on && cyc > Off - 0.5f;
                for (int i = 0; i < n; i++)
                {
                    var ra = set[i]; var rb = set[(i + 1) % n];
                    bool link = !ra.dead && !rb.dead && now < ra.until && now < rb.until && (n > 2 || i == 0);
                    if (!link) { if (arcs[i] != null) { KillArc(arcs[i]); arcs[i] = null; } continue; }
                    if (arcs[i] == null) arcs[i] = MakeArc(RodTip(ra), RodTip(rb), 8);
                    var arc = arcs[i];
                    arc.A = RodTip(ra); arc.B = RodTip(rb);
                    arc.Alpha = on ? 1f : warn ? (Mathf.Sin(now * 40f) > 0f ? 0.45f : 0.05f) : 0.0f;
                    arc.Jag = on ? 0.13f : 0.05f;
                    JitterArc(arc, false);
                    if (on && now >= nextHurt && SegDist(Feet + Vector2.up * 0.15f, arc.A + Vector2.down * 0.45f, arc.B + Vector2.down * 0.45f) < 0.22f
                        && Shock(10f, (arc.A + arc.B) * 0.5f, 1f, 0f))
                        nextHurt = now + 0.6f;
                }
                foreach (var r in set) if (!r.dead && r.l != null) r.l.intensity = (on ? 1.1f : 0.5f) + 0.2f * Mathf.Sin(now * 12f);
                yield return null;
            }
            for (int i = 0; i < n; i++) KillArc(arcs[i]);
            foreach (var r in set) if (!r.dead) { SpriteVfx.Spawn("m3_dash_dust", r.p, 0.6f, layer, order - 1, false, 16f, false); BreakRod(r, false); }
        }

        /// <summary>Called from OnStruckHand (Phase 4): striking a rod -> discharge into the nearest lying hand, otherwise into the sky. The rod breaks.</summary>
        private void StrikeRods(Vector2 c, float r)
        {
            foreach (var rod in rods)
            {
                if (rod.dead) continue;
                if ((rod.p + Vector2.up * 0.3f - c).magnitude > r + 0.35f) continue;
                int best = -1; float bd = float.MaxValue;
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    if (!HandDown(side)) continue;
                    float d = (rig.HandWorld(side) - rod.p).magnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                Vector2 tip = RodTip(rod);
                if (best >= 0)
                {
                    Vector2 h = rig.HandWorld(best == 0 ? -1 : 1);
                    StartR(FlashArc(tip, h, 0.4f));
                    DamageVessel(Float("rod_hits", 2f), h);
                    downUntil[best] += 0.8f;
                    SkyBolt(h, 1.1f);
                    DrillBoard.Say("OVERLOAD!", StormCol);
                    BossJuice.Shake(2);
                }
                else
                {
                    StartR(FlashArc(tip, tip + new Vector2(Random.Range(-0.6f, 0.6f), 6f), 0.3f));
                    DrillBoard.Say("DISCHARGED", StormCol);
                }
                BreakRod(rod, true);
                break;                                                                   // only one rod per hit
            }
        }

        private IEnumerator FlashArc(Vector2 a, Vector2 b, float seconds)
        {
            var arc = MakeArc(a, b, 8);
            Sfx.Play("bolt_strike", b, 0.8f);
            for (float t = 0f; t < seconds && arc.Halo != null; t += Time.deltaTime)
            {
                arc.Alpha = 1f - t / seconds * 0.6f;
                arc.Jag = 0.22f;
                JitterArc(arc, false);
                yield return null;
            }
            KillArc(arc);
        }

        // ------------------------------------------------------------------ THUNDERSTORM
        private IEnumerator Thunderstorm(float pressure)
        {
            DrillBoard.Say("THUNDERSTORM", StormCol);
            DrillBoard.Yell("Sing with the storm.", 1.6f);
            jawHold = 0.9f; slumpTarget = 0f;
            Roar(0.8f);
            var orb = BossJuice.Light(rig.MouthWorld() + Vector2.up * 1.2f, StormCol, 3f, 0.5f, 0f);
            for (float t = 0f; t < 0.7f && !aborted; t += Time.deltaTime) { if (orb != null) orb.intensity = 0.5f + 2f * t; yield return null; }
            jawHold = -1f; slumpTarget = 0f;
            if (orb != null) Object.Destroy(orb.gameObject);
            if (aborted) yield break;
            int n = Mathf.RoundToInt(Mathf.Lerp(12f, 16f, pressure * 0.5f));
            float dur = Mathf.Lerp(3.6f, 3f, pressure * 0.5f);
            for (int i = 0; i < n && !aborted; i++)
            {
                Vector2 target = Random.value < 0.55f
                    ? ClampFloor(Feet + PlayerVel * 0.55f + Random.insideUnitCircle * 0.9f, 0.3f)
                    : new Vector2(Random.Range(Floor.xMin + 0.3f, Floor.xMax - 0.3f), Random.Range(Floor.yMin + 0.3f, Floor.yMax - 0.4f));
                StartR(Strike(target, Float("strike_warn", 0.85f), 14f));
                yield return Wait(dur / n);
            }
            yield return Wait(1.1f);
        }

        /// <summary>One bolt: warning ring shrinks for `warn` s -> strike. A rod within 1.3 u -> the bolt is ATTRACTED to the rod (harmless).</summary>
        private IEnumerator Strike(Vector2 at, float warn, float dmg)
        {
            Rod catcher = null; float cd = 1.3f;
            foreach (var r in rods) if (!r.dead && (r.p - at).magnitude < cd) { cd = (r.p - at).magnitude; catcher = r; }
            var ring = NewRing(at, 0.75f);
            var dot = new GameObject("strike mark");
            fieldObjs.Add(dot);
            dot.transform.position = at;
            var ds = dot.AddComponent<SpriteRenderer>();
            ds.sprite = Disc(); FxLighting.MakeUnlit(ds);
            ds.sortingLayerID = layer; ds.sortingOrder = -14;
            for (float t = 0f; t < warn && !aborted; t += Time.deltaTime)
            {
                float u = t / warn;
                float rk = Mathf.Lerp(0.75f, 0.35f, u) / (Ring().bounds.size.x * 0.5f);
                ring.transform.localScale = new Vector3(rk, rk * 0.6f, 1f);
                ring.color = new Color(StormWarn.r, StormWarn.g, StormWarn.b, 0.35f + 0.45f * u);
                float dk = Mathf.Lerp(0.2f, 0.7f, u);
                dot.transform.localScale = new Vector3(dk, dk * 0.6f, 1f);
                ds.color = new Color(1f, 0.85f, 0.35f, 0.12f + 0.18f * u);
                yield return null;
            }
            DestroyObj(ring); DestroyObj(dot);
            if (aborted) yield break;
            if (catcher != null && !catcher.dead)
            {
                SkyBolt(RodTip(catcher), 1f); RodCharge(catcher, 1);                                           // lightning rod attracts the bolt
                SpriteVfx.Spawn("vc_volt_impact", RodTip(catcher), 0.6f, layer, order + 3, false, 22f, false);
                yield break;
            }
            SkyBolt(at, 1.15f);
            if ((Feet - at).magnitude < 0.6f) Shock(dmg, at, 1f, 0f);
        }

        private SpriteRenderer NewRing(Vector2 at, float radius)
        {
            var g = new GameObject("storm ring");
            fieldObjs.Add(g);
            g.transform.position = at;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = SigilRing();                                                      // seal-style warning ring
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = -13;
            float rk = radius / (Ring().bounds.size.x * 0.5f);
            g.transform.localScale = new Vector3(rk, rk * 0.6f, 1f);
            sr.color = new Color(StormWarn.r, StormWarn.g, StormWarn.b, 0f);
            return sr;
        }

        // ------------------------------------------------------------------ WATCHER EYES
        private IEnumerator WatcherEyes(float pressure)
        {
            // WatcherEyes (unused): laser from the Vessel's OWN 2 eye sockets, the Vessel lunges down first
            DrillBoard.Say("STORM GAZE", StormCol);
            DrillBoard.Yell("I see every step you take.", 1.6f);
            lungeGoal = 1f;
            yield return Wait(0.6f);
            Vector2[] seat = { EyeSocket(-1), EyeSocket(1) };
            var eyes = new SpriteVfx[2];
            var eyeGlow = new Light2D[2];
            for (int i = 0; i < 2; i++)
            {
                SpriteVfx.Spawn("vc_volt_impact", seat[i], 0.7f, layer, order + 5, false, 20f, false);
                eyeGlow[i] = BossJuice.Light(seat[i], StormCol, 1f, 1.6f, 0f);
            }
            Sfx.Play("eye_beam_charge", chest, 0.8f);
            yield return Wait(0.4f);
            int volleys = pressure >= 1.2f ? 4 : 3;
            for (int v = 0; v < volleys && !aborted; v++)
            {
                bool both = v == volleys - 1;
                var who = both ? new[] { 0, 1 } : new[] { v % 2 };
                var marks = new List<(int i, SpriteRenderer ring, Vector2 aim)>();
                foreach (int i in who) marks.Add((i, NewRing(Feet, 0.45f), Feet));
                float track = Mathf.Lerp(1.1f, 0.85f, pressure * 0.5f);
                for (float t = 0f; t < track && !aborted; t += Time.deltaTime)              // target ring follows Ren
                {
                    for (int k = 0; k < marks.Count; k++)
                    {
                        var (i, ring, aim) = marks[k];
                        aim = Vector2.Lerp(aim, Feet + PlayerVel * 0.15f, 1f - Mathf.Exp(-7f * Time.deltaTime));
                        ring.transform.position = aim;
                        ring.color = new Color(StormWarn.r, StormWarn.g, StormWarn.b, 0.6f + 0.3f * Mathf.Sin(t * 20f));
                        marks[k] = (i, ring, aim);
                    }
                    SyncEyes(seat, eyeGlow);
                    yield return null;
                }
                if (aborted) break;
                var warns = new List<BeamWarn>();
                var lines = new List<(Vector2 a, Vector2 b)>();
                foreach (var (i, ring, aim) in marks)
                {
                    Vector2 a = EyePos(eyes, seat, i), d = (aim - a).normalized, b = a + d * 16f;
                    lines.Add((a, b));
                    var bw = BeamWarn.Spawn(a, b, 0.28f, StormWarn, 0.35f, layer, order + 3);
                    if (bw != null) warns.Add(bw);
                    DestroyObj(ring);
                }
                Sfx.Play("eye_beam_charge", chest, 0.5f);
                for (float t = 0f; t < 0.35f && !aborted; t += Time.deltaTime) { SyncEyes(seat, eyeGlow); yield return null; }
                foreach (var bw in warns) if (bw != null) bw.Release();
                if (aborted) break;
                // YELLOW LASER
                Sfx.Play("eye_beam", chest, 1f);
                BossJuice.Shake(1);
                var beams = new List<VArc>();
                foreach (var (a, b) in lines) { var arc = MakeArc(a, b, 10); arc.Jag = 0.03f; beams.Add(arc); }
                bool hit = false;
                for (float t = 0f; t < 0.45f && !aborted; t += Time.deltaTime)
                {
                    for (int k = 0; k < beams.Count; k++)
                    {
                        var arc = beams[k];
                        arc.Alpha = 1f - Mathf.Clamp01((t - 0.3f) / 0.15f);
                        BeamWidth(arc, 2.2f - t * 2f);
                        if (!hit && SegDist(Feet + Vector2.up * 0.2f, lines[k].a, lines[k].b) < 0.3f) hit = Shock(16f, lines[k].a, 1.2f, 0f);
                    }
                    if (Random.value < 0.5f) foreach (var (a, b) in lines)
                        {
                            Vector2 e = ClipToFloor(a, b);
                            SpriteVfx.Spawn("vc_volt_impact", e, 0.5f, layer, order + 3, false, 24f, false);
                        }
                    yield return null;
                }
                foreach (var arc in beams) KillArc(arc);
                yield return Wait(0.35f);
            }
            for (int i = 0; i < 2; i++) if (eyeGlow[i] != null) Object.Destroy(eyeGlow[i].gameObject);
            lungeGoal = 0f;
            yield return Wait(0.4f);
        }

        private Vector2 EyePos(SpriteVfx[] eyes, Vector2[] seat, int i) => eyes[i] != null ? (Vector2)eyes[i].transform.position : seat[i];

        /// <summary>Vessel eye socket (side -1 left / +1 right).</summary>
        private Vector2 EyesMid => (EyeSocket(-1) + EyeSocket(1)) * 0.5f;
        private Vector2 EyeSocket(int side) => rig.EyeWorld(side);                          // follows the lowered head

        /// <summary>Update the 2 eye sockets + eye lights every frame (the Vessel lunging / breathing).</summary>
        private void SyncEyes(Vector2[] seat, Light2D[] glow)
        {
            for (int i = 0; i < 2; i++)
            {
                seat[i] = EyeSocket(i == 0 ? -1 : 1);
                if (glow != null && glow[i] != null) { glow[i].transform.position = seat[i]; glow[i].intensity = 1.4f + 0.4f * Mathf.Sin(Time.time * 25f + i); }
            }
        }

        private static void BobEyes(SpriteVfx[] eyes, Vector2[] seat)
        {
            for (int i = 0; i < 2; i++)
                if (eyes[i] != null) eyes[i].transform.position = seat[i] + new Vector2(0f, Mathf.Sin(Time.time * 2.3f + i * 1.7f) * 0.12f);
        }

        /// <summary>Laser beam: multiplies the 3-layer lightning thickness (straighter, thicker).</summary>
        private static void BeamWidth(VArc arc, float k)
        {
            JitterArc(arc, false);
            for (int i = 0; i < arc.Halo.Length; i++)
            {
                var s = arc.Halo[i]; if (s != null) s.transform.localScale = new Vector3(s.transform.localScale.x, 0.34f * 100f * k * 0.5f, 1f);
                s = arc.Mid[i]; if (s != null) s.transform.localScale = new Vector3(s.transform.localScale.x, 0.14f * 100f * k * 0.5f, 1f);
                s = arc.Core[i]; if (s != null) s.transform.localScale = new Vector3(s.transform.localScale.x, 0.06f * 100f * k * 0.5f, 1f);
            }
        }

        /// <summary>Point where the beam (a -> b) leaves the arena (to place the end spark on the floor).</summary>
        private Vector2 ClipToFloor(Vector2 a, Vector2 b)
        {
            Vector2 last = a;
            for (int s = 1; s <= 40; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, s / 40f);
                if (Floor.Contains(p)) last = p;
                else if (Floor.Contains(last)) return last;
            }
            return last;
        }

        // ------------------------------------------------------------------ LIVE WIRES
        private IEnumerator LiveWires(float pressure)
        {
            DrillBoard.Say("LIVE WIRES", StormCol);
            DrillBoard.Yell("Touch them. I dare you.", 1.4f);
            int waves = 3;
            for (int w = 0; w < waves && !aborted; w++)
            {
                var wires = new List<(Vector2 a, Vector2 b)>();
                bool horiz = w == 0 || (w == 2 && Random.value < 0.5f), vert = w == 1 || w == 2;
                if (w == 2 && pressure < 1f) { horiz = Random.value < 0.5f; vert = !horiz; }
                if (horiz)
                {
                    int n = 2 + (pressure >= 1f ? 1 : 0);
                    float off = Random.Range(0f, 1f);
                    for (int k = 0; k < n; k++)
                    {
                        float y = Floor.yMin + Floor.height * ((k + 0.5f + off * 0.5f) / (n + 0.5f));
                        wires.Add((new Vector2(Floor.xMin - 0.1f, y), new Vector2(Floor.xMax + 0.1f, y)));
                    }
                }
                if (vert)
                {
                    int n = 3 + (pressure >= 1f ? 1 : 0);
                    float off = Random.Range(0f, 1f);
                    for (int k = 0; k < n; k++)
                    {
                        float x = Floor.xMin + Floor.width * ((k + 0.5f + off * 0.5f) / (n + 0.5f));
                        wires.Add((new Vector2(x, Floor.yMin - 0.05f), new Vector2(x, Floor.yMax + 0.05f)));
                    }
                }
                // small rods at both ends + faint warning wire
                var posts = new List<Rod>();
                var arcs = new List<VArc>();
                foreach (var (a, b) in wires)
                {
                    posts.Add(PlantRod(a, 99f, 1.1f)); posts.Add(PlantRod(b, 99f, 1.1f));
                    var arc = MakeArc(a + Vector2.up * 0.42f, b + Vector2.up * 0.42f, 10, false);
                    arc.Alpha = 0f; arcs.Add(arc);
                }
                Sfx.Play("trap_fire", Floor.center, 0.3f);
                float warnT = Mathf.Lerp(1f, 0.8f, pressure * 0.5f);
                for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime)
                {
                    foreach (var arc in arcs) { arc.Alpha = Mathf.Sin(Time.time * 35f) > 0.2f ? 0.28f : 0.06f; arc.Jag = 0.04f; JitterArc(arc, false); }
                    yield return null;
                }
                if (aborted) { foreach (var arc in arcs) KillArc(arc); foreach (var p in posts) BreakRod(p, false); yield break; }
                Sfx.Play("bolt_strike", Floor.center, 0.7f);
                BossJuice.Shake(1);
                foreach (var p in posts) SpriteVfx.Spawn("vc_volt_impact", RodTip(p), 0.55f, layer, order + 3, false, 22f, false);
                bool hit = false;
                float live = Float("wire_seconds", 1.3f);
                for (float t = 0f; t < live && !aborted; t += Time.deltaTime)
                {
                    for (int k = 0; k < arcs.Count; k++)
                    {
                        var arc = arcs[k];
                        arc.Alpha = 1f - Mathf.Clamp01((t - live + 0.2f) / 0.2f);
                        arc.Jag = 0.12f;
                        JitterArc(arc, false);
                        if (!hit && t > 0.05f && SegDist(Feet + Vector2.up * 0.15f, wires[k].a, wires[k].b) < 0.2f)
                            hit = Shock(8f, Feet + Vector2.up * 0.5f, 0.3f, Float("wire_stun", 2f));
                    }
                    yield return null;
                }
                foreach (var arc in arcs) KillArc(arc);
                foreach (var p in posts) BreakRod(p, false);
                yield return Wait(0.45f);
            }
            yield return Wait(0.4f);
        }

        // ------------------------------------------------------------------ EYE OF THE STORM (second half of the phase)
        /// <summary>
        /// EYE OF THE STORM = AROUND THE LIGHTNING RODS.
        /// The Vessel plants 3 rods across the arena -> a lightning storm strikes everywhere EXCEPT around the rods (1.3 u); away from every rod for more than 0.6 s
        /// -> bolts aim straight at Ren. Striking a rod still discharges it.
        /// </summary>
        private IEnumerator EyeOfTheStorm(float pressure)
        {
            nextEyeStorm = Time.time + Float("eye_storm_every", 25f);
            DrillBoard.Say("EYE OF THE STORM", StormCol);
            DrillBoard.Yell("Hide behind my rods, little conductor.", 1.8f);
            Roar(1f);
            var planted = new List<Rod>();
            for (int k = 0; k < 3 && !aborted; k++)
            {
                float fx = Floor.xMin + Floor.width * (0.2f + 0.3f * k) + Random.Range(-0.5f, 0.5f);
                float fy = Mathf.Lerp(Floor.yMin + 0.8f, Floor.yMax - 1f, k == 1 ? 0.25f : 0.7f) + Random.Range(-0.4f, 0.4f);
                Vector2 at = ClampFloor(new Vector2(fx, fy), 0.6f);
                var rod = PlantRod(at, 11f, 1.6f);
                planted.Add(rod); rods.Add(rod);
                SkyBolt(at, 1f);
                yield return Wait(0.15f);
            }
            Hints.Show("Only the storm rods are calm - stay close to one.", 3f);
            const float Dur = 9f;
            float t0 = Time.time, nextAmb = 0f, nextAim = 0f, outsideFor = 0f;
            var calm = new List<SpriteRenderer>();
            foreach (var r in planted) calm.Add(NewRing(r.p, 1.3f));
            while (!aborted)
            {
                float now = Time.time, t = now - t0, dt = Time.deltaTime;
                if (t >= Dur + 0.8f) break;
                stormDim = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((Dur + 0.8f - t) / 0.8f);
                for (int k = 0; k < planted.Count; k++)
                    if (calm[k] != null) calm[k].color = planted[k].dead ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 0.92f, 0.55f, (0.35f + 0.2f * Mathf.Sin(now * 6f + k)) * stormDim);
                bool nearRod = false;
                foreach (var r in rods) if (!r.dead && (r.p - Feet).magnitude < 1.3f) nearRod = true;
                if (t < Dur && now >= nextAmb)
                {
                    nextAmb = now + Random.Range(0.1f, 0.18f);
                    Vector2 p = new Vector2(Random.Range(Floor.xMin, Floor.xMax), Random.Range(Floor.yMin, Floor.yMax));
                    bool calmHere = false;
                    foreach (var r in rods) if (!r.dead && (r.p - p).magnitude < 1.5f) calmHere = true;
                    if (!calmHere && (p - Feet).magnitude > 1f) SkyBolt(p, Random.Range(0.8f, 1.05f));
                }
                outsideFor = nearRod ? 0f : outsideFor + dt;
                if (t > 1f && t < Dur && outsideFor > 0.6f && now >= nextAim)
                {
                    nextAim = now + 0.6f;
                    StartR(Strike(Feet + PlayerVel * 0.35f, 0.5f, 12f));
                    if (Random.value < 0.4f) DrillBoard.Say("TO THE RODS", StormCol);
                }
                yield return null;
            }
            stormDim = 0f;
            foreach (var c in calm) DestroyObj(c);
        }
    }
}
