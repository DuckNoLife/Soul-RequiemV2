using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE 2 CINDER (fire, the whole map turns volcanic red).
    ///   MAGMA BREATH  — the Vessel LOWERS ITS HEAD to the arena edge (VesselRig.HeadDip), opens its mouth and POURS A LAVA FALL onto the north edge;
    ///                   lava FLOODS from the foot of the fall across the arena (MagmaField: soft curved edges, 9 x 5 grid interpolated + noise), leaving ONE continuous
    ///                   curved path; the area about to flood glows dark red first.
    ///                   Standing in lava = BURN: HP drains (5 / 0.4 s) + fire clings to Ren, who keeps burning for 1.6 s after leaving the lava (3 / 0.5 s).
    ///   IMP DROP      — fist raised for 2 s (shaking, fire gathering), then opens: 3–4 imps drop around Ren (lava burst on landing). Max 4 alive.
    ///   SPITFIRE TRAP — a hand places a flamethrower next to Ren; warning ring at flame range + slowly turning guide lines (show the spin direction) for 1 s -> 4 (6)
    ///                   ROTATING FIRE JETS around it (accelerating 25 -> 70°/s; under high pressure it reverses mid-way, with a warning) for 5 s -> off, the hand PICKS it up again.
    ///   EMBER WISPS   — both eye sockets blaze and release 6–8 BIG FIREBALLS that chase Ren, then burst / die after ~3 s.
    ///   SLAM          — like Phase 1 (+ lava burst at the impact).
    /// Assets: _tools/s418/make_cinder.py, _tools/s421/make_s421.py (vc_magma_fall, vc_spit_h/v/d_loop). Nozzle / flame tip coordinates: _tools/s418/cinder_geom.json.
    /// Called by: PhaseLoop (move table), TickFight (TickBurn). Calls: MagmaField, MapEncounterDirector.SpawnInZone (Cinder_Imp).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static readonly Color FireCol = new Color(1f, 0.5f, 0.2f);
        private readonly List<EnemyAI> imps = new List<EnemyAI>();
        private readonly bool[] handBusy = new bool[2];
        private float jawHold = -1f;                                                    // >= 0: hold the jaw open at exactly this amount (breath / spit)
        private bool impWarned;
        private float burnUntil, nextBurn, burnSaid;
        private SpriteVfx burnFx;

        // horizontal flame: nozzle -> flame tip (units, unrotated) = (1.29, 0.30) -> 13.1° off the horizontal axis (cinder_geom.json)
        private const float FlameLen = 1.32f, FlameTilt = 13.1f;

        private Move[] FireMoves() => new[]
        {
            new Move { name = "SLAM", w = 1.5f, slam = true, run = pr => Slam(Feet.x < chest.x ? -1 : 1, pr) },
            new Move { name = "MAGMA BREATH", w = 1.6f, run = MagmaBreath },
            new Move { name = "IMP DROP", w = 1.1f, run = ImpDrop },
            new Move { name = "SPITFIRE TRAP", w = 1.3f, run = SpitfireTrap },
            new Move { name = "EMBER WISPS", w = 1.5f, run = EmberWisps },
        };

        // ------------------------------------------------------------------ BURN (standing in lava / on fire)
        private void Ignite()
        {
            burnUntil = Time.time + 1.6f;
            if (burnFx == null && Ctx.Player != null)
            {
                burnFx = SpriteVfx.SpawnAttached("abs_floor_flame", Ctx.Player, 0.55f, layer, order + 1, 14f, 999f);
                if (Time.time > burnSaid) { burnSaid = Time.time + 4f; DrillBoard.Say("BURNING", FireCol); Sfx.Play("st_burn", Feet, 0.7f); }
            }
        }

        /// <summary>Every frame: still burning -> HP drains; burn over -> remove the fire on Ren.</summary>
        private void TickBurn()
        {
            if (Time.time < burnUntil)
            {
                if (Time.time >= nextBurn && Hurt(3f, Feet + Vector2.up * 0.1f, 0f)) nextBurn = Time.time + 0.5f;
                return;
            }
            if (burnFx != null) { burnFx.Stop(); burnFx = null; }
        }

        private void ClearBurn()
        {
            burnUntil = 0f;
            if (burnFx != null) { burnFx.Stop(); burnFx = null; }
        }

        // ------------------------------------------------------------------ MAGMA BREATH
        private MagmaField magma;

        private void KillMagma()
        {
            if (magma != null) { magma.Destroy(); magma = null; }
        }

        private IEnumerator MagmaBreath(float pressure)
        {
            DrillBoard.Say("MAGMA BREATH", FireCol);
            DrillBoard.Yell("Walk where I let you. Nowhere else.", 1.8f);
            const int Cols = 9, Rows = 5;
            // safe path: from the north row to the south, each row shifts 0..2 columns, always connected (rows share a cell -> smoothing never breaks it)
            var lavaCells = new bool[Cols, Rows];
            for (int c = 0; c < Cols; c++) for (int r = 0; r < Rows; r++) lavaCells[c, r] = true;
            float cw = Floor.width / Cols;
            int col = Mathf.Clamp(Mathf.FloorToInt((Feet.x - Floor.xMin) / cw) + Random.Range(-2, 3), 0, Cols - 1);
            for (int r = Rows - 1; r >= 0; r--)
            {
                int next = Mathf.Clamp(col + Random.Range(-2, 3), 0, Cols - 1);
                for (int c = Mathf.Min(col, next); c <= Mathf.Max(col, next); c++) lavaCells[c, r] = false;
                if (r > 0) lavaCells[next, r - 1] = false;
                col = next;
            }
            // 1) lower the head to the arena edge, open the mouth, fire gathers inside; the area about to flood turns dark red (warning)
            KillMagma();
            Vector2 pour = new Vector2(Mathf.Clamp(rig.MouthWorld().x, Floor.xMin + 1f, Floor.xMax - 1f), Floor.yMax);
            magma = new MagmaField(Floor, lavaCells, pour, SpriteVfx.Load("vc_lava"), layer, order, Ctx.Host.transform);
            fieldObjs.Add(magma.Root);
            var field = magma;
            jawHold = 1f;
            slumpTarget = 0.3f;
            var mouthGlow = BossJuice.Light(rig.MouthWorld(), FireCol, 2.2f, 0.4f, 0f);
            Sfx.Play("quake_rumble", rig.MouthWorld(), 0.9f);
            for (float t = 0f; t < 0.9f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.9f;
                rig.HeadDip = u * u * (3f - 2f * u);
                if (mouthGlow != null) { mouthGlow.transform.position = rig.MouthWorld(); mouthGlow.intensity = Mathf.Lerp(0.4f, 1.8f, u); }
                field.Tick(Time.time, 0f, u * (0.3f + 0.12f * Mathf.Sin(Time.time * 14f)), 1f, 1f);
                yield return null;
            }
            if (aborted) { KillMagma(); EndMagmaPose(mouthGlow); yield break; }

            // 2) LAVA FALL (vc_magma_fall: the pack's waterfall layout, sliced vertically) pours from the mouth onto the north edge
            var fallFrames = SpriteVfx.Load("vc_magma_fall");
            const float FallK = 1.3f;
            var fallGo = new GameObject("magma fall");
            fieldObjs.Add(fallGo);
            var fsr = fallGo.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(fsr);
            fsr.sortingLayerID = layer; fsr.sortingOrder = order - 1;
            if (fallFrames.Length > 0) { fsr.sprite = fallFrames[0]; fsr.drawMode = SpriteDrawMode.Tiled; }
            fallGo.transform.localScale = new Vector3(FallK, FallK, 1f);
            var footLight = BossJuice.Light(pour, FireCol, 2.6f, 0f, 0f);
            Sfx.Play("trap_fire", rig.MouthWorld(), 0.9f);
            BossJuice.Shake(1);

            float tPour = Time.time + 0.35f, pourDur = Mathf.Lerp(2.4f, 1.8f, pressure * 0.5f);
            float pourEnd = tPour + pourDur + 0.5f, hotEnd = pourEnd + 3f, end = hotEnd + 1.3f;
            float nextHurt = 0f, nextSplash = 0f, fallStart = Time.time;
            bool lifted = false;
            // orange lights follow the lava that has arrived
            var glowPts = new List<(Light2D l, float d)>();
            for (int gx = 0; gx < 3; gx++)
                for (int gy = 0; gy < 2; gy++)
                {
                    var p = new Vector2(Floor.xMin + Floor.width * (gx + 0.5f) / 3f, Floor.yMin + Floor.height * (gy + 0.5f) / 2f);
                    var l = BossJuice.Light(p, FireCol, Floor.width * 0.3f, 0f, 0f);
                    if (l != null) glowPts.Add((l, field.Dist(p)));
                }
            while (Time.time < end && !aborted)
            {
                float now = Time.time;
                // fall: pours down (0.35 s) -> flows -> drains from the top (0.35 s)
                Vector2 m = rig.MouthWorld();
                float L = Mathf.Max(0.3f, m.y - pour.y + 0.1f);
                float grow = Mathf.Clamp01((now - fallStart) / 0.35f), shrink = Mathf.Clamp01((now - pourEnd) / 0.35f);
                float len = L * grow * (1f - shrink);
                if (fallGo != null)
                {
                    fsr.enabled = len > 0.02f;
                    fallGo.transform.position = new Vector3(pour.x, m.y - L * shrink, 0f);
                    if (fallFrames.Length > 0) fsr.sprite = fallFrames[(int)(now * 14f) % fallFrames.Length];
                    fsr.size = new Vector2(1.28f, len / FallK);
                    if (shrink >= 1f) { DestroyObj(fallGo); fallGo = null; }
                }
                if (!lifted && shrink >= 1f) { lifted = true; StartR(LiftHead(mouthGlow)); mouthGlow = null; }
                if (mouthGlow != null) mouthGlow.transform.position = m;
                // lava foam at the foot of the fall
                if (now >= tPour - 0.05f && now < pourEnd && now >= nextSplash)
                {
                    nextSplash = now + Random.Range(0.14f, 0.26f);
                    SpriteVfx.Spawn("vc_lava_impact", pour + new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(-0.15f, 0.1f)), Random.Range(0.8f, 1.15f), layer, order + 1, Random.value < 0.5f, 18f, false);
                }
                if (footLight != null) footLight.intensity = now < pourEnd ? 1.6f + 0.4f * Mathf.Sin(now * 17f) : Mathf.MoveTowards(footLight.intensity, 0f, Time.deltaTime * 3f);

                // lava surface: floods outward, hot -> cooling -> gone
                float fu = Mathf.Clamp01((now - tPour) / pourDur);
                float reachNow = field.MaxReach * (1f - (1f - fu) * (1f - fu));        // fast at first, slowing as it spreads
                float heat = Mathf.Clamp01(1f - (now - hotEnd) / 0.8f);
                float alpha = Mathf.Clamp01((end - now) / 0.5f);
                float warn = now < tPour + pourDur ? 0.3f + 0.12f * Mathf.Sin(now * 14f) : Mathf.Max(0f, 0.3f - (now - tPour - pourDur) * 0.6f);
                field.Tick(now, reachNow, warn, heat, alpha);
                foreach (var (l, d) in glowPts)
                    if (l != null) l.intensity = 0.85f * Mathf.Clamp01((reachNow - d) / 0.8f) * heat * (0.85f + 0.15f * Mathf.Sin(now * 5f + d));

                // burn: exactly where lava is visible
                if (heat > 0.3f && now >= nextHurt && field.Burning(Feet))
                {
                    Ignite();
                    if (Hurt(5f, Feet + Vector2.up * 0.1f, 0f)) nextHurt = now + 0.4f;
                }
                yield return null;
            }
            if (fallGo != null) DestroyObj(fallGo);
            if (footLight != null) Object.Destroy(footLight.gameObject);
            foreach (var (l, _) in glowPts) if (l != null) Object.Destroy(l.gameObject);
            if (magma == field) KillMagma(); else field.Destroy();
            if (!lifted) EndMagmaPose(mouthGlow);
        }

        private IEnumerator LiftHead(Light2D glow)
        {
            float d0 = rig.HeadDip;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                rig.HeadDip = Mathf.Lerp(d0, 0f, t / 0.7f);
                if (glow != null) glow.intensity = Mathf.Lerp(1.8f, 0f, t / 0.7f);
                yield return null;
            }
            rig.HeadDip = 0f;
            jawHold = -1f;
            slumpTarget = 0f;
            if (glow != null) Object.Destroy(glow.gameObject);
        }

        private void EndMagmaPose(Light2D glow)
        {
            if (glow != null) Object.Destroy(glow.gameObject);
            if (rig != null && rig.HeadDip > 0f) StartR(LiftHead(null));
            else { jawHold = -1f; slumpTarget = 0f; }
        }

        // ------------------------------------------------------------------ IMP DROP
        private IEnumerator ImpDrop(float pressure)
        {
            imps.RemoveAll(e => e == null || e.Health == null || e.Health.IsDead);
            int want = (pressure < 1f ? 3 : 4) - imps.Count;
            if (want <= 0) yield break;
            int side = FreeSide(Feet.x < chest.x ? -1 : 1);
            if (side == 0) yield break;
            DrillBoard.Say("IMP DROP", FireCol);
            DrillBoard.Yell("Little embers. Fetch.", 1.6f);
            Vector2 over = ClampFloor(new Vector2(Mathf.Lerp(Floor.center.x, Feet.x, 0.5f), Floor.center.y + 0.5f), 1f);
            var glow = BossJuice.Light(over + Vector2.up * 2.4f, FireCol, 1.8f, 1.4f, 0f);
            for (float t = 0f; t < 2f && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(side, over + Vector2.up * 2.4f + new Vector2(Mathf.Sin(t * 30f) * 0.03f * t, 0f), 8f, "hand_fist");
                if (glow != null) glow.transform.position = rig.HandWorld(side);
                if (Random.value < 0.18f) SpriteVfx.Spawn("abs_fire_blast", rig.HandWorld(side) + Random.insideUnitCircle * 0.45f, 0.45f, layer, order + 5, false, 20f, false);
                yield return null;
            }
            if (glow != null) Object.Destroy(glow.gameObject);
            if (aborted) yield break;
            rig.SetHand(side, over + Vector2.up * 2.5f, 20f, "hand_open");
            Sfx.Play("teleport_dark", over, 0.7f);
            SpriteVfx.Spawn("abs_fire_blast", rig.HandWorld(side), 1.2f, layer, order + 5, false, 18f, false);
            Vector2 from = rig.HandWorld(side) + Vector2.down * 0.3f;
            for (int i = 0; i < want; i++)
            {
                float a = (i / (float)want) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                Vector2 to = ClampFloor(Feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(1.3f, 2.0f), 0.5f);
                StartR(ImpFalls(from + Random.insideUnitCircle * 0.2f, to));
                yield return Wait(0.12f);
            }
            yield return Wait(0.7f);
            rig.RestHand(side, 5f);
        }

        private IEnumerator ImpFalls(Vector2 from, Vector2 to)
        {
            var v = SpriteVfx.Spawn("vc_imp_fall", from, 0.95f, layer, order + 3, Random.value < 0.5f, 12f, false);
            if (v != null) v.Looping(2f);
            var mark = RippleAt(to, FireCol);
            for (float t = 0f; t < 0.5f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.5f;
                if (v != null) v.transform.position = Vector2.Lerp(from, to, u * u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.4f;
                yield return null;
            }
            if (v != null) v.Stop();
            DestroyObj(mark);
            if (aborted) yield break;
            SpriteVfx.Spawn("vc_lava_impact", to, 1.1f, layer, order + 1, false, 18f, false);
            BossJuice.Flash(to, FireCol, 1.4f, 1.4f, 0.25f);
            Sfx.Play("hit_block", to, 0.45f);
            if ((Feet - to).magnitude < 0.45f) Hurt(6f, to, 0.6f);
            var e = Ctx.Director != null ? Ctx.Director.SpawnInZone("Cinder_Imp", to, null) : null;
            if (e != null) imps.Add(e);
            else if (!impWarned) { impWarned = true; Debug.LogWarning("[Vessel] No Cinder_Imp enemy yet - run Soul Requiem > Build Map 4 Enemies, then rebuild scene Map 4n."); }
        }

        /// <summary>Phase end / death: kill every imp.</summary>
        private void KillImps()
        {
            foreach (var e in imps)
                if (e != null && e.Health != null && !e.Health.IsDead) e.Health.TakeDamage(9999f, Vector2.zero, 0f);
            imps.Clear();
        }

        // ------------------------------------------------------------------ SPITFIRE TRAP (ROTATING fire)
        private IEnumerator SpitfireTrap(float pressure)
        {
            int side = FreeSide(Feet.x < chest.x ? -1 : 1);
            if (side == 0) yield break;
            int i = Idx(side);
            handBusy[i] = true;
            DrillBoard.Say("SPITFIRE TRAP", FireCol);
            const float DevScale = 1.4f;
            Vector2 at = ClampFloor(Feet + PlayerVel * 0.3f + Random.insideUnitCircle * 0.5f, 1.6f);
            var dev = SpriteVfx.Spawn("vc_spit_start", rig.HandWorld(side), DevScale, layer, order, false, 0.01f, false);
            if (dev != null) { dev.Looping(999f); loopFx.Add(dev); }
            for (float t = 0f; t < 0.7f && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(side, at + Vector2.up * 1.4f, 9f, "hand_claw");
                if (dev != null) dev.transform.position = rig.HandWorld(side) + Vector2.down * 0.4f;
                yield return null;
            }
            rig.SetHand(side, at + Vector2.up * 0.5f, 40f, "hand_claw");
            for (float t = 0f; t < 0.12f && !aborted; t += Time.deltaTime)
            {
                if (dev != null) dev.transform.position = Vector2.Lerp(dev.transform.position, at, 0.5f);
                yield return null;
            }
            if (dev != null) { dev.Stop(); dev = null; }
            if (aborted) { handBusy[i] = false; yield break; }
            SlamFx(at, 1);
            yield return Wait(0.2f);
            rig.RestHand(side, 6f);
            handBusy[i] = false;
            StartR(SpitfireRun(at, side, pressure, DevScale));
            yield return Wait(0.4f);
        }

        private IEnumerator SpitfireRun(Vector2 at, int side, float pressure, float devScale)
        {
            float S = Float("spit_scale", 1.5f), L = FlameLen * S * 0.95f;
            int jets = pressure >= 1.3f ? 6 : 4;
            Vector2 noz = at + Vector2.up * 0.36f;
            var body = SpriteVfx.Spawn("vc_spit_start", at, devScale, layer, order - 1, false, 16f, false);
            var glow = BossJuice.Light(noz, FireCol, L * 1.1f, 0.4f, 0f);
            if (glow != null) fieldObjs.Add(glow.gameObject);
            // warning: flame-range ring + slowly turning guide lines (spin direction)
            float dir = Random.value < 0.5f ? -1f : 1f, ang = Random.Range(0f, 360f);
            var ring = new GameObject("spitfire ring");
            fieldObjs.Add(ring);
            ring.transform.position = noz;
            var rr = ring.AddComponent<SpriteRenderer>();
            rr.sprite = SigilRing(); FxLighting.MakeUnlit(rr);
            rr.sortingLayerID = layer; rr.sortingOrder = -14;
            float rk = L / (Ring().bounds.size.x * 0.5f);
            ring.transform.localScale = new Vector3(rk, rk, 1f);
            var guides = new List<FxBeam>();                                              // guide lines = thin fire beams
            for (int j = 0; j < jets; j++) guides.Add(NewBeam(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.6f, 0.25f, 0.9f), new Color(0.9f, 0.3f, 0.1f, 0.5f), 0.09f, -13, true));
            Sfx.Play("trap_fire", at, 0.5f);
            for (float t = 0f; t < 1f && !aborted; t += Time.deltaTime)
            {
                ang += dir * 25f * Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 18f);
                rr.color = new Color(FireCol.r, FireCol.g, FireCol.b, 0.35f + 0.35f * pulse);
                for (int j = 0; j < jets; j++)
                {
                    float a = (ang + j * 360f / jets) * Mathf.Deg2Rad;
                    SetBeam(guides[j], noz, noz + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * L, 0.35f + 0.45f * pulse, 1f);
                }
                if (glow != null) glow.intensity = 0.4f + 0.6f * t;
                yield return null;
            }
            foreach (var g in guides) KillBeam(g);
            if (body != null) body.Stop();
            body = null;
            string headKey = "";                                                        // the nozzle head TURNS with the jets (switches horizontal / diagonal / vertical sprites)
            // ignite: each jet = a horizontal flame ROTATING around the nozzle (pivot = nozzle)
            var fl = new SpriteVfx[jets];
            for (int j = 0; j < jets; j++) fl[j] = SpriteVfx.Spawn("vc_flame_h_start", noz, S, layer, order + 2, false, 32f, false);
            Sfx.Play("trap_fire", at, 0.8f);
            BossJuice.Shake(1);
            float total = Float("spit_seconds", 5f), tStart = Time.time, nextHurt = 0f;
            bool looped = false, reversed = false;
            float speed = 25f, top = 70f + 20f * Mathf.Clamp01(pressure - 1f);
            while (Time.time - tStart < total && !aborted)
            {
                float t = Time.time - tStart;
                if (!looped && t >= 0.45f)
                {
                    looped = true;
                    for (int j = 0; j < jets; j++) { if (fl[j] != null) fl[j].Stop(); fl[j] = SpriteVfx.Spawn("vc_flame_h_loop", noz, S, layer, order + 2, false, 16f, false); if (fl[j] != null) { fl[j].Looping(999f); loopFx.Add(fl[j]); } }
                }
                // high pressure: reverse mid-way (slow down + 0.4 s warning flash)
                bool slowing = pressure >= 1.2f && !reversed && t > total * 0.45f && t < total * 0.45f + 0.4f;
                if (pressure >= 1.2f && !reversed && t >= total * 0.45f + 0.4f) { reversed = true; dir = -dir; speed = 20f; DrillBoard.Say("REVERSE", FireCol); }
                speed = slowing ? Mathf.MoveTowards(speed, 0f, 200f * Time.deltaTime) : Mathf.MoveTowards(speed, top, 30f * Time.deltaTime);
                ang += dir * speed * Time.deltaTime;
                SpitHead(ang, ref headKey, ref body, at, devScale);
                for (int j = 0; j < jets; j++)
                {
                    if (fl[j] == null) continue;
                    float a = ang + j * 360f / jets;
                    fl[j].transform.position = noz;
                    fl[j].transform.rotation = Quaternion.Euler(0f, 0f, a - FlameTilt);
                    fl[j].Tint(slowing && Mathf.Sin(t * 40f) < 0f ? new Color(1f, 0.6f, 0.4f) : Color.white);
                }
                if (glow != null) glow.intensity = 1.3f + 0.3f * Mathf.Sin(t * 13f);
                rr.color = new Color(FireCol.r, FireCol.g, FireCol.b, 0.25f);
                if (Time.time >= nextHurt && t > 0.25f)
                    for (int j = 0; j < jets; j++)
                    {
                        float a = (ang + j * 360f / jets) * Mathf.Deg2Rad;
                        Vector2 tip = noz + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * L;
                        if (SegDist(Feet + Vector2.up * 0.15f, noz, tip) < 0.26f) { Ignite(); if (Hurt(9f, noz, 0.8f)) nextHurt = Time.time + 0.4f; break; }
                    }
                yield return null;
            }
            for (int j = 0; j < jets; j++)
            {
                if (fl[j] == null) continue;
                var rot = fl[j].transform.rotation;
                fl[j].Stop();
                var e = SpriteVfx.Spawn("vc_flame_h_end", noz, S, layer, order + 2, false, 28f, false);
                if (e != null) e.transform.rotation = rot;
            }
            DestroyObj(ring);
            if (glow != null) Object.Destroy(glow.gameObject);
            if (body != null) body.Stop();
            var off = SpriteVfx.Spawn("vc_spit_end", at, devScale, layer, order - 1, false, 16f, false);
            if (off != null) { off.Looping(999f); loopFx.Add(off); }
            yield return Wait(0.7f);
            int hi = Idx(side);
            if (!aborted && HandFree(side))                                             // the hand comes back to pick up the trap (if the hand is busy the trap sinks by itself)
            {
                handBusy[hi] = true;
                for (float t = 0f; t < 0.5f && !aborted; t += Time.deltaTime) { rig.SetHand(side, at + Vector2.up * 0.5f, 10f, "hand_claw"); yield return null; }
                for (float t = 0f; t < 0.6f && !aborted; t += Time.deltaTime)
                {
                    rig.SetHand(side, at + Vector2.up * 2.4f, 6f, "hand_fist");
                    if (off != null) off.transform.position = rig.HandWorld(side) + Vector2.down * 0.4f;
                    yield return null;
                }
                rig.RestHand(side, 5f);
                handBusy[hi] = false;
            }
            if (off != null) off.Stop();
            SpriteVfx.Spawn("m3_dash_dust", at, 0.8f, layer, order - 1, false, 16f, false);
        }

        /// <summary>Pick the nozzle sprite from the direction of jet 0 (45° steps): horizontal / diagonal NE-SW (flipped) / vertical / diagonal NW-SE.</summary>
        private void SpitHead(float ang, ref string key, ref SpriteVfx body, Vector2 at, float devScale)
        {
            float a = ((ang % 180f) + 180f) % 180f;
            int k = Mathf.RoundToInt(a / 45f) % 4;
            string name = k == 0 ? "vc_spit_h_loop" : k == 2 ? "vc_spit_v_loop" : "vc_spit_d_loop";
            bool flip = k == 1;
            string want = name + (flip ? "|f" : "");
            if (want == key && body != null) return;
            key = want;
            if (body != null) body.Stop();
            body = SpriteVfx.Spawn(name, at, devScale, layer, order - 1, flip, 12f, false);
            if (body == null) body = SpriteVfx.Spawn("vc_spit_loop", at, devScale, layer, order - 1, false, 12f, false);
            if (body != null) { body.Looping(999f); loopFx.Add(body); }
        }

        // ------------------------------------------------------------------ EMBER WISPS
        private IEnumerator EmberWisps(float pressure)
        {
            DrillBoard.Say("EMBER WISPS", FireCol);
            lungeGoal = 1f;                                                             // lunge down onto the arena before spitting fire from the eyes
            yield return Wait(0.6f);
            Vector2[] eyes = { EyeSocket(-1), EyeSocket(1) };
            var g0 = BossJuice.Light(eyes[0], FireCol, 1.6f, 1.8f, 0f);
            var g1 = BossJuice.Light(eyes[1], FireCol, 1.6f, 1.8f, 0f);
            foreach (var eye in eyes) SpriteVfx.Spawn("vc_ember_pop", eye, 0.8f, layer, order + 6, false, 14f, false);
            Sfx.Play("eye_beam_charge", eyes[0], 0.7f);
            yield return Wait(0.75f);
            var frames = SpriteVfx.Load("vc_ember_wisp");                                // PixelLab fireball (flame tail + embers)
            if (frames.Length == 0) frames = SpriteVfx.Load("vc_lava_ball");
            int n = pressure < 1f ? 6 : 8;
            for (int k = 0; k < n && !aborted; k++)
            {
                Vector2 e = eyes[k % 2];
                Vector2 aim = (Feet - e).normalized;
                Vector2 v = (Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f)) * aim) * Mathf.Lerp(1.8f, 2.2f, pressure * 0.5f);
                var b = HallBullet.Spawn(frames, e, v, 9f, 0.16f, Float("wisp_life", 3.2f), Color.white, Float("wisp_scale", 1f), layer, order + 3);
                if (b != null)
                {
                    b.homing = 70f; b.stopOnWalls = false; b.faceVelocity = true; b.fps = 14f;
                    b.Glow(FireCol, 0.7f, 0.75f);
                    b.popFx = "vc_ember_pop"; b.popScale = 0.9f; b.popTint = Color.white;
                }
                SpriteVfx.Spawn("vc_ember_pop", e, 0.55f, layer, order + 6, false, 20f, false);
                Sfx.Play("trap_fire", e, 0.35f);
                yield return Wait(0.17f);
            }
            if (g0 != null) Object.Destroy(g0.gameObject);
            if (g1 != null) Object.Destroy(g1.gameObject);
            lungeGoal = 0f;
            yield return Wait(0.8f);
        }
    }
}
