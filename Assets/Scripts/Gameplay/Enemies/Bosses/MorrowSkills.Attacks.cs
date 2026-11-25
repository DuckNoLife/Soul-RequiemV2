using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SKILLS of MorrowSkills (one coroutine per skill). Settings / skill choice: MorrowSkills.cs.
    /// </summary>
    public partial class MorrowSkills
    {
        // ------------------------------------------------------------------ skill choice
        protected override IEnumerator Think()
        {
            if (Broken) { yield return null; yield break; }
            risen.RemoveAll(e => e == null || e.Health.IsDead);
            float gap = ai.GapToPlayer();
            float d = (PlayerPos - Pos).magnitude;

            // Morrow is a caster - NO basic melee attack.
            // Pressed close -> BossSkillsBase's "pinned" counter calls Reposition (pinnedTimeMax 0.8 s): Bell Toll, then Vespers Step away.
            // ANTIPHON: Ren lost his Q -> answer at once, skipping the shared rest (Ready, not ReadyG)
            if (RenShackled)
            {
                if (Ready("lances")) { yield return ChoirLances(true); yield break; }
                if (Ready("litany") && pillars.Exists(p => !p.broken)) { Announce("Antiphon", Gold, 0f); yield return LanternLitany(phase == 1 ? 3 : 4, 1, 11f); yield break; }
            }
            if (phase == 3 && ReadyG("requiem") && pillars.Exists(p => !p.broken)) { yield return Requiem(); yield break; }
            if (phase >= 2 && ReadyG("raise") && risen.Count < maxRisen) { yield return RaiseTheNamed(); yield break; }
            if (ReadyG("canticle") && !RenShackled) { yield return ShacklingCanticle(); yield break; }
            if (ReadyG("litany") && pillars.Exists(p => !p.broken)) { yield return LanternLitany(phase == 1 ? 2 : 3, 1, 11f); yield break; }
            if (phase == 3 && ReadyG("vespers") && d > 2.2f) { yield return VespersStep(true); yield break; }     // appear behind Ren + toll the bell
            if (ReadyG("toll") && d < 3.4f) { yield return TollOfHymns(); yield break; }
            if ((phase >= 2 || BrokenPillars >= 2) && ReadyG("dirge") && d > 1.6f) { yield return DirgeNotes(); yield break; }
            if (ReadyG("lances")) { yield return ChoirLances(false); yield break; }
            yield return Stalk(0.6f);
        }

        /// pressed close -> TOLL THE BELL first (daze + light damage), Bell Toll chains into Vespers away; otherwise (cornered) just Vespers.
        protected override IEnumerator Reposition()
        {
            if (ai.GapToPlayer() <= meleeRange * 1.8f && Ready("sweep")) yield return TollStrike();
            else yield return VespersStep(false);
        }

        protected override IEnumerator OnPhase(int p)
        {
            hp.Invulnerable = true;
            ai.Desired = Vector2.zero;
            ai.TeleportTo(ClampArena(center, 0.1f));
            SpriteVfx.Spawn("m3_vespers", Pos, 0.7f, Layer, Order + 1, false, 14f);
            ai.PlayState("Toll");
            Hint(p == 2 ? "\"The hymn bleeds. Rise, my Named!\"" : "\"Requiem. Let the Vale fall silent.\"", 3f);
            Announce(p == 2 ? "The Hymn Bleeds" : "Requiem", Ice, 0.5f);
            ChargeFx(Ice, 0.5f, 1f);
            yield return ai.WaitFor(0.5f);
            var w = Wave(Pos, 4.2f, 1.2f, 0f, "m3_wave_hymn");           // knockback only, no HP loss
            w.knockback = 4f;
            w.onHit = _ => { };
            Fired();
            BossJuice.Burst("m3x_choir_pillar", Pos, 1.5f, Color.white, Layer, Order + 2, 16f);   // light pillar strikes down on Morrow
            Boom(CastPoint, Ice, 1.6f, 3);
            yield return ai.WaitFor(1.0f);
            hp.Invulnerable = false;
            if (p == 3)
            {
                foreach (var pl in pillars) if (!pl.broken && !pl.blood) StartCoroutine(Drain(pl));   // every remaining pillar turns red at once
                Use("requiem", 4f);
            }
            if (p == 2) Use("raise", 0f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ close range
        private IEnumerator TollStrike()
        {
            SfxHooks.BossMove(this, "TollStrike"); // sound
            Use("sweep", 4f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Toll");
            Announce("Bell Toll", Ice, 0.45f);
            yield return Telegraph(0.45f, Ice);
            if (hp.IsDead) yield break;
            Fired();
            Boom(Pos + Vector2.up * 0.2f, Ice, 0.9f, 2);
            var ts = Wave(Pos, 1.5f, 0.6f, ai.damage * 0.45f, "m3_wave_hymn");  // bell rings in his ears: 1-beat daze, pushed away
            ts.knockback = 3f;
            ts.onHit = _ => { var s = PS; if (s != null) s.Daze(0.5f, "The bell rings in your skull!"); };
            SpriteVfx.Spawn("m3_lk_slam", Pos, 0.7f, Layer, Order - 1, false, 14f);
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
            if (!hp.IsDead && ai.GapToPlayer() <= meleeRange * 2.2f && Ready("vespers")) yield return VespersStep(false);
        }

        // ------------------------------------------------------------------ Toll of Hymns
        private IEnumerator TollOfHymns()
        {
            SfxHooks.BossMove(this, "TollOfHymns"); // sound
            Use("toll", 9f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Toll");
            Announce("Toll of Hymns", Ice, 0.45f);
            yield return Telegraph(0.45f, Ice);
            if (hp.IsDead) yield break;
            Fired();
            int rings = phase == 1 ? 1 : 2;                     // 1 / 2 / 2 rings
            float g0 = Random.Range(0f, 90f);
            for (int i = 0; i < rings && !hp.IsDead; i++)
            {
                var w = Wave(Pos, 3.6f, 1.7f, 0f, "m3_wave_hymn");     // big rings are hard to dodge -> no HP loss, they SEAL the F skill instead
                w.knockback = 1.2f;
                w.onHit = _ => { var s = PS; if (s != null) s.Silence(3.5f, "The hymn steals your voice. Your mantras fall silent!"); };
                float g = g0 + i * 22f;
                w.gapAngles = new[] { g, g + 90f, g + 180f, g + 270f };
                w.gapWidth = 38f;
                Boom(Pos + Vector2.up * 0.3f, Ice, 1.1f, 2);
                yield return ai.WaitFor(0.55f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Choir Lances
        private IEnumerator ChoirLances(bool antiphon)
        {
            SfxHooks.BossMove(this, "ChoirLances"); // sound
            Use("lances", 10f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Announce(antiphon ? "Antiphon" : "Choir Lances", antiphon ? Gold : Ice, 0.4f);   // /
            ChargeFx(Ice, 0.4f);
            yield return ai.WaitFor(0.4f);
            Fired();
            var frames = SpriteVfx.Load("m3x_choir_pillar");                // 68 px lance piece -> 144 px falling light pillar (ring wave at the base)
            if (frames.Length == 0) frames = SpriteVfx.Load("m3_choir_lance");
            int n = 2 + phase + (antiphon ? 2 : 0);
            float gap = antiphon ? 0.24f : 0.32f;
            for (int i = 0; i < n && !hp.IsDead && ai.Player != null; i++)
            {
                // LEADING - the first pillar lands where Ren stands, the next ones where Ren WILL be (velocity x 0.5 s, at most 1.2 units)
                Vector2 at = i == 0 ? PlayerPos : ClampArena(PlayerPos + Vector2.ClampMagnitude(renVel * 0.5f, 1.2f), 0.92f);
                if (phase == 3 && i % 2 == 1)                                    // phase 3: 2 extra diagonal pillars on both sides of the running direction
                {
                    Vector2 side = renVel.sqrMagnitude > 0.04f ? Vector2.Perpendicular(renVel.normalized) : Vector2.right;
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var gx = GroundStrike.Spawn(ClampArena(at + side * 0.75f * sgn, 0.92f), 0.85f, 0.34f, ai.damage * 0.8f, frames, 2, 16f, Order + 1, Ice);
                        gx.impactSfx = "ice_shatter"; gx.bodyScale = 0.88f; gx.impactLight = 1.2f;
                    }
                }
                var g = GroundStrike.Spawn(at, 0.85f, 0.34f, ai.damage * 0.8f, frames, 2, 16f, Order + 1, Ice);
                g.impactSfx = "ice_shatter"; // sound
                g.bodyScale = 0.88f;                                         // pillar base ring ~0.39 units at scale 1 = matches the 0.34 radius
                g.impactLight = 1.4f; g.impactShake = 1;
                yield return ai.WaitFor(gap);
            }
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ Dirge Notes
        private IEnumerator DirgeNotes()
        {
            SfxHooks.BossMove(this, "DirgeNotes"); // sound
            Use("dirge", 6f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Announce("Dirge Notes", Ice, 0.45f);
            ChargeFx(Ice, 0.45f, 0.6f);
            yield return ai.WaitFor(0.45f);
            Fired();
            int waves = (phase == 3 ? 2 : 1) + (BrokenPillars >= 3 ? 1 : 0);               // many pillars lost -> extra volley
            for (int w = 0; w < waves && !hp.IsDead; w++)
            {
                Vector2 aim = (PlayerPos - Pos).normalized;
                float baseA = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + (w == 1 ? 7.5f : 0f);
                for (int k = -2; k <= 2; k++)
                {
                    float a = (baseA + k * 15f) * Mathf.Deg2Rad;
                    ai.ShootProjectile(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 2.2f, ai.damage * 0.6f, 3.4f);
                }
                BossJuice.Flash(CastPoint, Ice, 1.4f, 1.1f, 0.25f);                // flash on firing
                yield return ai.WaitFor(0.45f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Raise the Named (graves around the edge)
        private IEnumerator RaiseTheNamed()
        {
            SfxHooks.BossMove(this, "RaiseTheNamed"); // sound
            Use("raise", 26f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Toll");
            Hint("Morrow rings for the Named. The graves stir.", 2f);
            Announce("Raise the Named", new Color(0.6f, 1f, 0.6f), 0.6f);
            yield return Telegraph(0.6f, new Color(0.6f, 1f, 0.6f));
            Fired();
            var dirr = MapEncounterDirector.Instance;
            int n = Mathf.Min(2, maxRisen - risen.Count);
            var angles = new List<float>(graveAngles);
            for (int i = 0; i < n && dirr != null && angles.Count > 0; i++)
            {
                int pick = Random.Range(0, angles.Count);
                float a = angles[pick] * Mathf.Deg2Rad;
                angles.RemoveAt(pick);
                Vector2 p = center + new Vector2(Mathf.Cos(a) * graveRadii.x, -Mathf.Sin(a) * graveRadii.y);
                SpriteVfx.Spawn("m3_ground_burst", p, 0.7f, Layer, Order, false, 12f);
                BossJuice.Burst("m3x_lk_quake", p, 0.7f, new Color(0.7f, 1f, 0.7f), Layer, Order - 1, 14f);   // graves crack with a blue glow
                BossJuice.Flash(p + Vector2.up * 0.2f, new Color(0.55f, 1f, 0.55f), 1.4f, 1.3f, 0.45f);
                var e = dirr.SpawnSummon("Undead", p, ai);
                if (e != null) risen.Add(e);
            }
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Vespers Step (teleport)
        private IEnumerator VespersStep(bool offensive)
        {
            Use("vespers", 10f);
            ai.Desired = Vector2.zero;
            Vector2 from = Pos;
            SpriteVfx.Spawn("m3_vespers", from, 0.7f, Layer, Order + 1, false, 16f);
            BossJuice.Flash(from + Vector2.up * 0.4f, Ice, 1.3f, 1f, 0.35f);
            yield return Fade(0f, 0.25f);
            Vector2 to;
            if (offensive && ai.Player != null)
            {
                Vector2 away = (PlayerPos - center).sqrMagnitude > 0.01f ? (PlayerPos - center).normalized : Vector2.right;
                to = ClampArena(PlayerPos + away * 0.6f, 0.85f);            // appear behind Ren (on the outer side)
            }
            else
            {
                var extra = new List<Vector2> { center };
                foreach (var p in pillars) extra.Add(center + (p.pos - center) * 0.8f);
                to = FarPoint(0.6f, 12, extra);
            }
            ai.TeleportTo(to);
            SpriteVfx.Spawn("m3_vespers", to, 0.7f, Layer, Order + 1, false, 16f);
            BossJuice.Flash(to + Vector2.up * 0.4f, Ice, 1.3f, 1f, 0.35f);
            yield return Fade(1f, 0.2f);
            StartCoroutine(Echo(from));
            if (offensive && !hp.IsDead) yield return TollStrike();
        }

        /// Afterimage at the old spot: bursts into a small Toll ring after 1 s.
        private IEnumerator Echo(Vector2 at)
        {
            GroundStrike.Spawn(at, 1.0f, 0.5f, 0f, null, 0, 14f, Order, Ice).impactSfx = "ice_shatter"; // sound: afterimage bursts   // warning ring where the afterimage will burst
            SpriteVfx.Spawn("m3_vespers", at, 0.45f, Layer, Order, false, 9f);
            yield return new WaitForSeconds(1.0f);
            var w = Wave(at, 1.4f, 0.7f, 0f, "m3_wave_hymn");          // echo: slows, no HP loss
            w.knockback = 0.8f;
            w.onHit = _ => { var s = PS; if (s != null) s.Slow(0.65f, 1.5f, "The echo of the hymn weighs on you."); };
            w.gapAngles = new[] { Random.Range(0f, 360f) };
            w.gapWidth = 60f;
        }

        /// count intact pillars (picked at random) fire together; waves volleys; cd = cooldown.
        private IEnumerator LanternLitany(int count, int waves, float cd)
        {
            Use("litany", cd);
            var src = pillars.FindAll(p => !p.broken);
            if (src.Count == 0) yield break;
            for (int i = src.Count - 1; i > 0; i--) { int r = Random.Range(0, i + 1); var tmp = src[i]; src[i] = src[r]; src[r] = tmp; }
            if (count < src.Count) src.RemoveRange(count, src.Count - count);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Announce(waves > 1 ? "Requiem of Lanterns" : "Lantern Litany", Ice, ChargeTime);
            for (int w = 0; w < waves && !hp.IsDead && ai.Player != null; w++)
            {
                var aims = new List<Vector2>();
                var warns = new List<BeamWarn>();
                var glows = new List<UnityEngine.Rendering.Universal.Light2D>();
                foreach (var p in src)
                {
                    Vector2 tgt = PlayerPos + (w > 0 ? Random.insideUnitCircle * 0.35f : Vector2.zero);
                    Vector2 dir = (tgt - p.lamp).sqrMagnitude > 0.01f ? (tgt - p.lamp).normalized : Vector2.down;
                    aims.Add(dir);
                    // large glow (ice-tinted abs_charge2) + light at the pillar lantern + warning line exactly as wide as the beam, core filling up
                    var cf = SpriteVfx.Spawn("abs_charge2", p.lamp, 0.65f, Layer, Order + 2, false, 12f / ChargeTime, false);
                    if (cf != null) cf.Tint(Ice);
                    SpriteVfx.Spawn("m3_lantern_charge", p.lamp, 0.7f, Layer, Order + 2, false, 12f / ChargeTime);
                    glows.Add(BossJuice.Light(p.lamp, Ice, 1.2f, 1.2f, ChargeTime + 0.1f));
                    warns.Add(BeamWarn.Spawn(p.lamp, p.lamp + dir * RayLen, RayHalfWidth, Ice, ChargeTime, Layer, Order + 1));
                    if (p.light != null) p.light.intensity *= 1.8f;
                }
                yield return ai.WaitFor(ChargeTime);
                foreach (var v in warns) if (v != null) v.Release();
                if (hp.IsDead) yield break;
                for (int i = 0; i < src.Count; i++)
                {
                    var p = src[i];
                    if (p.light != null) p.light.intensity /= 1.8f;
                    if (p.broken) continue;                                   // slashed apart while charging: cancel at once
                    Vector2 end = p.lamp + aims[i] * RayLen;
                    // hymn beam (m3x_hymn_beam: white core + 2 twisting bands, 28 px tall -> 0.4 units wide = exact hit area) + lights along the beam
                    var beam = Ray("m3x_hymn_beam", p.lamp, end, 0.96f, 1.45f, Order + 2, 16f, 0.42f);
                    if (beam == null) Ray("m3_lantern_ray", p.lamp, end, 0.71f, 1.1f, Order + 2, 16f);
                    SpriteVfx.Spawn("m3_hymn_pop", p.lamp, 0.7f, Layer, Order + 3, false, 16f);
                    BossJuice.Impact(p.lamp, Ice, 0.7f, 0, Layer, Order + 3);
                    for (int L = 1; L <= 3; L++) BossJuice.Flash(Vector2.Lerp(p.lamp, end, L * 0.22f), Ice, 1.3f, 1f, 0.4f);
                    StartCoroutine(RayDamage(p.lamp, end, 0.32f));
                }
                if (w == 0) Fired();
                BossJuice.Shake(2);
                yield return ai.WaitFor(waves > 1 ? 0.7f : 0.35f);
            }
            ai.PlayState("Idle");
        }

        /// The beam deals damage once if Ren is on it while it glows (can be rolled through).
        private IEnumerator RayDamage(Vector2 a, Vector2 b, float seconds)
        {
            float t = 0f;
            yield return new WaitForSeconds(0.05f);
            while (t < seconds && ai != null && ai.Player != null)
            {
                if (SegDist(PlayerPos, a, b) <= RayHalfWidth) { ai.Hurt(ai.damage * rayDamageMul, PlayerPos + (a - b).normalized * 0.3f, 2f); yield break; }
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ Requiem (phase 3): every remaining lantern fires 3 volleys
        private IEnumerator Requiem()
        {
            SfxHooks.BossMove(this, "Requiem"); // sound
            Use("requiem", 20f);
            yield return VespersToCenter();
            Hint("\"Sing, lanterns. Sing the Requiem!\"", 2.5f);
            rayDamageMul = 0.55f;                                       // 3 crossing volleys: each beam hits lighter
            yield return LanternLitany(6, 3, 11f);
            rayDamageMul = 0.8f;
            Use("requiem", 22f);
        }

        private IEnumerator VespersToCenter()
        {
            ai.Desired = Vector2.zero;
            SpriteVfx.Spawn("m3_vespers", Pos, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(0f, 0.2f);
            ai.TeleportTo(center);
            SpriteVfx.Spawn("m3_vespers", center, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(1f, 0.2f);
        }

        // ------------------------------------------------------------------ altar pillars: blood drain / lantern break
        private IEnumerator PillarCycle()
        {
            while (hp != null && !hp.IsDead)
            {
                if (Time.time >= nextPillar && !Broken)
                {
                    int n = 1;                                          // only 1 pillar at a time (phase 3: every pillar already turned red at the phase change)
                    var pool = pillars.FindAll(p => !p.broken && !p.blood);
                    if (pool.Count > 0)
                    {
                        Pillar first = pool[Random.Range(0, pool.Count)];
                        StartCoroutine(Drain(first));
                        if (n == 2)
                        {
                            // second pillar on the opposite side -> only time to break one
                            Pillar best = null; float bd = -1f;
                            foreach (var p in pool) { if (p == first) continue; float dd = (p.pos - first.pos).sqrMagnitude; if (dd > bd) { bd = dd; best = p; } }
                            if (best != null) StartCoroutine(Drain(best));
                        }
                        Hint(n == 2 ? "Two pillars run red. Shatter a lantern!" : "A pillar runs red. Shatter its lantern!", 2.5f);
                    }
                    nextPillar = Time.time + (phase == 1 ? pillarEvery : phase == 2 ? pillarEvery * 0.85f : pillarEvery * 0.75f);
                }
                yield return null;
            }
        }

        private IEnumerator Drain(Pillar p)
        {
            SfxHooks.BossMove(this, "Drain"); // sound
            var mm = MapMechanics.Instance;
            p.blood = true; p.hits = 0;
            p.drainEnd = Time.time + drainTime;
            if (mm != null) { mm.SetVisible(p.name, false); mm.SetVisible(p.name + "_blood", true); }
            Color oldLight = p.light != null ? p.light.color : Color.white;
            if (p.light != null) p.light.color = Blood;
            Wave(p.pos, 0.9f, 0.6f, 0f, "m3_wave_blood");
            float nextMote = 0f;
            while (Time.time < p.drainEnd && !p.broken && !hp.IsDead)
            {
                if (Time.time >= nextMote)                                   // stream of blood drops flowing from the lantern to Morrow
                {
                    StartCoroutine(FlyMote("m3x_blood_bolt", p.lamp, transform, Vector2.up * 0.45f, 0.55f, 0.3f));
                    nextMote = Time.time + 0.12f;
                }
                yield return null;
            }
            if (mm != null) { mm.SetVisible(p.name + "_blood", false); mm.SetVisible(p.name, !p.broken); }
            p.blood = false;
            if (hp.IsDead) yield break;
            if (p.broken)
            {
                if (mm != null) mm.SetVisible(p.name + "_broken", true);
                if (p.light != null) p.light.enabled = false;
                BossJuice.Burst("m3x_blood_burst", p.lamp, 1.2f, Color.white, Layer, Order + 2, 16f);   // lantern breaks with a blood splash
                Boom(p.lamp, Blood, 1f, 2);
                BossJuice.Stop(0.05f);
                StartCoroutine(BreakRoutine(3.5f, "The lantern shatters. Morrow is stunned!"));
            }
            else
            {
                if (p.light != null) p.light.color = oldLight;
                hp.Heal(hp.Max * drainHeal);
                pactStacks++;
                SpriteVfx.Spawn("m3_heal_lantern", Pos, 0.7f, Layer, Order - 1, false, 14f);
                Hint(pactStacks == 1 ? "Morrow drinks the blood. His hymn quickens." : "Blood Pact x" + pactStacks + ". Faster still.", 2f);
            }
        }

        // ----------------------------------------------------------------- SHACKLING CANTICLE (Q roll lock)
        private IEnumerator ShacklingCanticle()
        {
            Use("canticle", phase == 1 ? 16f : phase == 2 ? 14f : 12f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Hint("Golden notes circle Morrow. Do not let the hymn bind you - cut them down!", 2.4f);
            Announce("Shackling Canticle", Gold, 0.8f);
            ChargeFx(Gold, 0.8f);
            int count = 2 + phase;
            var mine = new List<Note>();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("CanticleNote");
                go.transform.position = CastPoint;
                var fx = SpriteVfx.Spawn("m3x_canticle_note", CastPoint, 0.75f, Layer, Order + 3, false, 12f, false);
                if (fx != null) { fx.Looping(noteLife + 2f); fx.Follow(go.transform); }
                BossJuice.Light(CastPoint, Gold, 0.8f, 0.8f, 0f, go.transform);
                var n = new Note { t = go.transform, fx = fx };
                notes.Add(n); mine.Add(n);
            }
            // wind-up: notes form a ring circling Morrow (radius grows) -> the note count can be read
            for (float t = 0f; t < 0.8f && !hp.IsDead; t += Time.deltaTime)
            {
                for (int i = 0; i < mine.Count; i++)
                    if (mine[i].t != null) mine[i].t.position = CastPoint + Orbit(i, mine.Count, t, Mathf.Lerp(0.2f, 0.75f, t / 0.8f));
                yield return null;
            }
            Fired();
            if (hp.IsDead) { KillNotes(mine); yield break; }
            BossJuice.Flash(CastPoint, Gold, 1.8f, 1.3f, 0.35f);
            for (int i = 0; i < mine.Count; i++)
            {
                if (mine[i].t != null) StartCoroutine(NoteFly(mine[i], i * 0.28f, mine, i));
            }
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        /// One note: keeps circling Morrow until its turn, then chases Ren (turns at most noteTurn °/s, speeds up slightly); touching Ren -> Q-locking chord zone.
        private IEnumerator NoteFly(Note n, float delay, List<Note> group, int idx)
        {
            float t0 = Time.time;
            while (Time.time - t0 < delay && !n.dead && n.t != null && !hp.IsDead)
            {
                n.t.position = CastPoint + Orbit(idx, group.Count, 0.8f + Time.time - t0, 0.75f);
                yield return null;
            }
            if (n.t == null) yield break;
            Vector2 pos = n.t.position;
            Vector2 dir = ((Vector2)PlayerPos + Vector2.up * 0.3f - pos).normalized;
            float life = 0f, speed = noteSpeed * (phase == 3 ? 1.2f : 1f);
            while (life < noteLife && !n.dead && n.t != null && hp != null && !hp.IsDead && ai.Player != null)
            {
                life += Time.deltaTime;
                Vector2 want = ((Vector2)PlayerPos + Vector2.up * 0.3f - pos);
                if (want.sqrMagnitude > 0.0001f)
                {
                    float cur = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, tgt = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
                    dir = Dir(Mathf.MoveTowardsAngle(cur, tgt, noteTurn * Time.deltaTime));
                }
                pos += dir * speed * (1f + 0.25f * life / noteLife) * Time.deltaTime;
                n.t.position = pos;
                if (((Vector2)PlayerPos + Vector2.up * 0.3f - pos).magnitude < 0.26f) { n.dead = true; StartCoroutine(Chord(pos)); }
                yield return null;
            }
            if (n.t != null)
            {
                if (!n.dead) BossJuice.Burst("bfx_flash", pos, 0.4f, Gold, Layer, Order + 3, 22f);   // out of momentum: fades
                Destroy(n.t.gameObject);
            }
            notes.Remove(n);
        }

        /// 0.6 s chord zone where the note burst: Ren inside it (even having just rolled through, if invulnerability ends while still inside) -> roll locked.
        private IEnumerator Chord(Vector2 at)
        {
            BossJuice.Burst("bfx_flash", at, 0.75f, Gold, Layer, Order + 3, 20f);
            BossJuice.Flash(at, Gold, 1.3f, 1.4f, 0.6f);
            var ring = SpriteVfx.Spawn("m3x_shackle", at + Vector2.down * 0.3f, 0.6f, Layer, Order - 1, false, 14f, false);
            if (ring != null) ring.Looping(0.6f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                var s = PS;
                if (s != null && !s.RollSealed && ((Vector2)PlayerPos + Vector2.up * 0.3f - at).magnitude < 0.45f && ai.PlayerHealth != null && !ai.PlayerHealth.Invulnerable)
                {
                    s.SealRoll(shackleSeconds, "The hymn binds your feet. You cannot dash!");
                    BossJuice.Shake(1);
                    BossJuice.Stop(0.05f);
                    break;
                }
                yield return null;
            }
        }
    }
}
