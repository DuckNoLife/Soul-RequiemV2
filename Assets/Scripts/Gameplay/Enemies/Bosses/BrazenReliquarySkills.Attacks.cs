using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SKILLS of BrazenReliquarySkills (one coroutine per skill). Settings / skill choice: BrazenReliquarySkills.cs.
    /// </summary>
    public sealed partial class BrazenReliquarySkills
    {
        protected override IEnumerator OnPhase(int p)
        {
            if (p >= 2 && !unbound) yield return Unbind();
            if (p == 3)
            {
                Hint("DOUBLE DOWN - the reel spins twice now!", 2.6f);
                yield return ai.Flash(0.8f, Molten);
                if (ai.Body != null) ai.Body.color = new Color(1f, 0.8f, 0.72f);
            }
        }

        // ------------------------------------------------------------------ brain
        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (Broken || transforming) { yield return null; yield break; }
            ai.FaceX((PlayerPos - Pos).x);
            if (!unbound) { yield return ThinkWarded(); yield break; }
            float d = (PlayerPos - Pos).magnitude;
            if (Time.time >= nextReel && reel != null) { yield return Jackpot(); yield break; }
            if (heat >= heatMax) { yield return Overheat(); yield break; }
            if (ai.GapToPlayer() <= meleeRange + 0.35f && Ready("bite")) { yield return Bite(); yield break; }
            if (ReadyG("ram") && d > 1.8f && d < 4.2f && Random.value < 0.45f) { yield return Ram(); yield break; }
            if (ReadyG("grape") && d > 1.5f) { yield return Grapeshot(); yield break; }
            yield return Stalk(0.6f, 1f);
        }

        /// <summary>On the pedestal it ONLY fires mortars from the sky (Mortar + Interdiction blocking the path to the rack);
        /// Grapeshot / Turret Sweep are not used here (phase 2 / kept for later).</summary>
        private IEnumerator ThinkWarded()
        {
            if (interdictPending) { yield return Interdiction(); yield break; }
            if (heat >= heatMax) { yield return Overheat(); yield break; }
            if (ReadyG("mortar")) { yield return Mortar(); yield break; }
            yield return ai.WaitFor(0.3f);
        }

        protected override IEnumerator Reposition()
        {
            ai.Desired = Vector2.zero;
            if (!unbound) { yield return ai.WaitFor(0.2f); yield break; }
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Brass);
            if (hp.IsDead) yield break;
            Muzzle(1.4f);
            Sfx.Play("duo_cannon", Pos);
            var w = Shockwave.Spawn(Pos, 1.5f, 0.45f, 8f, Order - 1, new Color(1f, 0.7f, 0.4f, 0.9f));
            w.knockback = 3.4f;
            Kick(Vector2.down, 0.04f, 0.2f);
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ PHASE 1: turret
        private IEnumerator Grapeshot()
        {
            Use("grape", unbound ? 4.5f : 3.4f);                       // phase 2 fires sparser volleys
            heat++;
            ai.Desired = Vector2.zero;
            ai.PlayState("Fire");
            yield return Telegraph(0.35f, Brass);
            if (hp.IsDead || ai.Player == null) yield break;
            int n = phase == 1 ? 5 : 7;
            int volleys = phase == 1 ? 1 : 2;
            for (int v = 0; v < volleys && !hp.IsDead && ai.Player != null; v++)
            {
                if (v > 0) { ai.PlayState("Fire"); yield return ai.WaitFor(0.35f); }
                Vector2 aim = (PlayerPos - MuzzlePos).normalized;
                float baseA = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + (v % 2 == 1 ? 6f : 0f);
                for (int k = 0; k < n; k++) ai.ShootProjectile(Dir(baseA + (k - (n - 1) * 0.5f) * 12f), shellSpeed, grapeDamage, 3.2f);
                Muzzle(1.5f);
                Sfx.Play("duo_cannon", Pos);
                Kick(-aim, 0.03f, 0.15f);
            }
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }

        private IEnumerator Mortar()
        {
            Use("mortar", phase == 1 ? 6f : 5f);
            heat++;
            ai.PlayState("Fire");
            yield return Telegraph(0.4f, Brass);
            if (hp.IsDead || ai.Player == null) yield break;
            Sfx.Play("duo_mortar", Pos);
            Muzzle(1.2f);
            int n = phase == 1 ? 3 : 4;
            Vector2 vel = PlayerVel();
            for (int i = 0; i < n && !hp.IsDead && ai.Player != null; i++)
            {
                Vector2 at = i == 0 ? PlayerPos : i == 1 ? PlayerPos + vel * 0.9f : PlayerPos + Random.insideUnitCircle * 1.1f;
                Shell(ClampArena(at, 0.95f), 1.05f, 0.42f, mortarDamage, 4f, Brass);
                yield return ai.WaitFor(0.18f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        private IEnumerator AfterShell(Vector2 at, float delay, float burnSeconds)
        {
            yield return new WaitForSeconds(delay);
            if (hp == null || hp.IsDead) yield break;
            if (burnSeconds > 0f)
                BurnPatch.Spawn(at, "duo_soulfire", 0.75f, burnSeconds, Layer, Order - 2, 0.34f, 4f, new Color(1f, 0.55f, 0.2f), new Color(1f, 0.6f, 0.3f));
            // enemies do NOT damage each other -> a mortar landing on the Champion no longer hurts / stuns him.
        }

        /// <summary>The Champion just called a rack: 3 mortars land along Ren -> rack, the fourth leads Ren.</summary>
        private IEnumerator Interdiction()
        {
            interdictPending = false;
            Use("mortar", 3.5f);
            ai.PlayState("Fire");
            yield return Telegraph(0.25f, Brass);
            if (hp.IsDead || ai.Player == null) yield break;
            Sfx.Play("duo_mortar", Pos);
            Muzzle(1.2f);
            Vector2 from = PlayerPos;
            float[] ts = { 0.3f, 0.55f, 0.8f };
            foreach (float t in ts) { Shell(ClampArena(Vector2.Lerp(from, interdictAt, t), 0.97f), 0.95f, 0.45f, mortarDamage * 0.9f, 2.5f, Brass); yield return ai.WaitFor(0.08f); }
            Shell(ClampArena(from + PlayerVel() * 0.5f, 0.97f), 1.0f, 0.45f, mortarDamage * 0.9f, 0f, Brass);
            yield return ai.WaitFor(0.3f);
            ai.PlayState("Idle");
        }

        /// <summary>Champion under 50%: the barrel sweeps a 100° arc firing 10 shots in a row.</summary>
        private IEnumerator TurretSweep()
        {
            Use("sweep", 7.5f);
            heat++;
            ai.PlayState("Fire");
            Vector2 aim = (PlayerPos - MuzzlePos).normalized;
            float a0 = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + (Random.value < 0.5f ? -50f : 50f);
            float sweep = a0 > Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg ? -100f : 100f;
            var warn = Ray("m3_ray_warn", MuzzlePos, MuzzlePos + Dir(a0) * 3f, 0.62f, 1.4f, Order - 1, 12f, 0.5f);
            if (warn != null) warn.Tint(Brass);
            yield return Telegraph(0.5f, Brass);
            for (int i = 0; i < 10 && !hp.IsDead; i++)
            {
                ai.ShootProjectile(Dir(a0 + sweep * i / 9f), shellSpeed * 1.1f, grapeDamage, 3.2f);
                if (i % 3 == 0) { Muzzle(1.1f); Sfx.Play("duo_cannon", Pos, 0.6f); }
                yield return ai.WaitFor(0.11f);
            }
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
        }

        private IEnumerator Overheat()
        {
            heat = 0;
            venting = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Vent");
            Sfx.Play("steam_vent", Pos);
            Hint(unbound ? "The Engine overheats - strike it now!" : "The Engine vents steam - its guns fall silent.", 2f);
            yield return ai.Flash(phase == 3 ? 2.0f : 2.4f, Molten);
            venting = false;
            ai.PlayState("Idle");
        }

        private IEnumerator Unbind()
        {
            unbound = true; transforming = true; lowering = true;
            ai.Desired = Vector2.zero;
            ai.CutsceneActor = true;
            DuoBullet.ClearAll();
            if (tether != null) { tether.Stop(); tether = null; }
            var playerHp = ai.Player != null ? ai.Player.GetComponent<Health>() : null;
            if (playerHp != null) playerHp.Invulnerable = true;
            CutsceneState.Begin();
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            bool done = false;
            StartCoroutine(HoldInput(() => done));
            if (cam != null) cam.Focus(baseHome + Vector2.up * 0.9f, size * 0.8f);
            Hint("The soul-tether snaps. With nothing holding it up, the turret begins to sink...", 2.6f);
            yield return new WaitForSeconds(0.7f);
            // 1) lower halfway
            Sfx.Play("lift_grind", baseHome);
            SpriteVfx.Spawn("lg_dust", baseHome + Vector2.left * 0.5f, 1.2f, Layer, Order + 1, false, 12f, false);
            SpriteVfx.Spawn("lg_dust", baseHome + Vector2.right * 0.5f, 1.2f, Layer, Order + 1, true, 12f, false);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime) { PlaceLowered(Mathf.SmoothStep(0f, Lift * 0.45f, t / 1.1f)); yield return null; }
            // 2) jam
            Sfx.Play("lift_jam", baseHome);
            Kick(Vector2.down, 0.05f, 0.2f);
            ai.PlayState("Idle");
            yield return new WaitForSeconds(0.45f);
            var b1 = SpeechBubble.Show(Pos + Vector2.up * 1.2f, "...?", 1.3f, new Color(1f, 0.8f, 0.45f), "The Brazen Reliquary");
            if (b1 != null) b1.Follow(transform);
            ai.FaceX(-1f); yield return new WaitForSeconds(0.4f);
            ai.FaceX(1f); yield return new WaitForSeconds(0.4f);
            ai.FaceX(-1f); yield return new WaitForSeconds(0.35f);
            // 3) hop twice
            float drop = Lift * 0.45f;
            for (int hop = 0; hop < 2; hop++)
            {
                Sfx.Play("spring_boing", Pos);
                for (float t = 0f; t < 0.32f; t += Time.deltaTime)
                {
                    float u = t / 0.32f;
                    ai.TeleportTo(baseHome + Vector2.up * (Lift - drop + 0.35f * 4f * u * (1f - u)));
                    yield return null;
                }
                PlaceLowered(drop);
                Kick(Vector2.down, 0.03f, 0.12f);
                yield return new WaitForSeconds(0.18f);
            }
            // 4) pedestal collapses, cannon faceplants
            Sfx.Play("lift_jam", baseHome, 0.7f);
            float d0 = drop;
            for (float t = 0f; t < 0.28f; t += Time.deltaTime) { PlaceLowered(Mathf.Lerp(d0, Lift + 0.1f, (t / 0.28f) * (t / 0.28f))); yield return null; }
            PlaceLowered(Lift);
            if (pedestal != null) pedestal.enabled = false;
            Sfx.Play("duo_engine_drop", baseHome);
            Kick(Vector2.down, 0.14f, 0.4f);
            var psr = ai.Player != null ? ai.Player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) CutsceneFx.Burst(this, new Vector3(baseHome.x, baseHome.y + 0.2f, 0f), 0.8f, 0.4f, 8, 0, psr.sortingLayerID, psr.sortingOrder + 1);
            StartCoroutine(CoinSpill(8));
            if (ai.Body != null && raisedOrder != 0) { ai.Body.sortingOrder -= raisedOrder; raisedOrder = 0; }
            ai.PlayState("Recover");
            yield return new WaitForSeconds(0.5f);
            ai.PlayState("Vent");
            Sfx.Play("steam_vent", Pos);
            var b2 = SpeechBubble.Show(Pos + Vector2.up * 1.2f, "*cough* *clank*", 1.3f, new Color(1f, 0.8f, 0.45f), "The Brazen Reliquary");
            if (b2 != null) b2.Follow(transform);
            yield return new WaitForSeconds(1.2f);
            // 5) reel pops up
            AttachReel();
            Sfx.Play("reel_stop", Pos);
            StartCoroutine(reel.Spin(7));
            var b3 = SpeechBubble.Show(Pos + Vector2.up * 1.5f, "INSERT SOUL.", 1.8f, new Color(1f, 0.85f, 0.3f), "The Brazen Reliquary");
            if (b3 != null) b3.Follow(transform);
            yield return new WaitForSeconds(1.7f);
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Fire");
            Muzzle(1.7f);
            Sfx.Play("duo_cannon", Pos);
            Kick(Vector2.down, 0.08f, 0.25f);
            yield return new WaitForSeconds(0.5f);
            // back to the fight
            if (pedCol != null) pedCol.enabled = false;
            if (bodyCols != null) foreach (var c in bodyCols) if (c != null) c.enabled = true;
            home = baseHome;
            lowering = false;
            done = true;
            if (cam != null) cam.Release();
            CutsceneState.End();
            if (playerHp != null) playerHp.Invulnerable = false;
            ai.CutsceneActor = false;
            Hint("THE JACKPOT ENGINE - strike it MID-SPIN to jolt the reel. Aim for BUST!", 3.6f);
            reelHinted = true;
            nextReel = Time.time + 2f;
            transforming = false;
            ai.PlayState("Idle");
            ResetTrackers();
        }

        private static IEnumerator HoldInput(System.Func<bool> done)
        {
            while (!done()) { GameInput.Blocked = true; yield return null; }
            GameInput.Blocked = false;
        }

        /// <summary>Coins scattering when the cannon faceplants (decoration, no damage).</summary>
        private IEnumerator CoinSpill(int n)
        {
            var coin = Icon("coin");
            var list = new List<(SpriteRenderer sr, Vector2 from, Vector2 to)>();
            for (int i = 0; i < n; i++)
            {
                var s = new GameObject("Spilled Coin").AddComponent<SpriteRenderer>();
                s.sprite = coin; s.sortingLayerID = Layer; s.sortingOrder = Order + 1;
                s.transform.localScale = Vector3.one * 0.35f;
                list.Add((s, baseHome + Vector2.up * 0.3f, baseHome + Random.insideUnitCircle * 1.1f));
            }
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float u = t / 0.6f;
                foreach (var c in list) if (c.sr != null) c.sr.transform.position = Vector2.Lerp(c.from, c.to, u) + Vector2.up * (0.6f * 4f * u * (1f - u));
                yield return null;
            }
            Sfx.Play("coin_spray", baseHome, 0.6f);
            yield return new WaitForSeconds(2.5f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                foreach (var c in list) if (c.sr != null) c.sr.color = new Color(1f, 1f, 1f, 1f - t / 0.6f);
                yield return null;
            }
            foreach (var c in list) if (c.sr != null) Destroy(c.sr.gameObject);
        }

        // ------------------------------------------------------------------ PHASE 2: reel
        private IEnumerator Jackpot()
        {
            nextReel = Time.time + (phase >= 3 ? 9f : 11.5f) * (Time.time < overclockUntil ? 0.8f : 1f);   // 8,5/6,5 -> 11,5/9 s
            ai.Desired = Vector2.zero;
            ai.PlayState("Idle");
            if (!reelHinted) { reelHinted = true; Hint("Strike the Engine while the reel spins to jolt it a notch!", 2.6f); }
            yield return SpinAndFire(Random.Range(12, 19));
            if (phase >= 3 && Alive && !Broken && reel.Current != JackpotReel.Bust)
            {
                yield return ai.WaitFor(0.35f);
                yield return SpinAndFire(Random.Range(6, 10));
            }
        }

        private IEnumerator SpinAndFire(int steps)
        {
            var spin = StartCoroutine(reel.Spin(steps));
            float chatter = Time.time + 0.6f;
            while (reel.Spinning && Alive)
            {
                ai.Desired = Vector2.zero;
                if (Time.time >= chatter) { chatter = Time.time + 1.6f; CoinRing(5, 1.7f); }   // sparser
                yield return null;
            }
            if (!Alive) yield break;
            yield return ai.WaitFor(0.25f);
            int s = reel.Current;
            switch (s)
            {
                case JackpotReel.Goblin: yield return RaiderBarrage(); break;
                case JackpotReel.Bomb: yield return PowderParty(); break;
                case JackpotReel.Skull: yield return GhostChorus(); break;
                case JackpotReel.Coin: yield return JackpotSpiral(); break;
                case JackpotReel.Bolt: yield return StormCoil(); break;
                case JackpotReel.Anvil: yield return DeadWeight(); break;
                case JackpotReel.Heart: yield return Overclock(); break;
                default: yield return Backfire(); break;
            }
        }

        private IEnumerator RaiderBarrage()
        {
            Hint("GOBLIN! The Engine is firing... goblins?!", 2f);
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Brass);
            var head = Icon("goblin");
            int n = phase >= 3 ? 4 : 3;
            goblins.RemoveAll(g => g == null || g.Health == null || g.Health.IsDead);
            for (int i = 0; i < n && Alive; i++)
            {
                Vector2 at = ClampArena(PlayerPos + Random.insideUnitCircle.normalized * Random.Range(1.0f, 1.8f), 0.9f);
                var g = GroundStrike.Spawn(at, 0.9f, 0.4f, 8f, SpriteVfx.Load("lg_dust"), 0, 16f, Order + 1, new Color(0.5f, 1f, 0.4f));
                g.impactSfx = "none";
                if (head != null) { g.shell = head; g.shellFrom = MuzzlePos + Vector2.up * 0.2f; g.shellArc = 1.7f; }
                Muzzle(1.2f);
                Sfx.Play("goblin_launch", Pos);
                StartCoroutine(LandGoblin(at, 0.9f));
                yield return ai.WaitFor(0.25f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        private IEnumerator LandGoblin(Vector2 at, float delay)
        {
            yield return new WaitForSeconds(delay);
            var dir = MapEncounterDirector.Instance;
            if (dir == null || !Alive || goblins.Count >= 5) yield break;
            var e = dir.SpawnSummon("Goblin_Torch", at, ai);
            if (e != null) goblins.Add(e);
        }

        private IEnumerator PowderParty()
        {
            Hint("BOMB! Powder rains down - keep moving!", 2f);
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Brass);
            var bomb = Icon("bomb");
            int n = phase >= 3 ? 8 : 6;
            float a0 = Random.Range(0f, 360f);
            for (int i = 0; i < n && Alive; i++)
            {
                Vector2 at = ClampArena(PlayerPos + Dir(a0 + i * 360f / n) * Random.Range(0.6f, 1.6f), 0.95f);
                var g = GroundStrike.Spawn(at, 0.7f, 0.2f, 0f, null, 0, 14f, Order + 1, Brass);
                g.impactSfx = "none";
                if (bomb != null) { g.shell = bomb; g.shellFrom = MuzzlePos + Vector2.up * 0.2f; g.shellArc = 1.5f; }
                StartCoroutine(BombFuse(at, 0.7f, 1.1f + i * 0.18f));
                if (i % 2 == 0) { Muzzle(1f); Sfx.Play("duo_mortar", Pos, 0.6f); }
                yield return ai.WaitFor(0.1f);
            }
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        private IEnumerator BombFuse(Vector2 at, float land, float fuse)
        {
            yield return new WaitForSeconds(land);
            if (!Alive) yield break;
            var sr = new GameObject("Bomb").AddComponent<SpriteRenderer>();
            sr.sprite = Icon("bomb");
            sr.sortingLayerID = Layer; sr.sortingOrder = Order;
            sr.transform.position = at;
            sr.transform.localScale = Vector3.one * 0.8f;
            var warn = GroundStrike.Spawn(at, fuse, 0.85f, 0f, null, 0, 14f, Order - 1, new Color(1f, 0.3f, 0.15f));
            warn.impactSfx = "none";
            Sfx.Play("keg_fuse", at, 0.5f);
            for (float t = 0f; t < fuse && sr != null; t += Time.deltaTime)
            {
                sr.color = Mathf.Repeat(t * (4f + t * 8f), 1f) < 0.5f ? new Color(1f, 0.45f, 0.35f) : Color.white;
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
            PowderKegMechanism.Blast(at, 0.85f, 18f, Layer, Order);
        }

        private IEnumerator GhostChorus()
        {
            Hint("SKULL! Ghost skulls hunt you - a touch seals your mantras and dash! Cut them down!", 2.4f);
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Soul);
            Sfx.Play("skull_wail", Pos);
            var skull = Icon("skull");
            int n = phase >= 3 ? 8 : 6;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Dir(i * 360f / n);
                // ghost skulls deal NO damage - touching Ren: seals Mantra + locks roll for 10 s
                var b = DuoBullet.Spawn(skull, Pos + Vector2.up * 0.45f + d * 0.4f, d * 1.4f, 0f, 0.18f, 6f, Layer, Order + 2, 0.55f);
                b.homing = 110f; b.maxSpeed = 1.9f; b.killable = true;
                b.onHitPlayer = SkullCurse;
            }
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
        }

        private IEnumerator JackpotSpiral()
        {
            Hint("COIN! A spiral of brass - circle with it!", 2f);
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Brass);
            var coin = Icon("coin");
            int arms = phase >= 3 ? 4 : 3;
            float a = Random.Range(0f, 360f), dirSign = Random.value < 0.5f ? 1f : -1f, nextSfx = 0f;
            for (float t = 0f; t < 2.2f && Alive && !Broken; t += 0.11f)
            {
                for (int k = 0; k < arms; k++)
                {
                    var b = DuoBullet.Spawn(coin, Pos + Vector2.up * 0.4f, Dir(a + k * 360f / arms) * 2.3f, 8f, 0.12f, 4f, Layer, Order + 2, 0.4f);
                    b.spinDeg = 360f;
                }
                a += 140f * 0.11f * dirSign;
                if (t >= nextSfx) { nextSfx = t + 0.5f; Sfx.Play("coin_spray", Pos, 0.6f); }
                yield return ai.WaitFor(0.11f);
            }
            ai.PlayState("Idle");
        }

        private IEnumerator StormCoil()
        {
            Hint("BOLT! Lightning follows your steps!", 2f);
            ai.PlayState("Fire");
            yield return Telegraph(0.25f, new Color(0.6f, 0.85f, 1f));
            var frames = SpriteVfx.Load("duo_bolt");
            int n = phase >= 3 ? 8 : 6;
            for (int i = 0; i < n && Alive && ai.Player != null; i++)
            {
                Vector2 at = ClampArena(PlayerPos + PlayerVel() * 0.35f, 0.97f);
                var g = GroundStrike.Spawn(at, 0.7f, 0.48f, 18f, frames, 3, 14f, Order + 1, new Color(0.55f, 0.85f, 1f));
                g.impactSfx = "bolt_strike";
                yield return ai.WaitFor(0.38f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        private IEnumerator DeadWeight()
        {
            Hint("ANVIL! Its shadow is on you - get out from under it!", 2f);
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Brass);
            var sh = new GameObject("Anvil Shadow").AddComponent<SpriteRenderer>();
            sh.sprite = Shadow();
            sh.sortingLayerID = Layer; sh.sortingOrder = Order - 2;
            Vector2 at = PlayerPos;
            float grow = 0f;
            for (float t = 0f; t < 1.4f && Alive && ai.Player != null; t += Time.deltaTime)
            {
                at = Vector2.MoveTowards(at, PlayerPos, 3.2f * Time.deltaTime);
                grow = Mathf.Min(1f, grow + Time.deltaTime);
                sh.transform.position = at;
                sh.transform.localScale = Vector3.one * (0.8f + grow * 1.1f);
                yield return null;
            }
            var anvil = Icon("anvil");
            var g = GroundStrike.Spawn(at, 0.5f, 0.85f, 30f, SpriteVfx.Load("m3_ground_burst"), 1, 16f, Order + 1, new Color(0.9f, 0.85f, 0.75f));
            g.impactSfx = "anvil_drop";
            g.knockback = 3f;
            if (anvil != null) { g.shell = anvil; g.shellFrom = at + Vector2.up * 4f; g.shellArc = 0.05f; g.shellScale = 2.2f; }
            yield return new WaitForSeconds(0.5f);
            Destroy(sh.gameObject);
            if (!Alive) yield break;
            Kick(Vector2.down, 0.1f, 0.3f);
            var w = Wave(at, 1.6f, 0.45f, 12f, "m3_wave_hymn");
            w.knockback = 2.4f;
            var rest = new GameObject("Anvil").AddComponent<SpriteRenderer>();
            rest.sprite = anvil; rest.sortingLayerID = Layer; rest.sortingOrder = Order;
            rest.transform.position = at; rest.transform.localScale = Vector3.one * 2.2f;
            Destroy(rest.gameObject, 2.5f);
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        private IEnumerator Overclock()
        {
            Hint("HEART! The Engine overclocks and mends itself!", 2.2f);
            hp.Heal(hp.Max * 0.07f);
            overclockUntil = Time.time + 8f;
            ai.PlayState("Vent");
            Sfx.Play("steam_vent", Pos);
            yield return ai.Flash(0.8f, new Color(1f, 0.35f, 0.35f));
            ai.PlayState("Idle");
        }

        private IEnumerator Backfire()
        {
            Sfx.Play("engine_backfire", Pos);
            SpriteVfx.Spawn("duo_keg_blast", Pos + Vector2.up * 0.4f, 1.3f, Layer, Order + 3, false, 16f, false);
            Kick(Vector2.down, 0.08f, 0.3f);
            hp.TakeDamage(hp.Max * 0.06f);
            yield return BreakRoutine(3.2f, "BUST! The Engine backfires and seizes up - strike now!");
        }

        // ------------------------------------------------------------------ PHASE 2: melee
        private IEnumerator Bite()
        {
            Use("bite", phase >= 3 ? 1.6f : 2.2f);
            int bites = phase >= 3 ? 2 : 1;
            for (int b = 0; b < bites && Alive && !Broken && ai.Player != null; b++)
            {
                Vector2 dir = (PlayerPos - Pos).normalized;
                ai.FaceX(dir.x);
                ai.PlayState("Prepare");
                yield return Telegraph(b == 0 ? 0.38f : 0.25f, Molten);
                if (!Alive || Broken) yield break;
                ai.PlayState(ai.HasAnimState("Bite") ? "Bite" : "Fire");
                bool hit = false;
                for (float t = 0f; t < 0.42f && Alive && !Broken; t += Time.deltaTime)
                {
                    ai.Desired = t < 0.25f ? dir * 2.8f : Vector2.zero;
                    if (!hit && t >= 0.24f)
                    {
                        hit = true;
                        Sfx.Play("duo_punch", Pos);
                        Vector2 jaw = Pos + dir * 0.5f + Vector2.up * 0.2f;
                        if (((Vector2)PlayerPos + Vector2.up * 0.2f - jaw).magnitude < 0.75f) ai.Hurt(biteDamage, Pos, 3.2f);
                    }
                    yield return null;
                }
                ai.Desired = Vector2.zero;
            }
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
        }

        private IEnumerator Ram()
        {
            Use("ram", phase >= 3 ? 6f : 8f);
            heat++;
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            Sfx.Play("ram_charge", Pos);
            // charge length = to Ren + 1.5 units (3..5), stops before obstacles; lane as wide as the brass cannon body
            float reach = DashTelegraph.Reach(Pos, dir, DashTelegraph.WantReach(Pos, PlayerPos, 1.5f, 3f, 5f), 0.35f, ai.BodyCollider, ai.Player);
            var tg = DashTelegraph.Spawn(Pos, dir, reach, DashTelegraph.WidthFor(ai.BodyCollider, 0.85f), Brass, 0.8f, Layer, Order - 1);
            yield return Telegraph(0.8f, Brass);
            tg.Release();
            if (hp.IsDead) yield break;
            ai.PlayState("Dash");
            bool hit = false;
            Vector2 last = Pos;
            float stuck = 0f;
            for (float t = 0f; t < reach / ramSpeed + 0.05f && !hp.IsDead; t += Time.deltaTime)
            {
                ai.Desired = dir * ramSpeed;
                if (!hit && ai.GapToPlayer() <= 0.1f) { ai.Hurt(ramDamage, Pos, 4f); hit = true; }
                stuck = (Pos - last).magnitude < ramSpeed * Time.deltaTime * 0.2f ? stuck + Time.deltaTime : 0f;
                last = Pos;
                if (stuck > 0.12f || EllipseR(Pos) > 0.98f) break;
                yield return null;
            }
            ai.Desired = Vector2.zero;
            ai.PlayState("Recover");
            if (stuck > 0.12f) { Sfx.Play("lg_wall_crash", Pos); Kick(-dir, 0.05f, 0.25f); }
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
        }
    }
}
