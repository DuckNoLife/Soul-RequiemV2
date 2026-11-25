using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// LANTERN KNIGHT - commander of the Black Lantern, miniboss guarding the bell tower (areas V5b, V9).
    /// He carries no lantern: his "lantern" is his oath. Every skill has its own animation and a readable warning.
    /// He has no basic slash: when close without a skill ready he circles Ren slowly, always facing him.
    ///   Charge (Dash)    : blue line shows the direction for 0.7 s, then a straight dash. Phase 2: 3 dashes, re-aiming each time.
    ///   Judgement (Slam) : raises the sword and brings it down - red ring at the impact point, then a shock wave spreads out (ground_impact VFX like
    ///                      the player's F skill, closed ring of radius waveRadius; dodge by rolling or staying out of range). Stun only, no damage.
    ///                      Phase 2: two waves back to back.
    ///   Vow (Vow)        : plants the sword for a 2 s oath, sigil under his feet. Oath completed: allies within the radius get a shield (take only 30% damage) for 6 s.
    ///                      Hitting him 3 times during the oath breaks it and stuns him 2.2 s. This is the counterattack window.
    ///   Lightless        : on entering phase 2 (50% HP) he snuffs out the light: the whole area goes dark (only Ichor lamps, tower lamps and Ren's light remain).
    /// </summary>
    public class LanternKnightSkills : MonoBehaviour, IEnemySkills
    {
        private const float StrafeSpeed = 0.45f;     // circling Ren: walk speed multiplier (and step rhythm)

        [Header("Charge")]
        public float chargeWindup = 0.7f, chargeSpeed = 4.2f, chargeTime = 0.55f, chargeEvery = 5f;
        [Header("Judgement")]
        public float slamWindup = 0.75f, slamEvery = 8f, slamRadius = 0.55f, waveRadius = 1.8f, waveTime = 1.25f, gapWidth = 46f;
        [Tooltip("Frame rate of the ground_impact wave (lower = slower spread, easier to read than the player's F skill).")]
        public float waveFps = 10f;
        [Header("Vow")]
        public float vowTime = 2.0f, vowEvery = 15f, wardRadius = 3.6f, wardTime = 6f, wardMul = 0.3f;
        public int vowBreakHits = 3;
        [Header("Lightless")]
        public float darkness = 0.45f;

        private EnemyAI ai;
        private bool engaged, lightlessDone;
        private float nextCharge, nextSlam, nextVow;
        private int hitsTaken;
        private SpriteRenderer chargeLine;
        private static Sprite pixel;
        private static readonly Color Cold = new Color(0.62f, 0.78f, 1f, 1f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f, 1f);         // oath
        private static readonly Color Verdict = new Color(1f, 0.35f, 0.3f, 1f);        // judgement

        private float VfxScale => ai.Vfx != null ? ai.Vfx.scale : 0.77f;
        private int Layer => ai.SortingLayer;
        private int Order => ai.SortingOrder;

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) { h.Damaged += (_, __) => hitsTaken++; h.Died += _ => Restore(); }
        }

        private void OnDisable() { Restore(); KillLine(); if (ai != null) BossJuice.Clear(ai.Health); }

        private void Restore()
        {
            if (lightlessDone) ZoneAmbience.DimMul = 1f;
        }

        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (ai.Player == null) yield break;
            if (!engaged)
            {
                engaged = true;
                nextCharge = Time.time + 2.5f;
                nextSlam = Time.time + 5f;
                nextVow = Time.time + 7f;
            }
            ai.LookAtPlayer = false;
            ai.MoveAnimSpeed = 1f;
            if (ai.InPhase2 && !lightlessDone) { yield return Lightless(); yield break; }

            Vector2 to = (Vector2)ai.Player.position - (Vector2)transform.position;
            float d = to.magnitude, gap = ai.GapToPlayer();

            if (Time.time >= nextVow && AlliesNear() > 0) { yield return Vow(); yield break; }
            if (Time.time >= nextSlam && d < 3.2f) { yield return Judgement(); yield break; }
            if (Time.time >= nextCharge && d > 1.3f && d < 5.5f) { yield return Charge(); yield break; }
            // no basic slash - the Knight only uses skills. Close with no skill ready: guard stance, circle Ren slowly.
            if (d < 1.4f)
            {
                // side-steps but the FACE always points at Ren (turning with the step direction made him show his back and flip back and forth),
                // and the step animation is slowed to the real speed (a full walk cycle at 45% speed looked like sliding)
                ai.LookAtPlayer = true;
                ai.Desired = Vector2.Perpendicular(to.normalized) * ai.moveSpeed * StrafeSpeed * (Mathf.Sin(Time.time * 0.7f) >= 0f ? 1f : -1f);
                ai.MoveAnimSpeed = StrafeSpeed;
                ai.PlayState("Move", false);
                yield break;
            }

            ai.LookAtPlayer = false;
            ai.MoveAnimSpeed = 1f;
            ai.Desired = to.normalized * ai.moveSpeed;
            ai.PlayState("Move", false);
        }

        // ------------------------------------------------------------------ Charge
        private IEnumerator Charge()
        {
            SfxHooks.BossMove(this, "Charge"); // sound
            int n = ai.InPhase2 ? 3 : 1;
            for (int i = 0; i < n && !ai.Health.IsDead && ai.Player != null; i++)
            {
                float wind = i == 0 ? chargeWindup : chargeWindup * 0.55f;
                ai.Desired = Vector2.zero;
                Vector2 dir = ai.DirToPlayer.normalized;
                ai.FaceX(dir.x);
                ai.PlayState(ai.HasAnimState("ChargePrep") ? "ChargePrep" : "Idle");    // crouch low, pull the sword back
                // warning lane as wide as the body; dash length = to Ren + 1 unit (2.4..4.2), stops before obstacles
                float width = DashTelegraph.WidthFor(ai.BodyCollider, 0.7f), reach = 2.4f;
                if (i == 0) BossJuice.Cast(ai.Health, n > 1 ? "Lantern Charge x3" : "Lantern Charge", Cold, wind);
                var lampGlow = BossJuice.Light((Vector2)transform.position + Vector2.up * 0.5f, Cold, 1.2f, 0.3f, 0f, transform);
                DashTelegraph tg = null;
                float t = 0f;
                while (t < wind && !ai.Health.IsDead)
                {
                    if (ai.Player != null && (t < wind * 0.75f || tg == null))
                    {
                        dir = ai.DirToPlayer.normalized;
                        reach = DashTelegraph.Reach(transform.position, dir, DashTelegraph.WantReach(transform.position, ai.Player.position, 1f, 2.4f, 4.2f), 0.25f, ai.BodyCollider, ai.Player);
                    }
                    if (tg == null) tg = DashTelegraph.Spawn(transform.position, dir, reach, width, Cold, wind, Layer, Order - 1);
                    else tg.Aim(transform.position, dir, reach);
                    ai.FaceX(dir.x);
                    if (lampGlow != null) lampGlow.intensity = Mathf.Lerp(0.3f, 1.4f, t / wind);           // lantern light gathers
                    t += Time.deltaTime;
                    yield return null;
                }
                if (tg != null) tg.Release();
                if (lampGlow != null) Destroy(lampGlow.gameObject);
                if (ai.Health.IsDead) yield break;
                if (i == 0) BossJuice.Fire(ai.Health);
                BossJuice.Flash((Vector2)transform.position + Vector2.up * 0.4f, Cold, 1.6f, 1.4f, 0.3f);   // launch the dash
                BossJuice.Burst("m3_dash_dust", transform.position, VfxScale * 1.2f, Color.white, Layer, Order - 1, 16f);

                ai.Desired = dir * chargeSpeed;              // run direction = animation direction (EnemyAI picks _S/_N from Desired)
                ai.PlayState("Dash");
                if (ai.Vfx != null) ai.Vfx.Dash(dir);
                bool hit = false;
                t = 0f;
                float runTime = reach / Mathf.Max(0.1f, chargeSpeed);
                while (t < runTime && !ai.Health.IsDead)
                {
                    ai.Desired = dir * chargeSpeed;
                    if (!hit && ai.GapToPlayer() <= 0.1f)
                    {
                        ai.Hurt(ai.damage * 1.2f, transform.position, 4f); hit = true;
                        BossJuice.Impact((Vector2)ai.Player.position + Vector2.up * 0.3f, Cold, 0.9f, 2, Layer, Order + 2);   // rammed
                        BossJuice.Stop(0.06f);
                    }
                    t += Time.deltaTime;
                    yield return null;
                }
                ai.Desired = Vector2.zero;
                ai.PlayState("Idle");
                yield return ai.WaitFor(i == n - 1 ? 0.6f : 0.15f);
            }
            nextCharge = Time.time + chargeEvery * (ai.InPhase2 ? 0.8f : 1f);
        }

        // ------------------------------------------------------------------ Judgement (Slam)
        private IEnumerator Judgement()
        {
            SfxHooks.BossMove(this, "Judgement"); // sound
            ai.Desired = Vector2.zero;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Slam");
            Vector2 at = (Vector2)transform.position + dir * 0.45f;
            // red ring exactly where the blade will land (GroundStrike applies the hit itself when the warning ends)
            // Judgement deals NO damage - the blade and the wave only stun (Daze) + push lightly
            var gs = GroundStrike.Spawn(at, slamWindup, slamRadius, 0f, null, 0, 14f, Order, Verdict);
            gs.knockback = 2f;
            gs.onHit = _ => Daze(0.9f);
            gs.impactLight = 0f;                                                           // light flash handled by Impact (larger)
            BossJuice.Cast(ai.Health, "Judgement", Verdict, slamWindup);
            var swordGlow = BossJuice.Light((Vector2)transform.position + Vector2.up * 0.6f, Verdict, 1.3f, 0.2f, 0f, transform);
            for (float tw = 0f; tw < slamWindup && !ai.Health.IsDead; tw += Time.deltaTime)
            {
                if (swordGlow != null) swordGlow.intensity = Mathf.Lerp(0.2f, 1.6f, tw / slamWindup);   // raised sword glows brighter
                yield return null;
            }
            if (swordGlow != null) Destroy(swordGlow.gameObject);
            if (ai.Health.IsDead) yield break;
            BossJuice.Fire(ai.Health);
            SpriteVfx.Spawn("m3_lk_slam", at, VfxScale, Layer, Order - 1, false, 14f);
            BossJuice.Burst("m3x_lk_quake", at, VfxScale * 1.3f, Color.white, Layer, Order - 2, 14f);   // cold glowing ground cracks + dust
            BossJuice.Impact(at + Vector2.up * 0.15f, Cold, 1.3f, 3, Layer, Order + 2);
            CameraFollow.KickMain(dir, 0.06f, 0.25f);
            JudgementWave(at);
            if (ai.InPhase2)
            {
                yield return ai.WaitFor(0.45f);
                if (ai.Health.IsDead) yield break;
                JudgementWave(at);
                BossJuice.Flash(at, Cold, 2.2f, 1.3f, 0.35f);                              // aftershock
                BossJuice.Shake(2);
            }
            yield return ai.WaitFor(0.7f);
            ai.PlayState("Idle");
            nextSlam = Time.time + slamEvery * (ai.InPhase2 ? 0.75f : 1f);
            yield return ai.WaitFor(0.3f);
        }

        /// Judgement wave: visual = ground_impact VFX (like Ren's F skill) scaled to waveRadius; a hidden Shockwave handles the hit.
        /// ground_impact: largest ring at frame 4, 142 px wide (radius 0.71 units at scale 1), squashed ~0.38.
        private void JudgementWave(Vector2 at)
        {
            float scale = waveRadius / 0.71f;
            SpriteVfx.Spawn("ground_impact", at, scale, Layer, Order - 1, false, waveFps);
            var w = Shockwave.Spawn(at, waveRadius, 4.2f / Mathf.Max(1f, waveFps), 0f, Order - 1, Color.clear);
            w.hidden = true;
            w.squash = 0.45f;
            w.band = 0.22f;
            w.knockback = 1.5f;
            w.onHit = _ => Daze(0.7f);
        }

        private void Daze(float s)
        {
            if (ai == null || ai.Player == null) return;
            var st = PlayerStatus.Of(ai.Player);
            if (st != null) st.Daze(s, "The Judgement rings in your skull!");
        }

        // ------------------------------------------------------------------ Vow
        private IEnumerator Vow()
        {
            SfxHooks.BossMove(this, "Vow"); // sound
            nextVow = Time.time + vowEvery;
            ai.Desired = Vector2.zero;
            ai.FaceX(ai.DirToPlayer.x);
            ai.PlayState("Vow");
            Hints.Show("The Lantern Knight swears a vow. Break it!", 2f);
            BossJuice.Cast(ai.Health, "Vow of the Lantern", Gold, vowTime);               // cast bar = time left to break the oath
            var vowGlow = BossJuice.Light(transform.position, Gold, 1.6f, 1.1f, 0f, transform);
            var sigil = SpriteVfx.SpawnAttached("m3_lk_sigil", transform, VfxScale, Layer, Order - 1, 10f, vowTime);
            hitsTaken = 0;
            float t = 0f;
            bool broken = false;
            while (t < vowTime && !ai.Health.IsDead)
            {
                if (hitsTaken >= vowBreakHits) { broken = true; break; }
                t += Time.deltaTime;
                yield return null;
            }
            if (sigil != null) sigil.Stop();
            if (vowGlow != null) Destroy(vowGlow.gameObject);
            if (ai.Health.IsDead) yield break;
            if (broken)
            {
                BossJuice.Clear(ai.Health);                                                // oath broken - gold flash + hit-stop
                BossJuice.Impact((Vector2)transform.position + Vector2.up * 0.4f, Gold, 1.1f, 2, Layer, Order + 2);
                BossJuice.Stop(0.07f);
                // no extra hint - visuals / animation / boss title already tell the player
                ai.PlayState(ai.HasAnimState("Stagger") ? "Stagger" : "Idle");   // kneels on one knee leaning on the sword: counterattack window
                yield return ai.Flash(2.2f, new Color(1f, 1f, 0.55f));
                ai.PlayState("Idle");
                yield break;
            }
            BossJuice.Fire(ai.Health);
            BossJuice.Flash(transform.position, Gold, wardRadius, 1.4f, 0.6f);               // oath completed - gold halo spreads over the allies
            foreach (var e in Allies())
            {
                var buff = e.GetComponent<VowWard>();
                if (buff == null) buff = e.gameObject.AddComponent<VowWard>();
                buff.Apply(wardMul, wardTime, Layer);
            }
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ Lightless (entering phase 2)
        private IEnumerator Lightless()
        {
            SfxHooks.BossMove(this, "Lightless"); // sound
            lightlessDone = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Vow");
            SpriteVfx.Spawn("m3_lk_lightless", transform.position, VfxScale, Layer, Order + 1, false, 12f);
            // no extra hint - visuals / animation / boss title already tell the player
            BossJuice.Cast(ai.Health, "Lightless", Cold, 0.6f);                           // skill name on the cast bar only (no dialogue box)
            yield return ai.WaitFor(0.6f);
            BossJuice.Fire(ai.Health);
            BossJuice.Impact((Vector2)transform.position + Vector2.up * 0.4f, Cold, 1.6f, 3, Layer, Order + 2);
            ZoneAmbience.DimMul = darkness;
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.3f);
            yield return ai.Flash(0.5f, new Color(0.55f, 0.65f, 1f));
            ai.PlayState("Idle");
            nextCharge = Time.time + 0.4f;                  // enter phase 2 with a dash chain
        }

        // ------------------------------------------------------------------ helpers
        private IEnumerable<EnemyAI> Allies()
        {
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (e != ai && e.Health != null && !e.Health.IsDead &&
                    ((Vector2)e.transform.position - (Vector2)transform.position).magnitude <= wardRadius)
                    yield return e;
        }

        private int AlliesNear() { int n = 0; foreach (var _ in Allies()) n++; return n; }

        private void KillLine()
        {
            if (chargeLine != null) { Destroy(chargeLine.gameObject); chargeLine = null; }
        }

        private void LateUpdate()
        {
            if (chargeLine != null && (ai == null || ai.Health == null || ai.Health.IsDead)) KillLine();
        }

        private SpriteRenderer MakeLine()
        {
            if (pixel == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            }
            var go = new GameObject("LK_ChargeLine");
            var s = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(s);
            s.sprite = pixel;
            s.sortingLayerID = Layer;
            s.sortingOrder = Order - 1;
            go.transform.localScale = new Vector3(chargeSpeed * chargeTime, 0.1f, 1f);
            return s;
        }
    }

    /// <summary>Oath shield on one enemy: reduces incoming damage for a few seconds, with a blue glow ring around it.</summary>
    public class VowWard : MonoBehaviour
    {
        private Health hp;
        private float until;
        private SpriteVfx fx;

        public void Apply(float mul, float seconds, int layer)
        {
            hp = GetComponent<Health>();
            if (hp == null) return;
            hp.DamageTakenMul = mul;
            until = Time.time + seconds;
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (fx != null) fx.Stop();
            fx = SpriteVfx.SpawnAttached("m3_lk_ward", transform, 0.7f, layer, (sr != null ? sr.sortingOrder : 2) + 1, 10f, seconds);
            enabled = true;
        }

        private void Update()
        {
            if (Time.time < until && hp != null && !hp.IsDead) return;
            if (hp != null) hp.DamageTakenMul = 1f;
            if (fx != null) fx.Stop();
            fx = null;
            enabled = false;
        }
    }
}
