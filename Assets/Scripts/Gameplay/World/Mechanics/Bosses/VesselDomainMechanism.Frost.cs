using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE 3 RIME rules (move table + moves in .Ice.cs). Adds an ice RULE like Burn in Phase 2, a lure / counter play, and escalation in the second half:
    ///   1. FROSTBITE (phase rule): every ice hit = +1 FROSTBITE (Ren turns bluer, ice crystals at his feet); 3 = FROZEN for 1.5 s inside an ice block
    ///      (vc_ice_block), then immune to frostbite for 3 s after breaking out; after 3.5 s without ice hits it thaws 1 level / 1.5 s.
    ///   2. ROLLING SNOWBALLS + COUNTER: snowballs landing on the ICE FLOOR do not break but ROLL and slide (bounce off the edges twice, touch = 12 + frostbite).
    ///      Ren STRIKES a rolling ball -> it shoots back fast; hitting a LYING HAND = 2.5 hits on the Vessel (ignores hand armour) + the hand stays frozen 1 s longer.
    ///   3. FROZEN HAND (lure reward): a Slam onto the iced floor -> the hand sticks in the ice: lies 1.5 s longer, hand armour +12 %.
    ///   4. WHITEOUT (phase HP < 50 %, about every 25 s): blizzard — fine mist around Ren + mist bands + wind-blown snow streaks, WIND pushes Ren sideways
    ///      (reverses mid-way, with a warning), snowballs keep falling around Ren for 6 s.
    ///   5. Atmosphere: the Vessel exhales cold breath every ~2.6 s; snow drifts with the wind.
    /// Called by: TickIce (every frame), OnStruckHand (snowball hits), Whiteout move (.Ice.cs table).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private int frostStacks;
        private float frostDecay, frozenUntil, frostImmuneUntil, nextWhiteout, nextVapor, windX;
        private SpriteRenderer iceBlock;
        private readonly bool[] frozenHand = new bool[2];
        private bool rollHinted;
        private static Sprite iceBlockSprite, fogSprite;

        private class RollBall { public Transform t; public SpriteRenderer sr; public Vector2 v; public bool launched; public float born; public int bounces; public bool dead; }
        private readonly List<RollBall> rollers = new List<RollBall>();

        // ------------------------------------------------------------------ 1) FROSTBITE -> FROZEN
        private void AddFrost(int n)
        {
            float now = Time.time;
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead || now < frostImmuneUntil || now < frozenUntil) return;
            frostStacks = Mathf.Min(3, frostStacks + n);
            frostDecay = now + 3.5f;
            hp.TintPulse(new Color(0.55f, 0.85f, 1f), 0.45f);
            SpriteVfx.Spawn("abs_ice_spikes", Feet, 0.9f + 0.15f * frostStacks, layer, order + 1, Random.value < 0.5f, 16f, false);
            if (frostStacks >= 3) FreezeRen();
            else DrillBoard.Say("FROSTBITE " + frostStacks + "/3", IceCol);
        }

        private void FreezeRen()
        {
            float now = Time.time;
            var hp = Ctx.PlayerHealth;
            frostStacks = 0;
            frozenUntil = now + Float("frozen_seconds", 1.5f);
            frostImmuneUntil = frozenUntil + 3f;
            if (hp != null) hp.Stun(frozenUntil - now);
            if (iceBlockSprite == null) iceBlockSprite = Resources.Load<Sprite>("VFX/vc_ice_block");
            var g = new GameObject("ice prison");
            fieldObjs.Add(g);
            g.transform.position = Feet;
            iceBlock = g.AddComponent<SpriteRenderer>();
            iceBlock.sprite = iceBlockSprite != null ? iceBlockSprite : Disc();
            FxLighting.MakeUnlit(iceBlock);
            iceBlock.sortingLayerID = layer; iceBlock.sortingOrder = order + 1;
            iceBlock.color = new Color(1f, 1f, 1f, 0.88f);
            Sfx.Play("trap_ice", Feet, 1f);
            BossJuice.Flash(Feet, IceCol, 1.8f, 2f, 0.25f);
            DrillBoard.Say("FROZEN SOLID", IceCol);
            DrillBoard.Yell("Stay. Like the others stayed.", 1.4f);
        }

        /// <summary>Every frame (called from TickIce): thaw frostbite, ice block on Ren then breaking, cold breath from the mouth.</summary>
        private void TickFrost()
        {
            float now = Time.time;
            if (frostStacks > 0 && now > frostDecay) { frostStacks--; frostDecay = now + 1.5f; }
            if (iceBlock != null)
            {
                iceBlock.transform.position = Feet;
                float left = frozenUntil - now;
                if (left < 0.4f) iceBlock.transform.position = Feet + new Vector2(Mathf.Sin(now * 70f) * 0.03f, 0f);   // shaking, about to break
                if (left <= 0f)
                {
                    SpriteVfx.Spawn("vc_snow_impact", Feet, 1.1f, layer, order + 2, false, 16f, false);
                    SpriteVfx.Spawn("abs_ice_shatter", Feet + Vector2.up * 0.35f, 0.9f, layer, order + 2, false, 18f, false);
                    Sfx.Play("ice_shatter", Feet, 0.9f);
                    DestroyObj(iceBlock); iceBlock = null;
                }
            }
            if (toneP == 2 && actState == ActState.Playing && jawHold < 0f && now >= nextVapor && rig != null)   // cold breath
            {
                nextVapor = now + Random.Range(2.2f, 3.2f);
                var v = SpriteVfx.Spawn("vc_frost_breath", rig.MouthWorld(), 0.75f, layer, order - 2, false, 12f, false);
                if (v != null) { v.transform.rotation = Quaternion.Euler(0f, 0f, -90f + Random.Range(-25f, 25f)); v.Tint(new Color(1f, 1f, 1f, 0.45f)); }
            }
        }

        private void KillFrost()
        {
            frostStacks = 0; frozenUntil = 0f; iceBlock = null;
            frozenHand[0] = frozenHand[1] = false;
            foreach (var b in rollers) b.dead = true;
            rollers.Clear();
            windX = 0f;
        }

        // ------------------------------------------------------------------ 2) ROLLING SNOWBALLS + COUNTER
        /// <summary>Snowball landing on the ice floor: rolls along its fall direction (`dir`), bounces off the edges, can be struck.</summary>
        private IEnumerator RollingSnowball(Vector2 at, Vector2 dir)
        {
            if (rollers.Count >= 4) yield break;
            if (snowSprite == null) snowSprite = Resources.Load<Sprite>("VFX/vc_snowball");
            var g = new GameObject("rolling snowball");
            fieldObjs.Add(g);
            g.transform.position = at;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = snowSprite != null ? snowSprite : Disc();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order;
            g.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            var b = new RollBall { t = g.transform, sr = sr, v = dir.normalized * Float("roll_speed", 2.6f), born = Time.time };
            rollers.Add(b);
            if (!rollHinted) { rollHinted = true; Hints.Show("Strike a rolling snowball to send it back at a fallen hand!", 3f); }
            float nextTrail = 0f;
            while (!aborted && !b.dead && b.t != null)
            {
                float now = Time.time, dt = Time.deltaTime;
                Vector2 p = (Vector2)b.t.position + b.v * dt;
                b.t.Rotate(0f, 0f, -b.v.x * 120f * dt);
                b.sr.sortingOrder = p.y < Feet.y ? order + 1 : order - 1;
                if (now >= nextTrail) { nextTrail = now + 0.08f; VoidMoteColored(p + Vector2.down * 0.15f, new Color(0.9f, 0.97f, 1f)); }
                if (!b.launched)
                {
                    // bounce off the arena edge
                    if (p.x < Floor.xMin + 0.2f || p.x > Floor.xMax - 0.2f) { b.v.x = -b.v.x; b.bounces++; p.x = Mathf.Clamp(p.x, Floor.xMin + 0.2f, Floor.xMax - 0.2f); Sfx.Play("hit_block", p, 0.3f); }
                    if (p.y < Floor.yMin + 0.2f || p.y > Floor.yMax - 0.2f) { b.v.y = -b.v.y; b.bounces++; p.y = Mathf.Clamp(p.y, Floor.yMin + 0.2f, Floor.yMax - 0.2f); Sfx.Play("hit_block", p, 0.3f); }
                    b.t.position = p;
                    if (b.bounces > 2 || now - b.born > 4.5f || now >= iceUntil + 0.3f) { Shatter(b, 1f); break; }
                    if ((Feet + Vector2.up * 0.2f - p).magnitude < 0.45f)
                    {
                        if (Hurt(12f, p, 1f)) Chill(0.8f, 1.5f, "Bowled over by the snow!");
                        Shatter(b, 1.1f); break;
                    }
                }
                else
                {
                    b.t.position = p;
                    bool done = false;
                    for (int i = 0; i < 2 && !done; i++)
                    {
                        int side = i == 0 ? -1 : 1;
                        if (!HandDown(side)) continue;
                        Vector2 h = rig.HandWorld(side);
                        if ((h - p).magnitude > 0.95f) continue;
                        DamageVessel(Float("roll_hits", 2.5f), h);                      // ignores hand armour: reward for the counter
                        downUntil[i] += 1f;
                        frozenHand[i] = true;
                        DrillBoard.Say("THE HAND FREEZES!", IceCol);
                        BossJuice.Shake(2);
                        for (int k = 0; k < 3; k++) SpriteVfx.Spawn("abs_rime", h + new Vector2((k - 1) * 0.45f, -0.25f), 0.95f, layer, order + 2, k == 2, 20f, false);
                        Shatter(b, 1.5f); done = true;
                    }
                    if (done) break;
                    if (!Floor.Overlaps(new Rect(p.x - 1f, p.y - 1f, 2f, 2f)) || now - b.born > 6f) { Shatter(b, 0.9f); break; }
                }
                yield return null;
            }
            rollers.Remove(b);
        }

        private void Shatter(RollBall b, float scale)
        {
            if (b.dead) return;
            b.dead = true;
            if (b.t == null) return;
            Vector2 p = b.t.position;
            SpriteVfx.Spawn("vc_snow_impact", p, 1.2f * scale, layer, order + 1, Random.value < 0.5f, 14f, false);
            Sfx.Play("ice_shatter", p, 0.6f);
            DestroyObj(b.t.gameObject);
        }

        /// <summary>Called from OnStruckHand: Ren's sword hits a rolling snowball -> it shoots back (direction Ren -> ball).</summary>
        private bool StrikeRollers(Vector2 c, float r)
        {
            bool any = false;
            foreach (var b in rollers)
            {
                if (b.dead || b.launched || b.t == null) continue;
                Vector2 p = b.t.position;
                if ((p - c).magnitude > r + 0.4f) continue;
                Vector2 d = p - Feet;
                if (d.sqrMagnitude < 0.0004f) d = c - Feet;
                if (d.sqrMagnitude < 0.0004f) d = Vector2.up;
                // light aim assist: if a lying hand is close to the hit direction, bend toward it (± 35°)
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    if (!HandDown(side)) continue;
                    Vector2 toHand = rig.HandWorld(side) - p;
                    if (Vector2.Angle(d, toHand) < 35f) { d = toHand; break; }
                }
                b.launched = true;
                b.v = d.normalized * 9f;
                b.sr.sortingOrder = order + 3;
                Sfx.Play("hit_block", p, 0.8f);
                SpriteVfx.Spawn("hit", p, 0.8f, layer, order + 4, false, 18f, false);
                DrillBoard.Say("RETURNED!", RelicBlue);
                any = true;
            }
            return any;
        }

        private void VoidMoteColored(Vector2 at, Color c)
        {
            var g = new GameObject("snow mote");
            fieldObjs.Add(g);
            g.transform.position = at;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = MarginArt.Pixel(); FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order - 1;
            float sz = 0.04f * 100f;
            g.transform.localScale = new Vector3(sz, sz, 1f);
            Ctx.Run(MoteAnim(g, sr, c, 0.15f, 0.5f, Random.Range(0f, 6f)));
        }

        // ------------------------------------------------------------------ 4) WHITEOUT (blizzard, second half of the phase)
        // Layers: (1) FINE MIST — soft gradient (bilinear filtering), clear within 2 u of Ren, thickening up to 4.8 u,
        // max 78 %, blue-white; (2) 2 MIST BANDS drifting with the wind (soft Perlin noise, 30 %); (3) WIND SNOW STREAKS: ~110 thin 1–2 px streaks flying fast and slanted
        // with the wind, near faint / far clear — reads instantly as "storm" while attacks stay visible; (4) normal snowfall also drifts with the wind.
        private static Sprite fogBankSprite;
        private IEnumerator Whiteout(float pressure)
        {
            nextWhiteout = Time.time + Float("whiteout_every", 25f);
            DrillBoard.Say("WHITEOUT", IceCol);
            DrillBoard.Yell("Lose your way in the white.", 1.8f);
            jawHold = 0.8f; slumpTarget = 0.2f;
            Sfx.Play("gas_hiss", rig.MouthWorld(), 1f);
            yield return Wait(0.8f);
            if (aborted) { jawHold = -1f; slumpTarget = 0f; yield break; }
            jawHold = -1f; slumpTarget = 0f;
            var fog = new GameObject("whiteout fog");
            fieldObjs.Add(fog);
            var fs = fog.AddComponent<SpriteRenderer>();
            fs.sprite = Fog(); FxLighting.MakeUnlit(fs);
            fs.sortingLayerID = layer; fs.sortingOrder = order + 30;
            float fogK = 24f / Mathf.Max(0.01f, fs.sprite.bounds.size.x);             // 24 u: clear centre 2 u, fully thick at 4.8 u
            fog.transform.localScale = new Vector3(fogK, fogK, 1f);
            var banks = new List<SpriteRenderer>();
            for (int i = 0; i < 2; i++)
            {
                var bg = new GameObject("whiteout bank");
                fieldObjs.Add(bg);
                var br = bg.AddComponent<SpriteRenderer>();
                br.sprite = FogBank(); FxLighting.MakeUnlit(br);
                br.sortingLayerID = layer; br.sortingOrder = order + 29;
                float bk = 16f / Mathf.Max(0.01f, br.sprite.bounds.size.x);
                bg.transform.localScale = new Vector3(bk, bk, 1f);
                banks.Add(br);
            }
            var streaks = new List<(SpriteRenderer r, float spd, float len, float a)>();
            var px = MarginArt.Pixel();
            for (int i = 0; i < 110; i++)
            {
                var sg = new GameObject("whiteout streak");
                fieldObjs.Add(sg);
                var sr = sg.AddComponent<SpriteRenderer>();
                sr.sprite = px; FxLighting.MakeUnlit(sr);
                sr.sortingLayerID = layer; sr.sortingOrder = order + 31;
                float far = Random.value;                                               // 0 near (big, fast, clear) .. 1 far
                streaks.Add((sr, Mathf.Lerp(11f, 6f, far), Mathf.Lerp(0.55f, 0.18f, far), Mathf.Lerp(0.75f, 0.3f, far)));
                sg.transform.position = Feet + new Vector2(Random.Range(-10f, 10f), Random.Range(-6f, 6f));
            }
            const float Dur = 6f;
            float dir = Random.value < 0.5f ? -1f : 1f, wind = Float("whiteout_wind", 1.6f);
            bool turned = false, warnedTurn = false;
            float nextBall = 0f, t0 = Time.time, gust = 0f;
            var st = RenStatus;
            while (!aborted)
            {
                float now = Time.time, t = now - t0, dt = Time.deltaTime;
                if (t >= Dur + 0.8f) break;
                float a = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((Dur + 0.8f - t) / 0.8f);
                a = a * a * (3f - 2f * a);
                // wind: warns 0.6 s before reversing (calm + text), then reverses
                if (!warnedTurn && t >= Dur * 0.5f - 0.6f) { warnedTurn = true; DrillBoard.Say("THE WIND TURNS", IceCol); Sfx.Play("gas_hiss", Feet, 0.6f); }
                if (!turned && t >= Dur * 0.5f) { turned = true; dir = -dir; }
                float calm = warnedTurn && !turned ? 0.15f : 1f;
                gust = Mathf.MoveTowards(gust, dir * calm, dt * 2.5f);                  // wind reverses smoothly
                float w = t < Dur ? gust * wind : 0f;
                windX = w * 2.5f;
                if (st != null) st.Push(new Vector2(w, 0f), 0.15f);
                // fine mist follows Ren
                Vector2 c = Feet + Vector2.up * 0.25f;
                fog.transform.position = c;
                fs.color = new Color(0.9f, 0.95f, 1f, 0.78f * a);
                // mist bands drift with the wind, wrapping around Ren
                for (int i = 0; i < banks.Count; i++)
                {
                    var br = banks[i];
                    if (br == null) continue;
                    float span = 16f, ox = Mathf.Repeat(t * gust * (2.2f + i) + i * 7f, span) - span * 0.5f;
                    br.transform.position = c + new Vector2(ox, (i == 0 ? 1.4f : -1.6f) + Mathf.Sin(t * 0.7f + i) * 0.3f);
                    br.color = new Color(0.92f, 0.96f, 1f, 0.3f * a);
                }
                // wind snow streaks
                Vector2 vd = new Vector2(Mathf.Abs(gust) > 0.05f ? Mathf.Sign(gust) : dir, -0.32f).normalized;
                float ang = Mathf.Atan2(vd.y, vd.x) * Mathf.Rad2Deg;
                for (int i = 0; i < streaks.Count; i++)
                {
                    var (r, spd, len, sa) = streaks[i];
                    if (r == null) continue;
                    var tr = r.transform;
                    Vector2 p = (Vector2)tr.position + vd * spd * Mathf.Max(0.25f, Mathf.Abs(gust)) * dt;
                    Vector2 rel = p - c;
                    if (Mathf.Abs(rel.x) > 10.5f || Mathf.Abs(rel.y) > 6.5f)            // left the frame -> respawn on the upwind side
                        p = c + new Vector2(-Mathf.Sign(vd.x) * Random.Range(8f, 10f), Random.Range(-6f, 6f));
                    tr.position = p;
                    tr.rotation = Quaternion.Euler(0f, 0f, ang);
                    tr.localScale = new Vector3(len * 100f, (len > 0.4f ? 0.02f : 0.012f) * 100f, 1f);
                    r.color = new Color(0.95f, 0.98f, 1f, sa * a * Mathf.Clamp01(Mathf.Abs(gust) + 0.2f));
                }
                if (t < Dur && now >= nextBall)
                {
                    nextBall = now + Mathf.Lerp(0.5f, 0.38f, pressure * 0.5f);
                    StartR(Snowball(ClampFloor(Feet + PlayerVel * 0.5f + Random.insideUnitCircle * 1.6f, 0.3f), pressure));
                }
                yield return null;
            }
            windX = 0f;
            DestroyObj(fog);
            foreach (var br in banks) if (br != null) DestroyObj(br.gameObject);
            foreach (var (r, _, _, _) in streaks) if (r != null) DestroyObj(r.gameObject);
        }

        /// <summary>Fine mist (256 px, bilinear): transparent at the centre (radius 1/6), thickening to 0.4, light Perlin noise so the edge is not perfectly round.</summary>
        private static Sprite Fog()
        {
            if (fogSprite != null) return fogSprite;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "whiteout_fog" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;                        // 0 centre -> 1 edge
                    float nz = (Mathf.PerlinNoise(x * 0.035f, y * 0.035f) - 0.5f) * 0.12f;
                    float k = Mathf.Clamp01((d + nz - 0.167f) / (0.4f - 0.167f));
                    k = k * k * (3f - 2f * k);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(255f * k));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            fogSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return fogSprite;
        }

        /// <summary>Drifting mist band: soft Perlin-noise ellipse 192 x 64 (bilinear), edges fading out.</summary>
        private static Sprite FogBank()
        {
            if (fogBankSprite != null) return fogBankSprite;
            const int W = 192, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "whiteout_bank" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H * 2f - 1f;
                    float e = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    float n = Mathf.PerlinNoise(x * 0.045f + 3f, y * 0.09f + 7f) * 0.7f + Mathf.PerlinNoise(x * 0.12f, y * 0.2f) * 0.3f;
                    float a = Mathf.Clamp01(e * e * (n * 1.6f - 0.35f));
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(255f * a));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            fogBankSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W / 4f);
            return fogBankSprite;
        }
    }
}
