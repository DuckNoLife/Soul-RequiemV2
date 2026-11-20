using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§344): THE HOLLOW CHAMPION — nửa "xương thịt" của song đấu Reliquary Wardens (Map 4; hình MEGAPACK Knight_LVL4 bản gốc lửa TÍM,
    /// khác Lantern Warden lửa đỏ; hoạt ảnh Slam / Rage làm bằng PixelLab). Áp sát, ép Ren vào tầm cối của Brazen Reliquary.
    ///   Gauntlet Rush : vạch báo -> lao đấm (Dash), cuối đòn nổ vòng nhỏ đẩy lùi; pha 2 nối 2 lần, pha 3 nối 3 lần.
    ///   Soulfire Slam : giơ hai nắm đấm (Slam) -> nện: vòng sóng lửa hồn (có khe pha 2+), vòng thứ hai rộng hơn ở pha 2+, rải vũng lửa tím.
    ///   Beacon Charge : Engine đánh dấu chỗ Ren bằng đạn tím -> Champion lao thẳng tới dấu, để lại vệt lửa hồn, nện nhỏ ở cuối.
    ///   Bảo vệ        : Ren đánh Engine mà Champion ở xa -> chạy về đứng chắn (Engine chỉ nhận 55% sát thương khi Champion sát bên).
    ///   Cối của Engine rơi trúng -> choáng 2,2 s, x2 sát thương (ShellHit).
    ///   Engine gục -> RAGE (pha 3): hoạt ảnh gào, lửa hồn bao quanh, nhanh hơn, nhịp chiêu x0,7.
    /// 19/11 (§345) THE WARDEN GALLERY: Engine ngồi trên bệ pháo không với tới -> Champion là mục tiêu bắt buộc. Tương tác môi trường:
    ///   • Lao (Rush / Beacon Charge) đâm trúng TƯỢNG -> tượng vỡ, Champion CHOÁNG 2 s (dụ hắn lao qua tượng);
    ///   • Powder Hurl: đi nhặt THÙNG THUỐC NỔ gần nhất, giơ lên đầu rồi ném vào chỗ Ren (vòng báo đỏ, nổ + vũng lửa);
    ///   • Slam châm các thùng quanh hắn (đánh hắn sát thùng = hắn tự dính nổ: KegHit choáng 2,4 s + 9% máu);
    ///   • mỗi lần choáng, Engine nã vòng cối quanh hắn (Covering Barrage) — đứng sát hắn là an toàn.
    ///   Engine trên bệ thì không quay về "chắn" (Guard).
    /// </summary>
    public sealed class HollowChampionSkills : BossSkillsBase
    {
        public float rushSpeed = 4.0f, rushDamage = 18f, slamDamage = 20f, slamRadius = 1.7f;
        private BrazenReliquarySkills partner;
        private bool hasCommand, enraged;
        private Vector2 commandAt;
        private SpriteVfx rageAura;
        private float baseSpeed;
        private static readonly Color Soul = new Color(0.72f, 0.42f, 1f), SoulDim = new Color(0.8f, 0.6f, 1f, 0.9f);

        private bool PartnerAlive => partner != null && partner.Alive;
        // 19/11 (§345): hp chỉ được gán ở lượt Act đầu tiên -> con kia hỏi sớm hơn thì tự lấy Health (tránh tưởng đồng đội đã chết)
        internal bool Alive { get { if (hp == null) hp = GetComponent<Health>(); return hp != null && !hp.IsDead; } }
        internal float HpPercent => Alive ? hp.Percent : 0f;
        private bool statueHinted, hurlHinted;
        private static MapMechanics MM => MapMechanics.Instance;

        protected override void OnEngage()
        {
            preferredRange = 1.1f;
            pinnedTimeMax = 2.4f; repositionCooldown = 3f;
            Use("rush", 1.2f); Use("slam", 3f); Use("guard", 4f);
            partner = FindFirstObjectByType<BrazenReliquarySkills>();
            if (partner != null) center = (Pos + (Vector2)partner.transform.position) * 0.5f;   // sân chung: tâm giữa hai con lúc vào trận
            var mm = MM;                                                                        // 19/11 (§345): Warden Gallery — sân = mốc duo_arena
            var arena = mm != null ? mm.MechRect("duo_arena") : null;
            if (arena.HasValue) { center = arena.Value.center; arenaRadii = arena.Value.size * 0.5f; }
            Use("hurl", 6f);
            baseSpeed = ai.moveSpeed;
        }

        protected override int PhaseFor(float p)
        {
            if (engaged && partner != null && !partner.Alive) return 3;
            return p <= 0.5f ? 2 : 1;
        }
        protected override float CooldownMul() => phase == 3 ? 0.7f : phase == 2 ? 0.85f : 1f;

        protected override IEnumerator OnPhase(int p)
        {
            if (p != 3 || enraged) yield break;
            enraged = true;
            hasCommand = false;
            ai.Desired = Vector2.zero;
            ai.PlayState("Rage");
            Hint("The Champion howls over the broken Engine. Soulfire pours off him.", 3f);
            Sfx.Play("v_troll_alert", Pos);
            yield return ai.Flash(0.9f, Soul);
            rageAura = SpriteVfx.Spawn("duo_soulfire", Pos + Vector2.down * 0.05f, 1.15f, Layer, Order - 1, false, 12f);
            if (rageAura != null) { rageAura.Looping(9999f); rageAura.Follow(transform); }
            ai.moveSpeed = baseSpeed * 1.25f;
            ai.PlayState("Idle");
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (rageAura != null) rageAura.Stop();
        }

        /// <summary>Brazen Reliquary gọi: dấu tím đã rơi -> lao tới đó ngay lượt sau.</summary>
        internal void CommandCharge(Vector2 at) { if (Alive && !enraged) { hasCommand = true; commandAt = at; } }

        /// <summary>Cối của Engine rơi trúng: mất máu, choáng, x2 sát thương.</summary>
        internal void ShellHit()
        {
            if (!Alive || Broken) return;
            hp.TakeDamage(hp.Max * 0.06f);
            hasCommand = false;
            StartCoroutine(BreakRoutine(2.2f, "The shell catches the Champion - he reels!"));
            if (partner != null) partner.CoverPartner();
        }

        /// <summary>19/11 (§345): thùng thuốc nổ nổ sát: mất 9% máu, choáng 2,4 s (x2 sát thương).</summary>
        internal void KegHit()
        {
            if (!Alive) return;
            hp.TakeDamage(hp.Max * 0.09f);
            if (Broken) return;
            hasCommand = false;
            StartCoroutine(BreakRoutine(2.4f, "The blast throws the Champion off his feet!"));
            if (partner != null) partner.CoverPartner();
        }

        /// <summary>Đang lao mà đâm trúng tượng -> tượng vỡ, true (người gọi dừng lao rồi choáng).</summary>
        private bool CrashStatue(Vector2 dir)
        {
            var mm = MM;
            var st = mm != null ? mm.StatueHit(Pos + dir * 0.22f, 0.26f) : null;
            if (st == null) return false;
            st.Shatter();
            return true;
        }

        private IEnumerator StatueStun()
        {
            ai.Desired = Vector2.zero;
            if (partner != null) partner.CoverPartner();
            yield return BreakRoutine(2.0f, "The Champion smashes through the statue and staggers!");
        }

        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (Broken) { yield return null; yield break; }
            if (hasCommand) { hasCommand = false; yield return BeaconCharge(commandAt); yield break; }
            ai.FaceX((PlayerPos - Pos).x);
            float gap = ai.GapToPlayer(), d = (PlayerPos - Pos).magnitude;
            if (gap <= meleeRange + 0.15f && Ready("slam")) { yield return Slam(); yield break; }
            // Ren đang đánh Engine mà mình ở xa -> về chắn
            if (PartnerAlive && !partner.Mounted && ReadyG("guard") && ((Vector2)partner.transform.position - PlayerPos).magnitude < 1.4f && (Pos - (Vector2)partner.transform.position).magnitude > 2.2f)
            { yield return Guard(); yield break; }
            if (ReadyG("hurl") && d > 1.7f)
            {
                var mm = MM;
                var keg = mm != null ? mm.NearestKeg(Pos, 3.2f) : null;
                if (keg != null && (keg.Center - PlayerPos).magnitude > 1.2f) { yield return PowderHurl(keg); yield break; }
            }
            if (ReadyG("slam") && d < 2.2f) { yield return Slam(); yield break; }
            if (ReadyG("rush") && d > 1.0f && d < 3.8f) { yield return Rush(phase == 1 ? 1 : phase == 2 ? 2 : 3); yield break; }
            yield return Stalk(0.5f, 1f);
        }

        // bị đè sát lâu: nện nhanh
        protected override IEnumerator Reposition() => Slam(true);

        // ------------------------------------------------------------------ Gauntlet Rush
        private IEnumerator Rush(int count)
        {
            Use("rush", phase == 1 ? 3.4f : 2.6f);
            for (int i = 0; i < count && Alive && ai.Player != null && !Broken; i++)
            {
                ai.Desired = Vector2.zero;
                Vector2 dir = (PlayerPos - Pos).normalized;
                ai.FaceX(dir.x);
                ai.PlayState("Prepare");
                float wind = i == 0 ? 0.55f : 0.32f;
                var line = Ray("m3_ray_warn", Pos, Pos + dir * 2.8f, 0.62f, 2f, Order - 1, 12f, wind);
                if (line != null) line.Tint(Soul);
                yield return Telegraph(wind, Soul);
                if (!Alive || Broken) yield break;
                ai.PlayState("Dash");
                Sfx.Play("duo_punch", Pos);
                bool hit = false, crashed = false;
                if (!statueHinted && MM != null && MM.StatueBlocks(Pos, PlayerPos, 0.05f))
                { statueHinted = true; Hint("He charges blindly - let a statue take the blow.", 2.4f); }
                for (float t = 0f; t < 0.7f && Alive && !Broken; t += Time.deltaTime)
                {
                    ai.Desired = dir * rushSpeed * (phase == 3 ? 1.15f : 1f);
                    if (!hit && ai.GapToPlayer() <= 0.12f) { ai.Hurt(rushDamage, Pos, 3.5f); hit = true; break; }
                    if (CrashStatue(dir)) { crashed = true; break; }            // 19/11 (§345)
                    yield return null;
                }
                if (crashed) { yield return StatueStun(); yield break; }
                ai.Desired = Vector2.zero;
                ai.PlayState("Recover");
                var w = Shockwave.Spawn(Pos + dir * 0.3f, 0.9f, 0.3f, 0f, Order - 1, SoulDim);   // đấm xuống đất: đẩy lùi, không mất máu
                w.knockback = 2.2f;
                Kick(dir, 0.03f, 0.15f);
                yield return ai.WaitFor(i == count - 1 ? 0.55f : 0.2f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Soulfire Slam
        private IEnumerator Slam(bool quick = false)
        {
            Use("slam", phase == 1 ? 6.5f : 5f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Slam");                                    // 9 khung @ 12 fps: giơ tay 0-4, nện khung 5
            SfxHooks.BossMove(this, "Slam");
            var warn = GroundStrike.Spawn(Pos, quick ? 0.35f : 0.42f, slamRadius * 0.55f, 0f, null, 0, 14f, Order - 1, Soul);   // vòng báo (không sát thương)
            warn.impactSfx = "none";
            yield return Telegraph(quick ? 0.35f : 0.42f, Soul);
            if (!Alive || Broken) yield break;
            Sfx.Play("duo_slam", Pos);
            SpriteVfx.Spawn("duo_soul_slam", Pos, 2.6f, Layer, Order - 1, false, 14f);
            Kick(Vector2.down, 0.07f, 0.3f);
            var w = Wave(Pos, slamRadius, 0.55f, slamDamage, "m3_wave_hymn");
            w.knockback = 2.6f;
            if (phase >= 2) { float g = Random.Range(0f, 120f); w.gapAngles = new[] { g, g + 120f, g + 240f }; w.gapWidth = 38f; }
            var mmS = MM;
            if (mmS != null) { mmS.IgniteKegs(Pos, slamRadius * 0.9f, 0.6f); mmS.ChipStatues(Pos, slamRadius * 0.6f, 1); }   // 19/11 (§345)
            int patches = phase == 1 ? 2 : phase == 2 ? 3 : 4;
            for (int k = 0; k < patches; k++)
            {
                Vector2 at = ClampArena(Pos + Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.3f), 0.95f);
                BurnPatch.Spawn(at, "duo_soulfire", 0.7f, phase == 3 ? 6f : 4.5f, Layer, Order - 2, 0.3f, 4f, Soul);
            }
            if (phase >= 2)
            {
                yield return ai.WaitFor(0.5f);
                if (!Alive || Broken) yield break;
                var w2 = Wave(Pos, slamRadius * 1.45f, 0.75f, slamDamage * 0.8f, "m3_wave_hymn");
                w2.knockback = 2f;
                float g2 = Random.Range(0f, 120f); w2.gapAngles = new[] { g2, g2 + 120f, g2 + 240f }; w2.gapWidth = 34f;
                Sfx.Play("duo_slam", Pos, 0.6f);
            }
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Beacon Charge (phối hợp với Engine)
        private IEnumerator BeaconCharge(Vector2 at)
        {
            Use("rush", 2.5f);
            ai.Desired = Vector2.zero;
            Vector2 dir = (at - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            var line = Ray("m3_ray_warn", Pos, at, 0.62f, 2.2f, Order - 1, 12f, 0.4f);
            if (line != null) line.Tint(Soul);
            yield return Telegraph(0.4f, Soul);
            if (!Alive || Broken) yield break;
            ai.PlayState("Dash");
            Sfx.Play("duo_punch", Pos);
            float nextTrail = 0f;
            bool hit = false;
            for (float t = 0f; t < 1.1f && Alive && !Broken; t += Time.deltaTime)
            {
                if ((at - Pos).magnitude < 0.2f) break;
                ai.Desired = (at - Pos).normalized * rushSpeed * 1.15f;
                if (!hit && ai.GapToPlayer() <= 0.12f) { ai.Hurt(rushDamage, Pos, 3f); hit = true; }
                if (CrashStatue((at - Pos).normalized)) { yield return StatueStun(); yield break; }   // 19/11 (§345)
                if (t >= nextTrail) { nextTrail = t + 0.22f; BurnPatch.Spawn(Pos, "duo_soulfire", 0.55f, 3f, Layer, Order - 2, 0.24f, 3f, Soul); }
                yield return null;
            }
            ai.Desired = Vector2.zero;
            ai.PlayState("Recover");
            Sfx.Play("duo_slam", Pos, 0.7f);
            SpriteVfx.Spawn("duo_soul_slam", Pos, 1.8f, Layer, Order - 1, false, 14f);
            var w = Wave(Pos, 1.2f, 0.4f, rushDamage * 0.85f, "m3_wave_hymn");
            w.knockback = 2.4f;
            Kick(Vector2.down, 0.05f, 0.2f);
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ 19/11 (§345): Powder Hurl (nhặt thùng thuốc nổ ném vào Ren)
        private IEnumerator PowderHurl(PowderKegMechanism keg)
        {
            Use("hurl", phase == 1 ? 10f : 7.5f);
            float until = Time.time + 1.8f;
            while (Alive && !Broken && keg.Ready && (keg.Center - Pos).magnitude > 0.42f && Time.time < until)
            {
                ai.Desired = (keg.Center - Pos).normalized * ai.moveSpeed * 1.35f;
                ai.FaceX(ai.Desired.x);
                ai.PlayState("Move", false);
                yield return null;
            }
            ai.Desired = Vector2.zero;
            if (!Alive || Broken || !keg.Ready || (keg.Center - Pos).magnitude > 0.7f || ai.Player == null) { ai.PlayState("Idle"); yield break; }
            var art = keg.Art;
            keg.Lift();
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Slam");                                     // khung giơ hai tay = nhấc thùng qua đầu
            var held = new GameObject("Held Keg").AddComponent<SpriteRenderer>();
            held.sprite = art;
            held.sortingLayerID = Layer; held.sortingOrder = Order + 2;
            held.transform.position = Pos + Vector2.up * 0.78f;
            if (!hurlHinted) { hurlHinted = true; Hint("The Champion hoists a powder keg - watch where it lands!", 2.2f); }
            Sfx.Play("keg_fuse", Pos, 0.8f);
            yield return Telegraph(0.45f, new Color(1f, 0.45f, 0.3f));
            if (held != null) Destroy(held.gameObject);
            if (!Alive || ai.Player == null) yield break;
            Vector2 vel = Vector2.zero;
            var prb = ai.Player.GetComponent<Rigidbody2D>();
            if (prb != null) vel = prb.linearVelocity;
            Vector2 at = ClampArena(PlayerPos + vel * 0.45f, 0.97f);
            var g = GroundStrike.Spawn(at, 0.95f, 0.95f, 0f, null, 0, 14f, Order + 1, new Color(1f, 0.3f, 0.15f));
            g.impactSfx = "none";
            if (art != null) { g.shell = art; g.shellFrom = Pos + Vector2.up * 0.78f; g.shellArc = 1.3f; }
            Sfx.Play("duo_punch", Pos, 0.7f);
            StartCoroutine(KegLands(at, 0.95f));
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        private IEnumerator KegLands(Vector2 at, float delay)
        {
            yield return new WaitForSeconds(delay);
            PowderKegMechanism.Blast(at, 0.95f, 24f, Layer, Order);
        }

        // ------------------------------------------------------------------ Bảo vệ Engine
        private IEnumerator Guard()
        {
            Use("guard", 7f);
            Hint("The Champion turns back to guard the Engine.", 1.8f);
            float end = Time.time + 1.6f;
            while (Time.time < end && Alive && PartnerAlive && !Broken)
            {
                Vector2 eng = partner.transform.position;
                Vector2 spot = eng + (PlayerPos - eng).normalized * 0.7f;            // đứng giữa Ren và Engine
                if ((spot - Pos).magnitude < 0.2f) break;
                ai.Desired = (spot - Pos).normalized * ai.moveSpeed * 1.5f;
                ai.FaceX(ai.Desired.x);
                ai.PlayState("Move", false);
                yield return null;
            }
            ai.Desired = Vector2.zero;
            if (Alive && !Broken && ai.Player != null && ai.GapToPlayer() < 1.6f) yield return Slam(true);
            else ai.PlayState("Idle");
        }
    }
}
