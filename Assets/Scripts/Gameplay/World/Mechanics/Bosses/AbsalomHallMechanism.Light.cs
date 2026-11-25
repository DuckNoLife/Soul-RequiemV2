using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// LIGHT school (gold) — "his own" skills, used in both phases' skill pool (LightSkill / PressureSkill).
    ///   Sunlance  — 3 light lines aimed at Ren (0.8 s), then each line fires 3 light lances in a row. Dodge: step out of the lines (gap between two lines).
    ///   Halo Ring — 2 rings of light orbs expand from his body, each with a 3-orb GAP (ring 2 gap on the opposite side). Dodge: slip through the gap or roll.
    ///   Sunfall   — warning ring under Ren then a light pillar falls (abs_sunfall). Dodge: run out of the ring.
    ///   ASCENSION DIVE (signature) — he shoots up off screen (lift-off pillar + dust), his SHADOW + a gold target ring chase Ren on the floor
    ///   for 1.7 s (slower than Ren) then LOCK for 0.55 s (white blinking ring) -> dives in 0.18 s -> light pillar + shockwave + floor cracks
    ///   (abs_dive_impact) radius 1 u, heavy shake, gold flash over the hall, 10 light orbs burst out (2 gaps). Dodge: leave the ring when it locks.
    /// Visuals: Sunlance = 120 px HOLY LANCE with halo rim + white core, self-lit, breaks into a light ring; muzzle flash + shake.
    ///   Halo Ring = 78 px tailed light orbs (abs_light_orb), each wave pops a light ring at his body + light flash.
    ///   Sunfall = PILLAR FROM THE SKY (abs_sunfall 148x198: falling pillar, base flash, shockwave, floor cracks) radius 0.55, shake + light flash.
    /// Uses: Shot, RingSprite, Hazard, FlashLight, Shake (.Skills.cs).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private IEnumerator Sunlance(int power)
        {
            yield return Cast(School.Light, "SUNLANCE", 0.35f, "eye_beam_charge");
            if (aborted) yield break;
            Vector2 from = Hand;
            Vector2 aim = (PlayerFeet + Vector2.up * 0.2f - from).normalized;
            float spread = power >= 2 ? 24f : 30f;
            float[] offs = power >= 2 ? new[] { 0f, spread, -spread } : new[] { spread * 0.5f, -spread * 0.5f };
            var tels = new List<DashTelegraph>();
            foreach (var o in offs) tels.Add(DashTelegraph.Spawn(from, Rot(aim, o), 10f, 0.3f, new Color(1f, 0.88f, 0.5f), 0.8f, Layer, Order - 2));
            yield return Wait(0.8f);
            foreach (var t in tels) if (t != null) t.Release();
            if (aborted) yield break;
            Sfx.Play("mantra_eyeray", abs.Pos, 0.8f);
            for (int k = 0; k < 3 && !aborted; k++)
            {
                // each volley — light ring flash at the muzzle + gold light + light shake; holy lances glow and break into a gold light ring
                Fx("abs_ice_shatter", from, 1.1f, Order + 4, 22f, new Color(1f, 0.9f, 0.55f));
                FlashLight(from, Sun, 2.4f, 1.6f, 0.25f);
                Shake(1);
                foreach (var o in offs)
                    BigShot("abs_sunlance2", from, Rot(aim, o) * 8.5f, Sun, 1f, 9f, 0.13f, 1.6f, "abs_ice_shatter", 0.8f, new Color(1f, 0.9f, 0.55f));
                yield return Wait(0.14f);
            }
            abs.Play("idle", 5f);
        }

        private IEnumerator HaloRing(int power)
        {
            yield return Cast(School.Light, "HALO RING", 0.55f, "bell_toll");
            if (aborted) yield break;
            int rings = power >= 2 ? 2 : 1;
            float gapAt = Vector2.SignedAngle(Vector2.right, PlayerFeet - abs.Body) + Random.Range(-70f, 70f);
            for (int r = 0; r < rings && !aborted; r++)
            {
                // 22 big light orbs (3-orb gap); light ring pops at his body + light flash + shake — every 3rd orb carries a light
                const int N = 22;
                float a0 = gapAt + r * 180f;
                Sfx.Play("shockwave", abs.Pos, 0.55f);
                Fx("abs_ice_shatter", abs.Body, 2f, Order + 3, 20f, new Color(1f, 0.92f, 0.6f));
                Fx("abs_charge2", abs.Body, 1f, Order + 3, 40f, Sun);
                FlashLight(abs.Body, Sun, 3.2f, 1.8f, 0.4f);
                Shake(1);
                for (int i = 0; i < N; i++)
                {
                    if (i < 3) continue;                                                // 3-orb gap at angle a0 (i = 0..2)
                    float ang = a0 + (i - 1) * 360f / N;
                    Vector2 d = Rot(Vector2.right, ang);
                    var b = Shot("abs_light_orb", abs.Body + d * 0.35f, d * 1.75f, Color.white, 1f, 9f, 0.13f, 6.5f);
                    b.stopOnWalls = true;
                    b.popFx = "abs_ice_shatter"; b.popScale = 0.6f; b.popTint = new Color(1f, 0.9f, 0.55f);
                    if (i % 3 == 0) b.Glow(Sun, 0.8f, 0.8f);
                }
                yield return Wait(0.95f);
            }
            abs.Play("idle", 5f);
        }

        /// <summary>Falling light pillars: `count` pillars, `gap` s apart, around Ren (the first one exactly on Ren).</summary>
        private IEnumerator Sunfall(int count, float gap, float radius, float damage)
        {
            // sky pillar abs_sunfall (148x198; floor ring ~1.3 u at scale 1) — frame 1 = pillar hits the floor = deals damage
            var frames = SpriteVfx.Load("abs_sunfall");
            if (frames.Length == 0) frames = SpriteVfx.Load("abs_sun_pillar");
            radius = Mathf.Max(radius, 0.55f);
            for (int i = 0; i < count && !aborted; i++)
            {
                Vector2 at = InArena(PlayerFeet + (i == 0 ? Vector2.zero : Random.insideUnitCircle * 0.9f), 0.3f);
                var g = GroundStrike.Spawn(at, 1.0f, radius, damage, frames, 1, 16f, Order + 1, Gold);
                Sfx.Play("holy_beam", at, 0.65f);                                  // sound
                if (g != null) { g.impactSfx = "bolt_strike"; g.bodyScale = radius / 0.62f; }
                Ctx.Run(SunfallBurst(at, 1.0f + 1f / 16f, radius));
                if (gap > 0f) yield return Wait(gap);
            }
        }

        /// <summary>When a pillar hits the floor — big gold light flash + shake + a few small light orbs splash out (no damage, light only).</summary>
        private IEnumerator SunfallBurst(Vector2 at, float delay, float radius)
        {
            yield return Wait(delay);
            if (aborted || state != State.Fight) yield break;
            FlashLight(at, Sun, 2.6f + radius * 2f, 2f, 0.55f);
            Shake(2);
            for (int k = 0; k < 6; k++)
            {
                Vector2 d = Rot(Vector2.right, k * 60f + Random.Range(-15f, 15f));
                var m = Shot("abs_light_orb", at + d * 0.2f, d * Random.Range(1.4f, 2.0f), Color.white, 0.75f, 0f, 0f, 0.35f);
                m.harmless = true; m.popFx = "none";
            }
        }

        /// <summary>CATACLYSM DIVE (upgraded Ascension Dive) — starfall = while up in the sky, unleash STARFALL (diagonal sweeps of light pillars) before diving.</summary>
        private IEnumerator AscensionDive(int power, bool starfall = false)
        {
            Color c = Sun;
            DrillBoard.Say(starfall ? "STARFALL" : "CATACLYSM DIVE", c);
            NoteSkill(School.Light, starfall ? "STARFALL" : "CATACLYSM DIVE");
            abs.Face(Vector2.down);
            abs.Play("ascend", 12f, false);
            abs.Flash(c, 0.8f);
            SealCast(c, 1.2f);
            Sfx.Play("bell_toll", abs.Pos, 0.8f);
            yield return Wait(0.35f);
            if (aborted) yield break;
            // 1) lift-off
            Vector2 ground = abs.Pos;
            SpriteVfx.Spawn("abs_takeoff", ground, 1.3f, Layer, Order + 1, false, 16f, false);
            Sfx.Play("shockwave", ground, 0.8f);
            Shake(1);
            FlashLight(ground, c, 2.2f, 1.2f, 0.4f);
            float h0 = abs.Lift;
            for (float t = 0f; t < 0.7f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.7f;
                liftOverride = Mathf.Lerp(h0, 6.5f, u * u);
                abs.Alpha = 1f - Mathf.Clamp01((u - 0.6f) / 0.4f);
                yield return null;
            }
            if (aborted) { liftOverride = -1f; yield break; }
            airHidden = true;
            if (starfall) { yield return StarfallBarrage(power); if (aborted) { airHidden = false; liftOverride = -1f; yield break; } }
            // 2) shadow + target ring chase Ren
            Vector2 mark = PlayerFeet;
            const float R = 1.0f;
            var ring = RingMark(mark, R, 0.07f, new Color(c.r, c.g, c.b, 0.85f), 0f);
            var disc = RingMark(mark, R * 0.5f, R * 0.5f, new Color(c.r, c.g, c.b, 0.12f), 0f);
            var holder = AddHazard(HazardShape.Circle, mark, mark, 0f, 0f, 0f, 0f, 5f);
            holder.Objs.Add(ring.gameObject); holder.Objs.Add(disc.gameObject);
            float chase = power >= 2 ? 1.7f : 2.1f, speed = power >= 2 ? 1.6f : 1.3f;
            for (float t = 0f; t < chase && !aborted; t += Time.deltaTime)
            {
                mark = Vector2.MoveTowards(mark, InArena(PlayerFeet, 0.4f), speed * Time.deltaTime);
                abs.Pos = mark;                                                        // his shadow follows the ring
                ring.transform.position = mark; disc.transform.position = mark;
                float s = 1f + 0.06f * Mathf.Sin(Time.time * 10f);
                ring.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (aborted) { airHidden = false; liftOverride = -1f; yield break; }
            // 3) lock
            Sfx.Play("dial_turn", mark, 1f);
            Sfx.Play("eye_beam_charge", mark, 0.8f);
            for (float t = 0f; t < 0.55f && !aborted; t += Time.deltaTime)
            {
                bool on = Mathf.Repeat(t * 14f, 1f) < 0.5f;
                ring.color = on ? new Color(1f, 1f, 1f, 1f) : new Color(c.r, c.g, c.b, 0.9f);
                disc.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.12f, 0.4f, t / 0.55f));
                yield return null;
            }
            if (aborted) { airHidden = false; liftOverride = -1f; yield break; }
            // 4) dive
            airHidden = false;
            abs.Pos = mark;
            abs.Play("dive", 30f, false);
            for (float t = 0f; t < 0.18f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.18f;
                liftOverride = Mathf.Lerp(6.5f, 0f, u * u);
                abs.Alpha = Mathf.Clamp01(u * 3f);
                yield return null;
            }
            liftOverride = 0f; abs.Alpha = 1f;
            holder.Dead = true;
            if (aborted) { liftOverride = -1f; yield break; }
            // 5) IMPACT — CATACLYSM: light pillar + gold SHOCKWAVE across the floor (abs_quake 290x116: flash, wave rim, cracks, dust, debris)
            //    + DAMAGING WAVE RING grows 0.6 -> 3.4 u in 0.75 s (only rolling through / running outside escapes) + 12 spinning ROCK SHARDS fly out (2 gaps)
            Fx("abs_dive_impact", mark, 1.25f, Order + 1, 16f, Color.white);
            var quake = SpriteVfx.Spawn("abs_quake", mark, 1.15f, Layer, Order - 1, false, 16f, false);
            Sfx.Play("mantra_groundslam", mark, 1f);
            Sfx.Play("bolt_strike", mark, 0.7f);
            Sfx.Play("keg_blast", mark, 0.6f);
            Shake(3);
            FlashLight(mark, c, 5.5f, 2.6f, 0.7f);
            if ((PlayerFeet - mark).magnitude <= R) HurtRen(16f, mark, 2.6f);
            Ctx.Run(ShockwaveBand(mark, 0.6f, power >= 2 ? 3.6f : 3.0f, 0.75f, 10f, c));
            RockBurst(mark, power >= 2 ? 12 : 9);
            yield return Wait(0.5f);
            // 6) fly back up
            liftOverride = -1f;
            abs.Play("idle", 5f);
            yield return Wait(0.4f);
        }

        /// <summary>Damaging SHOCKWAVE RING — grows from r0 to r1 over `seconds`, 0.2 u thick; hits once (a roll's invulnerability passes through).
        /// Visual: light ring (RingMark scaled to the radius) + rock dust along the rim + a light that follows it.</summary>
        private IEnumerator ShockwaveBand(Vector2 c, float r0, float r1, float seconds, float dmg, Color col)
        {
            var ring = RingMark(c, 1f, 0.06f, new Color(1f, 0.95f, 0.75f, 0.95f), 0f);
            var ring2 = RingMark(c, 1f, 0.14f, new Color(col.r, col.g, col.b, 0.4f), 0f);
            var holder = AddHazard(HazardShape.Circle, c, c, 0f, 0f, 0f, 0f, seconds + 0.5f);
            holder.Objs.Add(ring.gameObject); holder.Objs.Add(ring2.gameObject);
            bool hit = false;
            float nextDust = 0f;
            for (float t = 0f; t < seconds && !aborted; t += Time.deltaTime)
            {
                float u = t / seconds, r = Mathf.Lerp(r0, r1, 1f - (1f - u) * (1f - u));
                ring.transform.localScale = ring2.transform.localScale = new Vector3(r, r, 1f);
                float a = 1f - u * 0.7f;
                ring.color = new Color(1f, 0.95f, 0.75f, 0.95f * a);
                ring2.color = new Color(col.r, col.g, col.b, 0.45f * a);
                if (!hit && Mathf.Abs((PlayerFeet - c).magnitude - r) <= 0.22f) hit = HurtRen(dmg, c, 2f);
                if (Time.time >= nextDust)
                {
                    nextDust = Time.time + 0.05f;
                    Vector2 p = c + Rot(Vector2.right, Random.Range(0f, 360f)) * r;
                    if (A.Contains(p)) Fx("abs_arcane_hit", p, 0.45f, Order + 1, 22f, new Color(1f, 0.88f, 0.6f));
                }
                yield return null;
            }
            holder.Dead = true;
        }

        /// <summary>ROCK SHARDS flung out of the crater (abs_rock: 3 rock sprites x 4 spin frames, gold cracks) — 2 opposite gaps; burst into dust on walls.</summary>
        private void RockBurst(Vector2 c, int n)
        {
            var all = SpriteVfx.Load("abs_rock");
            int gap = Random.Range(0, n);
            for (int i = 0; i < n; i++)
            {
                if (i == gap || i == (gap + n / 2) % n) continue;
                Vector2 d = Rot(Vector2.right, i * 360f / n + Random.Range(-8f, 8f));
                var b = Shot("abs_rock", c + d * 0.35f, d * Random.Range(2.6f, 3.6f), Color.white, Random.Range(1f, 1.25f), 8f, 0.13f, 1.6f);
                if (b == null) continue;
                if (all.Length >= 12)
                {
                    int k = Random.Range(0, 3) * 4;
                    b.frames = new[] { all[k], all[k + 1], all[k + 2], all[k + 3] };
                    b.fps = Random.Range(10f, 16f);
                }
                b.faceVelocity = false;
                b.accel = -1.2f;                                                         // rocks slow down (friction) — unlike magic bullets
                b.popFx = "abs_arcane_hit"; b.popScale = 0.4f; b.popTint = new Color(0.75f, 0.65f, 0.5f);
            }
        }

        /// <summary>STARFALL (aerial skill while Absalom is hidden in the sky): 3 bands of light pillars SWEEP DIAGONALLY across the hall (7–9 pillars per band,
        /// 0.09 s apart, 0.8 s warning rings), each band in the opposite direction to the last; always leaves a gap between two pillars. Then he dives (Cataclysm).</summary>
        private IEnumerator StarfallBarrage(int power)
        {
            var frames = SpriteVfx.Load("abs_sunfall");
            int lines = power >= 2 ? 3 : 2;
            for (int L = 0; L < lines && !aborted; L++)
            {
                float ang = (L % 2 == 0 ? -25f : 205f) + Random.Range(-15f, 15f);
                Vector2 dir = Rot(Vector2.right, ang);
                Vector2 mid = InArena(PlayerFeet + Random.insideUnitCircle * 0.5f, 0.4f);
                int n = power >= 2 ? 9 : 7;
                Sfx.Play("bell_toll", mid, 0.6f);
                for (int i = 0; i < n && !aborted; i++)
                {
                    Vector2 at = mid + dir * ((i - (n - 1) * 0.5f) * 0.78f) + Rot(dir, 90f) * Random.Range(-0.12f, 0.12f);
                    if (!A.Contains(at)) continue;
                    var g = GroundStrike.Spawn(at, 0.8f, 0.42f, 10f, frames, 1, 16f, Order + 1, Gold);
                    if (g != null) { g.impactSfx = i % 2 == 0 ? "bolt_strike" : "none"; g.bodyScale = 0.42f / 0.62f; }
                    Ctx.Run(SunfallBurst(at, 0.8f + 1f / 16f, 0.42f));
                    yield return Wait(0.09f);
                }
                yield return Wait(0.45f);
            }
            yield return Wait(0.5f);
        }

        /// <summary>Random Light skill (never repeats the last one), including Ascension Dive.</summary>
        private int lastLight = -1;

        private IEnumerator LightSkill(int power)
        {
            int k;
            do k = Random.Range(0, 4); while (k == lastLight);
            lastLight = k;
            if (k == 0) yield return Sunlance(power);
            else if (k == 1) yield return HaloRing(power);
            else if (k == 3) yield return AscensionDive(power);
            else
            {
                yield return Cast(School.Light, "SUNFALL", 0.4f, "bell_toll");
                yield return Sunfall(power >= 2 ? 3 : 2, 0.55f, 0.42f, 9f);
                abs.Play("idle", 5f);
            }
        }
    }
}
