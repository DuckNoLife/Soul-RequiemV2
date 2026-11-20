using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// MATRIARCH ISOLDE — boss X3 (The Matriarch's Bower). Mẹ tộc Vey, ngủ ngàn năm dưới hầm mộ cũ. Máu là sức mạnh của bà:
    ///   MÔI TRƯỜNG
    ///   • 3 quan tài đá nhà Vey (isolde_coffin_0..2): Night Flight — bà hoá dơi, trốn vào MỘT quan tài. Quan tài thật rỉ máu
    ///     dưới đáy. Chém đúng -> lôi bà ra, BREAK (quỳ, x2 sát thương). Chém sai -> nắp bật, dơi ùa ra. Hết giờ -> bà phá nắp
    ///     lao ra (vòng máu) và hồi máu. Mỗi quan tài chỉ dùng được một lần.
    ///   • Vũng máu: mỗi giọt Crimson Rain rơi xuống để lại vũng máu. Bà lướt tới vũng để uống (Feast, hồi máu) — đánh 4 nhát
    ///     để giật bà khỏi bữa tiệc (BREAK). Chiêu F của Ren đốt khô vũng máu. Pha 3: mọi vũng còn lại PHUN GAI MÁU.
    ///   CHIÊU
    ///   • Blood Lash (cận chiến): cào móng 160°, pha 2+ cào hai nhát.
    ///   • Bat Dive: vạch đỏ báo -> hoá bầy dơi lao xuyên qua Ren, hiện ra cạnh Ren và cào.
    ///   • Children of the Night: thả 3/5/7 con dơi bay vòng cung đuổi theo Ren (chém trúng là dơi nổ). Thay cho mũi tên máu.
    ///   • Sanguine Siphon: dây máu nối Ren với bà 2,8 s, hút máu Ren hồi cho bà. Cắt dây: chạy xa > 3,8 hoặc NÚP SAU QUAN TÀI.
    ///   • Crimson Rain: giọt máu rơi từ trời xuống các vòng đỏ quanh Ren, để lại vũng máu.
    ///   • Mist Step: tan thành sương máu, hiện ra ở chỗ xa Ren (thoát góc / khi bị áp sát) rồi thả 3 con dơi.
    ///   • Sanguine Tide (pha 3): 3 vòng sóng máu có 3 khe, xoay dần; đồng thời vũng máu phun gai.
    /// Mọi hiệu ứng là ảnh PixelLab (m3_blood_mist, m3_blood_pool, m3_blood_orb, m3_wave_blood, m3_ray_warn, m3_break...).
    /// </summary>
    public class IsoldeSkills : BossSkillsBase
    {
        [Header("Quan tài")]
        public int coffinCount = 4;                 // 27/10: thêm quan tài thứ 4 thay hầm mộ
        public float flightTime = 7f, flightHeal = 0.06f;
        [Header("Vũng máu")]
        public int maxPools = 4;
        public float poolLife = 24f, poolRadius = 0.34f, feastPerSecond = 0.03f, feastTime = 2.6f;
        public int feastBreakHits = 4;
        [Header("Dơi (builder gán)")]
        public Sprite[] batFrames;
        [Header("Siphon")]
        public float siphonRange = 3.8f, siphonTime = 2.8f;

        private class Bat { public Transform t; public bool dead; }
        private readonly List<Bat> bats = new List<Bat>();

        private class Coffin
        {
            public string name; public Vector2 pos; public bool used;
            public int hits; public bool broken;                              // 27/10: quan tài phải bị phá huỷ
            public readonly List<GameObject> gos = new List<GameObject>();     // base + _open + _broken (dời cùng nhau khi tráo)
        }
        private bool guessOpen;                    // đang chờ Ren chọn quan tài
        [Header("Trò tráo quan tài")]
        public float wrongHeal = 0.08f, guessTime = 5f;
        [Header("Quan tài = mạng của bà (27/10)")]
        [Tooltip("Số nhát chém để phá một quan tài")] public int coffinHits = 3;
        [Tooltip("Rite of Repose dựng lại MỘT quan tài mỗi chừng này giây")] public float restoreEvery = 30f;
        [Tooltip("Hồi máu khi một quan tài vỡ = healStep x số quan tài đã vỡ (cấp số cộng: 3%, 6%, 9%, 12%)")] public float healStep = 0.03f;
        [Tooltip("Hồi máu liên tục mỗi giây = regenStep x số quan tài đã vỡ")] public float regenStep = 0.002f;
        [Tooltip("Khi còn quan tài, máu không xuống dưới mức này")] public float undyingFloor = 0.05f;
        private string baseName;
        private float nextFloorHint;
        private int Intact => coffins.FindAll(c => !c.broken).Count;
        private int Destroyed => coffins.Count - Intact;
        private class Pool { public Vector2 pos; public SpriteVfx vfx; public float until; }
        private readonly List<Coffin> coffins = new List<Coffin>();
        private readonly List<Pool> pools = new List<Pool>();
        private Coffin hideIn;
        private Coffin struckCoffin;
        private int hitsTaken;
        private float nextBurnHint;
        private static readonly Color Blood = new Color(0.92f, 0.12f, 0.2f, 1f);
        private static readonly Color DarkBlood = new Color(0.55f, 0.05f, 0.1f, 1f);

        protected override int PhaseFor(float p) => p > 0.6f ? 1 : p > 0.25f ? 2 : 3;
        protected override float CooldownMul() => phase == 3 ? 0.75f : 1f;

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) h.Damaged += (_, __) => hitsTaken++;
        }

        // ------------------------------------------------------------------ bắt đầu
        protected override void OnEngage()
        {
            Hint("\"Who knocks upon the Matriarch's door?\"", 3.5f);
            var mm = MapMechanics.Instance;
            for (int i = 0; i < coffinCount && mm != null; i++)
            {
                string n = "isolde_coffin_" + i;
                var gos = mm.Named(n);
                if (gos.Count == 0) continue;
                var r = gos[0].GetComponentInChildren<SpriteRenderer>();
                Vector2 c = r != null ? (Vector2)r.bounds.center : (Vector2)gos[0].transform.position;
                var cf = new Coffin { name = n, pos = c };
                foreach (var suf in new[] { "", "_open", "_broken" }) cf.gos.AddRange(mm.Named(n + suf));
                coffins.Add(cf);
            }
            PlayerCombat.Struck += OnStruck;
            baseName = ai.displayName;
            ready["rite"] = Time.time + restoreEvery;
            UpdateCoffinState();
            PlayerCombat.SkillCast += OnSkillCast;
            Use("bats", 2f); Use("siphon", 10f); Use("rain", 4f); Use("dive", 6f); Use("feast", 12f); Use("flight", 999f); Use("tide", 999f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PlayerCombat.Struck -= OnStruck;
            PlayerCombat.SkillCast -= OnSkillCast;
            foreach (var p in pools) if (p.vfx != null) p.vfx.Stop();
            pools.Clear();
            foreach (var b in bats) if (b.t != null) Destroy(b.t.gameObject);
            bats.Clear();
        }

        // ------------------------------------------------------------------ chọn chiêu
        protected override IEnumerator Think()
        {
            if (Broken) { yield return null; yield break; }
            TickPools();
            float gap = ai.GapToPlayer();
            float d = (PlayerPos - Pos).magnitude;

            if (gap <= meleeRange && Ready("lash")) { yield return BloodLash(); yield break; }
            if (Destroyed > 0 && ReadyG("rite")) { yield return RiteOfRepose(); yield break; }
            if (phase >= 2 && ReadyG("flight") && Intact >= 2) { yield return NightFlight(); yield break; }
            if (phase == 3 && ReadyG("tide")) { yield return SanguineTide(); yield break; }
            if (hp.Percent < 0.85f && pools.Count > 0 && ReadyG("feast")) { yield return Feast(); yield break; }
            if (d > 2.6f && ReadyG("dive")) { yield return BatDive(); yield break; }
            if (d < siphonRange - 0.6f && d > 1.4f && ReadyG("siphon")) { yield return SanguineSiphon(); yield break; }
            if (ReadyG("rain")) { yield return CrimsonRain(); yield break; }
            if (ReadyG("bats")) { yield return ChildrenOfTheNight(phase == 1 ? 3 : phase == 2 ? 4 : 5, true); yield break; }
            yield return Stalk(0.6f);
        }

        protected override IEnumerator Reposition()
        {
            var extra = new List<Vector2>();
            foreach (var c in coffins) extra.Add(center + (c.pos - center) * 0.75f);
            yield return MistStep(FarPoint(0.6f, 12, extra));
            if (!hp.IsDead) yield return ChildrenOfTheNight(2, false);
        }

        protected override IEnumerator OnPhase(int p)
        {
            hp.Invulnerable = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Hint(p == 2 ? "\"You woke me for this? Children of the Vey, to me!\"" : "\"The Vey do not die. We are merely... delayed.\"", 3f);
            yield return ai.WaitFor(0.5f);
            var w = Wave(Pos, 3.6f, 1.1f, 0f, "m3_wave_blood");          // chỉ đẩy lùi
            w.knockback = 4f;
            w.onHit = _ => { };
            Kick(Vector2.down, 0.07f, 0.3f);
            yield return ai.WaitFor(0.9f);
            hp.Invulnerable = false;
            if (p == 2) Use("flight", 0f);                       // vào pha 2 là trốn vào quan tài ngay
            if (p == 3)
            {
                // hút cạn mọi vũng máu còn lại: mỗi vũng hồi 2% -> lý do để đốt vũng sớm
                int n = 0;
                foreach (var pl in pools)
                {
                    SpriteVfx.Spawn("m3_blood_mist", pl.pos, 0.45f, Layer, Order, false, 16f);
                    if (pl.vfx != null) pl.vfx.Stop();
                    n++;
                }
                pools.Clear();
                if (n > 0) { hp.Heal(hp.Max * 0.02f * n); Hint("She drinks every drop that was spilled.", 2f); }
                Use("tide", 1.5f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Sanguine Kiss (chiêu hút máu khi bị áp sát, hồi 15 s)
        // 27/10: thay đòn cào thường. Một nhát cào nhẹ (x0,55), trúng thì hút máu: dòng giọt máu chảy về bà, bà hồi 4% máu tối đa.
        private IEnumerator BloodLash()
        {
            Use("lash", 15f);
            ai.Desired = Vector2.zero;
            Vector2 dir = (PlayerPos - Pos).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Attack");
            if (ai.Vfx != null) ai.Vfx.Windup(dir);
            yield return ai.WaitFor(0.45f);
            if (hp.IsDead) yield break;
            if (ai.Vfx != null) ai.Vfx.Swing(dir);
            Vector2 toP = PlayerPos - Pos;
            if (toP.magnitude <= 0.95f && Vector2.Angle(dir, toP) <= 80f && ai.PlayerHealth != null && !ai.PlayerHealth.Invulnerable)
            {
                ai.Hurt(ai.damage * 0.55f, Pos, 1.5f);
                hp.Heal(hp.Max * 0.04f);
                for (int k = 0; k < 6; k++) StartCoroutine(FlyMote("m3_blood_orb", PlayerPos + Vector2.up * 0.3f + Random.insideUnitCircle * 0.15f, transform, Vector2.up * 0.45f, 0.4f + k * 0.05f, 0.6f));
                Hint("Isolde drinks from the wound.", 1.4f);
            }
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.2f);
        }

        // ------------------------------------------------------------------ Bat Dive (áp sát)
        private IEnumerator BatDive()
        {
            Use("dive", 8f);
            ai.Desired = Vector2.zero;
            Vector2 from = Pos;
            Vector2 dir = (PlayerPos - from).normalized;
            Vector2 to = ClampArena(PlayerPos + dir * 0.6f, 0.85f);
            ai.FaceX(dir.x);
            ai.PlayState("Cast");
            var line = Ray("m3_ray_warn", from, to, 0.62f, 2f, Order - 1, 12f, 0.65f);   // vạch báo đường lao
            if (line != null) line.Tint(Blood);
            yield return ai.WaitFor(0.55f);
            if (line != null) line.Stop();
            float t;
            if (hp.IsDead) yield break;
            SpriteVfx.Spawn("m3_blood_mist", from, 0.6f, Layer, Order + 1, false, 18f);
            SetHidden(true);
            StartCoroutine(BatSwarm(from, to, 0.35f, 6));
            bool hit = false;
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                Vector2 p = Vector2.Lerp(from, to, t / 0.35f);
                if (!hit && (PlayerPos - p).magnitude < 0.38f) { hit = true; ai.Hurt(ai.damage * 0.9f, p - dir * 0.3f, 3f); }
                yield return null;
            }
            ai.TeleportTo(to);
            SpriteVfx.Spawn("m3_blood_mist", to, 0.6f, Layer, Order + 1, false, 18f);
            SetHidden(false);
            ResetTrackers();
            if (ai.GapToPlayer() <= meleeRange + 0.35f) { if (Ready("lash")) yield return BloodLash(); }
            else yield return ai.WaitFor(0.25f);
        }

        /// Bầy dơi bay từ a tới b (chỉ để nhìn).
        private IEnumerator BatSwarm(Vector2 a, Vector2 b, float seconds, int count)
        {
            if (batFrames == null || batFrames.Length == 0) yield break;
            var bats = new List<SpriteRenderer>();
            var offs = new List<Vector2>();
            Vector2 perp = Vector2.Perpendicular((b - a).normalized);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("BatSwarm");
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = batFrames[0];
                r.sortingLayerID = Layer; r.sortingOrder = Order + 1;
                r.flipX = b.x < a.x;
                go.transform.localScale = Vector3.one * Random.Range(0.55f, 0.8f);
                bats.Add(r);
                offs.Add(perp * Random.Range(-0.35f, 0.35f) + Vector2.up * Random.Range(0.1f, 0.6f));
            }
            float t = 0f;
            while (t < seconds + 0.15f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                for (int i = 0; i < bats.Count; i++)
                {
                    if (bats[i] == null) continue;
                    float lag = Mathf.Clamp01(k - i * 0.04f);
                    bats[i].transform.position = Vector2.Lerp(a, b, lag) + offs[i] * Mathf.Sin(lag * Mathf.PI);
                    bats[i].sprite = batFrames[(int)((t * 14f) + i) % batFrames.Length];
                }
                yield return null;
            }
            foreach (var r in bats) if (r != null) Destroy(r.gameObject);
        }

        // ------------------------------------------------------------------ Children of the Night (dơi đuổi)
        private IEnumerator ChildrenOfTheNight(int n, bool useCd)
        {
            if (useCd) Use("bats", 7f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            yield return ai.WaitFor(0.45f);
            if (hp.IsDead) yield break;
            Vector2 mouth = Pos + Vector2.up * 0.4f;
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.55f, Layer, Order + 1, false, 16f);
            Vector2 aim = (PlayerPos - mouth).normalized;
            float baseA = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int k = 0; k < n; k++) StartCoroutine(BatShot(mouth, baseA + (k - (n - 1) * 0.5f) * 24f, k * 0.07f));
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }

        /// Một con dơi: bay ra theo quạt, rẽ dần về phía Ren trong 1,1 s đầu rồi bay thẳng; chạm Ren hoặc bị chém thì nổ máu.
        private IEnumerator BatShot(Vector2 start, float angle, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var go = new GameObject("VeyBat");
            go.transform.position = start;
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingLayerID = Layer; r.sortingOrder = Order + 1;
            bool hasBat = batFrames != null && batFrames.Length > 0;
            var orb = hasBat ? null : SpriteVfx.Load("m3_blood_orb");
            go.transform.localScale = Vector3.one * (hasBat ? 0.7f : 0.8f);
            var bat = new Bat { t = go.transform };
            bats.Add(bat);
            Vector2 pos = start, dir = Dir(angle);
            float t = 0f, speed = 2.5f, life = 2.6f;
            while (t < life && !bat.dead && ai != null && !hp.IsDead)
            {
                t += Time.deltaTime;
                if (t < 1.1f && ai.Player != null)                      // rẽ về phía Ren, tối đa 120°/s
                {
                    Vector2 want = (PlayerPos + Vector2.up * 0.25f - pos).normalized;
                    float cur = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, tgt = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
                    dir = Dir(Mathf.MoveTowardsAngle(cur, tgt, 120f * Time.deltaTime));
                }
                pos += dir * speed * Time.deltaTime;
                go.transform.position = pos;
                r.flipX = dir.x < 0f;
                r.sprite = hasBat ? batFrames[(int)(t * 14f) % batFrames.Length] : (orb.Length > 0 ? orb[(int)(t * 12f) % orb.Length] : null);
                if (ai.Player != null && (PlayerPos + Vector2.up * 0.25f - pos).magnitude < 0.22f)
                {
                    ai.Hurt(ai.damage * 0.5f, pos - dir * 0.3f, 1f);
                    bat.dead = true;
                }
                yield return null;
            }
            SpriteVfx.Spawn("m3_blood_splash", pos, 0.5f, Layer, Order + 2, false, 18f);
            bats.Remove(bat);
            if (go != null) Destroy(go);
        }

        // ------------------------------------------------------------------ Sanguine Siphon (dây hút máu, quan tài che)
        private IEnumerator SanguineSiphon()
        {
            Use("siphon", 16f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Cast");
            Hint("She drinks from your veins. Get away, or hide behind a coffin!", 2.5f);
            Vector2 Mouth() => Pos + Vector2.up * 0.45f;
            Vector2 Heart() => PlayerPos + Vector2.up * 0.3f;
            var warn = Ray("m3_ray_warn", Mouth(), Heart(), 0.62f, 2f, Order + 1, 12f, 0.8f);
            if (warn != null) warn.Tint(Blood);
            float t = 0f;
            while (t < 0.7f && !hp.IsDead)                               // vạch báo bám theo Ren
            {
                PlaceRay(warn, Mouth(), Heart(), 0.62f, 2f);
                t += Time.deltaTime; yield return null;
            }
            if (warn != null) warn.Stop();
            t = 0f;
            float nextTick = Time.time + 0.3f, nextMote = 0f;
            while (t < siphonTime && !hp.IsDead && !Broken && ai.Player != null)
            {
                Vector2 a = Heart(), b = Mouth();
                if ((a - b).magnitude > siphonRange || CoffinBlocks(a, b)) { Hint("The blood-link snaps.", 1.4f); break; }
                if (Time.time >= nextMote)
                {
                    StartCoroutine(FlyMote("m3_blood_orb", a, transform, Vector2.up * 0.45f, 0.45f, 0.6f));
                    nextMote = Time.time + 0.08f;
                }
                if (Time.time >= nextTick)
                {
                    ai.Hurt(ai.damage * 0.3f, b, 0f);
                    hp.Heal(hp.Max * 0.01f);
                    nextTick = Time.time + 0.5f;
                }
                ai.FaceX(a.x - b.x);
                t += Time.deltaTime;
                yield return null;
            }
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.2f);
        }

        /// Quan tài (kể cả đã mở / vỡ) nằm giữa a và b thì cắt dây hút máu.
        private bool CoffinBlocks(Vector2 a, Vector2 b)
        {
            foreach (var c in coffins) if (SegDist(c.pos, a, b) < 0.42f) return true;
            return false;
        }

        // ------------------------------------------------------------------ Crimson Rain (để lại vũng máu)
        private IEnumerator CrimsonRain()
        {
            Use("rain", 10f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            yield return ai.WaitFor(0.5f);
            int n = 3 + phase;
            for (int i = 0; i < n && !hp.IsDead && ai.Player != null; i++)
            {
                Vector2 p = i == 0 ? PlayerPos : ClampArena(PlayerPos + Random.insideUnitCircle * 1.5f, 0.85f);
                DropBlood(p, 0.95f + i * 0.09f, i < 2);
            }
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
        }

        private void DropBlood(Vector2 p, float warn, bool leavePool)
        {
            var glob = SpriteVfx.Load("m3_blood_orb");
            // mưa máu phủ rộng -> không mất máu, mà gây HEMORRHAGE: 6 s không uống được bình máu
            var g = GroundStrike.Spawn(p, warn, poolRadius, 0f, SpriteVfx.Load("m3_blood_splash"), 1, 16f, Order, Blood);
            g.onHit = _ => { var s = PS; if (s != null) s.BlockHeal(6f); };
            g.bodyScale = 0.6f;
            if (glob.Length > 0) { g.shell = glob[0]; g.shellFrom = p + Vector2.up * 2.6f; g.shellArc = 0f; g.shellScale = 0.9f; }
            if (leavePool) StartCoroutine(PoolAfter(p, warn));
        }

        private IEnumerator PoolAfter(Vector2 p, float delay)
        {
            yield return new WaitForSeconds(delay + 0.05f);
            if (hp == null || hp.IsDead) yield break;
            foreach (var o in pools) if ((o.pos - p).magnitude < poolRadius * 1.5f) { o.until = Time.time + poolLife; yield break; }
            if (pools.Count >= maxPools) { if (pools[0].vfx != null) pools[0].vfx.Stop(); pools.RemoveAt(0); }
            var v = SpriteVfx.Spawn("m3_blood_pool", p, 0.75f, Layer, Order - 3, false, 10f);
            if (v != null) v.Looping(poolLife);
            pools.Add(new Pool { pos = p, vfx = v, until = Time.time + poolLife });
        }

        /// Đứng trong vũng máu: chân dính, đi chậm 25% (lý do để đốt vũng bằng chiêu F).
        protected override void Update()
        {
            base.Update();
            if (ai == null || hp == null || hp.IsDead || !engaged) return;
            if (Destroyed > 0 && !Broken) hp.Heal(hp.Max * regenStep * Destroyed * Time.deltaTime);   // hồi liên tục, tăng theo số quan tài đã vỡ
            if (Intact > 0 && hp.Current <= hp.Floor + 0.5f && Time.time >= nextFloorHint)
            {
                Hint("She cannot die while her coffins stand. Break them!", 2.5f);
                nextFloorHint = Time.time + 8f;
            }
            if (ai.Player == null || pools.Count == 0) return;
            foreach (var p in pools)
                if ((PlayerPos - p.pos).magnitude <= poolRadius + 0.05f) { var s = PS; if (s != null) s.Slow(0.75f, 0.3f); break; }
        }

        private void TickPools()
        {
            for (int i = pools.Count - 1; i >= 0; i--)
                if (Time.time >= pools[i].until || pools[i].vfx == null) { if (pools[i].vfx != null) pools[i].vfx.Stop(); pools.RemoveAt(i); }
        }

        /// Chiêu F của Ren (SkillCast) đốt khô vũng máu trong vùng nổ.
        private void OnSkillCast(Vector2 c, float r)
        {
            int burned = 0;
            for (int i = pools.Count - 1; i >= 0; i--)
            {
                if ((pools[i].pos - c).magnitude > r + 0.35f) continue;
                SpriteVfx.Spawn("m3_blood_mist", pools[i].pos, 0.4f, Layer, Order, false, 18f);
                if (pools[i].vfx != null) pools[i].vfx.Stop();
                pools.RemoveAt(i);
                burned++;
            }
            if (burned > 0 && Time.time >= nextBurnHint) { Hint("The spilled blood boils away.", 1.6f); nextBurnHint = Time.time + 6f; }
        }

        // ------------------------------------------------------------------ Feast (uống vũng máu)
        private IEnumerator Feast()
        {
            Use("feast", 16f);
            TickPools();
            if (pools.Count == 0) yield break;
            Pool target = null; float best = -1f;
            foreach (var p in pools)                                   // vũng xa Ren nhất -> bắt người chơi đuổi theo
            {
                float dd = (p.pos - PlayerPos).magnitude;
                if (dd > best) { best = dd; target = p; }
            }
            yield return MistStep(target.pos + Vector2.down * 0.05f);
            if (hp.IsDead) yield break;
            ai.Desired = Vector2.zero;
            ai.PlayState(ai.HasAnimState("Drink") ? "Drink" : "Cast");
            Hint("Isolde kneels to drink the spilled blood. Tear her away!", 2.5f);
            hitsTaken = 0;
            float t = 0f;
            bool broken = false;
            while (t < feastTime && !hp.IsDead)
            {
                if (hitsTaken >= feastBreakHits) { broken = true; break; }
                hp.Heal(hp.Max * feastPerSecond * Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
            }
            if (target.vfx != null) target.vfx.Stop();
            pools.Remove(target);
            if (hp.IsDead) yield break;
            if (broken) yield return BreakRoutine(2.6f, "You tear her from the feast!");
            else { Hint("The blood restores her.", 1.6f); ai.PlayState("Idle"); yield return ai.WaitFor(0.3f); }
        }

        // ------------------------------------------------------------------ Mist Step (dịch chuyển)
        private IEnumerator MistStep(Vector2 to)
        {
            ai.Desired = Vector2.zero;
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.7f, Layer, Order + 1, false, 16f);
            hp.Invulnerable = true;
            yield return Fade(0f, 0.22f);
            ai.TeleportTo(ClampArena(to, 0.9f));
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(1f, 0.2f);
            hp.Invulnerable = false;
        }

        // ------------------------------------------------------------------ Night Flight (quan tài)
        // Night Flight = trò "tráo quan tài" nhanh tay lẹ mắt:
        //   1. bầy dơi bay vào ĐÚNG MỘT quan tài (nhìn rõ), nắp rung, máu rỉ;
        //   2. 3 quan tài tráo chỗ nhau (pha 2: 4 lượt chậm, pha 3: 6 lượt nhanh) — mắt phải bám theo;
        //   3. Ren có 5 s chém một quan tài. Đúng: lôi bà ra, bà gục 3 s (Break). Sai / hết giờ: bà phá nắp lao ra, hồi 8% máu.
        //   Sau đó cả 3 quan tài bị niêm (mở/vỡ); 20 s sau bà dùng Rite of Repose dựng lại.
        private IEnumerator NightFlight()
        {
            Use("flight", 30f);
            var mm = MapMechanics.Instance;
            var intact = coffins.FindAll(c => !c.broken);
            hideIn = intact[Random.Range(0, intact.Count)];
            struckCoffin = null;
            guessOpen = false;
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            Hint("\"Find me, child. If your eyes are quick enough.\"", 2.5f);
            yield return ai.WaitFor(0.45f);
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.8f, Layer, Order + 1, false, 14f);
            Vector2 from = Pos;
            SetHidden(true);
            StartCoroutine(BatSwarm(from, hideIn.pos, 0.7f, 7));             // dơi bay vào ĐÚNG quan tài
            yield return ai.WaitFor(0.75f);
            ai.TeleportTo(hideIn.pos + Vector2.down * 0.35f);
            SpriteVfx.Spawn("m3_blood_mist", hideIn.pos, 0.5f, Layer, Order + 1, false, 16f);
            var seep = SpriteVfx.Spawn("m3_blood_pool", hideIn.pos + Vector2.down * 0.28f, 0.45f, Layer, Order - 3, false, 8f);
            if (seep != null) seep.Looping(1.2f);
            Kick(Vector2.down, 0.03f, 0.15f);
            yield return ai.WaitFor(1.0f);
            if (seep != null) seep.Stop();

            // tráo
            int swaps = phase >= 3 ? 6 : 4;
            float dur = phase >= 3 ? 0.36f : 0.5f;
            for (int k = 0; k < swaps && !hp.IsDead; k++)
            {
                int a = Random.Range(0, intact.Count), b = (a + Random.Range(1, intact.Count)) % intact.Count;
                yield return Swap(intact[a], intact[b], dur);
                yield return ai.WaitFor(0.08f);
            }
            ai.TeleportTo(hideIn.pos + Vector2.down * 0.35f);
            Hint("Which coffin? Strike it!", 2f);
            guessOpen = true;
            float end = Time.time + guessTime, nextRattle = 0f;
            while (Time.time < end && struckCoffin == null && !hp.IsDead)
            {
                if (Time.time >= nextRattle)                                    // mọi nắp cùng rung: không lộ
                {
                    foreach (var c in intact) SpriteVfx.Spawn("m3_ground_burst", c.pos + Vector2.down * 0.2f, 0.35f, Layer, Order + 1, false, 18f);
                    nextRattle = Time.time + 1.0f;
                }
                yield return null;
            }
            guessOpen = false;
            if (hp.IsDead) yield break;
            bool right = struckCoffin == hideIn;
            if (struckCoffin != null && !right && mm != null)                  // quan tài sai: bật nắp, trống rỗng (rồi đóng lại)
            {
                mm.SetVisible(struckCoffin.name, false);
                mm.SetVisible(struckCoffin.name + "_open", true);
                Hint("Empty!", 1.2f);
                yield return ai.WaitFor(0.6f);
                mm.SetVisible(struckCoffin.name + "_open", false);
                mm.SetVisible(struckCoffin.name, true);
            }
            if (right) BreakCoffin(hideIn, false);                             // đoán đúng: quan tài của bà vỡ luôn (không hồi máu)
            SpriteVfx.Spawn("m3_blood_mist", Pos, 0.8f, Layer, Order + 1, false, 14f);
            SetHidden(false);
            if (right)
            {
                Kick(Vector2.down, 0.06f, 0.25f);
                yield return BreakRoutine(3f, "You drag the Matriarch from her coffin!");
            }
            else
            {
                hp.Heal(hp.Max * wrongHeal);
                ai.PlayState("Cast");
                var w = Wave(Pos, 3.2f, 1.2f, 0f, "m3_wave_blood");             // phá nắp: đẩy lùi + làm chậm
                w.onHit = _ => { var s = PS; if (s != null) s.Slow(0.6f, 2f, "Blood clings to your legs."); };
                float g = Random.Range(0f, 120f);
                w.gapAngles = new[] { g, g + 120f, g + 240f };
                w.gapWidth = 40f;
                w.knockback = 3f;
                Hint(struckCoffin == null ? "Too slow. She rises, refreshed." : "Wrong coffin. She feeds and rises.", 2f);
                yield return ai.WaitFor(0.6f);
                ai.PlayState("Idle");
            }
            struckCoffin = null;
            hideIn = null;
            ResetTrackers();
        }

        /// Hai quan tài đổi chỗ theo hai cung ngược nhau (mọi trạng thái của quan tài dời cùng).
        private IEnumerator Swap(Coffin a, Coffin b, float dur)
        {
            Vector2 pa = a.pos, pb = b.pos;
            var startA = new List<Vector3>(); var startB = new List<Vector3>();
            foreach (var g in a.gos) startA.Add(g != null ? g.transform.position : Vector3.zero);
            foreach (var g in b.gos) startB.Add(g != null ? g.transform.position : Vector3.zero);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)), lift = Mathf.Sin(e * Mathf.PI) * 0.45f;
                Vector2 da = Vector2.Lerp(Vector2.zero, pb - pa, e) + Vector2.up * lift;
                Vector2 db = Vector2.Lerp(Vector2.zero, pa - pb, e) - Vector2.up * lift;
                for (int i = 0; i < a.gos.Count; i++) if (a.gos[i] != null) a.gos[i].transform.position = startA[i] + (Vector3)da;
                for (int i = 0; i < b.gos.Count; i++) if (b.gos[i] != null) b.gos[i].transform.position = startB[i] + (Vector3)db;
                yield return null;
            }
            a.pos = pb; b.pos = pa;
        }

        /// Rite of Repose: mỗi 30 s dựng lại MỘT quan tài đã vỡ (cái xa Ren nhất).
        private IEnumerator RiteOfRepose()
        {
            ready["rite"] = Time.time + restoreEvery;                          // đúng 30 s, không bị pha 3 rút ngắn
            Coffin pick = null; float best = -1f;
            foreach (var c in coffins) if (c.broken) { float d = (c.pos - PlayerPos).magnitude; if (d > best) { best = d; pick = c; } }
            if (pick == null) yield break;
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Hint("\"Rise again, my resting place.\"", 2.5f);
            SpriteVfx.Spawn("m3_blood_pool", pick.pos + Vector2.down * 0.25f, 0.6f, Layer, Order - 3, false, 10f);
            yield return ai.WaitFor(0.7f);
            var mm = MapMechanics.Instance;
            SpriteVfx.Spawn("m3_blood_mist", pick.pos, 0.6f, Layer, Order + 1, false, 16f);
            if (mm != null) { mm.SetVisible(pick.name + "_open", false); mm.SetVisible(pick.name + "_broken", false); mm.SetVisible(pick.name, true); }
            pick.broken = false; pick.hits = 0;
            UpdateCoffinState();
            yield return ai.WaitFor(0.3f);
            ai.PlayState("Idle");
        }

        /// Phá vỡ một quan tài: hiện _broken, máu bắn; bà hồi máu theo cấp số cộng (healStep x số quan tài đã vỡ).
        private void BreakCoffin(Coffin c, bool heal)
        {
            if (c.broken) return;
            c.broken = true;
            var mm = MapMechanics.Instance;
            if (mm != null) { mm.SetVisible(c.name, false); mm.SetVisible(c.name + "_open", false); mm.SetVisible(c.name + "_broken", true); }
            SpriteVfx.Spawn("m3_blood_mist", c.pos, 0.7f, Layer, Order + 2, false, 16f);
            SpriteVfx.Spawn("m3_blood_splash", c.pos, 0.8f, Layer, Order + 2, false, 16f);
            Kick(Vector2.down, 0.05f, 0.2f);
            UpdateCoffinState();
            int left = Intact;
            if (heal && !hp.IsDead)
            {
                float amt = healStep * Destroyed;
                hp.Heal(hp.Max * amt);
                for (int k = 0; k < 5; k++) StartCoroutine(FlyMote("m3_blood_orb", c.pos + Random.insideUnitCircle * 0.2f, transform, Vector2.up * 0.45f, 0.5f + k * 0.06f, 0.7f));
                Hint(left > 0 ? $"A coffin shatters. She drinks its blood (+{amt * 100f:0}%). {left} remain." : "The last coffin falls. Isolde can finally die!", 2.5f);
            }
            else if (left == 0) Hint("The last coffin falls. Isolde can finally die!", 2.5f);
            if (Ready("rite")) ready["rite"] = Time.time + restoreEvery;       // đếm 30 s từ lúc có quan tài vỡ
        }

        /// Mạng của bà: còn quan tài thì không chết (máu dừng ở undyingFloor). Tên trên thanh máu ghi số quan tài.
        private void UpdateCoffinState()
        {
            int left = Intact;
            hp.Floor = left > 0 ? hp.Max * undyingFloor : 0f;
            if (!string.IsNullOrEmpty(baseName)) ai.displayName = left > 0 ? $"{baseName}  —  Coffins {left}/{coffins.Count}" : baseName;
        }

        // ------------------------------------------------------------------ Sanguine Tide (pha 3)
        private IEnumerator SanguineTide()
        {
            Use("tide", 12f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            yield return Telegraph(0.5f, Blood);
            if (hp.IsDead) yield break;
            // vũng máu phun gai (Hemorrhage)
            var spike = SpriteVfx.Load("m3_root_spike");
            foreach (var p in pools)
            {
                var g = GroundStrike.Spawn(p.pos, 0.75f, 0.48f, ai.damage * 0.8f, spike, 2, 14f, Order + 1, DarkBlood);
                g.bodyScale = 0.6f;
            }
            float g0 = Random.Range(0f, 120f);
            for (int i = 0; i < 2 && !hp.IsDead; i++)
            {
                var w = Wave(Pos, 3.4f, 1.6f, 0f, "m3_wave_blood");      // sóng rộng: không mất máu, làm chậm 45%
                w.knockback = 1.2f;
                w.onHit = _ => { var s = PS; if (s != null) s.Slow(0.55f, 2.5f, "The Sanguine Tide drags at you."); };
                float g = g0 + i * 25f;
                w.gapAngles = new[] { g, g + 120f, g + 240f };
                w.gapWidth = 42f;
                Kick(Vector2.down, 0.03f, 0.15f);
                yield return ai.WaitFor(0.6f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ nhát chém của Ren trúng quan tài
        private void OnStruck(Vector2 c, float r)
        {
            foreach (var b in bats) if (!b.dead && b.t != null && ((Vector2)b.t.position - c).magnitude <= r + 0.2f) b.dead = true;   // chém dơi
            if (guessOpen)
            {
                if (hideIn == null || struckCoffin != null) return;
                foreach (var cf in coffins)
                    if (!cf.broken && (cf.pos - c).magnitude <= r + 0.4f) { struckCoffin = cf; SpriteVfx.Spawn("m3_hurt", cf.pos, 0.6f, Layer, Order + 2, false, 18f); break; }
                return;
            }
            if (hideIn != null) return;                                        // đang tráo: chưa tính
            foreach (var cf in coffins)                                        // bình thường: chém để phá quan tài
            {
                if (cf.broken || (cf.pos - c).magnitude > r + 0.4f) continue;
                cf.hits++;
                SpriteVfx.Spawn("m3_hurt", cf.pos, 0.6f, Layer, Order + 2, false, 18f);
                if (cf.hits >= coffinHits) BreakCoffin(cf, true);
                break;
            }
        }
    }
}
