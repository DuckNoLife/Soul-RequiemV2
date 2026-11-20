using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§344): THE HOLLOW CHAMPION — nửa "xương thịt" của song đấu Reliquary Wardens (Map 4; hình MEGAPACK Knight_LVL4, lửa phép TÍM).
    /// 20/11 (§350) "KỆ PHA LÊ": hắn là nguồn nâng bệ pháo (dây hồn nối sang Brazen Reliquary) -> phải hạ hắn trước.
    ///   MINIGAME 2 LỚP:
    ///     1. ĐUA: tay không một lúc thì GỌI một kệ pha lê (kệ bừng sáng, vòng aura màu nguyên tố, gào 1 s), chạy tới; Engine nã cối chặn đường Ren.
    ///        Ren tới trước bấm T (Attune) -> Ren hấp thụ bùa, hắn tới nơi tay trắng -> hụt hẫng 2,3 s (x2 sát thương).
    ///     2. CẮT HÚT: hắn tới trước -> đứng HÚT pha lê 1,1 s (mảnh pha lê bay vào ngực). Ren dồn đủ 6% máu hắn -> hút hỏng, choáng 2 s.
    ///   Cạn cả 4 kệ -> 15 s sau rèn lại. Hắn gục -> kệ còn sáng mở cho Ren tự hấp thụ (dùng đánh Engine).
    /// 20/11 (§353) LÀM LẠI VFX + BỘ CHIÊU (người dùng: chiêu đập đất giống VFX boss Map 3; mỗi nguyên tố 2 chiêu khác nhau, có hồi máu (chỉ lục),
    ///   có buff thủ; đổi màu chi tiết tím trên người thay vì lửa bao quanh; thêm aura nguyên tố):
    ///   NGOẠI HÌNH: hút xong -> mũ trùm / khói hồn / đá nắm đấm đổi màu pha lê (ChampionPalette, đổi AnimatorOverrideController)
    ///     + VÒNG AURA nguyên tố dưới chân (duo_aura_<c>: vòng sáng + hạt bay lên) + đèn Light2D màu pha lê. Bỏ lửa hồn bao quanh người.
    ///   TAY KHÔNG (tím): Gauntlet Rush (lao đấm 1/2 nhát) / Grave Quake (nện đất: nứt đá + bụi duo_quake, vòng chấn động có khe ở pha 2).
    ///   CRIMSON — lửa, ÁP SÁT: Inferno Ring (vòng lửa bùng quanh người, để lại 6 ngọn lửa trên mép vòng; pha 2 thêm vòng thứ hai có khe)
    ///                          / Flame Lunge (đâm lướt dài, vệt lửa duo_flame cháy trên đường; pha 2 lướt 2 lần).
    ///   AZURE — băng, PHÒNG THỦ: Glacial Ward (BUFF THỦ: khiên băng 6 s, giảm 55% sát thương nhận; Ren dồn đủ 5% máu hắn -> khiên vỡ, hắn choáng;
    ///                          hết giờ mà khiên còn -> NỔ BĂNG quanh người, trúng bị chậm) / Ice Spikes (hàng gai băng duo_ice_burst chạy tới Ren; pha 2: 3 hàng toả).
    ///   VERDANT — sinh, HỒI MÁU: Verdant Bloom (trồng MẦM PHA LÊ sau lưng: cột sáng duo_heal hồi 2% máu / 0,6 s trong 6 s — CHÉM MẦM 3 NHÁT để nhổ,
    ///                          nhổ được thì hắn choáng; chỉ dùng khi máu dưới 85%) / Thorn Snare (bụi gai duo_thorns mọc thành vòng quanh Ren: trúng độc + trói chậm).
    ///   GILDED — thánh quang, TẦM XA: Judgement (3/5 tia sét vàng duo_smite đuổi theo chỗ Ren đứng) / Radiant Halo (4/6 cầu sáng duo_light_orb xoay quanh hắn
    ///                          rồi lần lượt phóng vào Ren — chém vỡ được).
    ///   Quái KHÔNG gây sát thương cho nhau (§353): bỏ "cối của Engine rơi trúng Champion".
    /// </summary>
    public sealed class HollowChampionSkills : BossSkillsBase
    {
        public float rushSpeed = 4.0f, rushDamage = 18f, slamDamage = 20f, slamRadius = 1.7f;
        private BrazenReliquarySkills partner;
        private bool enraged, released, wardOn, bloomOn;
        private SpriteVfx rageAura, elemAura, wardBubble, bloomPillar;
        private GameObject bloomSeed;
        private System.Action<Vector2, float> bloomStruck;
        private Light2D elemLight;
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
        private static MapMechanics MM => MapMechanics.Instance;
        private Vector2 Chest => Pos + Vector2.up * 0.55f;

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
            rageAura = SpriteVfx.Spawn("duo_soulfire", Pos + Vector2.down * 0.05f, 1.15f, Layer, Order - 1, false, 12f, false);
            if (rageAura != null) { rageAura.Looping(9999f); rageAura.Follow(transform); rageAura.gameObject.SetActive(element.Length == 0); }
            ai.moveSpeed = baseSpeed * 1.25f;
            ai.PlayState("Idle");
        }

        private void OnFell()
        {
            SetElement("");
            EndWard();
            EndBloom();
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
            EndWard();
            EndBloom();
        }

        // ------------------------------------------------------------------ cập nhật: các kệ rèn lại
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
            nextCall = Time.time + 3f;                              // kẹt đường: kệ vẫn sáng, Ren vẫn hấp thụ được
            ai.PlayState("Idle");
        }

        /// <summary>Hút pha lê 1,1 s (mảnh pha lê màu bay từ kệ vào ngực); Ren dồn đủ 6% máu hắn -> hút hỏng, choáng.</summary>
        private IEnumerator Absorb(CrystalPillarMechanism c)
        {
            Color col = c.Tint;
            ai.FaceX(c.Tip.x - Pos.x);
            ai.PlayState(ai.HasAnimState("Rage") ? "Rage" : "Prepare");
            Sfx.Play("crystal_absorb", Pos);
            Hint("He drinks the crystal - hit him hard to break the flow!", 1.8f);
            var shard = WardenStatueMechanism.Load("VFX/props/crystal_shard_" + c.Crystal);
            float start = hp.Current, need = hp.Max * 0.06f, nextMote = 0f;
            bool broken = false;
            for (float t = 0f; t < 1.1f && Alive; t += Time.deltaTime)
            {
                ai.Desired = Vector2.zero;
                if (start - hp.Current >= need) { broken = true; break; }
                if (t >= nextMote) { nextMote = t + 0.16f; StartCoroutine(Mote(shard, c.Tip, 0.32f)); }
                if (ai.Body != null) ai.Body.color = Color.Lerp(Color.white, col, 0.35f + 0.3f * Mathf.Sin(t * 30f));
                yield return null;
            }
            if (ai.Body != null) ai.Body.color = Color.white;
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
            Hint("The Champion is charged with " + Pretty(c.Crystal).ToLowerInvariant() + " power!", 2.2f);
            Sfx.Play("relic_absorb", Pos, 0.8f);
            ElementBurst(Pos);
            yield return ai.Flash(0.35f, col);
            ai.PlayState("Idle");
        }

        /// <summary>Một mảnh pha lê nhỏ bay vòng nhẹ từ `from` vào ngực hắn rồi tan (dòng pha lê bị hút).</summary>
        private IEnumerator Mote(Sprite art, Vector2 from, float seconds)
        {
            if (art == null) yield break;
            var s = new GameObject("Crystal Mote").AddComponent<SpriteRenderer>();
            s.sprite = art; s.sortingLayerID = Layer; s.sortingOrder = Order + 2;
            FxLighting.MakeUnlit(s);
            s.transform.localScale = Vector3.one * 0.55f;
            Vector2 side = Random.insideUnitCircle * 0.25f;
            for (float t = 0f; t < seconds && s != null; t += Time.deltaTime)
            {
                float k = t / seconds;
                s.transform.position = Vector2.Lerp(from, Chest, k * k) + side * Mathf.Sin(k * Mathf.PI);
                s.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
                yield return null;
            }
            if (s != null) Destroy(s.gameObject);
        }

        /// <summary>Nổ hình nguyên tố tại chỗ (lúc hút xong) — chỉ là hình.</summary>
        private void ElementBurst(Vector2 at)
        {
            string n = element == "crimson" ? "duo_fire_ring" : element == "azure" ? "duo_ice_burst" : element == "verdant" ? "duo_thorns" : element == "gilded" ? "duo_smite" : null;
            if (n != null) SpriteVfx.Spawn(n, at, element == "crimson" ? 1.2f : 1f, Layer, Order + (element == "crimson" ? -1 : 1), Random.value < 0.5f, 16f, false);
        }

        private void SetElement(string e)
        {
            element = e ?? "";
            if (palette != null) palette.Set(element);
            if (elemAura != null) { elemAura.Stop(); elemAura = null; }
            if (rageAura != null) rageAura.gameObject.SetActive(element.Length == 0);   // lửa hồn tím của pha 3 nhường chỗ cho aura nguyên tố
            if (element.Length > 0)
            {
                elemAura = SpriteVfx.Spawn("duo_aura_" + element, Pos + Vector2.down * 0.02f, 1.2f, Layer, Order - 1, false, 12f, false);
                if (elemAura != null) { elemAura.Looping(9999f); elemAura.Follow(transform); }
            }
            if (elemLight == null && element.Length > 0)
            {
                var lg = new GameObject("Element Light");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                elemLight = lg.AddComponent<Light2D>();
                elemLight.lightType = Light2D.LightType.Point;
                elemLight.pointLightInnerRadius = 0.1f;
                elemLight.pointLightOuterRadius = 1.7f;
                elemLight.falloffIntensity = 0.6f;
                elemLight.intensity = 0.9f;
            }
            if (elemLight != null) { elemLight.enabled = element.Length > 0; elemLight.color = Tint; }
        }

        private void SpendElement()
        {
            if (--uses > 0) return;
            Hint("The " + Pretty(element).ToLowerInvariant() + " power fades from him.", 2f);
            SetElement("");
            nextCall = Time.time + (phase == 1 ? 6f : 4.5f);
        }

        /// <summary>Ren hấp thụ pha lê vàng: 2 mảnh bay vào hắn.</summary>
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

        // ------------------------------------------------------------------ bộ chiêu theo nguyên tố (2 chiêu / màu)
        private IEnumerator ElementTurn()
        {
            float d = (PlayerPos - Pos).magnitude;
            if (!ReadyG("elem")) { yield return Stalk(0.45f, 1f); yield break; }
            switch (element)
            {
                case "crimson":
                    if (d < 2.0f && Ready("ring")) yield return InfernoRing();
                    else if (d < 4.2f) yield return FlameLunge();
                    else { yield return Stalk(0.6f, 1.15f); yield break; }
                    break;
                case "azure":
                    if (!wardOn && Ready("ward")) yield return GlacialWard(); else yield return IceSpikes();
                    break;
                case "verdant":
                    if (!bloomOn && Ready("bloom") && hp.Percent < 0.85f) yield return VerdantBloom(); else yield return ThornSnare();
                    break;
                case "gilded":
                    if (d > 1.6f && Ready("halo")) yield return RadiantHalo(); else yield return Judgement();
                    break;
                default: yield break;
            }
            SpendElement();
        }

        private static void Chill(Health h) { var s = h != null ? PlayerStatus.Of(h) : null; if (s != null) s.Slow(0.55f, 1.6f, "Chilled!"); }
        private static void Snare(Health h)
        {
            if (h == null || !h.IsPlayer) return;
            h.ApplyPoison(0.02f, 3f);
            var s = PlayerStatus.Of(h);
            if (s != null) s.Slow(0.35f, 1.2f, "Snared by thorns!");
        }

        // CRIMSON -----------------------------------------------------------
        /// <summary>Inferno Ring: vòng lửa bùng quanh người, 6 ngọn lửa đọng trên mép vòng; pha 2 thêm vòng rộng có 3 khe.</summary>
        private IEnumerator InfernoRing()
        {
            Use("ring", 5.5f);
            Use("elem", phase == 1 ? 2.2f : 1.8f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Slam");
            const float r = 1.25f;
            var warn = GroundStrike.Spawn(Pos, 0.55f, r, 0f, null, 0, 14f, Order - 1, Tint);
            warn.impactSfx = "none";
            yield return Telegraph(0.55f, Tint);
            if (!Alive || Broken) yield break;
            Sfx.Play("duo_slam", Pos);
            Sfx.Play("trap_fire", Pos);
            Kick(Vector2.down, 0.07f, 0.3f);
            SpriteVfx.Spawn("duo_fire_ring", Pos, 2.3f, Layer, Order - 1, false, 16f, false);
            var w = Shockwave.Spawn(Pos, r, 0.4f, 20f, Order - 1, Tint);
            w.hidden = true; w.squash = 0.5f; w.knockback = 2.4f;             // hình do duo_fire_ring vẽ
            float a0 = Random.Range(0f, 60f);
            for (int k = 0; k < 6; k++)
                BurnPatch.Spawn(ClampArena(Pos + Dir(a0 + k * 60f) * r * 0.95f, 0.97f), "duo_flame", 0.8f, 3.5f, Layer, Order - 2, 0.24f, 4f, Tint);
            if (phase >= 2)
            {
                yield return ai.WaitFor(0.5f);
                if (Alive && !Broken)
                {
                    Sfx.Play("trap_fire", Pos, 0.7f);
                    var w2 = Shockwave.Spawn(Pos, 2.2f, 0.6f, 14f, Order - 1, Dim);
                    w2.knockback = 2f;
                    float g = Random.Range(0f, 120f); w2.gapAngles = new[] { g, g + 120f, g + 240f }; w2.gapWidth = 40f;
                }
            }
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        /// <summary>Flame Lunge: đâm lướt dài, vệt lửa duo_flame cháy trên đường; pha 2 lướt 2 lần.</summary>
        private IEnumerator FlameLunge()
        {
            Use("elem", phase == 1 ? 2.6f : 2.1f);
            int n = phase == 1 ? 1 : 2;
            for (int i = 0; i < n && Alive && !Broken; i++)
            {
                Vector2 dir = (PlayerPos - Pos).normalized;
                ai.FaceX(dir.x);
                ai.PlayState("Prepare");
                float wind = i == 0 ? 0.55f : 0.35f;
                var line = Ray("m3_ray_warn", Pos, Pos + dir * 2.7f, 0.62f, 1.6f, Order - 1, 12f, wind);
                if (line != null) line.Tint(Tint);
                yield return Telegraph(wind, Tint);
                if (!Alive || Broken) yield break;
                ai.PlayState("Dash");
                Sfx.Play("trap_fire", Pos);
                Vector2 start = Pos; bool hit = false; float nextTrail = 0f;
                for (float t = 0f; t < 0.4f && Alive && !Broken; t += Time.deltaTime)
                {
                    ai.Desired = dir * 6.5f;
                    if (!hit && ai.Player != null && SegDist(PlayerPos, start, Pos + dir * 0.6f) < 0.38f) { ai.Hurt(22f, Pos, 3f); hit = true; }
                    if (t >= nextTrail) { nextTrail = t + 0.08f; BurnPatch.Spawn(Pos, "duo_flame", 0.65f, 3f, Layer, Order - 2, 0.22f, 4f, Tint); }
                    yield return null;
                }
                ai.Desired = Vector2.zero;
                ai.PlayState("Recover");
                yield return ai.WaitFor(i == n - 1 ? 0.5f : 0.2f);
            }
            ai.PlayState("Idle");
        }

        // AZURE -------------------------------------------------------------
        /// <summary>Glacial Ward (BUFF THỦ): khiên băng 6 s giảm 55% sát thương; Ren phá được thì hắn choáng, không thì nổ băng khi hết giờ.</summary>
        private IEnumerator GlacialWard()
        {
            Use("ward", 14f);
            Use("elem", 1.0f);
            ai.Desired = Vector2.zero;
            ai.PlayState(ai.HasAnimState("Rage") ? "Rage" : "Prepare");
            Sfx.Play("trap_ice", Pos);
            Hint("Glacial Ward! His frost shell dulls your blows - shatter it with hits, or get clear before it bursts!", 2.6f);
            yield return Telegraph(0.4f, Tint);
            if (!Alive || Broken) yield break;
            StartCoroutine(WardRoutine());
            ai.PlayState("Idle");
        }

        private IEnumerator WardRoutine()
        {
            wardOn = true;
            wardBubble = SpriteVfx.Spawn("duo_frost_ward", Chest, 1.3f, Layer, Order + 1, false, 10f, false);
            if (wardBubble != null) { wardBubble.Looping(9999f); wardBubble.Follow(transform); }
            hp.Guard(0.45f, 6f);
            float start = hp.Current, need = hp.Max * 0.05f, end = Time.time + 6f;
            bool shattered = false;
            while (Alive && wardOn && Time.time < end)
            {
                if (start - hp.Current >= need) { shattered = true; break; }
                yield return null;
            }
            if (!wardOn) yield break;                                   // đã bị huỷ (gục / đặt lại)
            EndWard();
            if (!Alive) yield break;
            if (shattered)
            {
                Sfx.Play("ice_shatter", Pos);
                SpriteVfx.Spawn("duo_ice_burst", Pos, 1.1f, Layer, Order + 1, false, 18f, false);
                yield return BreakRoutine(1.6f, "The Glacial Ward shatters - he staggers!");
                yield break;
            }
            // khiên còn nguyên tới cuối -> nổ băng quanh người
            Sfx.Play("ice_shatter", Pos);
            Kick(Vector2.down, 0.06f, 0.25f);
            var w = Shockwave.Spawn(Pos, 1.8f, 0.45f, 14f, Order - 1, Tint);
            w.knockback = 2f; w.onHit = Chill;
            for (int i = 0; i < 6; i++)
                SpriteVfx.Spawn("duo_ice_burst", ClampArena(Pos + Dir(i * 60f + 30f) * 1.05f, 0.97f), 0.75f, Layer, Order + 1, i % 2 == 0, 18f, false);
        }

        /// <summary>Tắt khiên băng (bỏ giảm sát thương + hình bong bóng).</summary>
        private void EndWard()
        {
            if (!wardOn && wardBubble == null) return;
            wardOn = false;
            if (hp != null) hp.Guard(1f, 0f);                            // guardMul = 1 -> hết giảm sát thương
            if (wardBubble != null) { wardBubble.Stop(); wardBubble = null; }
        }

        /// <summary>Ice Spikes: hàng gai băng nhô lên lần lượt chạy về phía Ren (pha 2: 3 hàng toả), trúng bị chậm.</summary>
        private IEnumerator IceSpikes()
        {
            Use("elem", phase == 1 ? 2.6f : 2.1f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Slam");
            yield return Telegraph(0.45f, Tint);
            if (!Alive || Broken) yield break;
            Sfx.Play("duo_slam", Pos, 0.7f);
            Sfx.Play("trap_ice", Pos);
            Kick(Vector2.down, 0.05f, 0.2f);
            var frames = SpriteVfx.Load("duo_ice_burst");
            int lines = phase == 1 ? 1 : 3;
            float spread = 24f, baseA = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int s = 0; s < 6; s++)
                for (int l = 0; l < lines; l++)
                {
                    Vector2 at = Pos + Dir(baseA + (l - (lines - 1) * 0.5f) * spread) * (0.6f + s * 0.45f);
                    if (EllipseR(at) > 1.02f) continue;
                    var g = GroundStrike.Spawn(at, 0.35f + s * 0.09f, 0.3f, 14f, frames, 3, 16f, Order + 1, Tint);
                    g.bodyScale = 0.8f;
                    g.onHit = Chill;
                    g.impactSfx = l == 0 && s % 2 == 0 ? "ice_shatter" : "none";
                }
            yield return ai.WaitFor(0.9f);
            ai.PlayState("Idle");
        }

        // VERDANT -----------------------------------------------------------
        /// <summary>Verdant Bloom (HỒI MÁU — chỉ pha lê lục): trồng mầm pha lê sau lưng, cột sáng hồi máu 6 s; Ren chém mầm 3 nhát để nhổ.</summary>
        private IEnumerator VerdantBloom()
        {
            Use("bloom", 16f);
            Use("elem", 1.2f);
            ai.Desired = Vector2.zero;
            Vector2 away = Pos - PlayerPos;
            Vector2 at = ClampArena(Pos + (away.sqrMagnitude > 0.01f ? away.normalized : Vector2.up) * 0.9f, 0.9f);
            ai.FaceX(at.x - Pos.x);
            ai.PlayState("Slam");
            yield return Telegraph(0.45f, Tint);
            if (!Alive || Broken) yield break;
            Sfx.Play("root_spike", at);
            SpriteVfx.Spawn("duo_thorns", at, 0.7f, Layer, Order + 1, false, 18f, false);
            Hint("Verdant Bloom! The crystal sprout heals him - strike it 3 times to tear it out!", 2.6f);
            StartCoroutine(BloomRoutine(at));
            yield return ai.WaitFor(0.3f);
            ai.PlayState("Idle");
        }

        private IEnumerator BloomRoutine(Vector2 at)
        {
            bloomOn = true;
            var sr = new GameObject("Verdant Bloom").AddComponent<SpriteRenderer>();
            bloomSeed = sr.gameObject;
            sr.sprite = WardenStatueMechanism.Load("VFX/props/crystal_shard_verdant");
            sr.transform.position = at;
            sr.transform.localScale = Vector3.one * 1.5f;
            sr.sortingLayerID = Layer; sr.sortingOrder = Order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            FxLighting.MakeUnlit(sr);
            bloomPillar = SpriteVfx.Spawn("duo_heal", at, 0.9f, Layer, Order + 1, false, 12f, false);
            if (bloomPillar != null) bloomPillar.Looping(9999f);
            int hits = 0;
            bloomStruck = (c, r) =>
            {
                if (bloomSeed == null || ((Vector2)at + Vector2.up * 0.15f - c).sqrMagnitude > (r + 0.35f) * (r + 0.35f)) return;
                hits++;
                Sfx.Play("glass_break", at, 0.6f);
                SpriteVfx.Spawn("hit", at + Vector2.up * 0.2f, 0.7f, Layer, Order + 2, false);
                if (sr != null) sr.color = hits >= 2 ? new Color(1f, 0.6f, 0.6f) : new Color(1f, 0.85f, 0.85f);
            };
            PlayerCombat.Struck += bloomStruck;
            float end = Time.time + 6f, nextHeal = Time.time + 0.6f, nextFx = 0f;
            while (Alive && bloomOn && Time.time < end && hits < 3)
            {
                if (Time.time >= nextHeal)
                {
                    nextHeal += 0.6f;
                    hp.Heal(hp.Max * 0.02f);
                    StartCoroutine(Mote(sr != null ? sr.sprite : null, at + Vector2.up * 0.3f, 0.35f));
                }
                if (Time.time >= nextFx)
                {
                    nextFx = Time.time + 1.2f;
                    Sfx.Play("life_drain", Pos, 0.5f);
                    var hv = SpriteVfx.Spawn("duo_heal", Pos, 0.6f, Layer, Order + 1, false, 14f, false);
                    if (hv != null) hv.Follow(transform);
                }
                yield return null;
            }
            bool torn = hits >= 3;
            EndBloom();
            if (torn && Alive)
            {
                SpriteVfx.Spawn("duo_thorns", at, 0.8f, Layer, Order + 1, false, 18f, false);
                yield return BreakRoutine(1.6f, "The bloom is torn out - the Champion staggers!");
            }
        }

        private void EndBloom()
        {
            if (bloomStruck != null) { PlayerCombat.Struck -= bloomStruck; bloomStruck = null; }
            if (bloomPillar != null) { bloomPillar.Stop(); bloomPillar = null; }
            if (bloomSeed != null) { Destroy(bloomSeed); bloomSeed = null; }
            bloomOn = false;
        }

        /// <summary>Thorn Snare: bụi gai mọc thành vòng (tâm + 5 quanh) ngay chỗ Ren; trúng: độc + trói chậm. Pha 2: hai đợt đuổi theo.</summary>
        private IEnumerator ThornSnare()
        {
            Use("elem", phase == 1 ? 2.6f : 2.1f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Slam");
            yield return Telegraph(0.35f, Tint);
            if (!Alive || Broken) yield break;
            Sfx.Play("root_spike", Pos);
            var frames = SpriteVfx.Load("duo_thorns");
            int waves = phase == 1 ? 1 : 2;
            for (int wv = 0; wv < waves && Alive && !Broken; wv++)
            {
                Vector2 c = ClampArena(PlayerPos, 0.95f);
                float a0 = Random.Range(0f, 72f);
                for (int i = 0; i < 6; i++)
                {
                    Vector2 at = i == 0 ? c : ClampArena(c + Dir(a0 + i * 72f) * 0.78f, 0.97f);
                    var g = GroundStrike.Spawn(at, 0.7f, 0.3f, 12f, frames, 3, 16f, Order + 1, Tint);
                    g.bodyScale = 0.7f;
                    g.onHit = Snare;
                    g.impactSfx = i == 0 ? "root_spike" : "none";
                }
                yield return ai.WaitFor(0.75f);
            }
            yield return ai.WaitFor(0.3f);
            ai.PlayState("Idle");
        }

        // GILDED ------------------------------------------------------------
        /// <summary>Judgement: 3/5 tia sét vàng giáng xuống lần lượt đúng chỗ Ren đang đứng.</summary>
        private IEnumerator Judgement()
        {
            Use("elem", phase == 1 ? 2.8f : 2.2f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Slam");
            yield return Telegraph(0.35f, Tint);
            if (!Alive || Broken || ai.Player == null) yield break;
            var frames = SpriteVfx.Load("duo_smite");
            int n = phase == 1 ? 3 : 5;
            for (int i = 0; i < n && Alive && !Broken; i++)
            {
                Vector2 at = ClampArena(PlayerPos, 0.97f);
                var g = GroundStrike.Spawn(at, 0.75f, 0.42f, 16f, frames, 4, 14f, Order + 2, Tint);
                g.bodyScale = 0.9f;
                g.impactSfx = "bolt_strike";
                yield return ai.WaitFor(0.32f);
            }
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        /// <summary>Radiant Halo: 4/6 cầu sáng xoay quanh hắn rồi lần lượt phóng thẳng vào Ren (chém vỡ được).</summary>
        private IEnumerator RadiantHalo()
        {
            Use("halo", 6f);
            Use("elem", phase == 1 ? 2.2f : 1.8f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            Sfx.Play("hymn_pop", Pos);
            var frames = SpriteVfx.Load("duo_light_orb");
            if (frames.Length == 0) yield break;
            int n = phase == 1 ? 4 : 6;
            Transform me = transform, pl = ai.Player;
            for (int i = 0; i < n; i++)
            {
                float ang0 = i * 360f / n, release = 1.0f + i * 0.18f;
                bool fired = false; Vector2 from = Pos, dirv = Vector2.down, last = Pos;
                var b = DuoBullet.Spawn(frames[0], Chest, Vector2.zero, 12f, 0.16f, release + 2.4f, Layer, Order + 2, 1f);
                b.frames = frames; b.stopOnWalls = false; b.killable = true;
                b.path = t =>
                {
                    if (t < release)
                    {
                        Vector2 c = me != null ? (Vector2)me.position + Vector2.up * 0.55f : last;
                        last = c + Dir(ang0 + t * 240f) * Mathf.Lerp(0.3f, 0.85f, Mathf.Clamp01(t / 0.5f));
                        return last;
                    }
                    if (!fired)
                    {
                        fired = true; from = last;
                        Vector2 tgt = pl != null ? (Vector2)pl.position + Vector2.up * 0.2f : from + Vector2.down;
                        dirv = (tgt - from).sqrMagnitude > 0.0001f ? (tgt - from).normalized : Vector2.down;
                        Sfx.Play("hymn_pop", from, 0.5f);
                    }
                    return from + dirv * 4.6f * (t - release);
                };
            }
            yield return ai.WaitFor(1.0f);
            ai.PlayState("Idle");
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

        // ------------------------------------------------------------------ TAY KHÔNG: Grave Quake (nện đất)
        /// <summary>§353: bỏ vòng nện + sóng thánh ca kiểu Map 3 — nay là NỨT ĐÁ + bụi (duo_quake) và vòng chấn động màu hồn của chính hắn.</summary>
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
            Sfx.Play("quake_rumble", Pos, 0.6f);
            SpriteVfx.Spawn("duo_quake", Pos, 1.7f, Layer, Order - 1, Random.value < 0.5f, 14f, false);
            Kick(Vector2.down, 0.08f, 0.3f);
            var w = Shockwave.Spawn(Pos, slamRadius, 0.55f, slamDamage, Order - 1, Dim);
            w.knockback = 2.6f;
            if (phase >= 2) { float g = Random.Range(0f, 120f); w.gapAngles = new[] { g, g + 120f, g + 240f }; w.gapWidth = 38f; }
            int patches = phase == 1 ? 2 : 3;
            for (int k = 0; k < patches; k++)
            {
                Vector2 at = ClampArena(Pos + Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.3f), 0.95f);
                BurnPatch.Spawn(at, "duo_soulfire", 0.7f, 4.5f, Layer, Order - 2, 0.3f, 4f, Tint);
            }
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }
    }
}
