using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PRECENTOR MORROW — boss V11. Ca trưởng giáo hội, gậy chuông. Trận đấu là một bản thánh ca; đấu trường là nhạc cụ.
    /// MỌI hiệu ứng là ảnh PixelLab (Resources/VFX): tia m3_lantern_ray, vạch báo m3_ray_warn, tụ sáng m3_lantern_charge,
    /// mảnh sóng m3_wave_hymn, cột sáng m3_choir_lance, sương m3_vespers, giọt máu m3_blood_orb, Break m3_break.
    ///   MÔI TRƯỜNG — 6 cột tế (morrow_pillar_0..5), mỗi cột có đèn lồng thánh:
    ///   • Lantern Litany: đèn trên 2/3 cột tụ sáng (vạch báo 0,85 s) rồi BẮN TIA thánh xuyên đấu trường về phía Ren.
    ///     Cột đã vỡ không bắn được -> phá cột vừa là Break vừa làm chiêu này yếu đi.
    ///   • Blood Pact: theo chu kỳ một cột chuyển MÀU MÁU, máu chảy thành dòng giọt về Morrow. Chém vỡ đèn (3 nhát) trước khi hút xong
    ///     -> cột vỡ vĩnh viễn + Morrow BREAK (quỳ, x2 sát thương). Hút xong -> hồi 10% + chiêu nhanh hơn.
    ///   • Requiem (pha 3): Morrow về bệ tế, MỌI đèn còn lại bắn 3 loạt tia đan chéo.
    ///   • Cụm mộ quanh mép: Raise the Named gọi Risen Dead bò lên.
    ///   CHIÊU KHÁC
    ///   • Bell Sweep (cận chiến): quét gậy chuông 180°, đẩy lùi. Pha 2+: nối Toll Strike (gõ đất, vòng sóng nhỏ).
    ///   • Toll of Hymns: 1/2/3 vòng sóng có 4 khe, khe xoay dần mỗi vòng.
    ///   • Choir Lances: 4/5/6 cột sáng rơi nối đuôi đúng chỗ Ren đứng. • Dirge Notes: quạt 5 nốt (pha 2+: 2 loạt).
    ///   • Vespers Step: tan thành sương, hiện ở chỗ xa Ren (thoát góc) hoặc sau lưng Ren rồi quét (pha 2+).
    /// </summary>
    public class MorrowSkills : BossSkillsBase
    {
        [Header("Cột tế")]
        public int pillarCount = 6;
        public float pillarEvery = 18f, drainTime = 6.5f, drainHeal = 0.08f;
        public int pillarHits = 1;                  // 27/10: 1 nhát vỡ đèn
        [Header("Risen Dead")]
        public int maxRisen = 3;                    // tối đa 3 Risen còn sống cùng lúc
        public Vector2 graveRadii = new Vector2(3.52f, 2.37f);
        public float[] graveAngles = { -25f, 30f, 150f, 205f };

        private class Pillar
        {
            public int index; public string name; public Vector2 pos, lamp; public bool broken, blood; public int hits;
            public Light2D light; public float drainEnd;
        }
        private readonly List<Pillar> pillars = new List<Pillar>();
        private readonly List<EnemyAI> risen = new List<EnemyAI>();
        private int pactStacks;
        private float nextPillar;
        private static readonly Color Ice = new Color(0.65f, 0.82f, 1f, 1f);
        private static readonly Color Blood = new Color(0.9f, 0.12f, 0.15f, 1f);

        protected override int PhaseFor(float p) => p > 0.65f ? 1 : p > 0.30f ? 2 : 3;
        protected override float CooldownMul() => Mathf.Pow(0.9f, Mathf.Min(pactStacks, 3)) * (phase == 3 ? 0.85f : 1f);

        // ------------------------------------------------------------------ bắt đầu
        protected override void OnEngage()
        {
            Hint("\"You did us a kindness in the cistern. Let me return it.\"", 3.5f);
            var mm = MapMechanics.Instance;
            for (int i = 0; i < pillarCount && mm != null; i++)
            {
                string n = "morrow_pillar_" + i;
                var gos = mm.Named(n);
                if (gos.Count == 0) continue;
                var r = gos[0].GetComponentInChildren<SpriteRenderer>();
                Vector2 foot = r != null ? new Vector2(r.bounds.center.x, r.bounds.min.y + 0.12f) : (Vector2)gos[0].transform.position;
                Vector2 lamp = r != null ? new Vector2(r.bounds.center.x, r.bounds.min.y + r.bounds.size.y * 0.62f) : foot + Vector2.up * 0.5f;
                pillars.Add(new Pillar { index = i, name = n, pos = foot, lamp = lamp, light = mm.LightNamed(n + "_light") });
            }
            PlayerCombat.Struck += OnStruck;
            pinnedTimeMax = 0.8f; repositionCooldown = 2.5f;                        // pháp sư: bị áp sát lâu là dịch chuyển đi
            nextPillar = Time.time + 6f;
            Use("toll", 4f); Use("lances", 6f); Use("dirge", 2f); Use("raise", 999f); Use("vespers", 8f); Use("litany", 7f); Use("requiem", 4f);
            StartCoroutine(PillarCycle());
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PlayerCombat.Struck -= OnStruck;
        }

        // ------------------------------------------------------------------ chọn chiêu
        protected override IEnumerator Think()
        {
            if (Broken) { yield return null; yield break; }
            risen.RemoveAll(e => e == null || e.Health.IsDead);
            float gap = ai.GapToPlayer();
            float d = (PlayerPos - Pos).magnitude;

            // 27/10: Morrow là pháp sư — KHÔNG cận chiến. Bị áp sát: gõ chuông Toll Strike (choáng nhịp, không mất máu) rồi Vespers đi xa.
            // 27/10 (2): Morrow KHÔNG có đòn thường nào (bỏ Toll Strike cận chiến, Dirge Notes, Vespers tấn công).
            // Bị áp sát -> bộ đếm "bị đè" của BossSkillsBase gọi Vespers Step dịch chuyển đi (pinnedTimeMax 0,8 s).
            if (phase == 3 && ReadyG("requiem") && pillars.Exists(p => !p.broken)) { yield return Requiem(); yield break; }
            if (phase >= 2 && ReadyG("raise") && risen.Count < maxRisen) { yield return RaiseTheNamed(); yield break; }
            if (ReadyG("litany") && pillars.Exists(p => !p.broken)) { yield return LanternLitany(phase == 1 ? 2 : 3, 1, 11f); yield break; }
            if (ReadyG("toll") && d < 3.4f) { yield return TollOfHymns(); yield break; }
            if (ReadyG("lances")) { yield return ChoirLances(); yield break; }
            yield return Stalk(0.6f);
        }

        protected override IEnumerator Reposition() => VespersStep(false);

        protected override IEnumerator OnPhase(int p)
        {
            hp.Invulnerable = true;
            ai.Desired = Vector2.zero;
            ai.TeleportTo(ClampArena(center, 0.1f));
            SpriteVfx.Spawn("m3_vespers", Pos, 0.7f, Layer, Order + 1, false, 14f);
            ai.PlayState("Toll");
            Hint(p == 2 ? "\"The hymn bleeds. Rise, my Named!\"" : "\"Requiem. Let the Vale fall silent.\"", 3f);
            yield return ai.WaitFor(0.5f);
            var w = Wave(Pos, 4.2f, 1.2f, 0f, "m3_wave_hymn");           // chỉ đẩy lùi, không mất máu
            w.knockback = 4f;
            w.onHit = _ => { };
            Kick(Vector2.down, 0.08f, 0.35f);
            yield return ai.WaitFor(1.0f);
            hp.Invulnerable = false;
            if (p == 3)
            {
                foreach (var pl in pillars) if (!pl.broken && !pl.blood) StartCoroutine(Drain(pl));   // mọi cột còn lại đỏ cùng lúc
                Use("requiem", 4f);
            }
            if (p == 2) Use("raise", 0f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ cận chiến
        private IEnumerator TollStrike()
        {
            Use("sweep", 4f);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Toll");
            yield return Telegraph(0.45f, Ice);
            if (hp.IsDead) yield break;
            var ts = Wave(Pos, 1.5f, 0.6f, 0f, "m3_wave_hymn");                // chuông vang bên tai: choáng 1 nhịp, đẩy ra
            ts.knockback = 3f;
            ts.onHit = _ => { var s = PS; if (s != null) s.Daze(0.5f, "The bell rings in your skull!"); };
            SpriteVfx.Spawn("m3_lk_slam", Pos, 0.7f, Layer, Order - 1, false, 14f);
            Kick(Vector2.down, 0.03f, 0.15f);
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
            if (!hp.IsDead && ai.GapToPlayer() <= meleeRange * 1.6f && Ready("vespers")) yield return VespersStep(false);
        }

        // ------------------------------------------------------------------ Toll of Hymns
        private IEnumerator TollOfHymns()
        {
            Use("toll", 9f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Toll");
            yield return Telegraph(0.45f, Ice);
            if (hp.IsDead) yield break;
            int rings = phase == 1 ? 1 : 2;                     // 1 / 2 / 2 vòng
            float g0 = Random.Range(0f, 90f);
            for (int i = 0; i < rings && !hp.IsDead; i++)
            {
                var w = Wave(Pos, 3.6f, 1.7f, 0f, "m3_wave_hymn");     // vòng to khó né -> không mất máu, mà PHONG ẤN chiêu F
                w.knockback = 1.2f;
                w.onHit = _ => { var s = PS; if (s != null) s.Silence(3.5f, "The hymn steals your voice. Your mantras fall silent!"); };
                float g = g0 + i * 22f;
                w.gapAngles = new[] { g, g + 90f, g + 180f, g + 270f };
                w.gapWidth = 38f;
                Kick(Vector2.down, 0.03f, 0.15f);
                yield return ai.WaitFor(0.55f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Choir Lances
        private IEnumerator ChoirLances()
        {
            Use("lances", 10f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            yield return ai.WaitFor(0.4f);
            var frames = SpriteVfx.Load("m3_choir_lance");
            int n = 2 + phase;
            for (int i = 0; i < n && !hp.IsDead && ai.Player != null; i++)
            {
                var g = GroundStrike.Spawn(PlayerPos, 0.85f, 0.34f, ai.damage * 0.8f, frames, 2, 16f, Order + 1, Ice);
                g.bodyScale = 0.7f;
                yield return ai.WaitFor(0.32f);
            }
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ Dirge Notes
        private IEnumerator DirgeNotes()
        {
            Use("dirge", 6f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            yield return ai.WaitFor(0.45f);
            int waves = phase == 3 ? 2 : 1;
            for (int w = 0; w < waves && !hp.IsDead; w++)
            {
                Vector2 aim = (PlayerPos - Pos).normalized;
                float baseA = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + (w == 1 ? 7.5f : 0f);
                for (int k = -2; k <= 2; k++)
                {
                    float a = (baseA + k * 15f) * Mathf.Deg2Rad;
                    ai.ShootProjectile(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 2.2f, ai.damage * 0.6f, 3.4f);
                }
                yield return ai.WaitFor(0.45f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Raise the Named (mộ quanh mép)
        private IEnumerator RaiseTheNamed()
        {
            Use("raise", 26f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Toll");
            Hint("Morrow rings for the Named. The graves stir.", 2f);
            yield return Telegraph(0.6f, new Color(0.6f, 1f, 0.6f));
            var dirr = MapEncounterDirector.Instance;
            int n = Mathf.Min(2, maxRisen - risen.Count);
            var angles = new List<float>(graveAngles);
            for (int i = 0; i < n && dirr != null && angles.Count > 0; i++)
            {
                int pick = Random.Range(0, angles.Count);
                float a = angles[pick] * Mathf.Deg2Rad;
                angles.RemoveAt(pick);
                Vector2 p = center + new Vector2(Mathf.Cos(a) * graveRadii.x, -Mathf.Sin(a) * graveRadii.y);
                SpriteVfx.Spawn("m3_ground_burst", p, 0.7f, Layer, Order, false, 12f);
                var e = dirr.SpawnSummon("Undead", p, ai);
                if (e != null) risen.Add(e);
            }
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Vespers Step (dịch chuyển)
        private IEnumerator VespersStep(bool offensive)
        {
            Use("vespers", 10f);
            ai.Desired = Vector2.zero;
            Vector2 from = Pos;
            SpriteVfx.Spawn("m3_vespers", from, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(0f, 0.25f);
            Vector2 to;
            if (offensive && ai.Player != null)
            {
                Vector2 away = (PlayerPos - center).sqrMagnitude > 0.01f ? (PlayerPos - center).normalized : Vector2.right;
                to = ClampArena(PlayerPos + away * 0.6f, 0.85f);            // hiện ra sau lưng Ren (phía ngoài)
            }
            else
            {
                var extra = new List<Vector2> { center };
                foreach (var p in pillars) extra.Add(center + (p.pos - center) * 0.8f);
                to = FarPoint(0.6f, 12, extra);
            }
            ai.TeleportTo(to);
            SpriteVfx.Spawn("m3_vespers", to, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(1f, 0.2f);
            StartCoroutine(Echo(from));
            if (offensive && !hp.IsDead) yield return TollStrike();
        }

        /// Bóng mờ ở chỗ cũ: sau 1 s nổ thành vòng Toll nhỏ.
        private IEnumerator Echo(Vector2 at)
        {
            GroundStrike.Spawn(at, 1.0f, 0.5f, 0f, null, 0, 14f, Order, Ice);   // vòng báo chỗ bóng mờ sẽ nổ
            SpriteVfx.Spawn("m3_vespers", at, 0.45f, Layer, Order, false, 9f);
            yield return new WaitForSeconds(1.0f);
            var w = Wave(at, 1.4f, 0.7f, 0f, "m3_wave_hymn");          // dư âm: làm chậm, không mất máu
            w.knockback = 0.8f;
            w.onHit = _ => { var s = PS; if (s != null) s.Slow(0.65f, 1.5f, "The echo of the hymn weighs on you."); };
            w.gapAngles = new[] { Random.Range(0f, 360f) };
            w.gapWidth = 60f;
        }

        // ------------------------------------------------------------------ Lantern Litany: tia thánh bắn từ đèn trên cột tế
        private const float RayLen = 7.5f, RayHalfWidth = 0.2f, ChargeTime = 0.85f;
        private float rayDamageMul = 0.8f;

        /// count cột còn nguyên (lấy ngẫu nhiên) cùng bắn; waves loạt; cd = hồi chiêu.
        private IEnumerator LanternLitany(int count, int waves, float cd)
        {
            Use("litany", cd);
            var src = pillars.FindAll(p => !p.broken);
            if (src.Count == 0) yield break;
            for (int i = src.Count - 1; i > 0; i--) { int r = Random.Range(0, i + 1); var tmp = src[i]; src[i] = src[r]; src[r] = tmp; }
            if (count < src.Count) src.RemoveRange(count, src.Count - count);
            ai.Desired = Vector2.zero;
            ai.FaceX((PlayerPos - Pos).x);
            ai.PlayState("Cast");
            if (ai.Vfx != null) ai.Vfx.Cast();
            for (int w = 0; w < waves && !hp.IsDead && ai.Player != null; w++)
            {
                var aims = new List<Vector2>();
                var warns = new List<SpriteVfx>();
                foreach (var p in src)
                {
                    Vector2 tgt = PlayerPos + (w > 0 ? Random.insideUnitCircle * 0.35f : Vector2.zero);
                    Vector2 dir = (tgt - p.lamp).sqrMagnitude > 0.01f ? (tgt - p.lamp).normalized : Vector2.down;
                    aims.Add(dir);
                    SpriteVfx.Spawn("m3_lantern_charge", p.lamp, 0.7f, Layer, Order + 2, false, 12f / ChargeTime);
                    warns.Add(Ray("m3_ray_warn", p.lamp, p.lamp + dir * RayLen, 0.62f, 1.6f, Order + 1, 12f, ChargeTime + 0.1f));
                    if (warns[warns.Count - 1] != null) warns[warns.Count - 1].Tint(new Color(Ice.r, Ice.g, Ice.b, 0.9f));
                    if (p.light != null) p.light.intensity *= 1.8f;
                }
                yield return ai.WaitFor(ChargeTime);
                foreach (var v in warns) if (v != null) v.Stop();
                if (hp.IsDead) yield break;
                for (int i = 0; i < src.Count; i++)
                {
                    var p = src[i];
                    if (p.light != null) p.light.intensity /= 1.8f;
                    if (p.broken) continue;                                   // vừa bị chém vỡ trong lúc tụ sáng: tắt luôn
                    Vector2 end = p.lamp + aims[i] * RayLen;
                    Ray("m3_lantern_ray", p.lamp, end, 0.71f, 1.1f, Order + 2, 16f);
                    SpriteVfx.Spawn("m3_hymn_pop", p.lamp, 0.7f, Layer, Order + 3, false, 16f);
                    StartCoroutine(RayDamage(p.lamp, end, 0.32f));
                }
                Kick(Vector2.down, 0.04f, 0.18f);
                yield return ai.WaitFor(waves > 1 ? 0.7f : 0.35f);
            }
            ai.PlayState("Idle");
        }

        /// Tia gây sát thương một lần nếu Ren nằm trên tia trong lúc nó sáng (lăn né được).
        private IEnumerator RayDamage(Vector2 a, Vector2 b, float seconds)
        {
            float t = 0f;
            yield return new WaitForSeconds(0.05f);
            while (t < seconds && ai != null && ai.Player != null)
            {
                if (SegDist(PlayerPos, a, b) <= RayHalfWidth) { ai.Hurt(ai.damage * rayDamageMul, PlayerPos + (a - b).normalized * 0.3f, 2f); yield break; }
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ Requiem (pha 3): mọi đèn còn lại bắn 3 loạt
        private IEnumerator Requiem()
        {
            Use("requiem", 20f);
            yield return VespersToCenter();
            Hint("\"Sing, lanterns. Sing the Requiem!\"", 2.5f);
            rayDamageMul = 0.55f;                                       // 3 loạt đan chéo: mỗi tia nhẹ hơn
            yield return LanternLitany(6, 3, 11f);
            rayDamageMul = 0.8f;
            Use("requiem", 22f);
        }

        private IEnumerator VespersToCenter()
        {
            ai.Desired = Vector2.zero;
            SpriteVfx.Spawn("m3_vespers", Pos, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(0f, 0.2f);
            ai.TeleportTo(center);
            SpriteVfx.Spawn("m3_vespers", center, 0.7f, Layer, Order + 1, false, 16f);
            yield return Fade(1f, 0.2f);
        }

        // ------------------------------------------------------------------ cột tế: hút máu / phá đèn
        private IEnumerator PillarCycle()
        {
            while (hp != null && !hp.IsDead)
            {
                if (Time.time >= nextPillar && !Broken)
                {
                    int n = 1;                                          // mỗi lần chỉ 1 cột (pha 3: mọi cột đã đỏ lúc chuyển pha)
                    var pool = pillars.FindAll(p => !p.broken && !p.blood);
                    if (pool.Count > 0)
                    {
                        Pillar first = pool[Random.Range(0, pool.Count)];
                        StartCoroutine(Drain(first));
                        if (n == 2)
                        {
                            // cột thứ hai ở phía đối diện -> chỉ kịp phá một
                            Pillar best = null; float bd = -1f;
                            foreach (var p in pool) { if (p == first) continue; float dd = (p.pos - first.pos).sqrMagnitude; if (dd > bd) { bd = dd; best = p; } }
                            if (best != null) StartCoroutine(Drain(best));
                        }
                        Hint(n == 2 ? "Two pillars run red. Shatter a lantern!" : "A pillar runs red. Shatter its lantern!", 2.5f);
                    }
                    nextPillar = Time.time + (phase == 1 ? pillarEvery : phase == 2 ? pillarEvery * 0.85f : pillarEvery * 0.75f);
                }
                yield return null;
            }
        }

        private IEnumerator Drain(Pillar p)
        {
            var mm = MapMechanics.Instance;
            p.blood = true; p.hits = 0;
            p.drainEnd = Time.time + drainTime;
            if (mm != null) { mm.SetVisible(p.name, false); mm.SetVisible(p.name + "_blood", true); }
            Color oldLight = p.light != null ? p.light.color : Color.white;
            if (p.light != null) p.light.color = Blood;
            Wave(p.pos, 0.9f, 0.6f, 0f, "m3_wave_blood");
            float nextMote = 0f;
            while (Time.time < p.drainEnd && !p.broken && !hp.IsDead)
            {
                if (Time.time >= nextMote)                                   // dòng giọt máu chảy từ đèn về Morrow
                {
                    StartCoroutine(FlyMote("m3_blood_orb", p.lamp, transform, Vector2.up * 0.45f, 0.55f, 0.7f));
                    nextMote = Time.time + 0.12f;
                }
                yield return null;
            }
            if (mm != null) { mm.SetVisible(p.name + "_blood", false); mm.SetVisible(p.name, !p.broken); }
            p.blood = false;
            if (hp.IsDead) yield break;
            if (p.broken)
            {
                if (mm != null) mm.SetVisible(p.name + "_broken", true);
                if (p.light != null) p.light.enabled = false;
                SpriteVfx.Spawn("m3_blood_splash", p.lamp, 0.8f, Layer, Order + 2, false, 16f);
                Kick(Vector2.down, 0.05f, 0.2f);
                StartCoroutine(BreakRoutine(3.5f, "The lantern shatters. Morrow is stunned!"));
            }
            else
            {
                if (p.light != null) p.light.color = oldLight;
                hp.Heal(hp.Max * drainHeal);
                pactStacks++;
                SpriteVfx.Spawn("m3_heal_lantern", Pos, 0.7f, Layer, Order - 1, false, 14f);
                Hint(pactStacks == 1 ? "Morrow drinks the blood. His hymn quickens." : "Blood Pact x" + pactStacks + ". Faster still.", 2f);
            }
        }

        /// Nhát chém của người chơi (sự kiện Struck): trúng đèn cột đang đỏ.
        private void OnStruck(Vector2 c, float r)
        {
            foreach (var p in pillars)
            {
                if (!p.blood || p.broken) continue;
                if ((p.lamp - c).magnitude <= r + 0.35f || (p.pos - c).magnitude <= r + 0.3f)
                {
                    p.hits++;
                    SpriteVfx.Spawn("m3_hurt", p.lamp, 0.6f, Layer, Order + 2, false, 18f);
                    if (p.hits >= pillarHits) p.broken = true;
                    break;
                }
            }
        }
    }
}
