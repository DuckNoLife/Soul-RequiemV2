using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 17/11 (§284) THE LAST GUARD v3 — cận vệ cuối cùng của vua Nolan (Kroven), boss nhỏ Map 2b The Kroven Deep. Viết lại toàn bộ.
    /// Nguyên tắc: KHIÊN CHỈ CHẶN KHI ĐANG THỦ. Mọi đòn tấn công đều hạ khiên -> mỗi đòn để lộ một cửa sổ phản công.
    ///
    /// PHA 1 "Thành lũy" (khiên + giáo):
    ///   Shield Wall   : đang thủ (Idle/Move/lúc lao) khiên quay về phía Ren `turnRate` độ/giây; đòn có hướng vào mặt khiên
    ///                   (trong `blockArc`) bị dội, tóe lửa. Ren đứng sau lưng quá `rearPatience` giây -> hắn xoay người húc khiên.
    ///   Guard Break   : sát thương bị chặn dồn thành "sức ép" (giảm dần `strainDecay`/giây). Đủ 1/3, 2/3 khiên nứt;
    ///                   đủ `guardBreakAt` -> khiên vỡ thế, choáng `breakStun` giây (đòn nặng như Iaijutsu nạp đầy dồn nhanh hơn).
    ///   Spear Thrust  : vạch đỏ `thrustWarn` giây (khoá hướng ở 75%) -> lao giáo. HỤT -> giáo cắm xuống sàn, kẹt `stuckTime` giây.
    ///   Shield Charge : mũi tên đỏ dài tới vách `chargeWarn` giây -> lao thẳng sau khiên (trúng Ren: đẩy văng, lao tiếp).
    ///                   Đâm vào vách -> choáng `dazeTime` giây. Lăn ngang để né.
    ///   Shield Bash   : Ren sát mặt khiên -> nhấp nháy `bashWarn` giây rồi húc ngắn, đẩy lùi mạnh.
    /// PHA 2 "Last Stand" (≤ 50% máu): ném khiên xuống sàn, Ether rêu bùng xanh, cầm giáo hai tay (bộ hình riêng `lastStandController`).
    ///   Whirl         : vòng đỏ quanh hắn `whirlWarn` giây -> xoay giáo một vòng.
    ///   Triple Thrust : ba nhát đâm liền, mỗi nhát một vạch đỏ ngắn hơn, nhắm lại theo Ren.
    ///   Leaping Strike: vòng đỏ dưới chân Ren `leapWarn` giây -> nhảy tới, giáng giáo, sóng chấn động; tiếp đất hở `leapRecover` giây.
    /// 17/11 (§285): VFX riêng tự làm bằng PixelLab VFX (Resources/VFX/lg_*): whirl (trước/sau), slam, thrust, spark, break, dust, aura, stun.
    /// Thời điểm gây sát thương tính theo KHUNG HÌNH của clip (fps dựng trong Map2bBuilder, xem hằng số FPS_* dưới).
    /// Bẫy của The Kroven Deep tắt khi khu đấu được dọn (cờ &lt;khu&gt;_cleared) — không cần code ở đây.
    /// </summary>
    public class LastGuardSkills : MonoBehaviour, IEnemySkills
    {
        // fps các clip (khớp Map2bBuilder.States) — dùng để canh khung hình ra đòn
        public const float FPS_AIM = 12f, FPS_STUCK = 16f, FPS_CHARGE = 12f, FPS_BASH = 14f, FPS_WHIRL = 16f, FPS_LEAP = 14f, FPS_THRUST2 = 14f;

        [Header("Pha 2: bộ hình không khiên (Map2bBuilder gán)")]
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
        private bool engaged, lastStand, shieldUp = true, busy, breakPending;
        private float nextThrust, nextBash, nextCharge, nextWhirl, nextTriple, nextLeap, behindSince = -1f, strain, hintAt = -99f;
        private int cracks;
        private Vector2 guardDir = Vector2.down;
        private SpriteRenderer line;
        private SpriteVfx aura, auraFront;
        private float Scale => Mathf.Abs(transform.lossyScale.x);   // VFX vẽ theo cỡ sprite gốc -> nhân cùng tỉ lệ boss (1,35)
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

        // ================================================================== khiên
        /// Đòn đánh vào mặt khiên? (`hit` = hướng đẩy của đòn, từ Ren tới hắn -> đòn tới từ phía -hit)
        private bool Blocks(Vector2 hit)
        {
            if (lastStand || !shieldUp || hp == null || hp.IsDead) return false;
            return Vector2.Angle(-hit.normalized, guardDir) <= blockArc * 0.5f;
        }

        private void OnBlocked(Vector2 hit)
        {
            Vector2 at = Pos + guardDir * 0.25f + Vector2.up * 0.3f;
            Spawn("lg_spark", at, 1.1f, hit.x > 0f, 16f);              // tia lửa bật ngược về phía Ren
            CameraFollow.KickMain(hit, 0.025f, 0.08f);
            ai?.Rb?.AddForce(hit.normalized * 1.5f, ForceMode2D.Impulse);
            strain += hp.LastBlockedAmount;
            int c = Mathf.FloorToInt(strain / (guardBreakAt / 3f));
            if (c > cracks && c < 3)
            {
                cracks = c;
                Spawn("lg_break", at, 0.55f + 0.15f * c, false, 16f);   // khiên nứt
            }
            if (strain >= guardBreakAt) { breakPending = true; return; }   // xử lý ở nhịp Act kế tiếp (sau động tác đang dở)
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
            // Ren lẩn sau lưng: đếm giờ
            bool behind = Mathf.Abs(a) > 110f;
            behindSince = behind ? (behindSince < 0f ? Time.time : behindSince) : -1f;
        }

        // ================================================================== bộ não (EnemyAI gọi mỗi nhịp)
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

            // tiến chậm, khiên hướng về Ren; sát quá thì đứng thủ
            ai.LookAtPlayer = true;
            if (gap < 0.25f) { ai.Desired = Vector2.zero; ai.PlayState("Idle", false); yield break; }
            ai.MoveAnimSpeed = 1f;
            ai.Desired = to.normalized * ai.moveSpeed * (lastStand ? 1.25f : 0.8f);
            ai.PlayState("Move", false);
        }

        private IEnumerator Run(IEnumerator move)
        {
            busy = true;
            yield return move;
            busy = false;
            if (hp != null && !hp.IsDead && !lastStand) shieldUp = true;
        }

        // ================================================================== Spear Thrust (pha 1 + đơn lẻ pha 2)
        private IEnumerator Thrust(float warn, float damage, bool canStick)
        {
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Aim");                                  // giáo hạ ngang dần (9 khung @ FPS_AIM ~ thrustWarn)
            yield return Aim(warn, thrustLength, 0.12f, d => dir = d);
            if (hp.IsDead) yield break;
            shieldUp = false;                                     // khiên dạt sang bên khi lao giáo
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
                // HỤT: giáo cắm xuống sàn — cửa sổ lớn nhất của pha 1
                ai.PlayState("Stuck");
                Vector2 tip = Pos + dir * 0.6f;
                Spawn("lg_dust", tip, 0.9f, false, 14f, -1);
                Spawn("lg_slam", tip, 0.55f, false, 14f, -1);
                FirstHint("lg_stuck", "His spear bit into the stone. Now.");
                float s = 0f;
                while (s < stuckTime && !hp.IsDead)
                {
                    // giật giật như đang cố rút giáo
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
            SfxHooks.BossMove(this, "Charge"); // 19/11 (§335): âm
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Idle");
            float reach = chargeSpeed * chargeMaxTime;
            float wall = WallDistance(dir, reach);
            var l = line = MakeLine(Mathf.Min(reach, wall), 0.32f);
            float t = 0f;
            while (t < chargeWarn && !hp.IsDead)
            {
                if (t < chargeWarn * 0.7f && ai.Player != null) { dir = ai.DirToPlayer.normalized; wall = WallDistance(dir, reach); }
                ai.FaceX(dir.x);
                l.transform.position = Pos;
                l.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                l.transform.localScale = new Vector3(Mathf.Min(reach, wall), 0.32f, 1f);
                l.color = new Color(Warn.r, Warn.g, Warn.b, Mathf.Repeat(Time.time * 7f, 1f) < 0.5f ? 0.5f : 0.2f);
                if (Mathf.Repeat(t, 0.25f) < Time.deltaTime) Spawn("lg_dust", Pos - dir * 0.2f, 0.7f, false, 14f, -1);
                t += Time.deltaTime;
                yield return null;
            }
            KillLine();
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
                // chững lại (đâm vách / cột) = quãng đi được trong 0,1 s < 30% dự kiến
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

        // ================================================================== Shield Bash (turn = xoay người húc ra sau)
        private IEnumerator Bash(bool turn)
        {
            SfxHooks.BossMove(this, "Bash"); // 19/11 (§335): âm
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            Vector2 dir = ai.DirToPlayer.normalized;
            if (turn) guardDir = dir;                             // xoay phắt lại
            ai.FaceX(dir.x);
            ai.PlayState("Bash");                                 // khung 0..3 gồng (bashWarn), 4..5 húc
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

        // ================================================================== choáng (đâm vách / vỡ thế)
        private IEnumerator Dazed(float seconds)
        {
            shieldUp = false;
            ai.Desired = Vector2.zero;
            ai.PlayState(ai.HasAnimState("Dazed") ? "Dazed" : "Stagger");
            SpriteVfx.SpawnAttached("lg_stun_back", transform, Scale, Layer, Order - 1, 12f, seconds);   // sao choáng quay quanh mũ (sau + trước)
            SpriteVfx.SpawnAttached("lg_stun_front", transform, Scale, Layer, Order + 2, 12f, seconds);
            yield return ai.WaitFor(seconds);
            ai.PlayState("Idle");
        }

        private IEnumerator GuardBroken()
        {
            SfxHooks.BossMove(this, "GuardBroken"); // 19/11 (§335): âm
            KillLine();
            strain = 0f; cracks = 0;
            Spawn("lg_break", Pos + guardDir * 0.25f + Vector2.up * 0.35f, 1.4f, false, 16f);
            CameraFollow.KickMain(-guardDir, 0.07f, 0.25f);
            FirstHint("lg_break", "The guard breaks!");
            yield return Dazed(breakStun);
        }

        // ================================================================== vào pha 2
        private IEnumerator LastStand()
        {
            SfxHooks.BossMove(this, "LastStand"); // 19/11 (§337): âm
            lastStand = true;
            shieldUp = false; breakPending = false;
            KillLine();
            ai.Desired = Vector2.zero;
            ai.PlayState("Stagger");
            yield return ai.Flash(0.9f, Moss);
            if (hp.IsDead) yield break;
            // khiên rơi xuống sàn bên trái hắn, nằm đó tới hết trận
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
            aura = SpriteVfx.SpawnAttached("lg_aura_back", transform, Scale, Layer, Order - 1, 12f, 9999f);     // Ether rêu: quầng chân + đốm bay lên
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
            yield return ai.WaitFor(Mathf.Max(0f, whirlWarn - 3f / FPS_WHIRL));   // khung 3 của Whirl = lúc giáo quét qua
            if (hp.IsDead) yield break;
            ai.PlayState("Whirl");
            // vệt giáo xoay 360°: nửa sau lưng vẽ dưới thân, nửa trước vẽ trên thân (bắt đầu trễ 2 khung)
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
                ai.PlayState("Thrust");                           // khung 0..3 rút giáo, 4..5 đâm (FPS_THRUST2)
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
            yield return ai.WaitFor(0.8f);                        // hụt hơi sau loạt đâm: cửa sổ
            ai.PlayState("Idle");
            nextTriple = Time.time + tripleEvery;
            nextThrust = Time.time + thrustEvery;
        }

        // ================================================================== Leaping Strike (pha 2)
        private IEnumerator Leap()
        {
            SfxHooks.BossMove(this, "Leap"); // 19/11 (§337): âm
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = true;
            Vector2 target = ai.Player.position;
            var gs = GroundStrike.Spawn(target, leapWarn + 0.35f, leapRadius, leapDamage, null, 0, 14f, Order - 1, Warn);
            gs.knockback = 4.5f;
            ai.PlayState("Idle");
            yield return ai.Flash(leapWarn - 2f / FPS_LEAP, Moss);            // khung 0..2 của Leap = khom gối
            if (hp.IsDead) yield break;
            ai.FaceX(target.x - Pos.x);
            ai.PlayState("Leap");
            yield return ai.WaitFor(2f / FPS_LEAP);
            // khung 3..7 trên không: bay tới điểm rơi (không đẩy thân theo Desired)
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
            Spawn("lg_slam", Pos, 1.7f, false, 14f, -1);           // nứt sàn + bụi + đá văng
            Spawn("lg_dust", Pos, 1.5f, false, 14f, -1);
            CameraFollow.KickMain(Vector2.down, 0.1f, 0.35f);
            SoulRequiem.Core.Sfx.Play("heavy_land", transform.position); // 19/11 (§337): âm: đáp sau cú nhảy
            yield return ai.WaitFor(leapRecover);                 // giáo cắm, thở dốc: cửa sổ
            ai.PlayState("Idle");
            nextLeap = Time.time + leapEvery;
        }

        // ================================================================== tiện ích
        /// Vạch đỏ theo hướng nhắm trong `warn` giây; khoá hướng ở 75%.
        private IEnumerator Aim(float warn, float length, float width, System.Action<Vector2> setDir)
        {
            Vector2 dir = ai.DirToPlayer.normalized;
            var l = line = MakeLine(length, width);
            float t = 0f;
            while (t < warn && !hp.IsDead)
            {
                if (ai.Player != null && t < warn * 0.75f) dir = ai.DirToPlayer.normalized;
                ai.FaceX(dir.x);
                l.transform.position = Pos;
                l.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                l.color = new Color(Warn.r, Warn.g, Warn.b, Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? 0.65f : 0.25f);
                t += Time.deltaTime;
                yield return null;
            }
            setDir(dir);
            KillLine();
        }

        /// Khoảng tới vật cản đầu tiên (không tính chính hắn / Ren / trigger) theo hướng `dir`.
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

        /// Vệt mũi giáo đâm (lg_thrust vẽ hướng +x, neo ở gốc) — xoay theo hướng đâm, ngang tầm tay cầm giáo.
        private void Streak(Vector2 dir, float scale)
        {
            var v = Spawn("lg_thrust", Pos + Vector2.up * 0.42f + dir * 0.2f, 1.2f * scale, false, 16f, dir.y > 0.3f ? -1 : 1);
            if (v != null) v.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// orderOffset: +1 vẽ trên thân boss, -1 dưới (bụi / nứt sàn).
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
