using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE 3 RIME (ice, the whole map turns ice blue, light snowfall). Phase rules (frostbite, rolling snowballs, Whiteout): .Frost.cs.
    ///   FROST BREATH  — the Vessel lowers its head and inhales (cold mist gathers at the mouth), warning lines at the start + end direction -> BREATHES FROST
    ///                   (vc_frost_breath, PixelLab) SWEEPING an arc of ~100° across the arena; breath hit = 9 + frostbite (40 % slow for 1.6 s). Where the breath
    ///                   touches the floor ice shards form; the floor FREEZES for 10 s (vc_ice_sheet layer) -> Ren SLIDES (PlayerStatus.Ice: inertia, hard to stop / turn).
    ///   SNOWFALL      — 10–16 SNOWBALLS (vc_snowball, PixelLab) FALL DIAGONALLY from the sky toward the south, a growing shadow marks each landing spot
    ///                   (55 % aimed around Ren); hit = −15 HP + 20 % slow for 3 s; bursts into snow (vc_snow_impact). On the ice floor they roll instead (.Frost.cs).
    ///   GLACIER LANES — both arms spread wide as if to clap, the arena splits into 6 horizontal lanes; each wave warns 4 lanes (2 lanes safe), the left / right hand
    ///                   launches ICE LANCES (abs_ice_lance2) gliding along the lanes with ice thorns (abs_rime) rising behind; 3 waves (high pressure 4), safe lanes change each wave.
    ///   GLACIAL CROSS — eye move, full-arena diagonal X beams (.Rework.cs).
    ///   RIME FENCE    — both fists hit the two north corners; ICE THORNS rise around the arena edge and CLOSE IN 2 (3) steps over 12 s (each step warned 1 s ahead);
    ///                   standing outside the fence = 10 / 0.6 s + pushed inside. Runs IN THE BACKGROUND while other moves continue; thorns break when time is up.
    ///   WHITEOUT      — second half of the phase (.Frost.cs).
    ///   SLAM          — like Phase 1 (+ ice burst at the impact).
    /// At most 1 background layer of each kind (while the ice floor / fence exists, that move almost never repeats).
    /// Assets: _tools/s422/make_rime.py (PixelLab sources in _tools/s422/src).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static readonly Color IceCol = new Color(0.6f, 0.9f, 1f);
        private static readonly Color IceWarn = new Color(0.5f, 0.85f, 1f);
        private float iceUntil, iceStart, iceFade = 0.6f;
        private SpriteRenderer iceSheet;
        private bool fenceOn;
        private int fenceGen;
        private Rect fenceRect;
        private float fenceUntil, nextFenceHurt;
        private readonly List<SpriteRenderer> fenceZone = new List<SpriteRenderer>();
        private readonly List<(SpriteRenderer r, Vector2 v, float ph)> flakes = new List<(SpriteRenderer, Vector2, float)>();
        private static Sprite discSprite, snowSprite, iceSheetSprite;

        private Move[] IceMoves()
        {
            bool iced = Time.time < iceUntil;
            return new[]
            {
                new Move { name = "SLAM", w = 1.5f, slam = true, run = pr => Slam(Feet.x < chest.x ? -1 : 1, pr) },
                new Move { name = "FROST BREATH", w = iced ? 0.05f : 1.6f, run = FrostBreath },
                new Move { name = "SNOWFALL", w = 1.5f, run = Snowfall },
                new Move { name = "GLACIER LANES", w = 1.4f, run = GlacierLanes },
                new Move { name = "GLACIAL CROSS", w = 1.3f, run = GlacialCross },          // Phase 3 EYE move: full-arena diagonal beams (replaced Frost Gaze)
                new Move { name = "RIME FENCE", w = fenceOn ? 0.05f : 1.1f, run = RimeFence },
                new Move { name = "WHITEOUT", w = vhp < 0.5f && Time.time >= nextWhiteout ? 2.4f : 0f, run = Whiteout },   // second half of the phase
            };
        }

        private PlayerStatus RenStatus => Ctx.Player != null ? PlayerStatus.Of(Ctx.Player) : null;
        private void Chill(float mul, float seconds, string hint) { var st = RenStatus; if (st != null) st.Slow(mul, seconds, hint); AddFrost(1); }   // + frostbite
        private static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        // ------------------------------------------------------------------ every frame: ice floor, fence, snowfall
        private void TickIce(float dt)
        {
            float now = Time.time;
            if (iceSheet != null)
            {
                float a = now < iceUntil ? Mathf.Clamp01((now - iceStart) / iceFade) * Mathf.Clamp01((iceUntil - now) / 1.2f) : 0f;
                iceSheet.color = new Color(0.9f, 0.97f, 1f, 0.78f * a);
                if (now >= iceUntil + 0.1f) { DestroyObj(iceSheet); iceSheet = null; }
            }
            if (now < iceUntil && now - iceStart > iceFade * 0.5f) { var st = RenStatus; if (st != null) st.Ice(0.2f); }
            if (fenceOn && actState == ActState.Playing && now >= nextFenceHurt && !fenceRect.Contains(Feet))
            {
                Vector2 c = fenceRect.center, d = Feet - c;
                if (Hurt(10f, Feet + (d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.up), 1.3f))
                {
                    nextFenceHurt = now + 0.6f;
                    Chill(0.7f, 1f, "The frost bites. Stay inside the fence!");
                    SpriteVfx.Spawn("duo_ice_burst", Feet, 0.8f, layer, order + 1, false, 18f, false);
                }
            }
            TickFrost();
            TickSnow(dt);
        }

        private void KillIce()
        {
            iceUntil = 0f;
            iceSheet = null;                                                             // GameObject already in fieldObjs
            fenceOn = false; fenceGen++;
            fenceZone.Clear();
            KillFrost();
        }

        /// <summary>Freeze the floor for `seconds`, fading in over `fade` s (vc_ice_sheet layer tiled over the arena, below characters).</summary>
        private void FreezeFloor(float seconds, float fade)
        {
            if (iceSheet == null)
            {
                var g = new GameObject("ice sheet");
                fieldObjs.Add(g);
                g.transform.position = Floor.center;
                iceSheet = g.AddComponent<SpriteRenderer>();
                if (iceSheetSprite == null) iceSheetSprite = Resources.Load<Sprite>("VFX/vc_ice_sheet");
                if (iceSheetSprite != null) { iceSheet.sprite = iceSheetSprite; iceSheet.drawMode = SpriteDrawMode.Tiled; iceSheet.size = Floor.size; }
                else
                {
                    iceSheet.sprite = MarginArt.Pixel();
                    float k = 1f / Mathf.Max(0.0001f, iceSheet.sprite.bounds.size.x);
                    g.transform.localScale = new Vector3(Floor.width * k, Floor.height * k, 1f);
                }
                iceSheet.sortingLayerID = layer; iceSheet.sortingOrder = -17;
                iceSheet.color = new Color(1f, 1f, 1f, 0f);
                iceStart = Time.time; iceFade = Mathf.Max(0.2f, fade);
            }
            iceUntil = Mathf.Max(iceUntil, Time.time + seconds);
        }

        // ------------------------------------------------------------------ FROST BREATH
        private IEnumerator FrostBreath(float pressure)
        {
            DrillBoard.Say("FROST BREATH", IceCol);
            DrillBoard.Yell("Breathe in. You will not breathe out.", 1.8f);
            float dir = Random.value < 0.5f ? 1f : -1f;                                  // 1: sweep left -> right (angle -140 -> -40)
            float a0 = dir > 0 ? -140f : -40f, a1 = dir > 0 ? -40f : -140f;
            float L = Float("breath_len", 5.2f), S = L / 1.68f;                          // sprite is 168 px long from the mouth -> 1.68 u at scale 1
            // 1) lower the head, open the mouth and inhale: cold mist gathers at the mouth
            jawHold = 0.55f;
            slumpTarget = 0.25f;
            var glow = BossJuice.Light(rig.MouthWorld(), IceCol, 2.4f, 0.3f, 0f);
            Sfx.Play("gas_hiss", rig.MouthWorld(), 0.6f);
            for (float t = 0f; t < 0.7f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.7f;
                rig.HeadDip = 0.7f * u * u * (3f - 2f * u);
                if (glow != null) { glow.transform.position = rig.MouthWorld(); glow.intensity = Mathf.Lerp(0.3f, 1.2f, u); }
                yield return null;
            }
            if (aborted) { EndMagmaPose(glow); yield break; }
            // 2) warning: warning beam at the start direction (charging) + faint line at the end direction (shows where the sweep goes)
            float warnT = Mathf.Lerp(1.0f, 0.75f, pressure * 0.5f);
            Vector2 m = rig.MouthWorld();
            var warn = BeamWarn.Spawn(m, m + Dir(a0) * L, 0.5f, IceWarn, warnT, layer, -13);
            var endLine = NewLine("breath end guide", -13);
            for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime)
            {
                m = rig.MouthWorld();
                if (warn != null) warn.Aim(m, m + Dir(a0) * L);
                StretchLine(endLine, m + Dir(a1) * 0.6f, m + Dir(a1) * L, 0.04f);
                endLine.color = new Color(IceWarn.r, IceWarn.g, IceWarn.b, 0.25f + 0.2f * Mathf.Sin(t * 16f));
                if (Random.value < 0.35f) SpriteVfx.Spawn("abs_ice_spikes", m + Random.insideUnitCircle * 0.25f, 0.6f, layer, order + 3, false, 16f, false);
                if (glow != null) { glow.transform.position = m; glow.intensity = 1.2f + 0.8f * t / warnT; }
                yield return null;
            }
            if (warn != null) warn.Release();
            DestroyObj(endLine);
            if (aborted) { EndMagmaPose(glow); yield break; }
            // 3) BREATHE + SWEEP
            jawHold = 1f;
            m = rig.MouthWorld();
            var breath = SpriteVfx.Spawn("vc_frost_breath", m, S, layer, order + 2, false, 14f, false);
            if (breath != null) { breath.Looping(999f); loopFx.Add(breath); }
            Sfx.Play("gas_hiss", m, 1f);
            Sfx.Play("trap_ice", m, 0.8f);
            BossJuice.Shake(1);
            float sweepT = Mathf.Lerp(2.4f, 1.9f, pressure * 0.5f), nextHurt = 0f, nextFrost = 0f;
            FreezeFloor(10f + sweepT, sweepT);                                          // the floor freezes progressively during the sweep
            for (float t = 0f; t < sweepT && !aborted; t += Time.deltaTime)
            {
                float now = Time.time, u = t / sweepT, e = u * u * (3f - 2f * u);
                float ang = Mathf.Lerp(a0, a1, e);
                m = rig.MouthWorld();
                Vector2 d = Dir(ang);
                if (breath != null) { breath.transform.position = m; breath.transform.rotation = Quaternion.Euler(0f, 0f, ang); }
                if (glow != null) { glow.transform.position = m + d * L * 0.5f; glow.pointLightOuterRadius = L * 0.7f; glow.intensity = 1.6f; }
                if (now >= nextFrost)                                                    // ice shards form where the breath touches the floor
                {
                    nextFrost = now + 0.08f;
                    Vector2 fp = m + d * Random.Range(L * 0.4f, L * 0.95f);
                    if (Floor.Contains(fp)) SpriteVfx.Spawn("abs_ice_spikes", fp, 1.2f, layer, fp.y < Feet.y ? order + 1 : order - 1, Random.value < 0.5f, 14f, false);
                }
                Vector2 f = Feet + Vector2.up * 0.2f - m;
                float dist = f.magnitude, da = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg));
                float half = 13f + (dist < 1.5f ? 8f : 0f);                              // cone: wider near the mouth
                if (t > 0.15f && now >= nextHurt && dist > 0.45f && dist < L * 0.97f && da < half && Hurt(9f, m, 0.4f))
                {
                    nextHurt = now + 0.45f;
                    Chill(0.6f, 1.6f, "Frostbitten!");
                }
                yield return null;
            }
            if (breath != null) breath.Stop();
            if (aborted) { EndMagmaPose(glow); yield break; }
            DrillBoard.Say("THE FLOOR FREEZES", IceCol);
            Sfx.Play("ice_shatter", Floor.center, 0.6f);
            StartR(LiftHead(glow));
            yield return Wait(0.5f);
        }

        // ------------------------------------------------------------------ SNOWFALL
        private IEnumerator Snowfall(float pressure)
        {
            DrillBoard.Say("SNOWFALL", IceCol);
            DrillBoard.Yell("Let the sky bury you.", 1.6f);
            slumpTarget = 0.15f;                                                         // body tenses, chest flashes cold
            BossJuice.Flash(chest, IceCol, 3f, 2.5f, 0.45f);
            Sfx.Play("quake_rumble", chest, 0.6f);
            // snowballs only FALL from the sky (no bursts from the chest, no launch from the body)
            yield return Wait(0.5f);
            slumpTarget = 0f;
            int n = Mathf.RoundToInt(Mathf.Lerp(10f, 16f, pressure * 0.5f));
            float dur = Mathf.Lerp(3.4f, 2.8f, pressure * 0.5f);
            for (int i = 0; i < n && !aborted; i++)
            {
                Vector2 target = Random.value < 0.55f
                    ? ClampFloor(Feet + PlayerVel * 0.7f + Random.insideUnitCircle * 1.1f, 0.3f)
                    : new Vector2(Random.Range(Floor.xMin + 0.3f, Floor.xMax - 0.3f), Random.Range(Floor.yMin + 0.3f, Floor.yMax - 0.5f));
                StartR(Snowball(target, pressure));
                yield return Wait(dur / n);
            }
            yield return Wait(1.4f);
        }

        private IEnumerator Snowball(Vector2 target, float pressure)
        {
            if (snowSprite == null) snowSprite = Resources.Load<Sprite>("VFX/vc_snowball");
            var g = new GameObject("snowball");
            fieldObjs.Add(g);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = snowSprite != null ? snowSprite : Disc();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order + 4;
            // 1) rise from the body (duration is now 0, see below)
            Vector2 from = chest + new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(-0.4f, 0.6f));
            Vector2 apex = from + new Vector2(Random.Range(-1f, 1f), 3.6f);
            float spin = Random.Range(-360f, 360f);
            const float launch = 0f;                                                    // rise time 0 (no launch from the body)
            for (float t = 0f; t < launch && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.35f, e = 1f - (1f - u) * (1f - u);
                g.transform.position = Vector2.Lerp(from, apex, e);
                float s = Mathf.Lerp(0.6f, 0.85f, u);
                g.transform.localScale = new Vector3(s, s, 1f);
                g.transform.Rotate(0f, 0f, spin * Time.deltaTime);
                sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, Mathf.Clamp01((u - 0.7f) / 0.3f)));
                yield return null;
            }
            if (aborted) yield break;
            // 2) fall diagonally south onto the target: shadow + warning ring grow
            float side = target.x < Floor.center.x ? 1f : -1f;
            Vector2 sky = target + new Vector2(side * Random.Range(1.2f, 2.2f), 6.2f);
            var sh = new GameObject("snowball shadow");
            fieldObjs.Add(sh);
            sh.transform.position = target;
            var ssr = sh.AddComponent<SpriteRenderer>();
            ssr.sprite = Disc(); FxLighting.MakeUnlit(ssr);
            ssr.sortingLayerID = layer; ssr.sortingOrder = -14;
            var ring = new GameObject("snowball ring");
            fieldObjs.Add(ring);
            ring.transform.position = target;
            var rr = ring.AddComponent<SpriteRenderer>();
            rr.sprite = Ring(); FxLighting.MakeUnlit(rr);
            rr.sortingLayerID = layer; rr.sortingOrder = -13;
            float rk = 0.62f / (Ring().bounds.size.x * 0.5f);
            ring.transform.localScale = new Vector3(rk, rk * 0.6f, 1f);
            float fall = Mathf.Lerp(1.05f, 0.85f, pressure * 0.5f);
            for (float t = 0f; t < fall && !aborted; t += Time.deltaTime)
            {
                float u = t / fall, e = u * u;
                g.transform.position = Vector2.Lerp(sky, target + Vector2.up * 0.15f, e);
                float s = Mathf.Lerp(0.9f, 1.3f, u);
                g.transform.localScale = new Vector3(s, s, 1f);
                g.transform.Rotate(0f, 0f, spin * Time.deltaTime);
                sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(u * 4f));
                float ss = Mathf.Lerp(0.25f, 0.85f, u);
                sh.transform.localScale = new Vector3(ss, ss * 0.5f, 1f);
                ssr.color = new Color(0.02f, 0.05f, 0.12f, Mathf.Lerp(0.1f, 0.5f, u));
                rr.color = new Color(IceWarn.r, IceWarn.g, IceWarn.b, (0.35f + 0.35f * Mathf.Sin(t * 22f)) * Mathf.Clamp01(u * 3f));
                yield return null;
            }
            DestroyObj(g); DestroyObj(sh); DestroyObj(ring);
            if (aborted) yield break;
            if (Time.time < iceUntil && rollers.Count < 4)                               // landing on the ice floor -> does not break but ROLLS
            {
                SpriteVfx.Spawn("vc_snow_impact", target, 0.8f, layer, order + 1, Random.value < 0.5f, 16f, false);
                Sfx.Play("hit_block", target, 0.5f);
                if ((Feet - target).magnitude < 0.62f && Hurt(15f, target, 0.6f)) Chill(0.8f, 3f, "Snow clings to you!");
                StartR(RollingSnowball(target, new Vector2(-side, -1.2f)));
                yield break;
            }
            // 3) burst into snow
            SpriteVfx.Spawn("vc_snow_impact", target, 1.35f, layer, order + 1, Random.value < 0.5f, 14f, false);
            Sfx.Play("ice_shatter", target, 0.6f);
            if ((Feet - target).magnitude < 0.62f && Hurt(15f, target, 0.6f)) Chill(0.8f, 3f, "Snow clings to you!");
        }

        // ------------------------------------------------------------------ GLACIER LANES
        private IEnumerator GlacierLanes(float pressure)
        {
            if (!HandFree(-1) || !HandFree(1)) yield break;
            DrillBoard.Say("GLACIER LANES", IceCol);
            DrillBoard.Yell("Choose your row. Choose badly.", 1.6f);
            const int Lanes = 6;
            float lh = Floor.height / Lanes;
            float xl = Floor.xMin - 0.35f, xr = Floor.xMax + 0.35f;
            int waves = pressure >= 1.2f ? 4 : 3, shift = Random.Range(0, 3);
            // both arms spread wide as if to clap
            for (float t = 0f; t < 0.6f && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(-1, new Vector2(xl - 0.4f, Floor.center.y + 1f), 7f, "hand_open");
                rig.SetHand(1, new Vector2(xr + 0.4f, Floor.center.y + 1f), 7f, "hand_open");
                yield return null;
            }
            for (int w = 0; w < waves && !aborted; w++)
            {
                var lanes = new List<(float y, int from)>();
                float yl = 0f, yr = 0f; int nl = 0, nr = 0;
                for (int i = 0; i < Lanes; i++)
                {
                    int k = (i + w + shift) % 3;                                          // k = 2: safe lanes
                    if (k == 2) continue;
                    float y = Floor.yMin + (i + 0.5f) * lh;
                    int from = k == 0 ? -1 : 1;
                    lanes.Add((y, from));
                    if (from < 0) { yl += y; nl++; } else { yr += y; nr++; }
                }
                yl = nl > 0 ? yl / nl : Floor.center.y; yr = nr > 0 ? yr / nr : Floor.center.y;
                float warnT = Mathf.Lerp(0.95f, 0.7f, pressure * 0.5f);
                var warns = new List<BeamWarn>();
                foreach (var (y, from) in lanes)
                {
                    Vector2 a = new Vector2(from < 0 ? Floor.xMin : Floor.xMax, y), b = new Vector2(from < 0 ? Floor.xMax : Floor.xMin, y);
                    var bw = BeamWarn.Spawn(a, b, lh * 0.42f, IceWarn, warnT, layer, -13);
                    if (bw != null) warns.Add(bw);
                }
                Sfx.Play("drill_whoosh", Floor.center, 0.5f);
                for (float t = 0f; t < warnT && !aborted; t += Time.deltaTime)
                {
                    rig.SetHand(-1, new Vector2(xl - 0.3f, yl + 0.8f), 9f, "hand_point");
                    rig.SetHand(1, new Vector2(xr + 0.3f, yr + 0.8f), 9f, "hand_point");
                    yield return null;
                }
                foreach (var bw in warns) if (bw != null) bw.Release();
                if (aborted) yield break;
                Sfx.Play("trap_ice", Floor.center, 0.9f);
                BossJuice.Shake(1);
                BossJuice.Flash(new Vector2(xl, yl), IceCol, 2f, 1.5f, 0.2f);
                BossJuice.Flash(new Vector2(xr, yr), IceCol, 2f, 1.5f, 0.2f);
                foreach (var (y, from) in lanes) StartR(IceLane(y, from, lh, pressure));
                yield return Wait(Mathf.Lerp(1.3f, 1.05f, pressure * 0.5f));
            }
            yield return Wait(0.7f);
            rig.RestHand(-1, 5f); rig.RestHand(1, 5f);
        }

        /// <summary>One lane: an ice lance glides from edge `from` to the other edge, ice thorns rise behind it (live ~0.9 s).</summary>
        private IEnumerator IceLane(float y, int from, float lh, float pressure)
        {
            float x0 = from < 0 ? Floor.xMin - 0.3f : Floor.xMax + 0.3f, x1 = from < 0 ? Floor.xMax + 0.3f : Floor.xMin - 0.3f;
            float dir = Mathf.Sign(x1 - x0), len = Mathf.Abs(x1 - x0), speed = Mathf.Lerp(10f, 13f, pressure * 0.5f);
            const float Spacing = 0.55f, Life = 0.9f;
            var lance = SpriteVfx.Spawn("abs_ice_lance2", new Vector2(x0, y), 1.3f, layer, order + 2, dir < 0, 16f, false);
            if (lance != null) { lance.Looping(99f); loopFx.Add(lance); }
            var spikes = new List<(float x, float t)>();
            float travelled = 0f, nextSpike = 0.3f;
            bool hit = false;
            while (!aborted)
            {
                float now = Time.time;
                bool moving = travelled < len;
                if (moving)
                {
                    travelled = Mathf.Min(len, travelled + speed * Time.deltaTime);
                    if (lance != null) lance.transform.position = new Vector2(x0 + dir * travelled, y);
                    while (nextSpike <= travelled)
                    {
                        float sx = x0 + dir * nextSpike;
                        if (sx > Floor.xMin - 0.1f && sx < Floor.xMax + 0.1f)
                        {
                            Vector2 sp = new Vector2(sx, y - lh * 0.3f);
                            SpriteVfx.Spawn("abs_rime", sp, 0.85f, layer, sp.y < Feet.y ? order + 1 : order - 1, Random.value < 0.5f, 20f, false);
                            spikes.Add((sx, now));
                        }
                        nextSpike += Spacing;
                    }
                    if (travelled >= len && lance != null) { lance.Stop(); lance = null; Sfx.Play("ice_shatter", new Vector2(x1, y), 0.4f); }
                }
                else if (spikes.Count == 0 || now - spikes[spikes.Count - 1].t > Life) break;
                // hit: the lance tip or a live thorn in the same lane
                if (!hit && Mathf.Abs(Feet.y - y) < lh * 0.46f)
                {
                    bool touch = moving && Mathf.Abs(Feet.x - (x0 + dir * travelled)) < 0.5f;
                    if (!touch) foreach (var (sx, st) in spikes) if (now - st < Life && Mathf.Abs(Feet.x - sx) < Spacing * 0.6f) { touch = true; break; }
                    if (touch && Hurt(12f, new Vector2(Feet.x - dir, y), 0.9f)) { hit = true; Chill(0.7f, 1.5f, "Pinned by the ice!"); }
                }
                yield return null;
            }
            if (lance != null) lance.Stop();
        }

        // ------------------------------------------------------------------ RIME FENCE
        private IEnumerator RimeFence(float pressure)
        {
            if (!HandFree(-1) || !HandFree(1) || fenceOn) yield break;
            DrillBoard.Say("RIME FENCE", IceCol);
            DrillBoard.Yell("Smaller. Smaller still.", 1.6f);
            Vector2 pl = new Vector2(Floor.xMin + 0.9f, Floor.yMax - 0.35f), pr = new Vector2(Floor.xMax - 0.9f, Floor.yMax - 0.35f);
            for (float t = 0f; t < 0.55f && !aborted; t += Time.deltaTime)
            {
                rig.SetHand(-1, pl + Vector2.up * 1.7f, 8f, "hand_fist");
                rig.SetHand(1, pr + Vector2.up * 1.7f, 8f, "hand_fist");
                yield return null;
            }
            rig.SetHand(-1, pl, 45f, "hand_fist");
            rig.SetHand(1, pr, 45f, "hand_fist");
            yield return Wait(0.1f);
            if (aborted) yield break;
            SlamFx(pl, 1); SlamFx(pr, 1);
            StartR(FenceRun(pressure));
            yield return Wait(0.35f);
            rig.RestHand(-1, 5f); rig.RestHand(1, 5f);
            yield return Wait(1.2f);
        }

        private IEnumerator FenceRun(float pressure)
        {
            float total = Float("fence_seconds", 12f);
            float[] insets = pressure >= 1.2f ? new[] { 0.85f, 1.5f, 2.0f } : new[] { 0.85f, 1.6f };
            fenceOn = true; fenceGen++;
            fenceRect = Floor;
            fenceUntil = Time.time + total;
            float stepGap = (total - 1.5f) / insets.Length;
            for (int s = 0; s < insets.Length && fenceOn; s++)
            {
                float ix = insets[s], iy = insets[s] * 0.55f;
                var nr = new Rect(Floor.xMin + ix, Floor.yMin + iy, Floor.width - 2f * ix, Floor.height - 2f * iy);
                // 1 s warning: the area about to be lost is covered in blinking faint ice
                var warnZone = Strips(fenceRect, nr, -16);
                for (float t = 0f; t < 1f && fenceOn; t += Time.deltaTime)
                {
                    foreach (var z in warnZone) if (z != null) z.color = new Color(IceWarn.r, IceWarn.g, IceWarn.b, 0.18f + 0.18f * Mathf.Sin(t * 18f));
                    yield return null;
                }
                foreach (var z in warnZone) DestroyObj(z);
                if (!fenceOn) yield break;
                // thorns rise along the new edge (spreading both ways from the middle of the north edge)
                fenceRect = nr;
                int gen = ++fenceGen;
                foreach (var z in fenceZone) DestroyObj(z);
                fenceZone.Clear();
                fenceZone.AddRange(Strips(Floor, nr, -16));
                foreach (var z in fenceZone) if (z != null) z.color = new Color(0.75f, 0.92f, 1f, 0.5f);
                Sfx.Play("trap_ice", nr.center, 1f);
                Sfx.Play("ice_shatter", nr.center, 0.5f);
                BossJuice.Shake(1);
                foreach (var (p, dly) in Perimeter(nr, 0.5f)) StartR(IcePillar(p, dly, gen));
                float until = Mathf.Min(fenceUntil, Time.time + stepGap - 1f);
                while (Time.time < until && fenceOn) yield return null;
            }
            while (Time.time < fenceUntil && fenceOn) yield return null;
            if (!fenceOn) yield break;
            fenceOn = false; fenceGen++;
            foreach (var z in fenceZone) DestroyObj(z);
            fenceZone.Clear();
            DrillBoard.Say("THE FENCE SHATTERS", IceCol);
            Sfx.Play("glass_break", fenceRect.center, 0.9f);
            BossJuice.Flash(fenceRect.center, IceCol, 4f, 2f, 0.3f);
        }

        /// <summary>Thorn positions along the rectangle perimeter, with delays (thorns run from the middle of the north edge to both sides, ~12 u/s).</summary>
        private static List<(Vector2 p, float delay)> Perimeter(Rect r, float spacing)
        {
            var pts = new List<(Vector2, float)>();
            float per = 2f * (r.width + r.height);
            int n = Mathf.Max(8, Mathf.RoundToInt(per / spacing));
            for (int i = 0; i < n; i++)
            {
                float s = per * i / n;                                                   // start at the middle of the north edge, clockwise
                float d = Mathf.Min(s, per - s);
                float q = (s + r.width * 0.5f) % per;
                Vector2 p;
                if (q < r.width) p = new Vector2(r.xMin + q, r.yMax);
                else if (q < r.width + r.height) p = new Vector2(r.xMax, r.yMax - (q - r.width));
                else if (q < 2f * r.width + r.height) p = new Vector2(r.xMax - (q - r.width - r.height), r.yMin);
                else p = new Vector2(r.xMin, r.yMin + (q - 2f * r.width - r.height));
                pts.Add((p, d / 12f));
            }
            return pts;
        }

        /// <summary>One fence thorn: rises (frames 0–3), stands sparkling (4–11) until the step changes / time runs out, then sinks (12–15).</summary>
        private IEnumerator IcePillar(Vector2 p, float delay, int gen)
        {
            if (delay > 0f) yield return Wait(delay);
            if (aborted || !fenceOn || gen != fenceGen) yield break;
            var fr = SpriteVfx.Load("abs_rime");
            if (fr.Length < 16) yield break;
            var g = new GameObject("rime pillar");
            fieldObjs.Add(g);
            g.transform.position = p;
            float s = 0.9f;
            g.transform.localScale = new Vector3(Random.value < 0.5f ? -s : s, s, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer;
            sr.sortingOrder = p.y < fenceRect.center.y ? order + 1 : order - 1;
            Sfx.Play("ice_pillar", p, 0.5f);                                       // sound
            for (int f = 0; f < 4 && !aborted; f++) { sr.sprite = fr[f]; yield return Wait(0.05f); }
            float t0 = Random.Range(0f, 3f);
            while (!aborted && fenceOn && gen == fenceGen)
            {
                sr.sprite = fr[4 + (int)((Time.time + t0) * 8f) % 8];
                yield return null;
            }
            if (aborted) yield break;
            if (Random.value < 0.35f) SpriteVfx.Spawn("abs_ice_shatter", p + Vector2.up * 0.25f, 0.5f, layer, sr.sortingOrder + 1, false, 18f, false);
            Sfx.Play("ice_shatter", p, 0.35f);                                     // sound
            for (int f = 12; f < 16; f++) { if (sr == null) yield break; sr.sprite = fr[f]; yield return Wait(0.06f); }
            DestroyObj(g);
        }

        /// <summary>Up to 4 ice strips covering the area between `outer` and `inner` (outside the fence).</summary>
        private List<SpriteRenderer> Strips(Rect outer, Rect inner, int ord)
        {
            var list = new List<SpriteRenderer>();
            void Add(Rect r) { if (r.width > 0.01f && r.height > 0.01f) list.Add(Panel(r, ord)); }
            Add(new Rect(outer.xMin, inner.yMax, outer.width, outer.yMax - inner.yMax));
            Add(new Rect(outer.xMin, outer.yMin, outer.width, inner.yMin - outer.yMin));
            Add(new Rect(outer.xMin, inner.yMin, inner.xMin - outer.xMin, inner.height));
            Add(new Rect(inner.xMax, inner.yMin, outer.xMax - inner.xMax, inner.height));
            return list;
        }

        /// <summary>Ice patch (vc_ice_sheet tiled) covering a rectangle — transparent when created, tinted by code.</summary>
        private SpriteRenderer Panel(Rect r, int ord)
        {
            var g = new GameObject("rime zone");
            fieldObjs.Add(g);
            g.transform.position = r.center;
            var sr = g.AddComponent<SpriteRenderer>();
            if (iceSheetSprite == null) iceSheetSprite = Resources.Load<Sprite>("VFX/vc_ice_sheet");
            if (iceSheetSprite != null) { sr.sprite = iceSheetSprite; sr.drawMode = SpriteDrawMode.Tiled; sr.size = r.size; }
            else
            {
                sr.sprite = MarginArt.Pixel();
                float k = 1f / Mathf.Max(0.0001f, sr.sprite.bounds.size.x);
                g.transform.localScale = new Vector3(r.width * k, r.height * k, 1f);
            }
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = ord;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        private SpriteRenderer NewLine(string name, int ord)
        {
            var g = new GameObject(name);
            fieldObjs.Add(g);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = MarginArt.Pixel(); FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = ord;
            return sr;
        }

        /// <summary>White disc 1 u across (drop shadows, fallback ball).</summary>
        private static Sprite Disc()
        {
            if (discSprite != null) return discSprite;
            const int D = 32;
            var tex = new Texture2D(D, D, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "vessel_disc" };
            var px = new Color32[D * D];
            for (int y = 0; y < D; y++)
                for (int x = 0; x < D; x++)
                {
                    float d = new Vector2(x + 0.5f - D * 0.5f, y + 0.5f - D * 0.5f).magnitude / (D * 0.5f);
                    px[y * D + x] = new Color32(255, 255, 255, (byte)(d < 0.8f ? 255 : d < 1f ? 140 : 0));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            discSprite = Sprite.Create(tex, new Rect(0, 0, D, D), new Vector2(0.5f, 0.5f), D);
            return discSprite;
        }

        // ------------------------------------------------------------------ light snowfall while the map has the RIME tint
        private void TickSnow(float dt)
        {
            bool want = toneP == 2;
            if (flakes.Count > 0 && (!want || flakes[0].r == null))
            {
                foreach (var f in flakes) if (f.r != null) Object.Destroy(f.r.gameObject);
                flakes.Clear();
            }
            if (!want) return;
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            if (flakes.Count == 0)
            {
                var px = MarginArt.Pixel();
                float k = 1f / Mathf.Max(0.0001f, px.bounds.size.x);
                for (int i = 0; i < 70; i++)
                {
                    var g = new GameObject("snowflake");
                    g.transform.SetParent(Ctx.Host.transform, false);
                    var sr = g.AddComponent<SpriteRenderer>();
                    sr.sprite = px; FxLighting.MakeUnlit(sr);
                    sr.sortingLayerID = layer; sr.sortingOrder = order + 6;
                    float sz = (Random.value < 0.25f ? 0.03f : 0.02f) * k;
                    g.transform.localScale = new Vector3(sz, sz, 1f);
                    sr.color = new Color(0.92f, 0.97f, 1f, Random.Range(0.35f, 0.85f));
                    g.transform.position = new Vector3(c.x + Random.Range(-w, w), c.y + Random.Range(-h, h), 0f);
                    flakes.Add((sr, new Vector2(Random.Range(-0.3f, 0.05f), Random.Range(-0.45f, -0.95f)), Random.Range(0f, 6f)));
                }
            }
            float now = Time.time;
            foreach (var (r, v, ph) in flakes)
            {
                var t = r.transform;
                Vector2 p = (Vector2)t.position + (v + new Vector2(Mathf.Sin(now * 1.3f + ph) * 0.12f + windX, windX != 0f ? -0.4f : 0f)) * dt;   // Whiteout wind
                if (p.y < c.y - h - 0.2f || p.x < c.x - w - 0.3f || p.x > c.x + w + 0.3f)
                    p = new Vector2(c.x + Random.Range(-w, w + 1f), c.y + h + Random.Range(0.05f, 0.5f));
                t.position = new Vector3(p.x, p.y, 0f);
            }
        }
    }
}
