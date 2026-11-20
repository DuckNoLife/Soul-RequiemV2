using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// LANTERN KNIGHT — chỉ huy quân Black Lantern, miniboss canh tháp chuông (V5b, V9).
    /// Không cầm đèn: "đèn" của hắn là lời thề. Mỗi chiêu có một hoạt ảnh riêng và một cảnh báo đọc được:
    ///   (27/10 bỏ chém thường; 31/10 xoá hẳn code Cleave + hoạt ảnh Attack. Đứng gần mà chưa có chiêu: bước vòng quanh Ren, luôn nhìn Ren.)
    ///   Charge (Dash)    : vạch lam báo hướng 0,7 s rồi lao thẳng. Pha 2: lao 3 lần, nhắm lại mỗi lần.
    ///   Judgement (Slam) : giơ kiếm bổ xuống — vòng đỏ ở chỗ bổ, rồi sóng chấn động lan ra (27/10: VFX ground_impact giống
    ///                      chiêu F của người chơi, vòng kín bán kính waveRadius; né bằng lăn hoặc đứng ngoài tầm). Chỉ choáng, không sát thương.
    ///                      Pha 2: hai vòng sóng nối nhau.
    ///   Vow (Vow)        : cắm kiếm thề 2 s, ấn chú dưới chân. Xong lời thề: đồng đội trong bán kính được khiên (chỉ nhận 30% sát thương) 6 s.
    ///                      Đánh trúng hắn 3 lần trong lúc thề = vỡ lời thề, hắn choáng 2,2 s. Đây là cửa sổ phản công.
    ///   Lightless        : vào pha 2 (50% máu) hắn dập ánh sáng: cả khu tối lại (chỉ còn đèn Ichor, đèn tháp và đèn của Ren).
    /// </summary>
    public class LanternKnightSkills : MonoBehaviour, IEnemySkills
    {
        private const float StrafeSpeed = 0.45f;     // bước vòng quanh Ren: tỉ lệ tốc độ đi (và nhịp chân)

        [Header("Charge")]
        public float chargeWindup = 0.7f, chargeSpeed = 4.2f, chargeTime = 0.55f, chargeEvery = 5f;
        [Header("Judgement")]
        public float slamWindup = 0.75f, slamEvery = 8f, slamRadius = 0.55f, waveRadius = 1.8f, waveTime = 1.25f, gapWidth = 46f;
        [Tooltip("Tốc độ khung của sóng ground_impact (thấp = sóng lan chậm, dễ đọc hơn chiêu F của người chơi).")]
        public float waveFps = 10f;
        [Header("Vow")]
        public float vowTime = 2.0f, vowEvery = 15f, wardRadius = 3.6f, wardTime = 6f, wardMul = 0.3f;
        public int vowBreakHits = 3;
        [Header("Lightless")]
        public float darkness = 0.45f;

        private EnemyAI ai;
        private bool engaged, lightlessDone;
        private float nextCharge, nextSlam, nextVow;
        private int hitsTaken;
        private SpriteRenderer chargeLine;
        private static Sprite pixel;
        private static readonly Color Cold = new Color(0.62f, 0.78f, 1f, 1f);

        private float VfxScale => ai.Vfx != null ? ai.Vfx.scale : 0.77f;
        private int Layer => ai.SortingLayer;
        private int Order => ai.SortingOrder;

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) { h.Damaged += (_, __) => hitsTaken++; h.Died += _ => Restore(); }
        }

        private void OnDisable() { Restore(); KillLine(); }

        private void Restore()
        {
            if (lightlessDone) ZoneAmbience.DimMul = 1f;
        }

        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (ai.Player == null) yield break;
            if (!engaged)
            {
                engaged = true;
                nextCharge = Time.time + 2.5f;
                nextSlam = Time.time + 5f;
                nextVow = Time.time + 7f;
            }
            ai.LookAtPlayer = false;
            ai.MoveAnimSpeed = 1f;
            if (ai.InPhase2 && !lightlessDone) { yield return Lightless(); yield break; }

            Vector2 to = (Vector2)ai.Player.position - (Vector2)transform.position;
            float d = to.magnitude, gap = ai.GapToPlayer();

            if (Time.time >= nextVow && AlliesNear() > 0) { yield return Vow(); yield break; }
            if (Time.time >= nextSlam && d < 3.2f) { yield return Judgement(); yield break; }
            if (Time.time >= nextCharge && d > 1.3f && d < 5.5f) { yield return Charge(); yield break; }
            // 27/10: bỏ chém thường — LK chỉ dùng chiêu. Đứng gần mà chưa có chiêu: thủ thế, bước vòng chậm quanh Ren.
            if (d < 1.4f)
            {
                // 31/10: bước ngang nhưng MẶT luôn hướng về Ren (trước đây quay theo hướng bước -> quay lưng, lật qua lại),
                // chân bước chậm theo tốc độ thật (trước đây chạy đủ nhịp đi bộ ở 45% tốc độ -> trượt chân)
                ai.LookAtPlayer = true;
                ai.Desired = Vector2.Perpendicular(to.normalized) * ai.moveSpeed * StrafeSpeed * (Mathf.Sin(Time.time * 0.7f) >= 0f ? 1f : -1f);
                ai.MoveAnimSpeed = StrafeSpeed;
                ai.PlayState("Move", false);
                yield break;
            }

            ai.LookAtPlayer = false;
            ai.MoveAnimSpeed = 1f;
            ai.Desired = to.normalized * ai.moveSpeed;
            ai.PlayState("Move", false);
        }

        // ------------------------------------------------------------------ Charge
        private IEnumerator Charge()
        {
            SfxHooks.BossMove(this, "Charge"); // 19/11 (§335): âm
            int n = ai.InPhase2 ? 3 : 1;
            for (int i = 0; i < n && !ai.Health.IsDead && ai.Player != null; i++)
            {
                float wind = i == 0 ? chargeWindup : chargeWindup * 0.55f;
                ai.Desired = Vector2.zero;
                Vector2 dir = ai.DirToPlayer.normalized;
                ai.FaceX(dir.x);
                ai.PlayState(ai.HasAnimState("ChargePrep") ? "ChargePrep" : "Idle");    // hạ thấp người, kéo kiếm ra sau
                // §372: làn báo trước rộng bằng thân (thay vạch 0,1 đv); quãng lao = tới chỗ Ren + 1 đv (2,4..4,2), dừng trước vật cản
                float width = DashTelegraph.WidthFor(ai.BodyCollider, 0.7f), reach = 2.4f;
                DashTelegraph tg = null;
                float t = 0f;
                while (t < wind && !ai.Health.IsDead)
                {
                    if (ai.Player != null && (t < wind * 0.75f || tg == null))
                    {
                        dir = ai.DirToPlayer.normalized;
                        reach = DashTelegraph.Reach(transform.position, dir, DashTelegraph.WantReach(transform.position, ai.Player.position, 1f, 2.4f, 4.2f), 0.25f, ai.BodyCollider, ai.Player);
                    }
                    if (tg == null) tg = DashTelegraph.Spawn(transform.position, dir, reach, width, Cold, wind, Layer, Order - 1);
                    else tg.Aim(transform.position, dir, reach);
                    ai.FaceX(dir.x);
                    t += Time.deltaTime;
                    yield return null;
                }
                if (tg != null) tg.Release();
                if (ai.Health.IsDead) yield break;

                ai.Desired = dir * chargeSpeed;              // hướng chạy = hướng hoạt ảnh (EnemyAI chọn _S/_N theo Desired)
                ai.PlayState("Dash");
                if (ai.Vfx != null) ai.Vfx.Dash(dir);
                bool hit = false;
                t = 0f;
                float runTime = reach / Mathf.Max(0.1f, chargeSpeed);
                while (t < runTime && !ai.Health.IsDead)
                {
                    ai.Desired = dir * chargeSpeed;
                    if (!hit && ai.GapToPlayer() <= 0.1f) { ai.Hurt(ai.damage * 1.2f, transform.position, 4f); hit = true; }
                    t += Time.deltaTime;
                    yield return null;
                }
                ai.Desired = Vector2.zero;
                ai.PlayState("Idle");
                yield return ai.WaitFor(i == n - 1 ? 0.6f : 0.15f);
            }
            nextCharge = Time.time + chargeEvery * (ai.InPhase2 ? 0.8f : 1f);
        }

        // ------------------------------------------------------------------ Judgement (Slam)
        private IEnumerator Judgement()
        {
            SfxHooks.BossMove(this, "Judgement"); // 19/11 (§335): âm
            ai.Desired = Vector2.zero;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Slam");
            Vector2 at = (Vector2)transform.position + dir * 0.45f;
            // vòng đỏ ngay chỗ lưỡi kiếm sẽ bổ xuống (GroundStrike tự gây sát thương khi hết cảnh báo)
            // 27/10: Judgement KHÔNG gây sát thương — lưỡi kiếm và sóng chỉ làm choáng (Daze) + đẩy nhẹ
            var gs = GroundStrike.Spawn(at, slamWindup, slamRadius, 0f, null, 0, 14f, Order, new Color(1f, 0.35f, 0.3f));
            gs.knockback = 2f;
            gs.onHit = _ => Daze(0.9f);
            yield return ai.WaitFor(slamWindup);
            if (ai.Health.IsDead) yield break;
            SpriteVfx.Spawn("m3_lk_slam", at, VfxScale, Layer, Order - 1, false, 14f);
            CameraFollow.KickMain(dir, 0.06f, 0.25f);
            JudgementWave(at);
            if (ai.InPhase2)
            {
                yield return ai.WaitFor(0.45f);
                if (ai.Health.IsDead) yield break;
                JudgementWave(at);
            }
            yield return ai.WaitFor(0.7f);
            ai.PlayState("Idle");
            nextSlam = Time.time + slamEvery * (ai.InPhase2 ? 0.75f : 1f);
            yield return ai.WaitFor(0.3f);
        }

        /// Sóng Judgement: hình = VFX ground_impact (như chiêu F của Ren) phóng to tới waveRadius; Shockwave ẩn lo phần trúng.
        /// ground_impact: vòng lớn nhất ở khung 4, rộng 142 px (bán kính 0,71 đv ở scale 1), dẹt ~0,38.
        private void JudgementWave(Vector2 at)
        {
            float scale = waveRadius / 0.71f;
            SpriteVfx.Spawn("ground_impact", at, scale, Layer, Order - 1, false, waveFps);
            var w = Shockwave.Spawn(at, waveRadius, 4.2f / Mathf.Max(1f, waveFps), 0f, Order - 1, Color.clear);
            w.hidden = true;
            w.squash = 0.45f;
            w.band = 0.22f;
            w.knockback = 1.5f;
            w.onHit = _ => Daze(0.7f);
        }

        private void Daze(float s)
        {
            if (ai == null || ai.Player == null) return;
            var st = PlayerStatus.Of(ai.Player);
            if (st != null) st.Daze(s, "The Judgement rings in your skull!");
        }

        // ------------------------------------------------------------------ Vow
        private IEnumerator Vow()
        {
            SfxHooks.BossMove(this, "Vow"); // 19/11 (§335): âm
            nextVow = Time.time + vowEvery;
            ai.Desired = Vector2.zero;
            ai.FaceX(ai.DirToPlayer.x);
            ai.PlayState("Vow");
            Hints.Show("The Lantern Knight swears a vow. Break it!", 2f);
            var sigil = SpriteVfx.SpawnAttached("m3_lk_sigil", transform, VfxScale, Layer, Order - 1, 10f, vowTime);
            hitsTaken = 0;
            float t = 0f;
            bool broken = false;
            while (t < vowTime && !ai.Health.IsDead)
            {
                if (hitsTaken >= vowBreakHits) { broken = true; break; }
                t += Time.deltaTime;
                yield return null;
            }
            if (sigil != null) sigil.Stop();
            if (ai.Health.IsDead) yield break;
            if (broken)
            {
                // 16/11 (§269): bỏ thông báo thừa — hình / hoạt ảnh / bảng tên boss đã thể hiện
                ai.PlayState(ai.HasAnimState("Stagger") ? "Stagger" : "Idle");   // quỳ một gối chống kiếm: cửa sổ phản công
                yield return ai.Flash(2.2f, new Color(1f, 1f, 0.55f));
                ai.PlayState("Idle");
                yield break;
            }
            foreach (var e in Allies())
            {
                var buff = e.GetComponent<VowWard>();
                if (buff == null) buff = e.gameObject.AddComponent<VowWard>();
                buff.Apply(wardMul, wardTime, Layer);
            }
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ Lightless (vào pha 2)
        private IEnumerator Lightless()
        {
            SfxHooks.BossMove(this, "Lightless"); // 19/11 (§335): âm
            lightlessDone = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Vow");
            SpriteVfx.Spawn("m3_lk_lightless", transform.position, VfxScale, Layer, Order + 1, false, 12f);
            // 16/11 (§269): bỏ thông báo thừa — hình / hoạt ảnh / bảng tên boss đã thể hiện
            yield return ai.WaitFor(0.6f);
            ZoneAmbience.DimMul = darkness;
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.3f);
            yield return ai.Flash(0.5f, new Color(0.55f, 0.65f, 1f));
            ai.PlayState("Idle");
            nextCharge = Time.time + 0.4f;                  // vào pha 2 bằng một chuỗi lao
        }

        // ------------------------------------------------------------------ tiện ích
        private IEnumerable<EnemyAI> Allies()
        {
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (e != ai && e.Health != null && !e.Health.IsDead &&
                    ((Vector2)e.transform.position - (Vector2)transform.position).magnitude <= wardRadius)
                    yield return e;
        }

        private int AlliesNear() { int n = 0; foreach (var _ in Allies()) n++; return n; }

        private void KillLine()
        {
            if (chargeLine != null) { Destroy(chargeLine.gameObject); chargeLine = null; }
        }

        private void LateUpdate()
        {
            if (chargeLine != null && (ai == null || ai.Health == null || ai.Health.IsDead)) KillLine();
        }

        private SpriteRenderer MakeLine()
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
            var go = new GameObject("LK_ChargeLine");
            var s = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(s);
            s.sprite = pixel;
            s.sortingLayerID = Layer;
            s.sortingOrder = Order - 1;
            go.transform.localScale = new Vector3(chargeSpeed * chargeTime, 0.1f, 1f);
            return s;
        }
    }

    /// <summary>Khiên lời thề trên một quái: giảm sát thương nhận vào trong vài giây, có vòng sáng lam bao quanh.</summary>
    public class VowWard : MonoBehaviour
    {
        private Health hp;
        private float until;
        private SpriteVfx fx;

        public void Apply(float mul, float seconds, int layer)
        {
            hp = GetComponent<Health>();
            if (hp == null) return;
            hp.DamageTakenMul = mul;
            until = Time.time + seconds;
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (fx != null) fx.Stop();
            fx = SpriteVfx.SpawnAttached("m3_lk_ward", transform, 0.7f, layer, (sr != null ? sr.sortingOrder : 2) + 1, 10f, seconds);
            enabled = true;
        }

        private void Update()
        {
            if (Time.time < until && hp != null && !hp.IsDead) return;
            if (hp != null) hp.DamageTakenMul = 1f;
            if (fx != null) fx.Stop();
            fx = null;
            enabled = false;
        }
    }
}
