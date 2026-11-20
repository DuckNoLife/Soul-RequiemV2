using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§344): THE BRAZEN RELIQUARY — nửa "đồng" của song đấu Reliquary Wardens (Map 4, The Royal Reliquary; hình MEGAPACK Mimic_LVL4:
    /// cỗ pháo đồng có mắt, hoạt ảnh Fire / Vent làm bằng PixelLab). Chậm, giữ khoảng cách, khống chế sân bằng lửa; Hollow Champion áp sát.
    ///   Grapeshot  : quạt 5 (pha 2+: 7) đạn lửa, pha 2+ bắn hai loạt lệch nửa góc.
    ///   Mortar     : 3/4/6 quả cối rơi vào vòng báo (dưới chân Ren + đón đầu) -> nổ + VŨNG LỬA cam 4 s. QUẢ CỐI RƠI TRÚNG CHAMPION -> hắn choáng,
    ///                mất máu, nhận x2 sát thương (phản đòn chủ động: dụ cối rơi vào hắn).
    ///   Beacon     : quả đạn tím đánh dấu chỗ Ren -> Champion lập tức lao tới dấu, rải lửa hồn trên đường (phối hợp).
    ///   Brass Ram  : vạch báo -> lao thẳng (Dash), húc văng.
    ///   Overheat   : sau 4 chiêu thì xả hơi 2,8 s: đứng yên, nhận x1,75 sát thương (cửa sổ đánh trả).
    ///   Che chắn   : Champion đứng sát (≤ 1,3 đv) -> Engine chỉ nhận 55% sát thương ("tách" hai con ra).
    ///   Bị đè sát  : nổ nòng tại chỗ, đẩy Ren ra.
    ///   Champion gục -> MOLTEN CORE (pha 3): đỏ rực, nhịp chiêu x0,7, cối 6 quả, vũng lửa lâu hơn.
    /// 19/11 (§345) THE WARDEN GALLERY (phòng đấu riêng phía nam Reliquary): Engine đứng trên BỆ PHÁO (mốc `duo_gallery`) — Ren không với tới,
    ///   giáp đồng chỉ nhận 15% sát thương -> BẮT BUỘC hạ Hollow Champion trước. Trên bệ: chạy dọc ray tìm góc bắn; Grapeshot bị TƯỢNG chặn
    ///   (warden_statue) -> Engine dời sang chỗ có đường bắn, không có thì nã cối; cối rơi gần THÙNG THUỐC NỔ -> thùng nổ, rơi gần tượng -> tượng nứt.
    ///   Covering Barrage: Champion bị choáng (đâm tượng / dính thùng / dính cối) -> Engine nã vòng cối QUANH hắn (đứng sát Champion là an toàn
    ///   -> thưởng cho người dám áp sát). Champion gục -> Engine giật đứt khỏi bệ, nhảy xuống `duo_landing` (vòng báo, nện) rồi vào Molten Core.
    /// </summary>
    public sealed class BrazenReliquarySkills : BossSkillsBase
    {
        public float shellSpeed = 2.7f, grapeDamage = 11f, mortarDamage = 18f, ramDamage = 22f, ramSpeed = 4.2f;
        public int heatMax = 4;
        private int heat;
        private bool venting, enraged;
        private float nextGuardHint;
        private HollowChampionSkills partner;
        private bool mounted, coverPending, coverHinted, slideHinted;   // 19/11 (§345): bệ pháo Warden Gallery
        private Rect rail;
        private Vector2 landing;
        private float nextCover;
        private static readonly Color Brass = new Color(1f, 0.6f, 0.25f), Soul = new Color(0.72f, 0.42f, 1f), Molten = new Color(1f, 0.55f, 0.45f);

        private bool PartnerAlive => partner != null && partner.Alive;
        // 19/11 (§345): hp chỉ được gán ở lượt Act đầu tiên -> con kia hỏi sớm hơn thì tự lấy Health (tránh tưởng đồng đội đã chết)
        internal bool Alive { get { if (hp == null) hp = GetComponent<Health>(); return hp != null && !hp.IsDead; } }
        /// <summary>Đang đứng trên bệ pháo (Champion không cần về chắn).</summary>
        internal bool Mounted => mounted;
        private static MapMechanics MM => MapMechanics.Instance;

        protected override void OnEngage()
        {
            preferredRange = 2.0f;
            pinnedTimeMax = 1.2f; repositionCooldown = 3f;
            Use("grape", 1.4f); Use("mortar", 3.5f); Use("beacon", 8f); Use("ram", 6f);
            partner = FindFirstObjectByType<HollowChampionSkills>();
            if (partner != null) center = (Pos + (Vector2)partner.transform.position) * 0.5f;   // sân chung: tâm giữa hai con lúc vào trận
            // 19/11 (§345): Warden Gallery — sân = mốc duo_arena; đứng trong duo_gallery = lên bệ pháo
            var mm = MM;
            var arena = mm != null ? mm.MechRect("duo_arena") : null;
            if (arena.HasValue) { center = arena.Value.center; arenaRadii = arena.Value.size * 0.5f; }
            var g = mm != null ? mm.MechRect("duo_gallery") : null;
            if (g.HasValue && g.Value.Contains(Pos) && PartnerAlive)
            {
                mounted = true;
                rail = g.Value;
                var l = mm.MechRect("duo_landing");
                landing = l.HasValue ? l.Value.center : center;
                edgeLimit = 99f; pinnedTimeMax = 99f;                  // trên bệ: không "thoát góc" theo elip sân
                Use("grape", 1.0f); Use("mortar", 2.4f); Use("beacon", 6f);
                MakeGalleryEdgeLow();
            }
        }

        /// <summary>Mép bệ pháo là vách cao: đạn của Engine bay qua (gắn TrapLowCover cho khối chắn mặt trước bệ), người vẫn không trèo lên được.</summary>
        private void MakeGalleryEdgeLow()
        {
            var box = new Rect(rail.xMin - 0.6f, rail.yMin - 1.35f, rail.width + 1.2f, 1.3f);
            foreach (var c in Physics2D.OverlapBoxAll(box.center, box.size, 0f))
            {
                if (c == null || c.isTrigger || c.GetComponentInParent<Health>() != null || c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
                if (c.bounds.size.x > box.width + 0.5f || c.bounds.size.y > 3f) continue;   // tường bao dài (mép phòng) giữ nguyên chặn đạn
                if (c.GetComponent<TrapLowCover>() == null) c.gameObject.AddComponent<TrapLowCover>();
            }
        }

        /// <summary>Champion vừa bị choáng (tượng / thùng / cối): nã vòng cối quanh hắn ở lượt kế.</summary>
        internal void CoverPartner()
        {
            if (!Alive || !mounted || Time.time < nextCover) return;
            coverPending = true;
        }

        protected override int PhaseFor(float p)
        {
            if (engaged && partner != null && !partner.Alive) return 3;
            if (mounted && partner != null) return partner.HpPercent <= 0.5f ? 2 : 1;   // trên bệ: nhịp theo máu Champion
            return p <= 0.5f ? 2 : 1;
        }
        protected override float CooldownMul() => phase == 3 ? 0.7f : phase == 2 ? 0.85f : 1f;

        protected override IEnumerator OnPhase(int p)
        {
            if (p != 3 || enraged) yield break;
            enraged = true;
            ai.Desired = Vector2.zero;
            if (mounted) yield return LeapDown();
            Hint("\"Brass remembers its brother.\" The Reliquary glows white-hot.", 3f);
            ai.PlayState("Vent");
            Sfx.Play("steam_vent", Pos);
            yield return ai.Flash(1.2f, Molten);
            if (ai.Body != null) ai.Body.color = new Color(1f, 0.78f, 0.7f);
            ai.PlayState("Idle");
        }

        protected override void Update()
        {
            base.Update();
            if (hp == null || hp.IsDead || !engaged || Broken) return;
            // che chắn: Champion đứng sát -> giảm sát thương; xả hơi -> nhận thêm
            if (mounted) { hp.DamageTakenMul = 0.15f; return; }        // 19/11 (§345): giáp đồng trên bệ pháo
            bool guarded = !venting && PartnerAlive && ((Vector2)partner.transform.position - Pos).magnitude <= 1.3f;
            hp.DamageTakenMul = venting ? 1.75f : guarded ? 0.55f : 1f;
            if (guarded && ai.Player != null && ((Vector2)ai.Player.position - Pos).magnitude < 1.2f && Time.time >= nextGuardHint)
            {
                nextGuardHint = Time.time + 12f;
                Hint("The Champion shields the Engine. Draw them apart.", 2.4f);
            }
        }

        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (Broken) { yield return null; yield break; }
            ai.FaceX((PlayerPos - Pos).x);
            float d = (PlayerPos - Pos).magnitude;
            if (mounted) { yield return ThinkMounted(); yield break; }
            if (heat >= heatMax) { yield return Overheat(); yield break; }
            if (ReadyG("ram") && d > 1.6f && d < 4.2f && Random.value < 0.4f) { yield return Ram(); yield break; }
            if (PartnerAlive && ReadyG("beacon")) { yield return Beacon(); yield break; }
            if (ReadyG("mortar")) { yield return Mortar(); yield break; }
            if (ReadyG("grape")) { yield return Grapeshot(); yield break; }
            yield return Stalk(0.6f, 0.8f);
        }

        // ------------------------------------------------------------------ 19/11 (§345): trên bệ pháo
        private IEnumerator ThinkMounted()
        {
            if (coverPending && PartnerAlive) { yield return CoveringBarrage(); yield break; }
            if (heat >= heatMax) { yield return Overheat(); yield break; }
            if (PartnerAlive && ReadyG("beacon")) { yield return Beacon(); yield break; }
            var mm = MM;
            bool blocked = mm != null && mm.StatueBlocks(MuzzlePos, PlayerPos);
            if (ReadyG("grape"))
            {
                if (!blocked) { yield return Grapeshot(); yield break; }
                // tượng chắn: dời dọc ray tới chỗ có đường bắn
                float x = FiringSpotX(mm);
                if (!float.IsNaN(x))
                {
                    if (!slideHinted) { slideHinted = true; Hint("The Engine rolls along its gallery, hunting for a clear shot.", 2.2f); }
                    yield return RailMove(x, 1.4f, 1.35f);
                    if (mm == null || !mm.StatueBlocks(MuzzlePos, PlayerPos)) { yield return Grapeshot(); yield break; }
                }
            }
            if (ReadyG("mortar")) { yield return Mortar(); yield break; }
            // rảnh: trôi theo Ren dọc ray
            yield return RailMove(Mathf.Lerp(Pos.x, PlayerPos.x, 0.5f), 0.6f, 0.8f);
        }

        /// <summary>Hoành độ trên ray (gần chỗ hiện tại nhất) mà từ đó bắn thẳng tới Ren không bị tượng chắn; NaN nếu không có.</summary>
        private float FiringSpotX(MapMechanics mm)
        {
            float lo = rail.xMin + 0.35f, hi = rail.xMax - 0.35f, best = float.NaN, bestD = 99f;
            Vector2 off = MuzzlePos - Pos;
            for (int i = 0; i <= 10; i++)
            {
                float x = Mathf.Lerp(lo, hi, i / 10f);
                Vector2 m = new Vector2(x, Pos.y) + off;
                if (mm != null && mm.StatueBlocks(m, PlayerPos)) continue;
                float dd = Mathf.Abs(x - Pos.x);
                if (dd < bestD) { bestD = dd; best = x; }
            }
            return best;
        }

        /// <summary>Lăn dọc ray (giữ trong khung duo_gallery) tới hoành độ x.</summary>
        private IEnumerator RailMove(float x, float seconds, float speedMul)
        {
            x = Mathf.Clamp(x, rail.xMin + 0.35f, rail.xMax - 0.35f);
            float y = Mathf.Clamp(Pos.y, rail.yMin + 0.2f, rail.yMax - 0.2f);
            float end = Time.time + seconds;
            while (Time.time < end && Alive && mounted && Mathf.Abs(Pos.x - x) > 0.06f)
            {
                Vector2 to = new Vector2(x, y) - Pos;
                ai.Desired = to.normalized * ai.moveSpeed * speedMul;
                ai.FaceX((PlayerPos - Pos).x);
                ai.PlayState("Move", false);
                yield return null;
            }
            ai.Desired = Vector2.zero;
            ai.PlayState("Idle");
        }

        /// <summary>Champion choáng -> vòng cối quanh hắn; vòng trong an toàn (áp sát Champion là đúng).</summary>
        private IEnumerator CoveringBarrage()
        {
            coverPending = false;
            nextCover = Time.time + 9f;
            Use("mortar", 3f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Fire");
            yield return Telegraph(0.25f, Brass);
            if (!Alive || !PartnerAlive) { ai.PlayState("Idle"); yield break; }
            if (!coverHinted) { coverHinted = true; Hint("The Engine shells the ground around its fallen brother - stay close to him!", 2.6f); }
            Sfx.Play("duo_mortar", Pos);
            Muzzle(1.3f);
            Vector2 c = partner.transform.position;
            int n = phase >= 2 ? 8 : 6;
            float a0 = Random.Range(0f, 360f);
            var fly = SpriteVfx.Load("trap_fireball");
            var boom = SpriteVfx.Load("trap_fireball_end");
            for (int i = 0; i < n; i++)
            {
                Vector2 at = ClampArena(c + Dir(a0 + i * 360f / n) * 1.3f, 0.98f);
                var g = GroundStrike.Spawn(at, 0.95f, 0.46f, mortarDamage * 0.85f, boom, 0, 14f, Order + 1, Brass);
                g.impactSfx = i % 2 == 0 ? "duo_shell_land" : "none";
                g.bodyScale = 0.9f;
                if (fly.Length > 0) { g.shell = fly[0]; g.shellFrom = MuzzlePos + Vector2.up * 0.2f; g.shellArc = 1.5f; }
                StartCoroutine(AfterShell(at, 0.95f, 0f, false));
                yield return ai.WaitFor(0.05f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        /// <summary>Champion gục: Engine giật đứt khỏi bệ, nhảy xuống sân (vòng báo ở chỗ đáp), rồi chiến đấu dưới đất.</summary>
        private IEnumerator LeapDown()
        {
            Hint("With its brother fallen, the Engine tears itself from the gallery!", 3f);
            ai.PlayState("Prepare");
            Sfx.Play("ram_charge", Pos);
            yield return Telegraph(0.55f, Molten);
            var g = GroundStrike.Spawn(landing, 0.8f, 1.15f, ramDamage, SpriteVfx.Load("m3_ground_burst"), 1, 16f, Order + 1, Brass);
            g.impactSfx = "duo_engine_drop";
            g.bodyScale = 1.6f;
            g.knockback = 3.5f;
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols) if (c != null) c.enabled = false;
            ai.PlayState("Dash");
            Vector2 from = Pos;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float u = t / 0.8f;
                ai.Desired = Vector2.zero;
                ai.TeleportTo(Vector2.Lerp(from, landing, u * u) + Vector2.up * (1.1f * 4f * u * (1f - u)));
                yield return null;
            }
            ai.TeleportTo(landing);
            foreach (var c in cols) if (c != null) c.enabled = true;
            mounted = false;
            edgeLimit = 0.86f; pinnedTimeMax = 1.2f;
            hp.DamageTakenMul = 1f;
            Kick(Vector2.down, 0.1f, 0.35f);
            var w = Shockwave.Spawn(landing, 1.4f, 0.4f, 0f, Order - 1, new Color(1f, 0.7f, 0.4f, 0.9f));
            w.knockback = 3f;
            var mmL = MM; if (mmL != null) mmL.IgniteKegs(landing, 1.4f, 0.5f);
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.5f);
            ResetTrackers();
        }

        // bị đè sát: nổ nòng tại chỗ
        protected override IEnumerator Reposition()
        {
            ai.Desired = Vector2.zero;
            if (mounted) { yield return RailMove(rail.center.x + (Pos.x < rail.center.x ? 1f : -1f) * rail.width * 0.3f, 1.2f, 1.2f); yield break; }
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

        private Vector2 MuzzlePos => Pos + new Vector2((ai.Body != null && ai.Body.flipX ? -1f : 1f) * 0.38f, 0.32f);

        private void Muzzle(float scale)
        {
            var v = SpriteVfx.Spawn("duo_muzzle", MuzzlePos, scale, Layer, Order + 2, ai.Body != null && ai.Body.flipX, 15f);
        }

        // ------------------------------------------------------------------ Grapeshot
        private IEnumerator Grapeshot()
        {
            Use("grape", phase == 1 ? 3.4f : 2.6f);
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
                for (int k = 0; k < n; k++)
                {
                    float a = baseA + (k - (n - 1) * 0.5f) * 12f;
                    ai.ShootProjectile(Dir(a), shellSpeed, grapeDamage, 3.2f);
                }
                Muzzle(1.5f);
                Sfx.Play("duo_cannon", Pos);
                Kick(-aim, 0.03f, 0.15f);
            }
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Mortar (cối + vũng lửa; rơi trúng Champion = choáng)
        private IEnumerator Mortar()
        {
            Use("mortar", phase == 1 ? 6.5f : 5f);
            heat++;
            ai.Desired = Vector2.zero;
            ai.PlayState("Fire");
            yield return Telegraph(0.4f, Brass);
            if (hp.IsDead || ai.Player == null) yield break;
            Sfx.Play("duo_mortar", Pos);
            Muzzle(1.2f);
            int n = phase == 1 ? 3 : phase == 2 ? 4 : 6;
            var fly = SpriteVfx.Load("trap_fireball");
            var boom = SpriteVfx.Load("trap_fireball_end");
            Vector2 vel = Vector2.zero;
            var prb = ai.Player.GetComponent<Rigidbody2D>();
            if (prb != null) vel = prb.linearVelocity;
            for (int i = 0; i < n && !hp.IsDead && ai.Player != null; i++)
            {
                Vector2 at = i == 0 ? PlayerPos : i == 1 ? PlayerPos + vel * 0.9f : PlayerPos + Random.insideUnitCircle * 1.1f;
                at = ClampArena(at, 0.95f);
                var g = GroundStrike.Spawn(at, 1.05f, 0.42f, mortarDamage, boom, 0, 14f, Order + 1, Brass);
                g.impactSfx = "duo_shell_land";
                g.bodyScale = 0.9f;
                if (fly.Length > 0) { g.shell = fly[0]; g.shellFrom = MuzzlePos + Vector2.up * 0.2f; g.shellArc = 1.4f; }
                StartCoroutine(AfterShell(at, 1.05f, phase == 3 ? 6f : 4f, true));
                yield return ai.WaitFor(0.18f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        /// Cối chạm đất: để lại vũng lửa; Champion đứng trong vòng -> bị chính đồng đội bắn trúng.
        private IEnumerator AfterShell(Vector2 at, float delay, float burnSeconds, bool canHitPartner)
        {
            yield return new WaitForSeconds(delay);
            if (hp == null || hp.IsDead) yield break;
            if (burnSeconds > 0f)
                BurnPatch.Spawn(at, "duo_soulfire", 0.75f, burnSeconds, Layer, Order - 2, 0.34f, 4f, new Color(1f, 0.55f, 0.2f), new Color(1f, 0.6f, 0.3f));
            if (canHitPartner && PartnerAlive && ((Vector2)partner.transform.position - at).magnitude <= 0.42f + 0.28f) partner.ShellHit();
            var mm = MM;                                                  // 19/11 (§345): cối châm thùng thuốc nổ, làm nứt tượng
            if (mm != null) { mm.IgniteKegs(at, 0.75f, 0.4f); mm.ChipStatues(at, 0.5f, 2); }
        }

        // ------------------------------------------------------------------ Beacon (đánh dấu cho Champion lao tới)
        private IEnumerator Beacon()
        {
            Use("beacon", 11f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Soul);
            if (hp.IsDead || ai.Player == null) yield break;
            Vector2 at = ClampArena(PlayerPos, 0.95f);
            Muzzle(1.1f);
            Sfx.Play("duo_mortar", Pos);
            var fly = SpriteVfx.Load("trap_fireball");
            var g = GroundStrike.Spawn(at, 0.9f, 0.4f, 10f, SpriteVfx.Load("m3_ground_burst"), 1, 16f, Order + 1, Soul);
            g.impactSfx = "duo_shell_land";
            if (fly.Length > 0) { g.shell = fly[0]; g.shellFrom = MuzzlePos + Vector2.up * 0.2f; g.shellArc = 1.3f; g.shellColor = Soul; }
            Hint("A violet shell marks the ground - the Champion will come for it.", 2.2f);
            yield return ai.WaitFor(0.9f);
            if (PartnerAlive) partner.CommandCharge(at);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Brass Ram
        private IEnumerator Ram()
        {
            Use("ram", phase == 1 ? 9f : 7f);
            heat++;
            ai.Desired = Vector2.zero;
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            Sfx.Play("ram_charge", Pos);
            var line = Ray("m3_ray_warn", Pos, Pos + dir * 3.6f, 0.62f, 2.2f, Order - 1, 12f, 0.75f);
            if (line != null) line.Tint(Brass);
            yield return Telegraph(0.75f, Brass);
            if (hp.IsDead) yield break;
            ai.PlayState("Dash");
            bool hit = false;
            Vector2 last = Pos;
            float stuck = 0f;
            for (float t = 0f; t < 1.0f && !hp.IsDead; t += Time.deltaTime)
            {
                ai.Desired = dir * ramSpeed;
                if (!hit && ai.GapToPlayer() <= 0.1f) { ai.Hurt(ramDamage, Pos, 4f); hit = true; }
                stuck = (Pos - last).magnitude < ramSpeed * Time.deltaTime * 0.2f ? stuck + Time.deltaTime : 0f;
                last = Pos;
                if (stuck > 0.12f || EllipseR(Pos) > 0.98f) break;        // đâm tường / mép sân
                yield return null;
            }
            ai.Desired = Vector2.zero;
            ai.PlayState("Recover");
            if (stuck > 0.12f) { Sfx.Play("lg_wall_crash", Pos); Kick(-dir, 0.05f, 0.25f); }
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Overheat (cửa sổ đánh trả)
        private IEnumerator Overheat()
        {
            heat = 0;
            venting = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Vent");
            Sfx.Play("steam_vent", Pos);
            Hint(mounted ? "The Engine overheats - its guns fall silent. Go for the Champion!" : "The Engine overheats - strike it now!", 2f);
            yield return ai.Flash(phase == 3 ? 2.0f : 2.8f, Molten);
            venting = false;
            ai.PlayState("Idle");
        }
    }
}
