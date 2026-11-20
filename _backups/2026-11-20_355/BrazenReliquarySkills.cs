using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§344): THE BRAZEN RELIQUARY — nửa "đồng" của song đấu Reliquary Wardens (Map 4; hình MEGAPACK Mimic_LVL4: cỗ pháo đồng có mắt,
    /// hoạt ảnh Fire / Vent / Bite làm bằng PixelLab).
    /// 20/11 (§349) THIẾT KẾ LẠI — "THE RELIQUARY LISTS":
    ///   PHA 1 "ON THE TURRET" (Hollow Champion còn sống): 20/11 (§350, người dùng: khiên trông như đạn bắn xuyên ra ngoài) — bỏ khiên;
    ///     Engine ngồi trên BỆ PHÁO CAO giữa sân (mốc duo_core, ảnh engine_pedestal) — Ren chém không tới (chém vào chân bệ: tia lửa + nhắc),
    ///     dây hồn màu lửa của Champion nối sang hắn (hắn là nguồn nâng bệ). Là ụ pháo quấy rối: Grapeshot, Mortar, Turret Sweep (Champion dưới 50% máu),
    ///     INTERDICTION — Champion gọi giá vũ khí thì nã cối CHẶN ĐƯỜNG Ren tới giá (phối hợp minigame đua).
    ///     Quá nhiệt sau 4 chiêu -> im 2,4 s (nhịp thở).
    ///   CẢNH CHUYỂN PHA (hài): Champion gục -> bệ hạ xuống nửa chừng rồi KẸT, pháo ngơ ngác "...?", nhảy hai cái cho bệ xuống -> bệ sập,
    ///     pháo cắm mặt xuống sàn, ho khói, xu văng; ô quay bật lên "INSERT SOUL." -> nã một phát rồi vào trận.
    ///   PHA 2 "JACKPOT ENGINE": rời bệ, đi lại + CẬN CHIẾN (Maw Snap cắn, Brass Ram húc, nổ nòng khi bị đè).
    ///     Trên đầu mọc Ô QUAY (JackpotReel) — mỗi 8,5 s quay, Engine đứng nhả vòng xu; dừng ở ô nào bắn chiêu đó:
    ///       GOBLIN  — bắn 3-4 goblin cầm đuốc (Goblin_Torch Map 1) bay vòng cung rơi quanh Ren.
    ///       BOMB    — 6-8 quả bom rơi quanh Ren, cháy ngòi rồi nổ lần lượt.
    ///       SKULL   — 6-8 sọ ma bung ra rồi đuổi Ren chậm (chém vỡ được).
    ///       COIN    — xoắn ốc đồng xu 3-4 tay quay 2,2 s.
    ///       BOLT    — 6-8 tia sét rơi đuổi theo chỗ Ren (đón đầu).
    ///       ANVIL   — bóng đen bám chân Ren 1,4 s rồi khoá, đe sắt rơi + sóng chấn động.
    ///       HEART   — Engine hồi 7% máu + quá tải (chiêu nhanh hơn 8 s).
    ///       BUST    — nổ ngược trong ruột: mất 6% máu, choáng 3,2 s (x2 sát thương).
    ///     MINIGAME Ô QUAY: chém trúng Engine lúc ô đang quay -> ô GIẬT thêm 1 nấc (Jolt). Dải ô cố định, 3 nấc cuối chậm -> đếm nấc để đẩy
    ///     ô vào BUST, tránh HEART. Máu ≤ 45%: DOUBLE DOWN — sau mỗi lượt quay (trừ BUST) quay nhanh thêm lượt nữa.
    /// </summary>
    public sealed class BrazenReliquarySkills : BossSkillsBase
    {
        public float shellSpeed = 2.7f, grapeDamage = 10f, mortarDamage = 16f, ramDamage = 22f, ramSpeed = 4.2f, biteDamage = 22f;
        public int heatMax = 4;
        private int heat;
        private bool venting, unbound, transforming, cleaned, reelHinted, interdictPending;
        private float nextWardHint, nextReel, nextJolt, overclockUntil;
        private Vector2 home, interdictAt;
        private HollowChampionSkills partner;
        private SpriteVfx tether;
        private SpriteRenderer pedestal;                 // 20/11 (§350): bệ pháo cao giữa sân
        private SpriteMask pedMask;
        private BoxCollider2D pedCol;
        private Collider2D[] bodyCols;
        private Vector2 baseHome;                        // chân bệ
        private bool lowering;
        private int raisedOrder;
        private const float Lift = 1.22f, PedScale = 1f;   // 20/11 (§352): tháp đá crypt 128x134, tỉ lệ 1:1 (không mixel); mặt đỉnh cao 1,22 đv
        private JackpotReel reel;
        private readonly List<EnemyAI> goblins = new List<EnemyAI>();
        private static readonly Color Brass = new Color(1f, 0.6f, 0.25f), Soul = new Color(0.72f, 0.42f, 1f), Molten = new Color(1f, 0.55f, 0.45f);

        private bool PartnerAlive => partner != null && partner.Alive;
        // 19/11 (§345): hp chỉ được gán ở lượt Act đầu tiên -> con kia hỏi sớm hơn thì tự lấy Health
        internal bool Alive { get { if (hp == null) hp = GetComponent<Health>(); return hp != null && !hp.IsDead; } }
        private static MapMechanics MM => MapMechanics.Instance;

        protected override void OnEngage()
        {
            preferredRange = 1.4f;
            repositionCooldown = 3f;
            Use("grape", 1.5f); Use("mortar", 3.2f); Use("ram", 5f); Use("sweep", 6f);
            partner = FindFirstObjectByType<HollowChampionSkills>();
            var mm = MM;
            var arena = mm != null ? mm.MechRect("duo_arena") : null;
            if (arena.HasValue) { center = arena.Value.center; arenaRadii = arena.Value.size * 0.5f; }
            var core = mm != null ? mm.MechRect("duo_core") : null;
            baseHome = core.HasValue ? new Vector2(core.Value.center.x, core.Value.yMin + 0.1f) : Pos;
            home = baseHome;
            PlayerCombat.Struck += OnStruck;
            if (PartnerAlive) RaisePedestal();
            else { unbound = true; ai.TeleportTo(home); AttachReel(); }
        }

        private static Sprite whiteSprite;
        private static Sprite White()
        {
            if (whiteSprite != null) return whiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            return whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Dựng bệ pháo cao ở chân duo_core, đặt Engine lên đỉnh, tắt va chạm thân (không ai chạm tới), dây hồn sang Champion.</summary>
        private void RaisePedestal()
        {
            edgeLimit = 99f; pinnedTimeMax = 99f;
            var go = new GameObject("Engine Pedestal");
            go.transform.position = baseHome;
            go.transform.localScale = new Vector3(PedScale, PedScale, 1f);
            pedestal = go.AddComponent<SpriteRenderer>();
            pedestal.sprite = WardenStatueMechanism.Load("VFX/props/engine_pedestal");
            pedestal.spriteSortPoint = SpriteSortPoint.Pivot;
            if (ai.Body != null) { pedestal.sortingLayerID = ai.Body.sortingLayerID; pedestal.sortingOrder = ai.Body.sortingOrder; pedestal.sharedMaterial = ai.Body.sharedMaterial; }
            pedestal.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // lúc hạ: phần chìm dưới sàn bị cắt
            var mg = new GameObject("Pedestal Mask");
            mg.transform.position = baseHome + Vector2.up * 1.48f;
            mg.transform.localScale = new Vector3(75f, 75f, 1f);              // 4 px @100 -> 3 x 3 đv, mép dưới = mặt sàn
            pedMask = mg.AddComponent<SpriteMask>();
            pedMask.sprite = White();
            var cg = new GameObject("Pedestal Collider");
            cg.transform.position = baseHome + Vector2.up * 0.16f;
            pedCol = cg.AddComponent<BoxCollider2D>();
            pedCol.size = new Vector2(1.1f, 0.32f);
            bodyCols = GetComponentsInChildren<Collider2D>();
            foreach (var c in bodyCols) if (c != null) c.enabled = false;
            if (ai.Body != null) { raisedOrder = 2; ai.Body.sortingOrder += raisedOrder; }
            home = baseHome + Vector2.up * Lift;
            ai.TeleportTo(home);
            tether = Ray("m3_ray_warn", Pos, Pos + Vector2.right, 0.62f, 0.8f, Order - 3, 12f, 9999f);
            if (tether != null) tether.Tint(new Color(0.75f, 0.45f, 1f, 0.55f));
        }

        private void AttachReel()
        {
            if (reel == null) reel = JackpotReel.Attach(transform, 1.0f, Layer, Order + 6);
            edgeLimit = 0.86f; pinnedTimeMax = 1.2f;
            ai.moveSpeed = Mathf.Max(ai.moveSpeed, 1.1f);                // rời bệ: đi lại, áp sát cắn
            nextReel = Time.time + 2.5f;
        }

        protected override int PhaseFor(float p)
        {
            if (!engaged || partner == null || partner.Alive) return 1;
            return p <= 0.45f ? 3 : 2;
        }
        protected override float CooldownMul()
        {
            float m = phase == 3 ? 0.75f : phase == 2 ? 0.9f : PartnerAlive && partner.HpPercent <= 0.5f ? 0.85f : 1f;
            return Time.time < overclockUntil ? m * 0.75f : m;
        }

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

        protected override void OnDisable()
        {
            base.OnDisable();
            PlayerCombat.Struck -= OnStruck;
            if (tether != null) tether.Stop();
            if (pedestal != null) Destroy(pedestal.gameObject);
            if (pedMask != null) Destroy(pedMask.gameObject);
            if (pedCol != null) Destroy(pedCol.gameObject);
            Cleanup();
        }

        private void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            foreach (var g in goblins) if (g != null && g.Health != null && !g.Health.IsDead) g.Health.TakeDamage(99999f);
            goblins.Clear();
            DuoBullet.ClearAll();
        }

        protected override void Update()
        {
            base.Update();
            if (hp == null || !engaged) return;
            if (hp.IsDead) { Cleanup(); return; }
            if (!unbound || transforming)
            {
                hp.DamageTakenMul = 0f;                                   // trên bệ cao: không ai chạm tới
                if (!unbound && !lowering)
                {
                    if (((Vector2)transform.position - home).sqrMagnitude > 0.0004f) ai.TeleportTo(home);   // ghim trên đỉnh bệ
                    if (tether != null && partner != null)
                    {
                        PlaceRay(tether, baseHome + Vector2.up * 0.9f, (Vector2)partner.transform.position + Vector2.up * 0.6f, 0.62f, 0.8f);
                        var c = partner.Tint; tether.Tint(new Color(c.r, c.g, c.b, 0.55f));
                    }
                }
                return;
            }
            hp.DamageTakenMul = Broken ? 2f : venting ? 1.75f : 1f;   // BUST (BreakRoutine) = x2
        }

        /// <summary>Ren chém: pha 1 -> khiên dội tia lửa; pha 2 lúc ô đang quay -> ô giật một nấc.</summary>
        private void OnStruck(Vector2 c, float r)
        {
            if (this == null || hp == null || hp.IsDead || !engaged) return;
            if (!unbound)
            {
                if ((c - (baseHome + Vector2.up * 0.3f)).magnitude > r + 0.75f) return;
                SpriteVfx.Spawn("hit", baseHome + Vector2.up * 0.4f + Random.insideUnitCircle * 0.3f, 0.7f, Layer, Order + 4, false, 18f, false);
                Sfx.Play("break_stone", baseHome, 0.35f);
                if (Time.time >= nextWardHint) { nextWardHint = Time.time + 8f; Hint("The Engine sits out of reach atop its turret. Bring down the Champion who holds it up!", 2.6f); }
                return;
            }
            if ((c - (Pos + Vector2.up * 0.35f)).magnitude > r + 0.65f) return;
            if (reel != null && reel.Spinning && Time.time >= nextJolt) { nextJolt = Time.time + 0.15f; reel.Jolt(); }
        }

        /// <summary>Champion gọi giá vũ khí: lượt sau nã cối chặn đường Ren tới giá.</summary>
        internal void Interdict(Vector2 target)
        {
            if (!Alive || unbound) return;
            interdictPending = true; interdictAt = target;
        }

        // ------------------------------------------------------------------ não
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

        private IEnumerator ThinkWarded()
        {
            if (interdictPending) { yield return Interdiction(); yield break; }
            if (heat >= heatMax) { yield return Overheat(); yield break; }
            if (ReadyG("mortar")) { yield return Mortar(); yield break; }
            if (PartnerAlive && partner.HpPercent <= 0.5f && ReadyG("sweep")) { yield return TurretSweep(); yield break; }
            if (ReadyG("grape")) { yield return Grapeshot(); yield break; }
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

        private Vector2 MuzzlePos => Pos + new Vector2((ai.Body != null && ai.Body.flipX ? -1f : 1f) * 0.38f, 0.32f);
        private void Muzzle(float scale) => SpriteVfx.Spawn("duo_muzzle", MuzzlePos, scale, Layer, Order + 2, ai.Body != null && ai.Body.flipX, 15f);

        // ------------------------------------------------------------------ PHA 1: ụ pháo
        private IEnumerator Grapeshot()
        {
            Use("grape", unbound ? 3.0f : 3.4f);
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

        private Vector2 PlayerVel()
        {
            var prb = ai.Player != null ? ai.Player.GetComponent<Rigidbody2D>() : null;
            return prb != null ? prb.linearVelocity : Vector2.zero;
        }

        private void Shell(Vector2 at, float warn, float radius, float dmg, float burnSeconds, Color c)
        {
            var g = GroundStrike.Spawn(at, warn, radius, dmg, SpriteVfx.Load("trap_fireball_end"), 0, 14f, Order + 1, c);
            g.impactSfx = "duo_shell_land";
            g.bodyScale = 0.9f;
            var fly = SpriteVfx.Load("trap_fireball");
            if (fly.Length > 0) { g.shell = fly[0]; g.shellFrom = MuzzlePos + Vector2.up * 0.2f; g.shellArc = 1.4f; }
            StartCoroutine(AfterShell(at, warn, burnSeconds));
        }

        private IEnumerator AfterShell(Vector2 at, float delay, float burnSeconds)
        {
            yield return new WaitForSeconds(delay);
            if (hp == null || hp.IsDead) yield break;
            if (burnSeconds > 0f)
                BurnPatch.Spawn(at, "duo_soulfire", 0.75f, burnSeconds, Layer, Order - 2, 0.34f, 4f, new Color(1f, 0.55f, 0.2f), new Color(1f, 0.6f, 0.3f));
            // 20/11 (§353): quái KHÔNG gây sát thương cho nhau -> cối rơi trúng Champion không còn làm hắn mất máu / choáng.
        }

        /// <summary>Champion vừa gọi giá: 3 quả cối rơi dọc đường Ren -> giá, quả thứ tư đón đầu Ren.</summary>
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

        /// <summary>Champion dưới 50%: nòng quét một cung 100° bắn 10 phát nối nhau.</summary>
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

        // ------------------------------------------------------------------ chuyển pha: CẢNH HẠ BỆ (hài)
        private void PlaceLowered(float drop)
        {
            if (pedestal != null) pedestal.transform.position = baseHome + Vector2.down * drop;
            ai.TeleportTo(baseHome + Vector2.up * Mathf.Max(0f, Lift - drop));
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
            // 1) hạ xuống nửa chừng
            Sfx.Play("lift_grind", baseHome);
            SpriteVfx.Spawn("lg_dust", baseHome + Vector2.left * 0.5f, 1.2f, Layer, Order + 1, false, 12f, false);
            SpriteVfx.Spawn("lg_dust", baseHome + Vector2.right * 0.5f, 1.2f, Layer, Order + 1, true, 12f, false);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime) { PlaceLowered(Mathf.SmoothStep(0f, Lift * 0.45f, t / 1.1f)); yield return null; }
            // 2) kẹt
            Sfx.Play("lift_jam", baseHome);
            Kick(Vector2.down, 0.05f, 0.2f);
            ai.PlayState("Idle");
            yield return new WaitForSeconds(0.45f);
            var b1 = SpeechBubble.Show(Pos + Vector2.up * 1.2f, "...?", 1.3f, new Color(1f, 0.8f, 0.45f), "The Brazen Reliquary");
            if (b1 != null) b1.Follow(transform);
            ai.FaceX(-1f); yield return new WaitForSeconds(0.4f);
            ai.FaceX(1f); yield return new WaitForSeconds(0.4f);
            ai.FaceX(-1f); yield return new WaitForSeconds(0.35f);
            // 3) nhảy hai cái
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
            // 4) bệ sập hẳn, pháo cắm mặt
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
            // 5) ô quay bật lên
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
            // trả lại trận
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

        /// <summary>Xu văng ra khi pháo cắm mặt (trang trí, không gây sát thương).</summary>
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

        // ------------------------------------------------------------------ PHA 2: ô quay
        private IEnumerator Jackpot()
        {
            nextReel = Time.time + (phase >= 3 ? 6.5f : 8.5f) * (Time.time < overclockUntil ? 0.8f : 1f);
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
                if (Time.time >= chatter) { chatter = Time.time + 0.95f; CoinRing(6, 1.9f); }
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

        private Sprite Icon(string n) => WardenStatueMechanism.Load("VFX/props/reel_" + n);

        private void CoinRing(int n, float speed)
        {
            var coin = Icon("coin");
            float a0 = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                var b = DuoBullet.Spawn(coin, Pos + Vector2.up * 0.4f, Dir(a0 + i * 360f / n) * speed, 7f, 0.12f, 3.5f, Layer, Order + 2, 0.4f);
                b.spinDeg = 360f;
            }
            Sfx.Play("coin_spray", Pos, 0.5f);
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
            Hint("SKULL! Ghost skulls hunt you - cut them down!", 2f);
            ai.PlayState("Fire");
            yield return Telegraph(0.3f, Soul);
            Sfx.Play("skull_wail", Pos);
            var skull = Icon("skull");
            int n = phase >= 3 ? 8 : 6;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Dir(i * 360f / n);
                var b = DuoBullet.Spawn(skull, Pos + Vector2.up * 0.45f + d * 0.4f, d * 1.4f, 12f, 0.18f, 6f, Layer, Order + 2, 0.55f);
                b.homing = 110f; b.maxSpeed = 1.9f; b.killable = true;
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

        private static Sprite shadowSprite;
        private static Sprite Shadow()
        {
            if (shadowSprite != null) return shadowSprite;
            var tex = new Texture2D(48, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 24; y++)
                for (int x = 0; x < 48; x++)
                {
                    float dx = (x - 23.5f) / 24f, dy = (y - 11.5f) / 12f, r = dx * dx + dy * dy;
                    tex.SetPixel(x, y, r < 1f ? new Color(0f, 0f, 0f, 0.55f * (1f - r * 0.5f)) : Color.clear);
                }
            tex.Apply();
            return shadowSprite = Sprite.Create(tex, new Rect(0, 0, 48, 24), new Vector2(0.5f, 0.5f), 100f);
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

        // ------------------------------------------------------------------ PHA 2: cận chiến
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
