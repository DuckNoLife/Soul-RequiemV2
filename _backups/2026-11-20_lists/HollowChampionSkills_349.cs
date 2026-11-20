using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§344): THE HOLLOW CHAMPION — nửa "xương thịt" của song đấu Reliquary Wardens (Map 4; hình MEGAPACK Knight_LVL4 lửa TÍM).
    /// 20/11 (§349) THIẾT KẾ LẠI — "THE RELIQUARY LISTS" (người dùng: pháo kẹt trên bệ, hiệp sĩ lao linh tinh vào bom):
    ///   Champion là người GIỮ KHIÊN cho Brazen Reliquary (dây hồn tím nối hai con; Engine bất tử khi hắn còn sống) -> phải hạ hắn trước.
    ///   MINIGAME ĐUA GIÀNH VŨ KHÍ: 4 giá vũ khí relic (relic_rack) ở 4 góc sân, bị xích hồn. Tay không được một lúc, hắn GỌI một giá
    ///   (giá sáng tím + vạch đường, gào 1 s) rồi chạy tới; Engine nã cối CHẶN ĐƯỜNG Ren. Ren tới trước bấm T -> cướp bùa (RelicBoons) và
    ///   hắn tới nơi tay không -> hụt hẫng 2,3 s (x2 sát thương). Hắn tới trước -> cầm vũ khí, bộ chiêu ĐỔI HẲN, dùng 4-5 đòn thì vũ khí vỡ.
    ///   Hết cả 4 giá -> 15 s sau các giá rèn lại. Hắn gục -> giá còn xích mở hết cho Ren tự lấy (dùng đánh Engine).
    ///   TAY KHÔNG : Gauntlet Rush (lao đấm, nối 1/2 lần), Soulfire Slam (vòng sóng có khe, vũng lửa hồn).
    ///   HALBERD   : Long Sweep (quét 160° phía trước, pha 2 quét ngược lại rộng hơn) / Lunge Thrust (vạch báo -> đâm lướt dài).
    ///   OSSUARY MAUL: Fault Line (3/5 đường nứt đất chạy về phía Ren) / Aftershock (hai vòng chấn động nối nhau).
    ///   MORNINGSTAR: Penitent Whirl (xoay chuỳ quanh người 2,4-3 s vừa đi vừa quét — đứng ngoài vòng chuỳ) / Wrecking Toss (ném chuỳ thẳng rồi giật về).
    ///   TWIN HATCHETS: Returning Hatchets (2 rìu bay vòng cung ra rồi quay về) / Hatchet Rain (5 rìu rơi vào vòng báo quanh Ren).
    ///   Cối của Engine rơi trúng hắn -> choáng (ShellHit). Ren cướp được rìu -> ném 2 rìu vào hắn (TakeHatchets).
    /// </summary>
    public sealed class HollowChampionSkills : BossSkillsBase
    {
        public float rushSpeed = 4.0f, rushDamage = 18f, slamDamage = 20f, slamRadius = 1.7f;
        private BrazenReliquarySkills partner;
        private bool enraged, released;
        private SpriteVfx rageAura;
        private float baseSpeed, nextCall, rearmAt = -1f;
        private string weapon = "";                    // "" = tay không
        private int uses;
        private SpriteRenderer weaponSr;
        private float weaponAngle = 25f;
        private bool weaponFree;                      // vị trí vũ khí do chiêu điều khiển (xoay chuỳ, ném)
        private static readonly Color Soul = new Color(0.72f, 0.42f, 1f), SoulDim = new Color(0.8f, 0.6f, 1f, 0.9f);

        private bool PartnerAlive => partner != null && partner.Alive;
        // 19/11 (§345): hp chỉ được gán ở lượt Act đầu tiên -> con kia hỏi sớm hơn thì tự lấy Health
        internal bool Alive { get { if (hp == null) hp = GetComponent<Health>(); return hp != null && !hp.IsDead; } }
        internal float HpPercent => Alive ? hp.Percent : 0f;
        private static MapMechanics MM => MapMechanics.Instance;

        private static string Pretty(string w) =>
            w == "halberd" ? "Reliquary Halberd" : w == "hammer" ? "Ossuary Maul" : w == "flail" ? "Penitent's Morningstar" : w == "axes" ? "Twin Hatchets" : w;

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
            if (p == 2) { Hint("The Champion's soulfire flares - he fights faster now.", 2.4f); yield return ai.Flash(0.5f, Soul); yield break; }
            if (p != 3 || enraged) yield break;
            enraged = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Rage");
            Sfx.Play("v_troll_alert", Pos);
            yield return ai.Flash(0.9f, Soul);
            rageAura = SpriteVfx.Spawn("duo_soulfire", Pos + Vector2.down * 0.05f, 1.15f, Layer, Order - 1, false, 12f);
            if (rageAura != null) { rageAura.Looping(9999f); rageAura.Follow(transform); }
            ai.moveSpeed = baseSpeed * 1.25f;
            ai.PlayState("Idle");
        }

        private void OnFell()
        {
            HideWeapon();
            if (released) return;
            released = true;
            var mm = MM;
            if (mm == null) return;
            bool any = false;
            foreach (var r in mm.Racks()) if (r.Now == RelicRackMechanism.State.Sealed) { r.Release(); any = true; }
            if (any) Hints.Show("The rack chains fall away - claim what relics remain [T]!", 3f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (rageAura != null) rageAura.Stop();
            HideWeapon();
        }

        // ------------------------------------------------------------------ cập nhật: vũ khí trên tay, giá rèn lại
        protected override void Update()
        {
            base.Update();
            if (weaponSr != null && !weaponFree)
            {
                bool left = ai != null && ai.Body != null && ai.Body.flipX;
                weaponSr.flipX = left;
                weaponSr.transform.position = (Vector2)transform.position + new Vector2(left ? -0.26f : 0.26f, 0.34f);
                weaponSr.transform.rotation = Quaternion.Euler(0f, 0f, left ? weaponAngle : -weaponAngle);
                if (ai.Body != null) { weaponSr.sortingLayerID = ai.Body.sortingLayerID; weaponSr.sortingOrder = ai.Body.sortingOrder + 1; }
            }
            if (!engaged || !Alive) return;
            var mm = MM;
            if (mm == null) return;
            var racks = mm.Racks();
            if (racks.Count == 0) return;
            bool allEmpty = true;
            foreach (var r in racks) if (r.Now != RelicRackMechanism.State.Empty) { allEmpty = false; break; }
            if (!allEmpty) { rearmAt = -1f; return; }
            if (rearmAt < 0f) { rearmAt = Time.time + 15f; return; }
            if (Time.time < rearmAt) return;
            rearmAt = -1f;
            foreach (var r in racks) r.Rearm();
            Hint("Soulfire re-forges the relics on their racks.", 2.4f);
        }

        // ------------------------------------------------------------------ não
        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (Broken) { yield return null; yield break; }
            ai.FaceX((PlayerPos - Pos).x);
            if (weapon.Length == 0 && Time.time >= nextCall)
            {
                var rack = PickRack();
                if (rack != null) { yield return RaceFor(rack); yield break; }
                nextCall = Time.time + 2f;
            }
            if (weapon.Length > 0) { yield return WeaponTurn(); yield break; }
            float gap = ai.GapToPlayer(), d = (PlayerPos - Pos).magnitude;
            if (gap <= meleeRange + 0.15f && Ready("slam")) { yield return Slam(); yield break; }
            if (ReadyG("slam") && d < 2.2f) { yield return Slam(); yield break; }
            if (ReadyG("rush") && d > 1.0f && d < 3.8f) { yield return Rush(phase == 1 ? 1 : 2); yield break; }
            yield return Stalk(0.5f, 1f);
        }

        protected override IEnumerator Reposition() => Slam(true);

        // ------------------------------------------------------------------ minigame: đua giành vũ khí
        /// <summary>Giá còn xích, ưu tiên giá gần hắn (để cuộc đua sát nút: Ren phải chạy ngay khi nghe gọi).</summary>
        private RelicRackMechanism PickRack()
        {
            var mm = MM;
            if (mm == null) return null;
            RelicRackMechanism best = null; float bd = float.MaxValue;
            foreach (var r in mm.Racks())
            {
                if (r.Now != RelicRackMechanism.State.Sealed) continue;
                float d = (r.Stand - Pos).sqrMagnitude + Random.Range(0f, 1.5f);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        private IEnumerator RaceFor(RelicRackMechanism rack)
        {
            rack.Call();
            Hint("The Champion calls the " + Pretty(rack.Weapon) + "! Beat him to the rack and claim it [T]!", 3f);
            if (partner != null) partner.Interdict(rack.Stand);
            ai.FaceX(rack.Stand.x - Pos.x);
            ai.PlayState(ai.HasAnimState("Rage") ? "Rage" : "Prepare");
            Sfx.Play("v_troll_alert", Pos, 0.8f);
            var line = Ray("m3_ray_warn", Pos, rack.Stand, 0.62f, 1.6f, Order - 1, 12f, 1.4f);
            if (line != null) line.Tint(new Color(0.75f, 0.5f, 1f, 0.8f));
            yield return Telegraph(phase == 1 ? 1.0f : 0.8f, Soul);
            float until = Time.time + 5f;
            while (Alive && !Broken && Time.time < until && rack.Now == RelicRackMechanism.State.Called)
            {
                Vector2 to = rack.Stand - Pos;
                if (to.magnitude < 0.3f) break;
                ai.Desired = to.normalized * ai.moveSpeed * (phase == 1 ? 1.25f : 1.4f);
                ai.FaceX(to.x);
                ai.PlayState("Move", false);
                yield return null;
            }
            ai.Desired = Vector2.zero;
            if (!Alive) yield break;
            if (rack.ClaimedByRen)
            {
                nextCall = Time.time + 7f;
                yield return BreakRoutine(2.3f, "Empty-handed! The Champion reels.");
                yield break;
            }
            if ((rack.Stand - Pos).magnitude < 0.75f && rack.TakeByChampion()) { Equip(rack.Weapon); yield break; }
            nextCall = Time.time + 3f;                              // kẹt đường: giá vẫn mở, Ren vẫn lấy được
            ai.PlayState("Idle");
        }

        private void Equip(string w)
        {
            weapon = w;
            uses = phase == 1 ? 4 : 5;
            Hint("The Champion takes up the " + Pretty(w) + "!", 2.2f);
            Sfx.Play("relic_claim", Pos, 0.6f);
            SpriteVfx.Spawn("m3_break", Pos + Vector2.up * 0.6f, 0.7f, Layer, Order + 3, false, 14f);
            if (weaponSr == null)
            {
                weaponSr = new GameObject("Champion Weapon").AddComponent<SpriteRenderer>();
                weaponSr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
            weaponSr.gameObject.SetActive(true);
            weaponSr.sprite = WardenStatueMechanism.Load("VFX/props/relic_" + (w == "axes" ? "axe" : w));
            weaponSr.transform.localScale = Vector3.one * (w == "halberd" ? 1.15f : 1.25f);
            weaponAngle = 25f; weaponFree = false;
            ai.PlayState("Idle");
        }

        private void SpendWeapon()
        {
            if (--uses > 0) return;
            Hint("The " + Pretty(weapon) + " cracks apart in his grip.", 2f);
            Sfx.Play("break_stone", Pos, 0.6f);
            weapon = "";
            HideWeapon();
            nextCall = Time.time + (phase == 1 ? 6f : 4.5f);
        }

        private void HideWeapon()
        {
            if (weaponSr != null) weaponSr.gameObject.SetActive(false);
        }

        private IEnumerator Swing(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) { weaponAngle = Mathf.Lerp(from, to, t / seconds); yield return null; }
            weaponAngle = to;
        }

        /// <summary>Ren cướp được rìu: 2 rìu bay vào hắn.</summary>
        internal void TakeHatchets(Vector2 from)
        {
            var axe = WardenStatueMechanism.Load("VFX/props/relic_axe");
            for (int i = 0; i < 2; i++)
            {
                var b = DuoBullet.Spawn(axe, from, new Vector2(i == 0 ? -2.5f : 2.5f, 2f), 0f, 0.2f, 3f, Layer, Order + 2, 1.2f);
                b.spinDeg = 900f; b.stopOnWalls = false;
                b.hitEnemy = transform;
                bool first = i == 0;
                b.onHitEnemy = _ =>
                {
                    if (!Alive) return;
                    hp.TakeDamage(hp.Max * 0.07f);
                    Sfx.Play("duo_punch", Pos);
                    SpriteVfx.Spawn("hit", Pos + Vector2.up * 0.5f, 0.9f, Layer, Order + 2, false);
                    if (first && !Broken) StartCoroutine(BreakRoutine(0.9f, "The hatchets bite deep!"));
                };
            }
        }

        // ------------------------------------------------------------------ bộ chiêu theo vũ khí
        private IEnumerator WeaponTurn()
        {
            float d = (PlayerPos - Pos).magnitude;
            if (!ReadyG("weapon")) { yield return Stalk(0.45f, 1f); yield break; }
            switch (weapon)
            {
                case "halberd":
                    if (d < 1.9f) yield return LongSweep();
                    else if (d < 3.6f) yield return Thrust();
                    else { yield return Stalk(0.6f, 1.1f); yield break; }
                    break;
                case "hammer":
                    if (d < 2.0f && Ready("aftershock")) yield return Aftershock();
                    else yield return FaultLine();
                    break;
                case "flail":
                    if (d < 3.0f) yield return Whirl(); else yield return Toss();
                    break;
                case "axes":
                    if (d > 1.4f && Ready("boomer")) yield return Boomerang(); else yield return HatchetRain();
                    break;
                default: yield break;
            }
            SpendWeapon();
        }

        // HALBERD ---------------------------------------------------------
        private IEnumerator LongSweep()
        {
            Use("weapon", phase == 1 ? 2.4f : 1.9f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            var warn = GroundStrike.Spawn(Pos + dir * 0.85f, 0.5f, 0.95f, 0f, null, 0, 14f, Order - 1, Soul);
            warn.impactSfx = "none";
            StartCoroutine(Swing(25f, -70f, 0.45f));
            yield return Telegraph(0.5f, Soul);
            if (!Alive || Broken) yield break;
            yield return SweepArc(dir, 1.8f, 20f);
            if (phase >= 2 && Alive && !Broken)
            {
                yield return ai.WaitFor(0.3f);
                dir = (PlayerPos - Pos).normalized;
                yield return SweepArc(dir, 2.3f, 16f);
            }
            weaponAngle = 25f;
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }

        private IEnumerator SweepArc(Vector2 dir, float r, float dmg)
        {
            ai.PlayState("Recover");
            StartCoroutine(Swing(-70f, 110f, 0.18f));
            Sfx.Play("duo_punch", Pos);
            var w = Wave(Pos, r, 0.3f, dmg, "m3_wave_hymn");
            w.knockback = 2.4f;
            w.gapAngles = new[] { Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg };
            w.gapWidth = 200f;                                        // chỉ quét 160° phía trước: vòng ra sau lưng hắn là an toàn
            yield return ai.WaitFor(0.2f);
        }

        private IEnumerator Thrust()
        {
            Use("weapon", phase == 1 ? 2.6f : 2.1f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            var line = Ray("m3_ray_warn", Pos, Pos + dir * 2.7f, 0.62f, 1.6f, Order - 1, 12f, 0.6f);
            if (line != null) line.Tint(Soul);
            StartCoroutine(Swing(25f, 90f, 0.4f));
            yield return Telegraph(0.55f, Soul);
            if (!Alive || Broken) yield break;
            ai.PlayState("Dash");
            Sfx.Play("duo_punch", Pos);
            Vector2 start = Pos; bool hit = false;
            for (float t = 0f; t < 0.38f && Alive && !Broken; t += Time.deltaTime)
            {
                ai.Desired = dir * 6.5f;
                if (!hit && ai.Player != null && SegDist(PlayerPos, start, Pos + dir * 0.6f) < 0.38f) { ai.Hurt(22f, Pos, 3f); hit = true; }
                yield return null;
            }
            ai.Desired = Vector2.zero;
            ai.PlayState("Recover");
            weaponAngle = 25f;
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // OSSUARY MAUL ------------------------------------------------------
        private IEnumerator FaultLine()
        {
            Use("weapon", phase == 1 ? 3.0f : 2.4f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Slam");
            StartCoroutine(Swing(25f, -20f, 0.4f));
            yield return Telegraph(0.45f, Soul);
            if (!Alive || Broken) yield break;
            StartCoroutine(Swing(-20f, 150f, 0.12f));
            Sfx.Play("duo_slam", Pos);
            Kick(Vector2.down, 0.07f, 0.25f);
            SpriteVfx.Spawn("duo_soul_slam", Pos, 1.6f, Layer, Order - 1, false, 14f);
            var burst = SpriteVfx.Load("m3_ground_burst");
            int lines = phase == 1 ? 3 : 5;
            float spread = phase == 1 ? 22f : 18f;
            float baseA = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int s = 0; s < 7; s++)
                for (int l = 0; l < lines; l++)
                {
                    Vector2 at = Pos + Dir(baseA + (l - (lines - 1) * 0.5f) * spread) * (0.55f + s * 0.42f);
                    if (EllipseR(at) > 1.02f) continue;
                    var g = GroundStrike.Spawn(at, 0.32f + s * 0.08f, 0.3f, 16f, burst, 1, 16f, Order + 1, Soul);
                    g.impactSfx = l == 0 && s % 2 == 0 ? "rock_crash" : "none";
                }
            yield return ai.WaitFor(0.8f);
            weaponAngle = 25f;
            ai.PlayState("Idle");
        }

        private IEnumerator Aftershock()
        {
            Use("aftershock", 6.5f);
            Use("weapon", 2.2f);
            ai.PlayState("Slam");
            var warn = GroundStrike.Spawn(Pos, 0.5f, 1.0f, 0f, null, 0, 14f, Order - 1, Soul);
            warn.impactSfx = "none";
            StartCoroutine(Swing(25f, -20f, 0.45f));
            yield return Telegraph(0.5f, Soul);
            if (!Alive || Broken) yield break;
            StartCoroutine(Swing(-20f, 150f, 0.12f));
            Sfx.Play("duo_slam", Pos);
            Kick(Vector2.down, 0.08f, 0.3f);
            var w = Wave(Pos, 1.9f, 0.5f, 18f, "m3_wave_hymn");
            w.knockback = 2.6f;
            yield return ai.WaitFor(0.6f);
            if (Alive && !Broken)
            {
                var w2 = Wave(Pos, 2.7f, 0.7f, 14f, "m3_wave_hymn");
                w2.knockback = 2f;
                float g = Random.Range(0f, 120f); w2.gapAngles = new[] { g, g + 120f, g + 240f }; w2.gapWidth = 36f;
                Sfx.Play("duo_slam", Pos, 0.6f);
            }
            yield return ai.WaitFor(0.4f);
            weaponAngle = 25f;
            ai.PlayState("Idle");
        }

        // MORNINGSTAR -------------------------------------------------------
        private IEnumerator Whirl()
        {
            Use("weapon", phase == 1 ? 3.6f : 3.0f);
            ai.PlayState("Prepare");
            Hint("He whirls the morningstar - stay out of its reach!", 1.6f);
            yield return Telegraph(0.45f, Soul);
            if (!Alive || Broken) yield break;
            weaponFree = true;
            float dur = phase == 1 ? 2.4f : 3.0f, ang = Random.Range(0f, 360f), nextHit = 0f, nextWhoosh = 0f;
            for (float t = 0f; t < dur && Alive && !Broken; t += Time.deltaTime)
            {
                ang += 430f * Time.deltaTime;
                Vector2 head = Pos + Vector2.up * 0.3f + Dir(ang) * 1.05f;
                if (weaponSr != null)
                {
                    weaponSr.transform.position = head;
                    weaponSr.transform.rotation = Quaternion.Euler(0f, 0f, ang - 90f);
                }
                Vector2 to = PlayerPos - Pos;
                ai.Desired = to.magnitude > 0.6f ? to.normalized * ai.moveSpeed * 0.75f : Vector2.zero;
                ai.PlayState("Move", false);
                if (t >= nextWhoosh) { nextWhoosh = t + 0.42f; Sfx.Play("duo_punch", Pos, 0.45f); }
                if (t >= nextHit && ai.Player != null && ((Vector2)PlayerPos + Vector2.up * 0.2f - head).magnitude < 0.42f)
                { ai.Hurt(15f, head, 3f); nextHit = t + 0.6f; }
                yield return null;
            }
            ai.Desired = Vector2.zero;
            weaponFree = false;
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.7f);                            // chóng mặt sau khi xoay: cửa sổ đánh
            ai.PlayState("Idle");
        }

        private IEnumerator Toss()
        {
            Use("weapon", phase == 1 ? 2.8f : 2.2f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            var line = Ray("m3_ray_warn", Pos, Pos + dir * 3.2f, 0.62f, 1.4f, Order - 1, 12f, 0.5f);
            if (line != null) line.Tint(Soul);
            yield return Telegraph(0.5f, Soul);
            if (!Alive || Broken) yield break;
            ai.PlayState("Recover");
            Sfx.Play("duo_punch", Pos);
            var spr = weaponSr != null ? weaponSr.sprite : null;
            if (weaponSr != null) weaponSr.enabled = false;
            Vector2 start = Pos + Vector2.up * 0.3f;
            Transform me = transform;
            var b = DuoBullet.Spawn(spr, start, Vector2.zero, 18f, 0.3f, 1.2f, Layer, Order + 2, 1.2f);
            b.spinDeg = 720f; b.stopOnWalls = false;
            b.path = t =>
            {
                float u = t / 1.2f;
                Vector2 back = me != null ? (Vector2)me.position + Vector2.up * 0.3f : start;
                return u < 0.5f ? start + dir * 3.2f * Mathf.Sin(u * Mathf.PI) : Vector2.Lerp(start + dir * 3.2f * Mathf.Sin(u * Mathf.PI), back, (u - 0.5f) * 2f);
            };
            yield return ai.WaitFor(1.2f);
            if (weaponSr != null) weaponSr.enabled = true;
            yield return ai.WaitFor(0.3f);
            ai.PlayState("Idle");
        }

        // TWIN HATCHETS -----------------------------------------------------
        private IEnumerator Boomerang()
        {
            Use("boomer", 5f);
            Use("weapon", phase == 1 ? 2.4f : 2.0f);
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Prepare");
            yield return Telegraph(0.45f, Soul);
            if (!Alive || Broken) yield break;
            ai.PlayState("Recover");
            Sfx.Play("duo_punch", Pos);
            float reach = Mathf.Clamp((PlayerPos - Pos).magnitude + 0.7f, 1.8f, 3.8f);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Transform me = transform;
            var axe = WardenStatueMechanism.Load("VFX/props/relic_axe");
            int n = phase == 1 ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                float side = n == 2 ? (i == 0 ? -1f : 1f) : i - 1f;
                Vector2 start = Pos + Vector2.up * 0.3f;
                var b = DuoBullet.Spawn(axe, start, Vector2.zero, 14f, 0.22f, 1.6f, Layer, Order + 2, 1.15f);
                b.spinDeg = 900f; b.stopOnWalls = false;
                b.path = t =>
                {
                    float u = Mathf.Clamp01(t / 1.6f);
                    Vector2 home = me != null ? (Vector2)me.position + Vector2.up * 0.3f : start;
                    Vector2 outP = start + dir * reach * Mathf.Sin(u * Mathf.PI) + perp * side * 1.1f * Mathf.Sin(u * Mathf.PI * 2f) * 0.5f;
                    return Vector2.Lerp(outP, home, Mathf.Max(0f, u - 0.6f) * 2.5f);
                };
            }
            yield return ai.WaitFor(1.0f);
            ai.PlayState("Idle");
        }

        private IEnumerator HatchetRain()
        {
            Use("weapon", phase == 1 ? 2.8f : 2.2f);
            ai.PlayState("Slam");
            yield return Telegraph(0.35f, Soul);
            if (!Alive || Broken || ai.Player == null) yield break;
            var axe = WardenStatueMechanism.Load("VFX/props/relic_axe");
            int n = phase == 1 ? 5 : 7;
            for (int i = 0; i < n; i++)
            {
                Vector2 at = i == 0 ? PlayerPos : ClampArena(PlayerPos + Random.insideUnitCircle * 1.0f, 0.97f);
                var g = GroundStrike.Spawn(at, 0.8f, 0.34f, 14f, SpriteVfx.Load("hit"), 0, 16f, Order + 1, Soul);
                g.impactSfx = i % 2 == 0 ? "duo_shell_land" : "none";
                if (axe != null) { g.shell = axe; g.shellFrom = Pos + Vector2.up * 0.6f; g.shellArc = 1.5f; }
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
                if (line != null) line.Tint(Soul);
                yield return Telegraph(wind, Soul);
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
                var w = Shockwave.Spawn(Pos + dir * 0.3f, 0.9f, 0.3f, 0f, Order - 1, SoulDim);
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
            var warn = GroundStrike.Spawn(Pos, quick ? 0.35f : 0.42f, slamRadius * 0.55f, 0f, null, 0, 14f, Order - 1, Soul);
            warn.impactSfx = "none";
            yield return Telegraph(quick ? 0.35f : 0.42f, Soul);
            if (!Alive || Broken) yield break;
            Sfx.Play("duo_slam", Pos);
            SpriteVfx.Spawn("duo_soul_slam", Pos, 2.6f, Layer, Order - 1, false, 14f);
            Kick(Vector2.down, 0.07f, 0.3f);
            var w = Wave(Pos, slamRadius, 0.55f, slamDamage, "m3_wave_hymn");
            w.knockback = 2.6f;
            if (phase >= 2) { float g = Random.Range(0f, 120f); w.gapAngles = new[] { g, g + 120f, g + 240f }; w.gapWidth = 38f; }
            int patches = phase == 1 ? 2 : 3;
            for (int k = 0; k < patches; k++)
            {
                Vector2 at = ClampArena(Pos + Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.3f), 0.95f);
                BurnPatch.Spawn(at, "duo_soulfire", 0.7f, 4.5f, Layer, Order - 2, 0.3f, 4f, Soul);
            }
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }
    }
}
