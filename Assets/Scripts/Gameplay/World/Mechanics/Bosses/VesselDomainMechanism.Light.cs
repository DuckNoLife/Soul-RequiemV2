using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// WATCHTOWER SEARCHLIGHTS — the outer lights are not always on; now and then they switch on and search the arena, and if they catch Ren something
    /// answers — a different response in each phase (P1: the Vessel itself attacks).
    /// One meaning only: LIGHT = THE VESSEL'S WATCHING EYE. Caught by the light = SPOTTED -> retaliation. The lights are OFF most of the time
    /// (dark, readable arena); when one switches on, something is coming.
    /// One shared schedule for both towers (WatchTowerMechanism: SetLit / SnapAim / Hunt / InPool):
    ///   OFF (rest `light_gap_min..max`, shorter at low phase HP) -> WARM-UP 0.8 s (tower eye flashes + charge sound, light pool appears at the tower's side)
    ///   SEARCH 4.5 s (1.6 u pool tracks Ren at 1.5 u/s — much slower than Ren, dodgeable) -> 0.35 s of continuous contact = "SPOTTED!" -> RETALIATION -> off.
    ///   Only 1 tower searches at a time (Eclipse: 2).
    /// RETALIATION per phase (Caught):
    ///   P1 HOLLOW  — CRUSHING GRIP (.Rework.cs): a hand grabs and lifts Ren, strike it to drop it.
    ///   P2 CINDER  — FIREBRAND: the tower eye lobs 3 firebombs in arcs around Ren; burst 10, leaving a fire carpet for 3 s (standing in it = burn).
    ///   P3 RIME    — FLASH FREEZE (.Rework.cs): Ren frozen in ice under a falling icicle.
    ///   P4 TEMPEST — STATIC CHARGE (.Rework.cs): Ren is charged, ground it at a lightning rod.
    ///   P5 UMBRA   — SHADOW WRAITH: REN'S OWN SHADOW rises from the light pool and walks Ren's exact path (0.9 s behind) for 7 s;
    ///                touch = 10 + slow; strike the shadow = it dissolves.
    /// FrostCage and LightningLance are older P3 / P4 responses, kept but no longer called by Caught.
    /// No towers -> the light rule switches itself off. Outside the fight (scenes, breaks, LAST GRASP) the lights are decoration.
    /// Called by: TickFight (TickLight every frame). Related: WatchTowerMechanism (tower sprite + pool), OnStruckHand (cage / shadow hits).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private List<WatchTowerMechanism> towers = new List<WatchTowerMechanism>();
        private float towersAt = -99f;
        private bool lightDriven, spottedReady, searchHinted, lightHushed;   // no Eye of the Storm light rule
        // ONE SCHEDULE for both towers — only 1 search at a time, then rest `light_gap_min..max` s
        // (separate timers made one tower switch on as soon as the other went off = almost always lit). At phase start wait `light_first` s.
        private float lightNextAt, lightHoldUntil; private int lightLast = -1;
        private sealed class Seeker { public int state; public float t0, until, caught; }   // 0 off, 1 warm-up, 2 searching, 3 just caught
        private readonly List<Seeker> seekers = new List<Seeker>();
        private static readonly string[] CatchName = { "CAUGHT", "FIREBRAND", "FLASH FREEZE", "STATIC CHARGE", "SHADOW WRAITH" };

        private void TickLight(float dt)
        {
            float now = Time.time;
            if (now - towersAt > 2f) { towers = WatchTowerMechanism.Towers(); towersAt = now; }
            bool fight = actState == ActState.Playing && rig != null && !lastGrasp && !CutsceneState.Active;
            if (!fight)
            {
                // fight in progress (awake, not finished) -> lights fully OFF between phases / while down / in LAST GRASP; before the awakening + after the end = decorative lights
                bool hush = Ctx.Flags.HasAll(AwakeFlag) && !Ctx.Flags.HasAll(DoneFlag);
                if (lightDriven || hush != lightHushed)
                {
                    lightDriven = false; lightHushed = hush;
                    foreach (var t in towers) if (t != null) { t.Hunt(null); t.SetPool(0f); t.Boost = 1f; t.SetLit(hush ? 0f : 1f); }
                    WatchTowerMechanism.GlowOverride = null;
                    seekers.Clear();
                }
                spottedReady = false;
                return;
            }
            if (!lightDriven)
            {
                lightDriven = true;
                seekers.Clear();
                lightNextAt = now + Float("light_first", 8f) + 4f;                  // wait for the phase intro to end (CamSettle calls HoldLights to reset)
                for (int i = 0; i < towers.Count; i++)
                {
                    seekers.Add(new Seeker { state = 0 });
                    if (towers[i] != null) { towers[i].SetLit(0f); towers[i].Boost = 1f; }
                }
            }
            while (seekers.Count < towers.Count) seekers.Add(new Seeker { state = 0 });
            RecordTrail();                                                              // for Shadow Wraith (P5)
            WatchTowerMechanism.GlowOverride = phase == 4 ? new Color(0.92f, 0.88f, 1f) : (Color?)null;
            int active = 0;
            foreach (var s in seekers) if (s.state != 0) active++;
            // only start a new search when NO tower is lit, the rest is over, and the Vessel is not using an eye / mouth / grab move (avoid stacking attacks)
            bool calm = !grabbing && !frozenSolid && !charged && !tetherOn && lungeGoal <= 0f && jawHold < 0f && (rig == null || rig.Lunge < 0.2f);
            if (active == 0 && calm && now >= lightNextAt && now >= lightHoldUntil && towers.Count > 0)
            {
                bool both = eclipseOn;                                                  // Eclipse (P5): both towers search together
                int pick = (lightLast + 1) % towers.Count;                              // alternate left / right
                for (int i = 0; i < towers.Count; i++)
                {
                    if (!both && i != pick) continue;
                    var t = towers[i]; if (t == null) continue;
                    var s = seekers[i];
                    s.state = 1; s.t0 = now; active++;
                    Vector2 start = ClampFloor(new Vector2(t.EyePos.x < Floor.center.x ? Floor.xMin + 0.9f : Floor.xMax - 0.9f, Feet.y), 0.6f);
                    t.SnapAim(start); t.SetPool(Float("search_pool", 1.6f));
                    Sfx.Play("eye_beam_charge", t.EyePos, 0.45f);
                }
                lightLast = pick;
            }
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i]; var s = seekers[i];
                if (t == null) continue;
                switch (s.state)
                {
                    case 0:                                                                 // off, waiting for its turn
                        t.SetLit(0f); t.Boost = 1f;
                        break;
                    case 1:                                                                 // warm-up: flash, pool fades in
                        t.Boost = 1.6f + 0.6f * Mathf.Sin(now * 30f);
                        t.SetLit(Mathf.Clamp01((now - s.t0) / 0.8f));
                        if (now - s.t0 >= 0.8f) { s.state = 2; s.t0 = now; s.caught = 0f; t.SetLit(1f); t.Boost = 1.2f; }
                        break;
                    case 2:                                                                 // searching for Ren
                        t.Hunt(ClampFloor(Feet, 0.4f), Float("search_speed", 1.5f));
                        bool on = t.InPool(Feet);
                        s.caught = on ? s.caught + dt : Mathf.Max(0f, s.caught - dt);
                        t.Boost = 1.2f + 1.2f * Mathf.Clamp01(s.caught / 0.35f);
                        if (!searchHinted) { searchHinted = true; Hints.Show("The watchtowers are searching. Stay out of their light.", 3.5f); }
                        if (s.caught >= Float("search_catch", 0.35f))
                        {
                            s.state = 3; s.t0 = now;
                            t.Hunt(Feet, 0f);
                            t.Boost = 2.4f;
                            Caught(i, t);
                        }
                        else if (now - s.t0 >= Float("search_seconds", 3.5f)) Off(s, t, now);
                        break;
                    case 3:                                                                 // just caught: stay lit 0.5 s, then off
                        if (now - s.t0 >= 0.5f) Off(s, t, now);
                        break;
                }
            }
        }

        private void Off(Seeker s, WatchTowerMechanism t, float now)
        {
            s.state = 0;
            t.SetLit(0f); t.Boost = 1f;
            bool anyOn = false;
            foreach (var o in seekers) if (o != s && o.state != 0) anyOn = true;
            if (!anyOn) lightNextAt = now + LightGap();                                  // search done -> both towers rest
        }

        /// <summary>Rest between two searches — 11–16 s (Tiled `light_gap_min` / `light_gap_max`), up to 25 % shorter at low Vessel HP.</summary>
        private float LightGap() => Random.Range(Float("light_gap_min", 11f), Float("light_gap_max", 16f)) * Mathf.Lerp(1f, 0.75f, 1f - vhp);

        /// <summary>Phase start (after the camera settles) — no search for `light_first` s.</summary>
        private void HoldLights(float extra = 0f)
        {
            lightHoldUntil = Time.time + Float("light_first", 8f) + extra;
            lightNextAt = Mathf.Max(lightNextAt, lightHoldUntil);
        }

        /// <summary>Tower i caught Ren -> retaliation for the current phase.</summary>
        private void Caught(int i, WatchTowerMechanism t)
        {
            int p = Mathf.Clamp(phase, 0, 4);
            DrillBoard.Say(CatchName[p], p == 0 ? WarnViolet : PhaseColor);
            Sfx.Play("bell_toll", t.EyePos, 0.35f);
            BossJuice.Flash(Feet, Color.Lerp(PhaseColor, Color.white, 0.5f), 1.6f, 1.5f, 0.2f);
            Vector2 at = Feet;
            if (p == 0) { if (FreeSide(1) != 0 || FreeSide(-1) != 0) spottedReady = true; }
            else if (p == 1) StartR(Firebrand(t.EyePos, at));
            else if (p == 2) StartR(FlashFreeze(at));                                   // replaced the ice cage
            else if (p == 3) StartR(StaticCharge());                                     // replaced the lightning lance (overlapped the eye laser)
            else StartR(ShadowWraith(at));
        }

        // ------------------------------------------------------------------ P1: spotted -> CRUSHING GRIP (called from PhaseLoop)
        private IEnumerator Spotted()
        {
            yield return CrushingGrip();                                                // grab, lift and squeeze Ren
        }

        // ------------------------------------------------------------------ P2: FIREBRAND — 3 arcing firebombs + fire carpet
        private IEnumerator Firebrand(Vector2 eye, Vector2 at)
        {
            var bolt = SpriteVfx.Load("abs_fire_bolt");
            for (int k = 0; k < 3 && !aborted; k++)
            {
                Vector2 target = ClampFloor(k == 0 ? at : at + PlayerVel * 0.5f + Random.insideUnitCircle * 0.9f, 0.4f);
                StartR(FireBomb(eye, target, bolt));
                yield return Wait(0.22f);
            }
        }

        private IEnumerator FireBomb(Vector2 from, Vector2 to, Sprite[] bolt)
        {
            const float Flight = 0.95f;
            var ring = NewRing(to, 0.6f);
            var g = new GameObject("firebrand");
            fieldObjs.Add(g);
            var sr = g.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order + 4;
            var l = BossJuice.Light(from, FireCol, 1f, 1.2f, 0f);
            Sfx.Play("trap_fire", from, 0.5f);
            Vector2 prev = from, mid = (from + to) * 0.5f + Vector2.up * Mathf.Max(1.6f, (from - to).magnitude * 0.35f);
            for (float t = 0f; t < Flight && !aborted; t += Time.deltaTime)
            {
                float u = t / Flight, w = 1f - u;
                Vector2 p = w * w * from + 2f * w * u * mid + u * u * to;
                Vector2 d = p - prev; prev = p;
                g.transform.position = p;
                if (d.sqrMagnitude > 0.000001f) g.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                if (bolt.Length > 0) sr.sprite = bolt[(int)(Time.time * 14f) % bolt.Length];
                if (l != null) l.transform.position = p;
                float rk = Mathf.Lerp(0.6f, 0.4f, u) / (Ring().bounds.size.x * 0.5f);
                ring.transform.localScale = new Vector3(rk, rk * 0.6f, 1f);
                ring.color = new Color(1f, 0.55f, 0.2f, 0.4f + 0.5f * u);
                yield return null;
            }
            DestroyObj(g); DestroyObj(ring);
            if (l != null) Object.Destroy(l.gameObject);
            if (aborted) yield break;
            SpriteVfx.Spawn("vc_lava_impact", to, 1.1f, layer, order + 1, false, 16f, false);
            SpriteVfx.Spawn("abs_fire_blast", to + Vector2.up * 0.15f, 0.7f, layer, order + 2, false, 18f, false);
            Sfx.Play("bolt_strike", to, 0.4f);
            if ((Feet - to).magnitude < 0.6f) { Hurt(10f, to, 0.8f); Ignite(); }
            // fire carpet 3 s
            var pool = SpriteVfx.Spawn("abs_fire_pool", to, 1f, layer, -13, Random.value < 0.5f, 12f, false);
            if (pool != null) pool.Looping(3f);
            var pl = BossJuice.Light(to, FireCol, 1.1f, 0.9f, 0f);
            float nextBurn = 0f;
            for (float t = 0f; t < 3f && !aborted; t += Time.deltaTime)
            {
                Vector2 q = Feet - to;
                if ((q.x * q.x) / 0.36f + (q.y * q.y) / 0.16f <= 1f && Time.time >= nextBurn) { Ignite(); if (Hurt(3f, to, 0f)) nextBurn = Time.time + 0.5f; }
                if (pl != null) pl.intensity = 0.9f * Mathf.Clamp01((3f - t) / 0.5f) * (0.85f + 0.15f * Mathf.Sin(Time.time * 13f));
                yield return null;
            }
            if (pool != null) pool.Stop();
            if (pl != null) Object.Destroy(pl.gameObject);
        }

        // ------------------------------------------------------------------ P3: FROST CAGE — ring of ice pillars traps Ren, strike one to escape (unused, see header)
        private Vector2 cageAt;
        private readonly List<SpriteRenderer> cagePillars = new List<SpriteRenderer>();
        private bool cageOn, cageBroken;

        private IEnumerator FrostCage(Vector2 at)
        {
            if (cageOn) yield break;
            const int N = 8; const float R = 1.0f, Sq = 0.62f;
            at = ClampFloor(at, 1f);
            cageAt = at; cageBroken = false;
            var marks = new List<SpriteRenderer>();
            for (int k = 0; k < N; k++)
            {
                float a = k * Mathf.PI * 2f / N;
                marks.Add(NewRing(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * Sq) * R, 0.18f));
            }
            Sfx.Play("gas_hiss", at, 0.6f);
            for (float t = 0f; t < 0.55f && !aborted; t += Time.deltaTime)
            {
                foreach (var m in marks) m.color = new Color(0.7f, 0.95f, 1f, Mathf.Sin(t * 30f) > 0f ? 0.9f : 0.4f);
                yield return null;
            }
            foreach (var m in marks) DestroyObj(m);
            if (aborted) yield break;
            // pillars rise
            var blockS = Resources.Load<Sprite>("VFX/vc_ice_block");
            cagePillars.Clear();
            for (int k = 0; k < N; k++)
            {
                float a = k * Mathf.PI * 2f / N;
                Vector2 p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * Sq) * R;
                var sr = NewLine("frost cage pillar", p.y < at.y ? order + 1 : order - 1);
                sr.sprite = blockS != null ? blockS : Disc();
                sr.transform.position = p;
                sr.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
                sr.color = new Color(0.9f, 0.97f, 1f, 0.92f);
                cagePillars.Add(sr);
                SpriteVfx.Spawn("abs_ice_spikes", p, 0.9f, layer, order + 2, k % 2 == 0, 18f, false);
            }
            cageOn = true;
            Sfx.Play("trap_ice", at, 1f);
            Sfx.Play("ice_shatter", at, 0.5f);
            BossJuice.Shake(1);
            // a pillar rising right on Ren
            foreach (var sr in cagePillars) if (sr != null && ((Vector2)sr.transform.position - Feet).magnitude < 0.32f) { Hurt(8f, sr.transform.position, 1f); Chill(0.7f, 1.2f, "Caged in ice!"); break; }
            Vector2 d0 = Feet - at;
            bool inside = new Vector2(d0.x, d0.y / Sq).magnitude < R;
            if (inside) DrillBoard.Say("CAGED - STRIKE A PILLAR", IceCol);
            float nextHit = 0f, dur = Float("cage_seconds", 3.2f);
            for (float t = 0f; t < dur && !aborted && !cageBroken; t += Time.deltaTime)
            {
                Vector2 d = Feet - at;
                float rr = new Vector2(d.x, d.y / Sq).magnitude;
                if (inside && rr > R - 0.12f && Time.time >= nextHit)
                {
                    nextHit = Time.time + 0.6f;
                    Vector2 outside = at + (d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down) * (R + 0.6f);
                    if (Hurt(8f, outside, 1.2f)) Chill(0.7f, 1.2f, "The cage bites!");
                    SpriteVfx.Spawn("duo_ice_burst", Feet, 0.7f, layer, order + 2, false, 20f, false);
                }
                if (!inside && rr < R - 0.2f) inside = true;                                   // push inside
                yield return null;
            }
            // break / melt
            foreach (var sr in cagePillars)
                if (sr != null)
                {
                    SpriteVfx.Spawn("abs_ice_shatter", (Vector2)sr.transform.position + Vector2.up * 0.25f, 0.7f, layer, order + 2, false, 20f, false);
                    DestroyObj(sr);
                }
            cagePillars.Clear();
            Sfx.Play("ice_shatter", at, 0.9f);
            if (cageBroken) DrillBoard.Say("SHATTERED", RelicBlue);
            cageOn = false;
        }

        /// <summary>Ren's sword hits an ice cage pillar -> the cage breaks.</summary>
        private void StrikeCage(Vector2 c, float r)
        {
            if (!cageOn) return;
            foreach (var sr in cagePillars)
                if (sr != null && ((Vector2)sr.transform.position + Vector2.up * 0.2f - c).magnitude < r + 0.35f) { cageBroken = true; return; }
        }

        // ------------------------------------------------------------------ P4: LIGHTNING LANCE — straight lightning spear from the tower eye (unused, see header)
        private IEnumerator LightningLance(WatchTowerMechanism t)
        {
            Vector2 eye = t.EyePos, aim = Feet;
            var warn = BeamWarn.Spawn(eye, eye + (aim - eye).normalized * 14f, 0.22f, StormWarn, 0.7f, layer, order + 3);
            var glow = BossJuice.Light(eye, StormCol, 1.4f, 0.5f, 0f);
            Sfx.Play("eye_beam_charge", eye, 0.8f);
            for (float k = 0f; k < 0.7f && !aborted; k += Time.deltaTime)
            {
                if (k < 0.45f) aim = Vector2.Lerp(aim, Feet + PlayerVel * 0.15f, 1f - Mathf.Exp(-8f * Time.deltaTime));
                if (warn != null) warn.Aim(eye, eye + (aim - eye).normalized * 14f);
                if (glow != null) glow.intensity = 0.5f + 2.5f * k;
                yield return null;
            }
            if (warn != null) warn.Release();
            if (glow != null) Object.Destroy(glow.gameObject);
            if (aborted) yield break;
            Vector2 dir = (aim - eye).normalized, end = ClipToFloor(eye, eye + dir * 14f);
            // a lightning rod on the lance path -> the lance strikes the rod
            Rod catcher = null; float best = float.MaxValue;
            foreach (var r in rods)
            {
                if (r.dead) continue;
                if (SegDist(RodTip(r), eye, end) < 0.5f) { float d = (RodTip(r) - eye).magnitude; if (d < best) { best = d; catcher = r; } }
            }
            if (catcher != null) end = RodTip(catcher);
            var arc = MakeArc(eye, end, 12);
            arc.Jag = 0.08f;
            Sfx.Play("bolt_strike", end, 1f);
            BossJuice.Shake(1);
            SpriteVfx.Spawn("vc_volt_impact", end, 0.9f, layer, order + 3, false, 22f, false);
            bool hit = false;
            for (float k = 0f; k < 0.4f && !aborted; k += Time.deltaTime)
            {
                arc.Alpha = 1f - Mathf.Clamp01((k - 0.25f) / 0.15f);
                BeamWidth(arc, 1.6f - k * 2f);
                if (!hit && catcher == null && SegDist(Feet + Vector2.up * 0.2f, eye, end) < 0.3f) hit = Shock(14f, eye, 1.2f, 0f);
                yield return null;
            }
            KillArc(arc);
            if (catcher != null) { SkyBolt(RodTip(catcher), 0.9f); DrillBoard.Say("THE ROD TAKES IT", StormCol); }
        }

        // ------------------------------------------------------------------ P5: SHADOW WRAITH — Ren's own shadow walks Ren's exact path
        private sealed class Wraith { public SpriteRenderer body, rim; public Light2D l; public float born; public bool dead; }
        private readonly List<Wraith> wraiths = new List<Wraith>();
        private readonly List<(float t, Vector2 p, Sprite s, bool flip)> trail = new List<(float, Vector2, Sprite, bool)>();
        private SpriteRenderer renSr;

        /// <summary>Record Ren's path + sprite (for the chasing shadow). Called every frame from TickUmbra.</summary>
        private void RecordTrail()
        {
            if (Ctx.Player == null) return;
            if (renSr == null) renSr = Ctx.Player.GetComponentInChildren<SpriteRenderer>();
            float now = Time.time;
            trail.Add((now, Feet, renSr != null ? renSr.sprite : null, renSr != null && renSr.flipX));
            while (trail.Count > 0 && now - trail[0].t > 2f) trail.RemoveAt(0);
        }

        private (Vector2 p, Sprite s, bool flip) TrailAt(float t)
        {
            for (int i = trail.Count - 1; i >= 0; i--) if (trail[i].t <= t) return (trail[i].p, trail[i].s, trail[i].flip);
            return trail.Count > 0 ? (trail[0].p, trail[0].s, trail[0].flip) : (Feet, null, false);
        }

        private IEnumerator ShadowWraith(Vector2 at)
        {
            var w = new Wraith { born = Time.time };
            var g = new GameObject("shadow wraith");
            fieldObjs.Add(g);
            g.transform.position = at;
            w.rim = new GameObject("rim").AddComponent<SpriteRenderer>();
            w.rim.transform.SetParent(g.transform, false);
            w.rim.transform.localScale = new Vector3(1.12f, 1.06f, 1f);
            FxLighting.MakeUnlit(w.rim);
            w.rim.sortingLayerID = layer; w.rim.sortingOrder = order - 1;
            w.body = g.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(w.body);
            w.body.sortingLayerID = layer; w.body.sortingOrder = order;
            if (renSr != null) { w.body.transform.localScale = renSr.transform.lossyScale; }
            w.l = BossJuice.Light(at, UmbraCol, 0.9f, 0.7f, 0f);
            wraiths.Add(w);
            SpriteVfx.Spawn("abs_grasp", at, 0.9f, layer, order - 1, false, 24f, false);
            SpriteVfx.Spawn("vc_umbral_pop", at + Vector2.up * 0.35f, 1f, layer, order + 2, false, 16f, false);
            Sfx.Play("teleport_dark", at, 0.7f);
            float life = Float("wraith_seconds", 7f), delay = Float("wraith_delay", 0.9f);
            Vector2 pos = at;
            bool hit = false;
            while (!aborted && !w.dead && Time.time - w.born < life && !hit)
            {
                float age = Time.time - w.born;
                var (p, s, flip) = TrailAt(Time.time - delay);
                pos = Vector2.MoveTowards(pos, p, Float("wraith_speed", 3.4f) * Time.deltaTime);
                g.transform.position = pos;
                if (s != null) { w.body.sprite = s; w.rim.sprite = s; w.body.flipX = flip; w.rim.flipX = flip; }
                float a = Mathf.Clamp01(age / 0.4f) * Mathf.Clamp01((life - age) / 0.4f);
                w.body.color = new Color(0.12f, 0.03f, 0.2f, 0.92f * a);
                w.rim.color = new Color(0.75f, 0.4f, 1f, (0.45f + 0.25f * Mathf.Sin(Time.time * 9f)) * a);
                w.body.sortingOrder = pos.y < Feet.y ? order + 1 : order - 1;
                w.rim.sortingOrder = w.body.sortingOrder - 1;
                if (w.l != null) w.l.transform.position = pos + Vector2.up * 0.3f;
                if (age > 0.5f && (pos - Feet).magnitude < 0.38f && Hurt(10f, pos, 0.9f))
                {
                    hit = true;
                    var st = RenStatus; if (st != null) st.Slow(0.6f, 2f, "Your shadow drags at you.");
                }
                yield return null;
            }
            SpriteVfx.Spawn("vc_umbral_pop", pos + Vector2.up * 0.35f, 1.1f, layer, order + 2, false, 16f, false);
            if (w.dead) DrillBoard.Say("DISPELLED", RelicBlue);
            if (w.l != null) Object.Destroy(w.l.gameObject);
            DestroyObj(g);
            wraiths.Remove(w);
        }

        /// <summary>Sword hit on the shadow -> it dissolves.</summary>
        private void StrikeWraiths(Vector2 c, float r)
        {
            foreach (var w in wraiths)
                if (!w.dead && w.body != null && ((Vector2)w.body.transform.position + Vector2.up * 0.3f - c).magnitude < r + 0.4f) w.dead = true;
        }

        private void KillLightFx()
        {
            foreach (var w in wraiths) w.dead = true;
            wraiths.Clear();
            cageOn = false; cagePillars.Clear();
        }
    }
}
