using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE LAST GUARD - the last bodyguard of King Nolan (Kroven), small boss of Map 2b The Kroven Deep.
    /// Rule: THE SHIELD ONLY BLOCKS WHILE GUARDING. Every attack lowers the shield -> every attack opens a counterattack window.
    ///
    /// PHASE 1 "Bulwark" (shield + spear):
    ///   Shield Wall   : while guarding (Idle/Move/during the charge) the shield turns toward Ren at `turnRate` degrees/second; directional hits on the shield face
    ///                   (inside `blockArc`) bounce off with sparks. Ren behind his back longer than `rearPatience` seconds -> he spins and bashes.
    ///   Guard Break   : blocked damage builds "strain" (decays `strainDecay`/second). At 1/3 and 2/3 the shield cracks;
    ///                   at `guardBreakAt` -> guard broken, stunned `breakStun` seconds (heavy hits such as a fully charged Iaijutsu build it faster).
    ///   Spear Thrust  : red line for `thrustWarn` seconds (direction locks at 75%) -> spear lunge. MISS -> the spear sticks in the floor for `stuckTime` seconds.
    ///   Shield Charge : long red arrow to the wall for `chargeWarn` seconds -> straight charge behind the shield (hits Ren: knocks him away and keeps going).
    ///                   Hits a wall -> dazed `dazeTime` seconds. Roll sideways to dodge.
    ///   Shield Bash   : Ren right at the shield face -> flashes for `bashWarn` seconds, then a short bash with strong knockback.
    /// PHASE 2 "Last Stand" (≤ 50% HP): throws the shield down, moss Ether flares green, two-handed spear (own sprite set `lastStandController`).
    ///   Whirl         : red ring around him for `whirlWarn` seconds -> one full spear spin.
    ///   Triple Thrust : three thrusts in a row, each with a shorter red line, re-aiming at Ren.
    ///   Leaping Strike: red ring under Ren for `leapWarn` seconds -> leaps over, slams the spear down, shock wave; exposed `leapRecover` seconds after landing.
    /// Own PixelLab VFX (Resources/VFX/lg_*): whirl (back/front), slam, thrust, spark, break, dust, aura, stun.
    /// Hit timing follows the clip FRAMES (fps set in Map2bBuilder, see the FPS_* constants below).
    /// The Kroven Deep traps turn off when the arena is cleared (flag &lt;zone&gt;_cleared) - no code needed here.
    /// </summary>
    public class LastGuardSkills : MonoBehaviour, IEnemySkills
    {
        // clip fps (matches Map2bBuilder.States) - used to time the attack frames
        public const float FPS_AIM = 12f, FPS_STUCK = 16f, FPS_CHARGE = 12f, FPS_BASH = 14f, FPS_WHIRL = 16f, FPS_LEAP = 14f, FPS_THRUST2 = 14f;

        [Header("Phase 2: sprite set without the shield (set by Map2bBuilder)")]
        public RuntimeAnimatorController lastStandController;
        public Sprite droppedShield;

        [Header("Shield Wall / Guard Break")]
        public float turnRate = 95f, blockArc = 130f, rearPatience = 1.4f;
        public float guardBreakAt = 120f, strainDecay = 6f, breakStun = 2.2f;
        [Header("Spear Thrust")]
        public float thrustWarn = 0.75f, thrustSpeed = 5.2f, thrustTime = 0.26f, thrustLength = 1.6f, thrustEvery = 2.6f, thrustDamage = 18f, stuckTime = 1.3f;
        [Header("Shield Charge")]
        public float chargeWarn = 0.85f, chargeSpeed = 4.4f, chargeMaxTime = 1.2f, chargeEvery = 7.5f, chargeDamage = 22f, chargeKnock = 6f, dazeTime = 2.2f;
        [Header("Shield Bash")]
        public float bashWarn = 0.35f, bashSpeed = 4f, bashTime = 0.18f, bashKnock = 5f, bashEvery = 2.4f, bashDamage = 12f;
        [Header("Last Stand")]
        public float whirlWarn = 0.8f, whirlRadius = 0.95f, whirlEvery = 5f, whirlDamage = 20f;
        public float tripleWarn = 0.45f, tripleEvery = 4.4f, tripleDamage = 14f;
        public float leapWarn = 0.9f, leapRadius = 0.7f, leapEvery = 6.5f, leapDamage = 26f, leapRecover = 1.1f;

        private EnemyAI ai;
        private Health hp;
        private Animator anim;
        private bool engaged, lastStand, shieldUp = true, breakPending;
        private float nextThrust, nextBash, nextCharge, nextWhirl, nextTriple, nextLeap, behindSince = -1f, strain, hintAt = -99f;
        private int cracks;
        private Vector2 guardDir = Vector2.down;
        private SpriteRenderer line;
        private SpriteVfx aura, auraFront;
        private float Scale => Mathf.Abs(transform.lossyScale.x);   // VFX are drawn for the original sprite size -> multiplied by the boss scale (1.35)
        private static Sprite pixel;
        private static readonly Color Warn = new Color(1f, 0.32f, 0.26f, 1f);
        private static readonly Color Moss = new Color(0.45f, 1f, 0.55f, 1f);

        private int Layer => ai != null ? ai.SortingLayer : 0;
        private int Order => ai != null ? ai.SortingOrder : 2;
        private Vector2 Pos => transform.position;

        private void Awake()
        {
            hp = GetComponent<Health>();
            anim = GetComponent<Animator>();
            if (hp != null)
            {
                hp.BlockCheck = Blocks;
                hp.Blocked += OnBlocked;
                hp.Died += _ => { hp.BlockCheck = null; KillLine(); if (aura != null) aura.Stop(); if (auraFront != null) auraFront.Stop(); };
            }
        }

        private void OnDisable() => KillLine();

        // ================================================================== shield
        /// Did the hit land on the shield face? (`hit` = knockback direction, from Ren to him -> the hit comes from -hit)
        private bool Blocks(Vector2 hit)
        {
            if (lastStand || !shieldUp || hp == null || hp.IsDead) return false;
            return Vector2.Angle(-hit.normalized, guardDir) <= blockArc * 0.5f;
        }

        private void OnBlocked(Vector2 hit)
        {
            Vector2 at = Pos + guardDir * 0.25f + Vector2.up * 0.3f;
            Spawn("lg_spark", at, 1.1f, hit.x > 0f, 16f);              // sparks bounce back toward Ren
            CameraFollow.KickMain(hit, 0.025f, 0.08f);
            ai?.Rb?.AddForce(hit.normalized * 1.5f, ForceMode2D.Impulse);
            strain += hp.LastBlockedAmount;
            int c = Mathf.FloorToInt(strain / (guardBreakAt / 3f));
            if (c > cracks && c < 3)
            {
                cracks = c;
                Spawn("lg_break", at, 0.55f + 0.15f * c, false, 16f);   // shield cracks
            }
            if (strain >= guardBreakAt) { breakPending = true; return; }   // handled on the next Act tick (after the current move)
            if (Time.time - hintAt > 40f)
            {
                hintAt = Time.time;
                Hints.Show("The shield turns your blade. Strike when he attacks, get behind him, or keep hammering until it cracks.", 4f);
            }
        }

        private void Update()
        {
            if (ai == null || ai.Player == null || hp == null || hp.IsDead) return;
            if (strain > 0f) { strain = Mathf.Max(0f, strain - strainDecay * Time.deltaTime); cracks = Mathf.Min(cracks, Mathf.FloorToInt(strain / (guardBreakAt / 3f))); }
            if (lastStand || !shieldUp) { behindSince = -1f; return; }
            Vector2 want = ai.DirToPlayer.sqrMagnitude > 0.0001f ? ai.DirToPlayer.normalized : guardDir;
            float a = Vector2.SignedAngle(guardDir, want);
            float step = Mathf.Clamp(a, -turnRate * Time.deltaTime, turnRate * Time.deltaTime);
            guardDir = (Quaternion.Euler(0, 0, step) * guardDir).normalized;
            // Ren sneaking behind: count time
            bool behind = Mathf.Abs(a) > 110f;
            behindSince = behind ? (behindSince < 0f ? Time.time : behindSince) : -1f;
        }

        // ================================================================== brain (EnemyAI calls it every tick)
        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (ai.Player == null) yield break;
            if (!engaged)
            {
                engaged = true;
                guardDir = ai.DirToPlayer.normalized;
                nextThrust = Time.time + 1.6f; nextBash = Time.time + 1.0f; nextCharge = Time.time + 4.5f;
            }
            if (ai.InPhase2 && !lastStand) { yield return Run(LastStand()); yield break; }
            if (breakPending && !lastStand) { breakPending = false; yield return Run(GuardBroken()); yield break; }

            Vector2 to = ai.DirToPlayer;
            float d = to.magnitude, gap = ai.GapToPlayer();
            if (!lastStand)
            {
                if (behindSince > 0f && Time.time - behindSince > rearPatience && gap < 0.6f) { yield return Run(Bash(true)); yield break; }
                if (Time.time >= nextBash && gap < 0.3f) { yield return Run(Bash(false)); yield break; }
                if (Time.time >= nextCharge && d > 1.4f) { yield return Run(Charge()); yield break; }
                if (Time.time >= nextThrust && d > 0.55f && d < thrustLength + 0.2f) { yield return Run(Thrust(thrustWarn, thrustDamage, true)); yield break; }
            }
            else
            {
                if (Time.time >= nextWhirl && d < whirlRadius + 0.15f) { yield return Run(Whirl()); yield break; }
                if (Time.time >= nextLeap && d > 1.3f) { yield return Run(Leap()); yield break; }
                if (Time.time >= nextTriple && d < thrustLength + 0.3f) { yield return Run(Triple()); yield break; }
            }

            // advance slowly with the shield toward Ren; too close -> hold guard
            ai.LookAtPlayer = true;
            if (gap < 0.25f) { ai.Desired = Vector2.zero; ai.PlayState("Idle", false); yield break; }
            ai.MoveAnimSpeed = 1f;
            ai.Desired = to.normalized * ai.moveSpeed * (lastStand ? 1.25f : 0.8f);
            ai.PlayState("Move", false);
        }

        private IEnumerator Run(IEnumerator move)
        {
            yield return move;
            if (hp != null && !hp.IsDead && !lastStand) shieldUp = true;
        }

        // ================================================================== Spear Thrust (phase 1 + single use in phase 2)
        private IEnumerator Thrust(float warn, float damage, bool canStick)
        {
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Aim");                                  // spear lowers gradually (9 frames @ FPS_AIM ~ thrustWarn)
            yield return Aim(warn, thrustLength, 0.12f, d => dir = d);
            if (hp.IsDead) yield break;
            shieldUp = false;                                     // shield swings aside during the thrust
            bool hit = false;
            float t = 0f;
            Streak(dir, 1f);
            CameraFollow.KickMain(dir, 0.02f, 0.1f);
            while (t < thrustTime && !hp.IsDead)
            {
                ai.Desired = dir * thrustSpeed;
                if (!hit && ai.GapToPlayer() <= 0.28f) { ai.Hurt(damage, Pos, 3f); hit = true; }
                t += Time.deltaTime;
                yield return null;
            }
            ai.Desired = Vector2.zero;
            if (!hit && canStick)
            {
                // MISS: the spear sticks in the floor - the biggest window of phase 1
                ai.PlayState("Stuck");
                Vector2 tip = Pos + dir * 0.6f;
                Spawn("lg_dust", tip, 0.9f, false, 14f, -1);
                Spawn("lg_slam", tip, 0.55f, false, 14f, -1);
                FirstHint("lg_stuck", "His spear bit into the stone. Now.");
                float s = 0f;
                while (s < stuckTime && !hp.IsDead)
                {
                    // jerks as if trying to pull the spear out
                    if (Mathf.Repeat(s, 0.35f) < Time.deltaTime) ai.Rb?.AddForce(-dir * 0.4f, ForceMode2D.Impulse);
                    s += Time.deltaTime;
                    yield return null;
                }
                ai.PlayState("Pull");
                yield return ai.WaitFor(0.4f);
            }
            else yield return ai.WaitFor(lastStand ? 0.35f : 0.55f);
            ai.PlayState("Idle");
            nextThrust = Time.time + thrustEvery;
        }

        // ================================================================== Shield Charge
        private IEnumerator Charge()
        {
            SfxHooks.BossMove(this, "Charge"); // sound
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Idle");
            float reach = chargeSpeed * chargeMaxTime;
            float wall = WallDistance(dir, reach);
            // shared warning lane (as wide as the shield + Ren's body, fill, arrow, target mark where he hits the wall)
            var tg = DashTelegraph.Spawn(Pos, dir, Mathf.Min(reach, wall), DashTelegraph.WidthFor(ai.BodyCollider, 0.8f), Warn, chargeWarn, Layer, Order - 1);
            float t = 0f;
            while (t < chargeWarn && !hp.IsDead)
            {
                if (t < chargeWarn * 0.7f && ai.Player != null) { dir = ai.DirToPlayer.normalized; wall = WallDistance(dir, reach); }
                ai.FaceX(dir.x);
                tg.Aim(Pos, dir, Mathf.Min(reach, wall));
                if (Mathf.Repeat(t, 0.25f) < Time.deltaTime) Spawn("lg_dust", Pos - dir * 0.2f, 0.7f, false, 14f, -1);
                t += Time.deltaTime;
                yield return null;
            }
            tg.Release();
            if (hp.IsDead) yield break;
            ai.PlayState("Charge");
            bool hitPlayer = false, hitWall = false;
            t = 0f;
            Vector2 last = Pos; float lastCheck = 0f;
            while (t < chargeMaxTime && !hp.IsDead)
            {
                ai.Desired = dir * chargeSpeed;
                if (!hitPlayer && ai.GapToPlayer() <= 0.22f)
                {
                    hitPlayer = true;
                    Vector2 side = Vector2.Perpendicular(dir) * Mathf.Sign(Vector2.Dot(Vector2.Perpendicular(dir), ai.DirToPlayer) + 0.001f);
                    ai.Hurt(chargeDamage, Pos - side * 0.3f, chargeKnock);
                    CameraFollow.KickMain(dir, 0.06f, 0.2f);
                }
                if (Mathf.Repeat(t, 0.12f) < Time.deltaTime) Spawn("lg_dust", Pos - dir * 0.25f, 0.9f, false, 16f, -1);
                // stalled (hit a wall / pillar) = distance covered in 0.1 s < 30% of expected
                if (t - lastCheck >= 0.1f)
                {
                    if (t > 0.15f && Vector2.Distance(Pos, last) < chargeSpeed * 0.1f * 0.3f) { hitWall = true; break; }
                    last = Pos; lastCheck = t;
                }
                t += Time.deltaTime;
                yield return null;
            }
            ai.Desired = Vector2.zero;
            nextCharge = Time.time + chargeEvery;
            if (hitWall)
            {
                Spawn("lg_break", Pos + dir * 0.35f + Vector2.up * 0.4f, 1.1f, false, 16f);
                Spawn("lg_slam", Pos + dir * 0.3f, 1.0f, false, 14f, -1);
                CameraFollow.KickMain(dir, 0.08f, 0.3f);
                FirstHint("lg_wall", "He hit the wall. He's reeling.");
                yield return Dazed(dazeTime);
            }
            else
            {
                shieldUp = false;
                ai.PlayState("Idle");
                yield return ai.WaitFor(0.6f);
            }
        }

        // ================================================================== Shield Bash (turn = spin around and bash backwards)
        private IEnumerator Bash(bool turn)
        {
            SfxHooks.BossMove(this, "Bash"); // sound
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            Vector2 dir = ai.DirToPlayer.normalized;
            if (turn) guardDir = dir;                             // snap around
            ai.FaceX(dir.x);
            ai.PlayState("Bash");                                 // frames 0..3 wind-up (bashWarn), 4..5 bash
            yield return ai.Flash(bashWarn, new Color(1f, 0.85f, 0.6f));
            if (hp.IsDead) yield break;
            bool hit = false;
            float t = 0f;
            while (t < bashTime && !hp.IsDead)
            {
                ai.Desired = dir * bashSpeed;
                if (!hit && ai.GapToPlayer() <= 0.16f) { ai.Hurt(bashDamage, Pos, bashKnock); hit = true; }
                t += Time.deltaTime;
                yield return null;
            }
            ai.Desired = Vector2.zero;
            if (hit) { CameraFollow.KickMain(dir, 0.05f, 0.18f); Spawn("lg_spark", Pos + dir * 0.4f + Vector2.up * 0.35f, 1.3f, dir.x < 0f, 16f); }
            shieldUp = false;
            behindSince = -1f;
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
            nextBash = Time.time + bashEvery;
        }

        // ================================================================== stun (wall hit / guard break)
        private IEnumerator Dazed(float seconds)
        {
            shieldUp = false;
            ai.Desired = Vector2.zero;
            ai.PlayState(ai.HasAnimState("Dazed") ? "Dazed" : "Stagger");
            SpriteVfx.SpawnAttached("lg_stun_back", transform, Scale, Layer, Order - 1, 12f, seconds);   // stun stars circling the helmet (back + front)
            SpriteVfx.SpawnAttached("lg_stun_front", transform, Scale, Layer, Order + 2, 12f, seconds);
            yield return ai.WaitFor(seconds);
            ai.PlayState("Idle");
        }

        private IEnumerator GuardBroken()
        {
            SfxHooks.BossMove(this, "GuardBroken"); // sound
            KillLine();
            strain = 0f; cracks = 0;
            Spawn("lg_break", Pos + guardDir * 0.25f + Vector2.up * 0.35f, 1.4f, false, 16f);
            CameraFollow.KickMain(-guardDir, 0.07f, 0.25f);
            FirstHint("lg_break", "The guard breaks!");
            yield return Dazed(breakStun);
        }

        // ================================================================== entering phase 2
        private IEnumerator LastStand()
        {
            SfxHooks.BossMove(this, "LastStand"); // sound
            lastStand = true;
            shieldUp = false; breakPending = false;
            KillLine();
            ai.Desired = Vector2.zero;
            ai.PlayState("Stagger");
            yield return ai.Flash(0.9f, Moss);
            if (hp.IsDead) yield break;
            // the shield drops to the floor on his left and stays there for the rest of the fight
            if (droppedShield != null)
            {
                var go = new GameObject("LG_DroppedShield");
                go.transform.position = Pos + new Vector2(-0.45f, -0.05f);
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = droppedShield; s.sortingLayerID = Layer; s.sortingOrder = Order; s.spriteSortPoint = SpriteSortPoint.Pivot;
                go.transform.localScale = Vector3.one * 1.35f;
            }
            Spawn("lg_dust", Pos + new Vector2(-0.45f, 0.02f), 1.0f, false, 14f, -1);
            if (lastStandController != null && anim != null) anim.runtimeAnimatorController = lastStandController;
            Spawn("lg_slam", Pos, 1.3f, false, 14f, -1);
            aura = SpriteVfx.SpawnAttached("lg_aura_back", transform, Scale, Layer, Order - 1, 12f, 9999f);     // moss Ether: glow at the feet + rising motes
            auraFront = SpriteVfx.SpawnAttached("lg_aura_front", transform, Scale, Layer, Order + 1, 12f, 9999f);
            CameraFollow.KickMain(Vector2.down, 0.06f, 0.35f);
            Hints.Show("He casts the shield aside.", 2.5f);
            ai.PlayState("Idle", true);
            nextWhirl = Time.time + 0.8f; nextTriple = Time.time + 2f; nextLeap = Time.time + 3.5f;
            yield return ai.WaitFor(0.4f);
        }

        // ================================================================== Whirl (pha 2)
        private IEnumerator Whirl()
        {
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = true;
            ai.PlayState("Idle");
            var gs = GroundStrike.Spawn(Pos, whirlWarn, whirlRadius, whirlDamage, null, 0, 14f, Order - 1, Warn);
            gs.knockback = 3.5f;
            yield return ai.WaitFor(Mathf.Max(0f, whirlWarn - 3f / FPS_WHIRL));   // Whirl frame 3 = the moment the spear sweeps
            if (hp.IsDead) yield break;
            ai.PlayState("Whirl");
            // 360° spear trail: the back half drawn below the body, the front half above it (starts 2 frames late)
            SpriteVfx.Spawn("lg_whirl_back", Pos, Scale, Layer, Order - 1, false, 16f);
            yield return ai.WaitFor(2f / 16f);
            SpriteVfx.Spawn("lg_whirl_front", Pos, Scale, Layer, Order + 1, false, 16f);
            yield return ai.WaitFor(Mathf.Max(0f, 3f / FPS_WHIRL - 2f / 16f));
            Spawn("lg_dust", Pos, 1.3f, false, 14f, -1);
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
            nextWhirl = Time.time + whirlEvery;
            yield return ai.WaitFor(0.3f);
        }

        // ================================================================== Triple Thrust (pha 2)
        private IEnumerator Triple()
        {
            for (int i = 0; i < 3 && !hp.IsDead; i++)
            {
                ai.Desired = Vector2.zero;
                ai.LookAtPlayer = false;
                Vector2 dir = ai.DirToPlayer.normalized;
                ai.FaceX(dir.x);
                ai.PlayState("Thrust");                           // frames 0..3 pull back, 4..5 thrust (FPS_THRUST2)
                yield return Aim(i == 0 ? tripleWarn + 0.15f : tripleWarn, thrustLength * 0.85f, 0.1f, d => dir = d);
                if (hp.IsDead) yield break;
                bool hit = false;
                float t = 0f;
                Streak(dir, 0.9f);
                while (t < thrustTime * 0.85f && !hp.IsDead)
                {
                    ai.Desired = dir * thrustSpeed * 1.1f;
                    if (!hit && ai.GapToPlayer() <= 0.28f) { ai.Hurt(tripleDamage, Pos, 2.5f); hit = true; }
                    t += Time.deltaTime;
                    yield return null;
                }
                ai.Desired = Vector2.zero;
                yield return ai.WaitFor(0.12f);
            }
            yield return ai.WaitFor(0.8f);                        // out of breath after the thrust chain: window
            ai.PlayState("Idle");
            nextTriple = Time.time + tripleEvery;
            nextThrust = Time.time + thrustEvery;
        }

        // ================================================================== Leaping Strike (pha 2)
        private IEnumerator Leap()
        {
            SfxHooks.BossMove(this, "Leap"); // sound
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = true;
            Vector2 target = ai.Player.position;
            var gs = GroundStrike.Spawn(target, leapWarn + 0.35f, leapRadius, leapDamage, null, 0, 14f, Order - 1, Warn);
            gs.knockback = 4.5f;
            ai.PlayState("Idle");
            yield return ai.Flash(leapWarn - 2f / FPS_LEAP, Moss);            // Leap frames 0..2 = crouch
            if (hp.IsDead) yield break;
            ai.FaceX(target.x - Pos.x);
            ai.PlayState("Leap");
            yield return ai.WaitFor(2f / FPS_LEAP);
            // frames 3..7 airborne: fly to the landing point (body not pushed by Desired)
            ai.Airborne = true;
            Vector2 from = Pos;
            float air = 5f / FPS_LEAP, t = 0f;
            Vector3 baseScale = transform.localScale;
            while (t < air && !hp.IsDead)
            {
                t += Time.fixedDeltaTime;
                float k = Mathf.Clamp01(t / air);
                ai.Rb.MovePosition(Vector2.Lerp(from, target, k * k * (3f - 2f * k)));
                transform.localScale = baseScale * (1f + 0.12f * Mathf.Sin(k * Mathf.PI));
                yield return new WaitForFixedUpdate();
            }
            transform.localScale = baseScale;
            ai.Airborne = false;
            if (hp.IsDead) yield break;
            Spawn("lg_slam", Pos, 1.7f, false, 14f, -1);           // floor cracks + dust + flying rocks
            Spawn("lg_dust", Pos, 1.5f, false, 14f, -1);
            CameraFollow.KickMain(Vector2.down, 0.1f, 0.35f);
            SoulRequiem.Core.Sfx.Play("heavy_land", transform.position); // sound: landing after the leap
            yield return ai.WaitFor(leapRecover);                 // spear planted, panting: window
            ai.PlayState("Idle");
            nextLeap = Time.time + leapEvery;
        }

        // ================================================================== helpers
        /// Red line along the aim direction for `warn` seconds; direction locks at 75%.
        private IEnumerator Aim(float warn, float length, float width, System.Action<Vector2> setDir)
        {
            Vector2 dir = ai.DirToPlayer.normalized;
            // shared warning lane - at least 0.55 units wide (a 0.12 unit spear line was almost invisible)
            var tg = DashTelegraph.Spawn(Pos, dir, length, Mathf.Max(width, 0.55f), Warn, warn, Layer, Order - 1);
            float t = 0f;
            while (t < warn && !hp.IsDead)
            {
                if (ai.Player != null && t < warn * 0.75f) dir = ai.DirToPlayer.normalized;
                ai.FaceX(dir.x);
                tg.Aim(Pos, dir, length);
                t += Time.deltaTime;
                yield return null;
            }
            setDir(dir);
            tg.Release();
        }

        /// Distance to the first obstacle (ignoring himself / Ren / triggers) along `dir`.
        private float WallDistance(Vector2 dir, float max)
        {
            float best = max;
            foreach (var h in Physics2D.RaycastAll(Pos, dir, max))
            {
                if (h.collider == null || h.collider.isTrigger || h.collider == ai.BodyCollider) continue;
                if (ai.Player != null && h.collider.transform.IsChildOf(ai.Player)) continue;
                if (h.collider.GetComponentInParent<EnemyAI>() != null) continue;
                best = Mathf.Min(best, h.distance);
            }
            return best;
        }

        /// Spear thrust trail (lg_thrust drawn toward +x, anchored at its base) - rotated to the thrust direction, at spear-hand height.
        private void Streak(Vector2 dir, float scale)
        {
            var v = Spawn("lg_thrust", Pos + Vector2.up * 0.42f + dir * 0.2f, 1.2f * scale, false, 16f, dir.y > 0.3f ? -1 : 1);
            if (v != null) v.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// orderOffset: +1 draws above the boss body, -1 below (dust / floor cracks).
        private SpriteVfx Spawn(string name, Vector2 at, float scale, bool flip, float fps, int orderOffset = 1) =>
            SpriteVfx.Spawn(name, at, scale, Layer, Order + orderOffset, flip, fps);

        private static void FirstHint(string key, string text) => FirstTimeHints.Show(key, text, 2.5f);

        private void KillLine()
        {
            if (line != null) { Destroy(line.gameObject); line = null; }
        }

        private SpriteRenderer MakeLine(float length, float width)
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
            KillLine();
            var go = new GameObject("LG_WarnLine");
            var s = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(s);
            s.sprite = pixel;
            s.sortingLayerID = Layer;
            s.sortingOrder = Order - 1;
            go.transform.position = Pos;
            go.transform.localScale = new Vector3(length, width, 1f);
            return s;
        }
    }
}
