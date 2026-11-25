using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SKILLS of IsoldeSkills (one coroutine per skill). Settings / skill choice: IsoldeSkills.cs.
    /// </summary>
    public partial class IsoldeSkills
    {
        // ------------------------------------------------------------------ skill choice
        protected override IEnumerator Think()
        {
            if (Broken) { yield return null; yield break; }
            TickPools();
            float gap = ai.GapToPlayer();
            float d = (PlayerPos - Pos).magnitude;

            if (gap <= meleeRange && Ready("lash")) { yield return BloodLash(); yield break; }
            if (Destroyed > 0 && ReadyG("rite")) { yield return RiteOfRepose(); yield break; }
            if (phase >= 2 && ReadyG("flight") && Intact >= 2) { yield return NightFlight(); yield break; }
            if (phase == 3 && ReadyG("tide")) { yield return SanguineTide(); yield break; }
            if (hp.Percent < 0.85f && pools.Count > 0 && ReadyG("feast")) { yield return Feast(); yield break; }
            if (d > 2.6f && ReadyG("dive")) { yield return BatDive(); yield break; }
            if (d < siphonRange - 0.6f && d > 1.4f && ReadyG("siphon")) { yield return SanguineSiphon(); yield break; }
            if (ReadyG("rain")) { yield return CrimsonRain(); yield break; }
            if (ReadyG("bats")) { yield return ChildrenOfTheNight(phase == 1 ? 3 : phase == 2 ? 4 : 5, true); yield break; }
            yield return Stalk(0.6f);
        }

        protected override IEnumerator Reposition()
        {
            var extra = new List<Vector2>();
            foreach (var c in coffins) extra.Add(center + (c.pos - center) * 0.75f);
            yield return MistStep(FarPoint(0.6f, 12, extra));
            if (!hp.IsDead) yield return ChildrenOfTheNight(2, false);
        }

        protected override IEnumerator OnPhase(int p)
        {
            hp.Invulnerable = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Hint(p == 2 ? "\"You woke me for this? Children of the Vey, to me!\"" : "\"The Vey do not die. We are merely... delayed.\"", 3f);
            Announce(p == 2 ? "Blood Moon Rising" : "The Vey Do Not Die", Blood, 0.5f);
            ChargeFx(Blood, 0.5f, 1f);
            yield return ai.WaitFor(0.5f);
            var w = Wave(Pos, 3.6f, 1.1f, 0f, "m3_wave_blood");          // knockback only
            w.knockback = 4f;
            w.onHit = _ => { };
            Fired();
            Boom(CastPoint, Blood, 1.6f, 3);                                  // phase change burst
            BossJuice.Burst("m3x_blood_burst", Pos, 2.2f, Color.white, Layer, Order - 1, 14f);
            yield return ai.WaitFor(0.9f);
            hp.Invulnerable = false;
            if (p == 2) Use("flight", 0f);                       // entering phase 2: hide in a coffin at once
            if (p == 3)
            {
                // drain every remaining pool: each heals 2% -> a reason to burn pools early
                int n = 0;
                foreach (var pl in pools)
                {
                    SpriteVfx.Spawn("m3_blood_mist", pl.pos, 0.45f, Layer, Order, false, 16f);
                    if (pl.vfx != null) pl.vfx.Stop();
                    n++;
                }
                pools.Clear();
                if (n > 0) { hp.Heal(hp.Max * 0.02f * n * lifestealMul); Hint("She drinks every drop that was spilled.", 2f); }
                Use("tide", 1.5f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Sanguine Kiss (lifesteal when pressed close, 15 s cooldown)
        // replaces the normal swipe. A light swipe (x0.55); on hit it drains: a stream of blood drops flows to her and she heals 4% max HP.
        private IEnumerator BloodLash()
        {
            SfxHooks.BossMove(this, "BloodLash"); // sound
            Use("lash", 15f);
            ai.Desired = Vector2.zero;
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Attack");
            if (ai.Vfx != null) ai.Vfx.Windup(dir);
            Announce("Sanguine Kiss", Blood, 0.45f);
            yield return ai.WaitFor(0.45f);
            if (hp.IsDead) yield break;
            if (ai.Vfx != null) ai.Vfx.Swing(dir);
            Fired();
            Vector2 toP = PlayerPos - Pos;
            if (toP.magnitude <= 0.95f && Vector2.Angle(dir, toP) <= 80f && ai.PlayerHealth != null && !ai.PlayerHealth.Invulnerable)
            {
                ai.Hurt(ai.damage * 0.55f, Pos, 1.5f);
                hp.Heal(hp.Max * 0.04f * lifestealMul);                        // 4% -> 2%
                Boom(PlayerPos + Vector2.up * 0.3f, Blood, 0.7f, 2);           // bite lands -> blood burst + hit-stop
                BossJuice.Burst("m3x_blood_burst", PlayerPos + Vector2.up * 0.25f, 0.8f, Color.white, Layer, Order + 2, 18f);
                BossJuice.Stop(0.05f);
                for (int k = 0; k < 6; k++) StartCoroutine(FlyMote("m3x_blood_bolt", PlayerPos + Vector2.up * 0.3f + Random.insideUnitCircle * 0.15f, transform, Vector2.up * 0.45f, 0.4f + k * 0.05f, 0.32f));
                Hint("Isolde drinks from the wound.", 1.4f);
            }
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.2f);
        }

        // ------------------------------------------------------------------ Bat Dive (gap closer)
        private IEnumerator BatDive()
        {
            SfxHooks.BossMove(this, "BatDive"); // sound
            Use("dive", 8f);
            ai.Desired = Vector2.zero;
            Vector2 from = Pos;
            Vector2 dir = (PlayerPos - from).normalized;
            Vector2 to = ClampArena(PlayerPos + dir * 0.6f, 0.85f);
            ai.FaceX(dir.x);
            ai.PlayState("Cast");
            // warning lane along the dive (0.76 units wide = hit area), the fill completes exactly when she turns into bats
            var tg = DashTelegraph.Spawn(from, to - from, (to - from).magnitude, 0.76f, Blood, 0.6f, Layer, Order - 1);
            Announce("Bat Dive", Blood, 0.6f);
            ChargeFx(Blood, 0.6f);
            yield return ai.WaitFor(0.6f);
            tg.Release();
            Fired();
            float t;
            if (hp.IsDead) yield break;
            SpriteVfx.Spawn("m3_blood_mist", from, 0.6f, Layer, Order + 1, false, 18f);
            SetHidden(true);
            StartCoroutine(BatSwarm(from, to, 0.35f, 6));
            bool hit = false;
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                Vector2 p = Vector2.Lerp(from, to, t / 0.35f);
                if (!hit && (PlayerPos - p).magnitude < 0.38f)
                {
                    hit = true; ai.Hurt(ai.damage * 0.9f, p - dir * 0.3f, 3f);
                    Boom(PlayerPos + Vector2.up * 0.3f, Blood, 0.8f, 2);
                    BossJuice.Burst("m3x_blood_burst", PlayerPos + Vector2.up * 0.25f, 0.9f, Color.white, Layer, Order + 2, 18f);
                }
                yield return null;
            }
            ai.TeleportTo(to);
            SpriteVfx.Spawn("m3_blood_mist", to, 0.6f, Layer, Order + 1, false, 18f);
            BossJuice.Burst("m3x_blood_burst", to, 1.2f, Color.white, Layer, Order - 1, 16f);        // lands with a blood splash
            BossJuice.Flash(to + Vector2.up * 0.3f, Blood, 1.6f, 1.2f, 0.3f);
            BossJuice.Shake(1);
            SetHidden(false);
            ResetTrackers();
            if (ai.GapToPlayer() <= meleeRange + 0.35f) { if (Ready("lash")) yield return BloodLash(); }
            else yield return ai.WaitFor(0.25f);
        }

        /// Bat swarm flying from a to b (visual only).
        private IEnumerator BatSwarm(Vector2 a, Vector2 b, float seconds, int count)
        {
            SfxHooks.BossMove(this, "BatSwarm"); // sound
            if (batFrames == null || batFrames.Length == 0) yield break;
            var bats = new List<SpriteRenderer>();
            var offs = new List<Vector2>();
            Vector2 perp = Vector2.Perpendicular((b - a).normalized);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("BatSwarm");
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = batFrames[0];
                r.sortingLayerID = Layer; r.sortingOrder = Order + 1;
                r.flipX = b.x < a.x;
                go.transform.localScale = Vector3.one * Random.Range(0.55f, 0.8f);
                bats.Add(r);
                offs.Add(perp * Random.Range(-0.35f, 0.35f) + Vector2.up * Random.Range(0.1f, 0.6f));
            }
            float t = 0f;
            while (t < seconds + 0.15f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                for (int i = 0; i < bats.Count; i++)
                {
                    if (bats[i] == null) continue;
                    float lag = Mathf.Clamp01(k - i * 0.04f);
                    bats[i].transform.position = Vector2.Lerp(a, b, lag) + offs[i] * Mathf.Sin(lag * Mathf.PI);
                    bats[i].sprite = batFrames[(int)((t * 14f) + i) % batFrames.Length];
                }
                yield return null;
            }
            foreach (var r in bats) if (r != null) Destroy(r.gameObject);
        }

        // ------------------------------------------------------------------ Children of the Night (chasing bats)
        private IEnumerator ChildrenOfTheNight(int n, bool useCd)
        {
            SfxHooks.BossMove(this, "ChildrenOfTheNight"); // sound
            if (useCd) Use("bats", 7f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            if (useCd) { Announce("Children of the Night", Blood, 0.45f); ChargeFx(Blood, 0.45f); }
            yield return ai.WaitFor(0.45f);
            if (hp.IsDead) yield break;
            if (useCd) Fired();
            Vector2 mouth = Pos + Vector2.up * 0.4f;
            BossJuice.Flash(mouth, Blood, 1.5f, 1.1f, 0.3f);
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.55f, Layer, Order + 1, false, 16f);
            Vector2 aim = (PlayerPos - mouth).normalized;
            float baseA = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int k = 0; k < n; k++) StartCoroutine(BatShot(mouth, baseA + (k - (n - 1) * 0.5f) * 24f, k * 0.07f));
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }

        /// One bat: flies out in a fan, turns toward Ren during the first 1.1 s, then flies straight; bursts into blood on touching Ren or being slashed.
        private IEnumerator BatShot(Vector2 start, float angle, float delay)
        {
            SfxHooks.BossMove(this, "BatShot"); // sound
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var go = new GameObject("VeyBat");
            go.transform.position = start;
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingLayerID = Layer; r.sortingOrder = Order + 1;
            bool hasBat = batFrames != null && batFrames.Length > 0;
            var orb = hasBat ? null : SpriteVfx.Load("m3x_blood_bolt");                   // 12 px orb -> 73 px blood dart
            go.transform.localScale = Vector3.one * (hasBat ? 0.7f : 0.45f);
            BossJuice.Light(start, Blood, 0.7f, 0.7f, 0f, go.transform);                    // blood bats glow red on their own
            var bat = new Bat { t = go.transform };
            bats.Add(bat);
            Vector2 pos = start, dir = Dir(angle);
            float t = 0f, speed = 2.5f, life = 2.6f;
            while (t < life && !bat.dead && ai != null && !hp.IsDead)
            {
                t += Time.deltaTime;
                if (t < 1.1f && ai.Player != null)                      // turn toward Ren, at most 120°/s
                {
                    Vector2 want = (PlayerPos + Vector2.up * 0.25f - pos).normalized;
                    float cur = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, tgt = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
                    dir = Dir(Mathf.MoveTowardsAngle(cur, tgt, 120f * Time.deltaTime));
                }
                pos += dir * speed * Time.deltaTime;
                go.transform.position = pos;
                if (hasBat) r.flipX = dir.x < 0f;
                else go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                r.sprite = hasBat ? batFrames[(int)(t * 14f) % batFrames.Length] : (orb.Length > 0 ? orb[(int)(t * 12f) % orb.Length] : null);
                if (ai.Player != null && (PlayerPos + Vector2.up * 0.25f - pos).magnitude < 0.22f)
                {
                    ai.Hurt(ai.damage * 0.5f, pos - dir * 0.3f, 1f);
                    bat.dead = true;
                }
                yield return null;
            }
            BossJuice.Burst("m3x_blood_burst", pos, 0.6f, Color.white, Layer, Order + 2, 18f);
            BossJuice.Flash(pos, Blood, 0.9f, 0.9f, 0.22f);
            bats.Remove(bat);
            if (go != null) Destroy(go);
        }

        // ------------------------------------------------------------------ Sanguine Siphon (lifesteal tether, coffins block it)
        private IEnumerator SanguineSiphon()
        {
            SfxHooks.BossMove(this, "SanguineSiphon"); // sound
            Use("siphon", 16f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Cast");
            Hint("She drinks from your veins. Get away, or hide behind a coffin!", 2.5f);
            Vector2 Mouth() => Pos + Vector2.up * 0.45f;
            Vector2 Heart() => PlayerPos + Vector2.up * 0.3f;
            // warning line (wide band + core that fills up) instead of the 4 px m3_ray_warn; skill name on the HUD; blood gathers at her mouth
            var warn = BeamWarn.Spawn(Mouth(), Heart(), 0.12f, Blood, 0.7f, Layer, Order + 1);
            Announce("Sanguine Siphon", Blood, 0.7f);
            var mouthGlow = BossJuice.Light(Mouth(), Blood, 1.2f, 0.9f, 0f, transform);
            float t = 0f;
            while (t < 0.7f && !hp.IsDead)                               // warning line follows Ren
            {
                warn.Aim(Mouth(), Heart());
                t += Time.deltaTime; yield return null;
            }
            warn.Release();
            Fired();
            // twisting BLOOD TETHER (m3x_blood_tether, flowing toward her mouth) + flash at Ren's heart
            var tether = Ray("m3x_blood_tether", Mouth(), Heart(), 0.96f, 1.1f, Order + 1, 14f, siphonTime + 0.3f);
            var heartGlow = BossJuice.Light(Heart(), Blood, 0.9f, 1.1f, 0f);
            BossJuice.Flash(Heart(), Blood, 1.3f, 1.4f, 0.25f);
            t = 0f;
            float nextTick = Time.time + 0.3f, nextMote = 0f;
            while (t < siphonTime && !hp.IsDead && !Broken && ai.Player != null)
            {
                Vector2 a = Heart(), b = Mouth();
                if ((a - b).magnitude > siphonRange || CoffinBlocks(a, b))
                {
                    Hint("The blood-link snaps.", 1.4f);
                    BossJuice.Burst("m3x_blood_burst", Vector2.Lerp(a, b, 0.5f), 0.7f, Color.white, Layer, Order + 2, 18f);   // tether snaps with a blood splash
                    BossJuice.Flash(Vector2.Lerp(a, b, 0.5f), Blood, 1f, 1.2f, 0.25f);
                    break;
                }
                PlaceRay(tether, b, a, 0.96f, 1.1f);
                if (heartGlow != null) heartGlow.transform.position = a;
                if (Time.time >= nextMote)
                {
                    StartCoroutine(FlyMote("m3x_blood_bolt", a, transform, Vector2.up * 0.45f, 0.45f, 0.28f));
                    nextMote = Time.time + 0.12f;
                }
                if (Time.time >= nextTick)
                {
                    ai.Hurt(ai.damage * 0.3f, b, 0f);
                    hp.Heal(hp.Max * 0.01f * lifestealMul);                   // 0.5% per tick
                    nextTick = Time.time + 0.5f;
                }
                ai.FaceX(a.x - b.x);
                t += Time.deltaTime;
                yield return null;
            }
            if (tether != null) tether.Stop();
            if (heartGlow != null) Destroy(heartGlow.gameObject);
            if (mouthGlow != null) Destroy(mouthGlow.gameObject);
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.2f);
        }

        // ------------------------------------------------------------------ Crimson Rain (leaves blood pools)
        private IEnumerator CrimsonRain()
        {
            Use("rain", 10f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Announce("Crimson Rain", Blood, 0.5f);
            ChargeFx(Blood, 0.5f);
            yield return ai.WaitFor(0.5f);
            Fired();
            BossJuice.Flash(Pos + Vector2.up * 1.2f, Blood, 2.2f, 1.2f, 0.4f);
            int n = 3 + phase;
            for (int i = 0; i < n && !hp.IsDead && ai.Player != null; i++)
            {
                Vector2 p = i == 0 ? PlayerPos : ClampArena(PlayerPos + Random.insideUnitCircle * 1.5f, 0.85f);
                DropBlood(p, 0.95f + i * 0.09f, i < 2);
            }
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
        }

        private IEnumerator PoolAfter(Vector2 p, float delay)
        {
            yield return new WaitForSeconds(delay + 0.05f);
            if (hp == null || hp.IsDead) yield break;
            foreach (var o in pools) if ((o.pos - p).magnitude < poolRadius * 1.5f) { o.until = Time.time + poolLife; yield break; }
            if (pools.Count >= maxPools) { if (pools[0].vfx != null) pools[0].vfx.Stop(); pools.RemoveAt(0); }
            var v = SpriteVfx.Spawn("m3_blood_pool", p, 0.75f, Layer, Order - 3, false, 10f);
            if (v != null) v.Looping(poolLife);
            pools.Add(new Pool { pos = p, vfx = v, until = Time.time + poolLife });
        }

        // ------------------------------------------------------------------ Feast (drinking a blood pool)
        private IEnumerator Feast()
        {
            SfxHooks.BossMove(this, "Feast"); // sound
            Use("feast", 16f);
            TickPools();
            if (pools.Count == 0) yield break;
            Pool target = null; float best = -1f;
            foreach (var p in pools)                                   // pool farthest from Ren -> forces the player to chase
            {
                float dd = (p.pos - PlayerPos).magnitude;
                if (dd > best) { best = dd; target = p; }
            }
            yield return MistStep(target.pos + Vector2.down * 0.05f);
            if (hp.IsDead) yield break;
            ai.Desired = Vector2.zero;
            ai.PlayState(ai.HasAnimState("Drink") ? "Drink" : "Cast");
            Hint("Isolde kneels to drink the spilled blood. Tear her away!", 2.5f);
            Announce("Feast", Blood, feastTime);                           // cast bar = time left to interrupt the feast
            var feastGlow = BossJuice.Light(target.pos, Blood, 1.6f, 1f, 0f);
            hitsTaken = 0;
            float t = 0f;
            bool broken = false;
            while (t < feastTime && !hp.IsDead)
            {
                if (hitsTaken >= feastBreakHits) { broken = true; break; }
                hp.Heal(hp.Max * feastPerSecond * lifestealMul * Time.deltaTime);   // 3%/s -> 1,5%/s
                if (feastGlow != null) feastGlow.intensity = 0.8f + 0.4f * Mathf.Sin(t * 8f);
                t += Time.deltaTime;
                yield return null;
            }
            if (feastGlow != null) Destroy(feastGlow.gameObject);
            if (broken) { BossJuice.Clear(hp); BossJuice.Burst("m3x_blood_burst", Pos, 1.4f, Color.white, Layer, Order + 2, 16f); BossJuice.Shake(2); }
            else Fired();
            if (target.vfx != null) target.vfx.Stop();
            pools.Remove(target);
            if (hp.IsDead) yield break;
            if (broken) yield return BreakRoutine(2.6f, "You tear her from the feast!");
            else { Hint("The blood restores her.", 1.6f); ai.PlayState("Idle"); yield return ai.WaitFor(0.3f); }
        }

        // ------------------------------------------------------------------ Mist Step (teleport)
        private IEnumerator MistStep(Vector2 to)
        {
            SfxHooks.BossMove(this, "MistStep"); // sound
            ai.Desired = Vector2.zero;
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.7f, Layer, Order + 1, false, 16f);
            hp.Invulnerable = true;
            yield return Fade(0f, 0.22f);
            ai.TeleportTo(ClampArena(to, 0.9f));
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(1f, 0.2f);
            hp.Invulnerable = false;
        }

        // ------------------------------------------------------------------ Night Flight (coffins)
        // Night Flight = a fast "coffin shell game":
        //   1. the bat swarm flies into EXACTLY ONE intact coffin (clearly visible), its lid shakes, blood seeps out;
        //   2. the intact coffins swap places (phase 2: 4 slow swaps, phase 3: 6 fast swaps) - the player must track it;
        //   3. Ren has guessTime seconds to strike a coffin. Right: her coffin breaks, she is dragged out and kneels 3 s (Break).
        //      Wrong / too slow: she bursts out (knockback + slow wave) and heals wrongHeal.
        private IEnumerator NightFlight()
        {
            SfxHooks.BossMove(this, "NightFlight"); // sound
            Use("flight", 30f);
            var mm = MapMechanics.Instance;
            var intact = coffins.FindAll(c => !c.broken);
            hideIn = intact[Random.Range(0, intact.Count)];
            struckCoffin = null;
            guessOpen = false;
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            Hint("\"Find me, child. If your eyes are quick enough.\"", 2.5f);
            Announce("Night Flight", Blood, 0.45f);
            yield return ai.WaitFor(0.45f);
            Fired();
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.8f, Layer, Order + 1, false, 14f);
            Vector2 from = Pos;
            SetHidden(true);
            StartCoroutine(BatSwarm(from, hideIn.pos, 0.7f, 7));             // bats fly into the RIGHT coffin
            yield return ai.WaitFor(0.75f);
            ai.TeleportTo(hideIn.pos + Vector2.down * 0.35f);
            SpriteVfx.Spawn("m3_blood_mist", hideIn.pos, 0.5f, Layer, Order + 1, false, 16f);
            var seep = SpriteVfx.Spawn("m3_blood_pool", hideIn.pos + Vector2.down * 0.28f, 0.45f, Layer, Order - 3, false, 8f);
            if (seep != null) seep.Looping(1.2f);
            Kick(Vector2.down, 0.03f, 0.15f);
            yield return ai.WaitFor(1.0f);
            if (seep != null) seep.Stop();

            // swap
            int swaps = phase >= 3 ? 6 : 4;
            float dur = phase >= 3 ? 0.36f : 0.5f;
            for (int k = 0; k < swaps && !hp.IsDead; k++)
            {
                int a = Random.Range(0, intact.Count), b = (a + Random.Range(1, intact.Count)) % intact.Count;
                yield return Swap(intact[a], intact[b], dur);
                yield return ai.WaitFor(0.08f);
            }
            ai.TeleportTo(hideIn.pos + Vector2.down * 0.35f);
            Hint("Which coffin? Strike it!", 2f);
            guessOpen = true;
            float end = Time.time + guessTime, nextRattle = 0f;
            while (Time.time < end && struckCoffin == null && !hp.IsDead)
            {
                if (Time.time >= nextRattle)                                    // every lid shakes together: gives nothing away
                {
                    foreach (var c in intact) SpriteVfx.Spawn("m3_ground_burst", c.pos + Vector2.down * 0.2f, 0.35f, Layer, Order + 1, false, 18f);
                    nextRattle = Time.time + 1.0f;
                }
                yield return null;
            }
            guessOpen = false;
            if (hp.IsDead) yield break;
            bool right = struckCoffin == hideIn;
            if (struckCoffin != null && !right && mm != null)                  // wrong coffin: lid pops open, empty (then closes again)
            {
                mm.SetVisible(struckCoffin.name, false);
                mm.SetVisible(struckCoffin.name + "_open", true);
                Hint("Empty!", 1.2f);
                yield return ai.WaitFor(0.6f);
                mm.SetVisible(struckCoffin.name + "_open", false);
                mm.SetVisible(struckCoffin.name, true);
            }
            if (right) BreakCoffin(hideIn, false);                             // right guess: her coffin breaks at once (no heal)
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.8f, Layer, Order + 1, false, 14f);
            SetHidden(false);
            if (right)
            {
                Kick(Vector2.down, 0.06f, 0.25f);
                yield return BreakRoutine(3f, "You drag the Matriarch from her coffin!");
            }
            else
            {
                hp.Heal(hp.Max * wrongHeal);
                ai.PlayState("Cast");
                var w = Wave(Pos, 3.2f, 1.2f, 0f, "m3_wave_blood");             // bursting out: knockback + slow
                w.onHit = _ => { var s = PS; if (s != null) s.Slow(0.6f, 2f, "Blood clings to your legs."); };
                float g = Random.Range(0f, 120f);
                w.gapAngles = new[] { g, g + 120f, g + 240f };
                w.gapWidth = 40f;
                w.knockback = 3f;
                Hint(struckCoffin == null ? "Too slow. She rises, refreshed." : "Wrong coffin. She feeds and rises.", 2f);
                yield return ai.WaitFor(0.6f);
                ai.PlayState("Idle");
            }
            struckCoffin = null;
            hideIn = null;
            ResetTrackers();
        }

        /// Two coffins swap along two opposite arcs (every coffin sprite state moves with them).
        private IEnumerator Swap(Coffin a, Coffin b, float dur)
        {
            Vector2 pa = a.pos, pb = b.pos;
            var startA = new List<Vector3>(); var startB = new List<Vector3>();
            foreach (var g in a.gos) startA.Add(g != null ? g.transform.position : Vector3.zero);
            foreach (var g in b.gos) startB.Add(g != null ? g.transform.position : Vector3.zero);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)), lift = Mathf.Sin(e * Mathf.PI) * 0.45f;
                Vector2 da = Vector2.Lerp(Vector2.zero, pb - pa, e) + Vector2.up * lift;
                Vector2 db = Vector2.Lerp(Vector2.zero, pa - pb, e) - Vector2.up * lift;
                for (int i = 0; i < a.gos.Count; i++) if (a.gos[i] != null) a.gos[i].transform.position = startA[i] + (Vector3)da;
                for (int i = 0; i < b.gos.Count; i++) if (b.gos[i] != null) b.gos[i].transform.position = startB[i] + (Vector3)db;
                yield return null;
            }
            a.pos = pb; b.pos = pa;
        }

        /// Rite of Repose: every 30 s rebuilds ONE broken coffin (the one farthest from Ren).
        private IEnumerator RiteOfRepose()
        {
            ready["rite"] = Time.time + restoreEvery;                          // exactly 30 s, not shortened in phase 3
            Coffin pick = null; float best = -1f;
            foreach (var c in coffins) if (c.broken) { float d = (c.pos - PlayerPos).magnitude; if (d > best) { best = d; pick = c; } }
            if (pick == null) yield break;
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Hint("\"Rise again, my resting place.\"", 2.5f);
            SpriteVfx.Spawn("m3_blood_pool", pick.pos + Vector2.down * 0.25f, 0.6f, Layer, Order - 3, false, 10f);
            Announce("Rite of Repose", Blood, 0.7f);
            ChargeFx(Blood, 0.7f);
            var riteGlow = BossJuice.Light(pick.pos, Blood, 1.2f, 1.2f, 1.1f);
            yield return ai.WaitFor(0.7f);
            Fired();
            BossJuice.Burst("m3x_blood_burst", pick.pos, 1.1f, Color.white, Layer, Order + 2, 16f);
            var mm = MapMechanics.Instance;
            SpriteVfx.Spawn("m3_blood_mist", pick.pos, 0.6f, Layer, Order + 1, false, 16f);
            if (mm != null) { mm.SetVisible(pick.name + "_open", false); mm.SetVisible(pick.name + "_broken", false); mm.SetVisible(pick.name, true); }
            pick.broken = false; pick.hits = 0;
            UpdateCoffinState();
            yield return ai.WaitFor(0.3f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Sanguine Tide (pha 3)
        private IEnumerator SanguineTide()
        {
            Use("tide", 12f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Announce("Sanguine Tide", Blood, 0.5f);
            yield return Telegraph(0.5f, Blood);
            if (hp.IsDead) yield break;
            Fired();
            Boom(CastPoint, Blood, 1.2f, 3);
            // blood pools spray spikes (Hemorrhage)
            var spike = SpriteVfx.Load("m3_root_spike");
            foreach (var p in pools)
            {
                var g = GroundStrike.Spawn(p.pos, 0.75f, 0.48f, ai.damage * 0.8f, spike, 2, 14f, Order + 1, DarkBlood);
                g.impactSfx = "blood_spike"; // sound
                g.bodyScale = 0.8f;
                g.bodyColor = new Color(1f, 0.45f, 0.5f);                  // blood spikes (red-tinted root spike image) + light flash
                g.impactLight = 1.3f; g.impactShake = 1;
            }
            float g0 = Random.Range(0f, 120f);
            for (int i = 0; i < 2 && !hp.IsDead; i++)
            {
                var w = Wave(Pos, 3.4f, 1.6f, 0f, "m3_wave_blood");      // wide wave: no HP loss, slows 45%
                w.knockback = 1.2f;
                w.onHit = _ => { var s = PS; if (s != null) s.Slow(0.55f, 2.5f, "The Sanguine Tide drags at you."); };
                float g = g0 + i * 25f;
                w.gapAngles = new[] { g, g + 120f, g + 240f };
                w.gapWidth = 42f;
                Kick(Vector2.down, 0.03f, 0.15f);
                yield return ai.WaitFor(0.6f);
            }
            ai.PlayState("Idle");
        }
    }
}
