using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§344): THE HOLLOW CHAMPION — nửa "xương thịt" của song đấu Reliquary Wardens (Map 4; hình MEGAPACK Knight_LVL4, lửa phép TÍM).
    /// 20/11 (§350) "CỘT PHA LÊ" (người dùng: vũ khí không hợp đấu sĩ -> 4 cột pha lê 4 màu, chạy tới cột nào dùng bộ chiêu cột đó, lửa đổi màu theo):
    ///   Hắn là nguồn nâng bệ pháo (dây hồn nối sang Brazen Reliquary) -> phải hạ hắn trước.
    ///   MINIGAME 2 LỚP:
    ///     1. ĐUA: tay không một lúc thì GỌI một cột pha lê (cột bừng sáng, vạch đường màu pha lê, gào 1 s), chạy tới; Engine nã cối chặn đường Ren.
    ///        Ren tới trước bấm T (Attune) -> Ren hấp thụ bùa của pha lê, hắn tới nơi tay trắng -> hụt hẫng 2,3 s (x2 sát thương).
    ///     2. CẮT HÚT: hắn tới trước -> đứng HÚT pha lê 1,1 s (tia màu từ đỉnh cột vào ngực). Ren dồn đủ 6% máu hắn trong lúc hút -> pha lê vỡ
    ///        tung, hút hỏng, hắn choáng 2 s. Hút xong -> lửa phép trên người đổi màu pha lê (ChampionPalette) + bộ chiêu nguyên tố, 4-5 đòn thì cạn.
    ///   Cạn cả 4 cột -> 15 s sau rèn lại. Hắn gục -> cột còn sáng mở cho Ren tự hấp thụ (dùng đánh Engine).
    ///   TAY KHÔNG (lửa tím): Gauntlet Rush (lao đấm nối 1/2), Soulfire Slam (vòng sóng có khe, vũng lửa hồn).
    ///   CRIMSON (lửa): Flame Arc (quét 160° trước mặt, để lại dải lửa; pha 2 quét ngược) / Blazing Lunge (đâm lướt dài, vệt lửa trên đường).
    ///   AZURE (băng): Rime Lines (3/5 đường gai băng chạy về phía Ren, trúng bị chậm) / Frost Nova (hai vòng băng nối nhau, làm chậm).
    ///   VERDANT (độc): Thorn Orbit (3 mảnh pha lê xoay quanh người vừa đi vừa quét, trúng bị độc) / Venom Shard (ném mảnh lớn thẳng rồi giật về, vũng độc).
    ///   GILDED (thánh quang): Halo Shards (mảnh vàng bay vòng cung ra rồi quay về) / Judgement Rain (giáo sáng rơi vào vòng báo quanh Ren).
    /// </summary>
    public sealed class HollowChampionSkills : BossSkillsBase
    {
        public float rushSpeed = 4.0f, rushDamage = 18f, slamDamage = 20f, slamRadius = 1.7f;
        private BrazenReliquarySkills partner;
        private bool enraged, released;
        private SpriteVfx rageAura, elemAura;
        private float baseSpeed, nextCall, rearmAt = -1f;
        private string element = "";                    // "" = lửa tím gốc
        private int uses;
        private ChampionPalette palette;
        private static readonly Color Soul = new Color(0.72f, 0.42f, 1f);

        private bool PartnerAlive => partner != null && partner.Alive;
        // 19/11 (§345): hp chỉ được gán ở lượt Act đầu tiên -> con kia hỏi sớm hơn thì tự lấy Health
        internal bool Alive { get { if (hp == null) hp = GetComponent<Health>(); return hp != null && !hp.IsDead; } }
        internal float HpPercent => Alive ? hp.Percent : 0f;
        /// <summary>Màu lửa phép hiện tại (dây hồn của Engine đổi theo).</summary>
        internal Color Tint => element.Length > 0 ? CrystalPillarMechanism.ColorOf(element) : Soul;
        private Color Dim => new Color(Tint.r, Tint.g, Tint.b, 0.9f);
        private string Fire => element.Length > 0 ? "duo_soulfire_" + element : "duo_soulfire";
        private string SlamVfx => element.Length > 0 ? "duo_soul_slam_" + element : "duo_soul_slam";
        private static MapMechanics MM => MapMechanics.Instance;

        private static string Pretty(string c) =>
            c == "crimson" ? "Crimson crystal" : c == "azure" ? "Azure crystal" : c == "verdant" ? "Verdant crystal" : c == "gilded" ? "Gilded crystal" : c;

        protected override void OnEngage()
        {
            preferredRange = 1.1f;
            pinnedTimeMax = 2.4f; repositionCooldown = 3f;
            Use("rush", 1.2f); Use("slam", 3f);
            partner = FindFirstObjectByType<BrazenReliquarySkills>();
            var mm = MM;
            var arena = mm != null ? mm.MechRect("duo_arena") : null;
            if (arena.HasValue) { center = arena.Value.center; arenaRadii = arena.Value.size * 0.5f; }
            else if (partner != null) center = (Pos + (Vector2)partner.transform.position) * 0.5f;
            baseSpeed = ai.moveSpeed;
            nextCall = Time.time + 3.5f;
            palette = GetComponent<ChampionPalette>();
            hp.Died += _ => OnFell();
        }

        protected override int PhaseFor(float p)
        {
            if (engaged && partner != null && !partner.Alive) return 3;
            return p <= 0.5f ? 2 : 1;
        }
        protected override float CooldownMul() => phase == 3 ? 0.7f : phase == 2 ? 0.85f : 1f;

        protected override IEnumerator OnPhase(int p)
        {
            if (p == 2) { Hint("The Champion's soulfire flares - he fights faster now.", 2.4f); yield return ai.Flash(0.5f, Tint); yield break; }
            if (p != 3 || enraged) yield break;
            enraged = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Rage");
            Sfx.Play("v_troll_alert", Pos);
            yield return ai.Flash(0.9f, Tint);
            rageAura = SpriteVfx.Spawn(Fire, Pos + Vector2.down * 0.05f, 1.15f, Layer, Order - 1, false, 12f);
            if (rageAura != null) { rageAura.Looping(9999f); rageAura.Follow(transform); }
            ai.moveSpeed = baseSpeed * 1.25f;
            ai.PlayState("Idle");
        }

        private void OnFell()
        {
            SetElement("");
            if (released) return;
            released = true;
            var mm = MM;
            if (mm == null) return;
            bool any = false;
            foreach (var c in mm.Crystals()) if (c.Now == CrystalPillarMechanism.State.Dormant) { c.Release(); any = true; }
            if (any) Hints.Show("The crystals blaze free - attune to what remains [T]!", 3f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (rageAura != null) rageAura.Stop();
            if (elemAura != null) elemAura.Stop();
        }

        // ------------------------------------------------------------------ cập nhật: các cột rèn lại
        protected override void Update()
        {
            base.Update();
            if (!engaged || !Alive) return;
            var mm = MM;
            if (mm == null) return;
            var all = mm.Crystals();
            if (all.Count == 0) return;
            foreach (var c in all) if (c.Now != CrystalPillarMechanism.State.Spent) { rearmAt = -1f; return; }
            if (rearmAt < 0f) { rearmAt = Time.time + 15f; return; }
            if (Time.time < rearmAt) return;
            rearmAt = -1f;
            foreach (var c in all) c.Rearm();
            Hint("The crystals slowly glow back to life.", 2.4f);
        }

        // ------------------------------------------------------------------ não
        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (Broken) { yield return null; yield break; }
            ai.FaceX((PlayerPos - Pos).x);
            if (element.Length == 0 && Time.time >= nextCall)
            {
                var c = PickCrystal();
                if (c != null) { yield return RaceFor(c); yield break; }
                nextCall = Time.time + 2f;
            }
            if (element.Length > 0) { yield return ElementTurn(); yield break; }
            float gap = ai.GapToPlayer(), d = (PlayerPos - Pos).magnitude;
            if (gap <= meleeRange + 0.15f && Ready("slam")) { yield return Slam(); yield break; }
            if (ReadyG("slam") && d < 2.2f) { yield return Slam(); yield break; }
            if (ReadyG("rush") && d > 1.0f && d < 3.8f) { yield return Rush(phase == 1 ? 1 : 2); yield break; }
            yield return Stalk(0.5f, 1f);
        }

        protected override IEnumerator Reposition() => Slam(true);

        // ------------------------------------------------------------------ minigame: đua + hút pha lê
        private CrystalPillarMechanism PickCrystal()
        {
            var mm = MM;
            if (mm == null) return null;
            CrystalPillarMechanism best = null; float bd = float.MaxValue;
            foreach (var c in mm.Crystals())
            {
                if (c.Now != CrystalPillarMechanism.State.Dormant) continue;
                float d = (c.Stand - Pos).sqrMagnitude + Random.Range(0f, 1.5f);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        private IEnumerator RaceFor(CrystalPillarMechanism c)
        {
            c.Call();
            Color col = c.Tint;
            Hint("The Champion reaches for the " + Pretty(c.Crystal) + "! Get there first and attune [T]!", 3f);
            if (partner != null) partner.Interdict(c.Stand);
            ai.FaceX(c.Stand.x - Pos.x);
            ai.PlayState(ai.HasAnimState("Rage") ? "Rage" : "Prepare");
            Sfx.Play("v_troll_alert", Pos, 0.8f);
            var line = Ray("m3_ray_warn", Pos, c.Stand, 0.62f, 1.6f, Order - 1, 12f, 1.4f);
            if (line != null) line.Tint(new Color(col.r, col.g, col.b, 0.8f));
            yield return Telegraph(phase == 1 ? 1.0f : 0.8f, col);
            float until = Time.time + 5f;
            while (Alive && !Broken && Time.time < until && c.Now == CrystalPillarMechanism.State.Called)
            {
                Vector2 to = c.Stand - Pos;
                if (to.magnitude < 0.3f) break;
                ai.Desired = to.normalized * ai.moveSpeed * (phase == 1 ? 1.25f : 1.4f);
                ai.FaceX(to.x);
                ai.PlayState("Move", false);
                yield return null;
            }
            ai.Desired = Vector2.zero;
            if (!Alive) yield break;
            if (c.ClaimedByRen)
            {
                nextCall = Time.time + 7f;
                yield return BreakRoutine(2.3f, "The crystal is spent - the Champion grasps at nothing!");
                yield break;
            }
            if ((c.Stand - Pos).magnitude < 0.75f && c.TakeByChampion()) { yield return Absorb(c); yield break; }
            nextCall = Time.time + 3f;                              // kẹt đường: cột vẫn sáng, Ren vẫn hấp thụ được
            ai.PlayState("Idle");
        }

        /// <summary>Hút pha lê 1,1 s; Ren dồn đủ 6% máu hắn -> hút hỏng, choáng.</summary>
        private IEnumerator Absorb(CrystalPillarMechanism c)
        {
            Color col = c.Tint;
            ai.FaceX(c.Tip.x - Pos.x);
            ai.PlayState(ai.HasAnimState("Rage") ? "Rage" : "Prepare");
            Sfx.Play("crystal_absorb", Pos);
            Hint("He drinks the crystal - hit him hard to break the flow!", 1.8f);
            var beam = Ray("m3_ray_warn", c.Tip, Pos + Vector2.up * 0.6f, 0.62f, 1.3f, Order + 1, 14f, 1.2f);
            if (beam != null) beam.Tint(new Color(col.r, col.g, col.b, 0.9f));
            float start = hp.Current, need = hp.Max * 0.06f;
            bool broken = false;
            for (float t = 0f; t < 1.1f && Alive; t += Time.deltaTime)
            {
                ai.Desired = Vector2.zero;
                if (start - hp.Current >= need) { broken = true; break; }
                if (ai.Body != null) ai.Body.color = Color.Lerp(Color.white, col, 0.35f + 0.3f * Mathf.Sin(t * 30f));
                yield return null;
            }
            if (ai.Body != null) ai.Body.color = Color.white;
            if (beam != null) beam.Stop();
            if (!Alive) yield break;
            if (broken)
            {
                var bv = SpriteVfx.Spawn("m3_break", c.Tip, 1.1f, Layer, Order + 3, false, 14f);
                if (bv != null) bv.Tint(col);
                Sfx.Play("ward_break", c.Tip, 0.7f);
                nextCall = Time.time + 6f;
                yield return BreakRoutine(2f, "The crystal shatters in his grip - he staggers!");
                yield break;
            }
            SetElement(c.Crystal);
            uses = phase == 1 ? 4 : 5;
            Hint("The Champion burns with " + Pretty(c.Crystal).ToLowerInvariant() + " fire!", 2.2f);
            Sfx.Play("relic_claim", Pos, 0.6f);
            SpriteVfx.Spawn(SlamVfx, Pos, 1.6f, Layer, Order - 1, false, 14f);
            yield return ai.Flash(0.35f, col);
            ai.PlayState("Idle");
        }

        private void SetElement(string e)
        {
            element = e ?? "";
            if (palette != null) palette.Set(element);
            if (elemAura != null) { elemAura.Stop(); elemAura = null; }
            if (element.Length > 0)
            {
                elemAura = SpriteVfx.Spawn(Fire, Pos + Vector2.down * 0.05f, 1.0f, Layer, Order - 1, false, 12f, false);
                if (elemAura != null) { elemAura.Looping(9999f); elemAura.Follow(transform); }
            }
        }

        private void SpendElement()
        {
            if (--uses > 0) return;
            Hint("The " + Pretty(element).ToLowerInvariant() + " fire gutters out.", 2f);
            SetElement("");
            nextCall = Time.time + (phase == 1 ? 6f : 4.5f);
        }

        /// <summary>Ren hấp thụ pha lê vàng (hoặc rìu §349): 2 mảnh bay vào hắn.</summary>
        internal void TakeHatchets(Vector2 from, string sprite)
        {
            var art = WardenStatueMechanism.Load(sprite);
            for (int i = 0; i < 2; i++)
            {
                var b = DuoBullet.Spawn(art, from, new Vector2(i == 0 ? -2.5f : 2.5f, 2f), 0f, 0.2f, 3f, Layer, Order + 2, 1.2f);
                b.spinDeg = 900f; b.stopOnWalls = false;
                b.hitEnemy = transform;
                bool first = i == 0;
                b.onHitEnemy = _ =>
                {
                    if (!Alive) return;
                    hp.TakeDamage(hp.Max * 0.07f);
                    Sfx.Play("duo_punch", Pos);
                    SpriteVfx.Spawn("hit", Pos + Vector2.up * 0.5f, 0.9f, Layer, Order + 2, false);
                    if (first && !Broken) StartCoroutine(BreakRoutine(0.9f, "Radiant shards pierce the Champion!"));
                };
            }
        }

        private Sprite Shard => WardenStatueMechanism.Load("VFX/props/crystal_shard_" + (element.Length > 0 ? element : "azure"));

        // ------------------------------------------------------------------ bộ chiêu theo nguyên tố
        private IEnumerator ElementTurn()
        {
            float d = (PlayerPos - Pos).magnitude;
            if (!ReadyG("elem")) { yield return Stalk(0.45f, 1f); yield break; }
            switch (element)
            {
                case "crimson":
                    if (d < 1.9f) yield return FlameArc();
                    else if (d < 3.6f) yield return BlazingLunge();
                    else { yield return Stalk(0.6f, 1.1f); yield break; }
                    break;
                case "azure":
                    if (d < 2.0f && Ready("nova")) yield return FrostNova(); else yield return RimeLines();
                    break;
                case "verdant":
                    if (d < 3.0f) yield return ThornOrbit(); else yield return VenomShard();
                    break;
                case "gilded":
                    if (d > 1.4f && Ready("halo")) yield return HaloShards(); else yield return JudgementRain();
                    break;
                default: yield break;
            }
            SpendElement();
        }

        private static void Chill(Health h) { var s = h != null ? PlayerStatus.Of(h) : null; if (s != null) s.Slow(0.55f, 1.6f, "Chilled!"); }
        private static void Envenom(Health h) { if (h != null && h.IsPlayer) h.ApplyPoison(0.02f, 3f); }

        // CRIMSON -----------------------------------------------------------
        private IEnumerator FlameArc()
        {
            Use("elem", phase == 1 ? 2.4f : 1.9f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            var warn = GroundStrike.Spawn(Pos + dir * 0.85f, 0.5f, 0.95f, 0f, null, 0, 14f, Order - 1, Tint);
            warn.impactSfx = "none";
            yield return Telegraph(0.5f, Tint);
            if (!Alive || Broken) yield break;
            yield return SweepArc(dir, 1.8f, 20f);
            if (phase >= 2 && Alive && !Broken)
            {
                yield return ai.WaitFor(0.3f);
                yield return SweepArc((PlayerPos - Pos).normalized, 2.3f, 16f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        private IEnumerator SweepArc(Vector2 dir, float r, float dmg)
        {
            ai.PlayState("Recover");
            Sfx.Play("duo_punch", Pos);
            var w = Wave(Pos, r, 0.3f, dmg, "m3_wave_hymn");
            w.knockback = 2.4f;
            w.gapAngles = new[] { Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg };
            w.gapWidth = 200f;                                        // chỉ quét 160° phía trước: vòng ra sau lưng hắn là an toàn
            float a0 = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int k = -2; k <= 2; k++)                             // dải lửa đọng lại dọc mép cung
                BurnPatch.Spawn(ClampArena(Pos + Dir(a0 + k * 30f) * r * 0.85f, 0.97f), Fire, 0.6f, 3.5f, Layer, Order - 2, 0.28f, 4f, Tint);
            yield return ai.WaitFor(0.2f);
        }

        private IEnumerator BlazingLunge()
        {
            Use("elem", phase == 1 ? 2.6f : 2.1f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            var line = Ray("m3_ray_warn", Pos, Pos + dir * 2.7f, 0.62f, 1.6f, Order - 1, 12f, 0.6f);
            if (line != null) line.Tint(Tint);
            yield return Telegraph(0.55f, Tint);
            if (!Alive || Broken) yield break;
            ai.PlayState("Dash");
            Sfx.Play("duo_punch", Pos);
            Vector2 start = Pos; bool hit = false; float nextTrail = 0f;
            for (float t = 0f; t < 0.4f && Alive && !Broken; t += Time.deltaTime)
            {
                ai.Desired = dir * 6.5f;
                if (!hit && ai.Player != null && SegDist(PlayerPos, start, Pos + dir * 0.6f) < 0.38f) { ai.Hurt(22f, Pos, 3f); hit = true; }
                if (t >= nextTrail) { nextTrail = t + 0.08f; BurnPatch.Spawn(Pos, Fire, 0.55f, 3f, Layer, Order - 2, 0.24f, 4f, Tint); }
                yield return null;
            }
            ai.Desired = Vector2.zero;
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // AZURE -------------------------------------------------------------
        private IEnumerator RimeLines()
        {
            Use("elem", phase == 1 ? 3.0f : 2.4f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Slam");
            yield return Telegraph(0.45f, Tint);
            if (!Alive || Broken) yield break;
            Sfx.Play("duo_slam", Pos);
            Kick(Vector2.down, 0.07f, 0.25f);
            SpriteVfx.Spawn(SlamVfx, Pos, 1.6f, Layer, Order - 1, false, 14f);
            var burst = SpriteVfx.Load("m3_ground_burst");
            int lines = phase == 1 ? 3 : 5;
            float spread = phase == 1 ? 22f : 18f;
            float baseA = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int s = 0; s < 7; s++)
                for (int l = 0; l < lines; l++)
                {
                    Vector2 at = Pos + Dir(baseA + (l - (lines - 1) * 0.5f) * spread) * (0.55f + s * 0.42f);
                    if (EllipseR(at) > 1.02f) continue;
                    var g = GroundStrike.Spawn(at, 0.32f + s * 0.08f, 0.3f, 14f, burst, 1, 16f, Order + 1, Tint);
                    g.bodyColor = Color.Lerp(Color.white, Tint, 0.6f);
                    g.onHit = Chill;
                    g.impactSfx = l == 0 && s % 2 == 0 ? "glass_break" : "none";
                }
            yield return ai.WaitFor(0.8f);
            ai.PlayState("Idle");
        }

        private IEnumerator FrostNova()
        {
            Use("nova", 6.5f);
            Use("elem", 2.2f);
            ai.PlayState("Slam");
            var warn = GroundStrike.Spawn(Pos, 0.5f, 1.0f, 0f, null, 0, 14f, Order - 1, Tint);
            warn.impactSfx = "none";
            yield return Telegraph(0.5f, Tint);
            if (!Alive || Broken) yield break;
            Sfx.Play("duo_slam", Pos);
            Kick(Vector2.down, 0.08f, 0.3f);
            SpriteVfx.Spawn(SlamVfx, Pos, 2.4f, Layer, Order - 1, false, 14f);
            var w = Wave(Pos, 1.9f, 0.5f, 16f, "m3_wave_hymn");
            w.knockback = 2.2f; w.onHit = Chill;
            yield return ai.WaitFor(0.6f);
            if (Alive && !Broken)
            {
                var w2 = Wave(Pos, 2.7f, 0.7f, 12f, "m3_wave_hymn");
                w2.knockback = 2f; w2.onHit = Chill;
                float g = Random.Range(0f, 120f); w2.gapAngles = new[] { g, g + 120f, g + 240f }; w2.gapWidth = 36f;
                Sfx.Play("glass_break", Pos, 0.6f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        // VERDANT -----------------------------------------------------------
        private IEnumerator ThornOrbit()
        {
            Use("elem", phase == 1 ? 3.6f : 3.0f);
            ai.PlayState("Prepare");
            Hint("Venom shards whirl around him - stay out of their ring!", 1.6f);
            yield return Telegraph(0.45f, Tint);
            if (!Alive || Broken) yield break;
            var art = Shard;
            var shards = new List<SpriteRenderer>();
            for (int i = 0; i < 3; i++)
            {
                var s = new GameObject("Thorn Shard").AddComponent<SpriteRenderer>();
                s.sprite = art; s.sortingLayerID = Layer; s.sortingOrder = Order + 2;
                s.transform.localScale = Vector3.one * 1.1f;
                shards.Add(s);
            }
            float dur = phase == 1 ? 2.4f : 3.0f, ang = Random.Range(0f, 360f), nextHit = 0f, nextWhoosh = 0f;
            for (float t = 0f; t < dur && Alive && !Broken; t += Time.deltaTime)
            {
                ang += 300f * Time.deltaTime;
                Vector2 to = PlayerPos - Pos;
                ai.Desired = to.magnitude > 0.6f ? to.normalized * ai.moveSpeed * 0.75f : Vector2.zero;
                ai.PlayState("Move", false);
                if (t >= nextWhoosh) { nextWhoosh = t + 0.45f; Sfx.Play("duo_punch", Pos, 0.4f); }
                for (int i = 0; i < shards.Count; i++)
                {
                    Vector2 p = Pos + Vector2.up * 0.35f + Dir(ang + i * 120f) * 1.05f;
                    shards[i].transform.position = p;
                    shards[i].transform.rotation = Quaternion.Euler(0f, 0f, ang + i * 120f);
                    if (t >= nextHit && ai.Player != null && ((Vector2)PlayerPos + Vector2.up * 0.2f - p).magnitude < 0.36f)
                    { ai.Hurt(13f, p, 2.5f); Envenom(ai.Player.GetComponent<Health>()); nextHit = t + 0.6f; }
                }
                yield return null;
            }
            foreach (var s in shards) if (s != null) Destroy(s.gameObject);
            ai.Desired = Vector2.zero;
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.7f);                            // chóng mặt sau khi xoay: cửa sổ đánh
            ai.PlayState("Idle");
        }

        private IEnumerator VenomShard()
        {
            Use("elem", phase == 1 ? 2.8f : 2.2f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            var line = Ray("m3_ray_warn", Pos, Pos + dir * 3.2f, 0.62f, 1.4f, Order - 1, 12f, 0.5f);
            if (line != null) line.Tint(Tint);
            yield return Telegraph(0.5f, Tint);
            if (!Alive || Broken) yield break;
            ai.PlayState("Recover");
            Sfx.Play("duo_punch", Pos);
            Vector2 start = Pos + Vector2.up * 0.3f;
            Transform me = transform;
            Color col = Tint; string fire = Fire; int layer = Layer, order = Order;
            var b = DuoBullet.Spawn(Shard, start, Vector2.zero, 16f, 0.3f, 1.2f, Layer, Order + 2, 1.7f);
            b.spinDeg = 720f; b.stopOnWalls = false;
            bool dropped = false;
            b.path = t =>
            {
                float u = t / 1.2f;
                Vector2 back = me != null ? (Vector2)me.position + Vector2.up * 0.3f : start;
                Vector2 tip = start + dir * 3.2f * Mathf.Sin(Mathf.Min(u, 0.5f) * Mathf.PI);
                if (u >= 0.5f && !dropped) { dropped = true; BurnPatch.Spawn(tip, fire, 0.75f, 4f, layer, order - 2, 0.36f, 4f, col); }
                return u < 0.5f ? tip : Vector2.Lerp(tip, back, (u - 0.5f) * 2f);
            };
            yield return ai.WaitFor(1.4f);
            ai.PlayState("Idle");
        }

        // GILDED ------------------------------------------------------------
        private IEnumerator HaloShards()
        {
            Use("halo", 5f);
            Use("elem", phase == 1 ? 2.4f : 2.0f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            yield return Telegraph(0.45f, Tint);
            if (!Alive || Broken) yield break;
            ai.PlayState("Recover");
            Sfx.Play("duo_punch", Pos);
            float reach = Mathf.Clamp((PlayerPos - Pos).magnitude + 0.7f, 1.8f, 3.8f);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Transform me = transform;
            int n = phase == 1 ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                float side = n == 2 ? (i == 0 ? -1f : 1f) : i - 1f;
                Vector2 start = Pos + Vector2.up * 0.3f;
                var b = DuoBullet.Spawn(Shard, start, Vector2.zero, 14f, 0.22f, 1.6f, Layer, Order + 2, 1.3f);
                b.spinDeg = 900f; b.stopOnWalls = false;
                b.path = t =>
                {
                    float u = Mathf.Clamp01(t / 1.6f);
                    Vector2 home = me != null ? (Vector2)me.position + Vector2.up * 0.3f : start;
                    Vector2 outP = start + dir * reach * Mathf.Sin(u * Mathf.PI) + perp * side * 0.55f * Mathf.Sin(u * Mathf.PI * 2f);
                    return Vector2.Lerp(outP, home, Mathf.Max(0f, u - 0.6f) * 2.5f);
                };
            }
            yield return ai.WaitFor(1.0f);
            ai.PlayState("Idle");
        }

        private IEnumerator JudgementRain()
        {
            Use("elem", phase == 1 ? 2.8f : 2.2f);
            ai.PlayState("Slam");
            yield return Telegraph(0.35f, Tint);
            if (!Alive || Broken || ai.Player == null) yield break;
            var art = Shard;
            int n = phase == 1 ? 5 : 7;
            for (int i = 0; i < n; i++)
            {
                Vector2 at = i == 0 ? PlayerPos : ClampArena(PlayerPos + Random.insideUnitCircle * 1.0f, 0.97f);
                var g = GroundStrike.Spawn(at, 0.8f, 0.34f, 14f, SpriteVfx.Load("m3_ground_burst"), 1, 16f, Order + 1, Tint);
                g.bodyColor = Color.Lerp(Color.white, Tint, 0.5f);
                g.impactSfx = i % 2 == 0 ? "duo_shell_land" : "none";
                if (art != null) { g.shell = art; g.shellFrom = at + Vector2.up * 3f; g.shellArc = 0.05f; g.shellScale = 1.6f; }
                yield return ai.WaitFor(0.09f);
            }
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ cối của Engine rơi trúng
        internal void ShellHit()
        {
            if (!Alive || Broken) return;
            hp.TakeDamage(hp.Max * 0.06f);
            StartCoroutine(BreakRoutine(2.2f, "The shell catches the Champion - he reels!"));
        }

        // ------------------------------------------------------------------ TAY KHÔNG: Gauntlet Rush
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
                if (line != null) line.Tint(Tint);
                yield return Telegraph(wind, Tint);
                if (!Alive || Broken) yield break;
                ai.PlayState("Dash");
                Sfx.Play("duo_punch", Pos);
                for (float t = 0f; t < 0.7f && Alive && !Broken; t += Time.deltaTime)
                {
                    ai.Desired = dir * rushSpeed * (phase == 3 ? 1.15f : 1f);
                    if (ai.GapToPlayer() <= 0.12f) { ai.Hurt(rushDamage, Pos, 3.5f); break; }
                    yield return null;
                }
                ai.Desired = Vector2.zero;
                ai.PlayState("Recover");
                var w = Shockwave.Spawn(Pos + dir * 0.3f, 0.9f, 0.3f, 0f, Order - 1, Dim);
                w.knockback = 2.2f;
                Kick(dir, 0.03f, 0.15f);
                yield return ai.WaitFor(i == count - 1 ? 0.55f : 0.2f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ TAY KHÔNG: Soulfire Slam
        private IEnumerator Slam(bool quick = false)
        {
            Use("slam", phase == 1 ? 6.5f : 5f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Slam");
            SfxHooks.BossMove(this, "Slam");
            var warn = GroundStrike.Spawn(Pos, quick ? 0.35f : 0.42f, slamRadius * 0.55f, 0f, null, 0, 14f, Order - 1, Tint);
            warn.impactSfx = "none";
            yield return Telegraph(quick ? 0.35f : 0.42f, Tint);
            if (!Alive || Broken) yield break;
            Sfx.Play("duo_slam", Pos);
            SpriteVfx.Spawn(SlamVfx, Pos, 2.6f, Layer, Order - 1, false, 14f);
            Kick(Vector2.down, 0.07f, 0.3f);
            var w = Wave(Pos, slamRadius, 0.55f, slamDamage, "m3_wave_hymn");
            w.knockback = 2.6f;
            if (phase >= 2) { float g = Random.Range(0f, 120f); w.gapAngles = new[] { g, g + 120f, g + 240f }; w.gapWidth = 38f; }
            int patches = phase == 1 ? 2 : 3;
            for (int k = 0; k < patches; k++)
            {
                Vector2 at = ClampArena(Pos + Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.3f), 0.95f);
                BurnPatch.Spawn(at, Fire, 0.7f, 4.5f, Layer, Order - 2, 0.3f, 4f, Tint);
            }
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }
    }
}
