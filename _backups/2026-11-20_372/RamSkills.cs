using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE ETHER RAM (mini-boss Boiler Room — chuồng nhốt cũ của giáo phái). Viết lại 06/11 (c) theo yêu cầu người dùng:
    ///
    /// Nội tại "Da Ether": mặc định chỉ nhận `armorDamageMul` (50%) sát thương. Dính nước xả (SluiceMechanism) -> choáng
    ///   `quenchStun` giây (nhận `quenchDamageMul`), rồi "Ướt sũng" `soakedTime` giây: MẤT giảm sát thương + chậm `soakedSlow`.
    ///   Van trên tường bắc được đánh dấu (mũi tên + quầng sáng, <see cref="SluiceMechanism.Alert"/>) suốt trận.
    ///
    /// Pha 1: đi nhanh hơn Ren (moveSpeed trong EnemyDefinition), áp sát thì HÚC (gồng ngắn, lao 0,7 đv). Thỉnh thoảng (mỗi
    ///   `chargeEveryMin..Max` giây, khi Ren ở xa) gồng LAO thẳng: đâm tường -> choáng `wallStunTime`, đá trần rơi.
    /// Pha 2 "Rampage": lao LIÊN TỤC như lò xo — đâm tường thì đá rơi, bật ra theo hướng phản xạ (lệch ngẫu nhiên), không nhắm ai
    ///   nhưng nhanh (`rampageSpeed`) và đôi lúc bật về phía Ren (`rampageAimChance`). Tường không làm nó choáng — chỉ nước dừng được nó.
    ///   Cứ `rampageBreathEvery` giây thở dốc `rampageBreath` giây (cửa sổ nghỉ cho người chơi).
    /// Chống kẹt (người dùng: Ram đẩy Ren kẹt tường mãi): Ram không đẩy vật lý vào Ren khi đi (dừng sát rồi húc); lúc lao / húc / Rampage
    ///   tắt va chạm với Ren (gây sát thương + đẩy rồi lao xuyên qua).
    /// Giữ từ bản trước: vệt độc khi lao (nước rửa trôi), hút vệt độc -> quá tải (lao nhanh, gồng ngắn).
    /// </summary>
    public class RamSkills : MonoBehaviour, IEnemySkills
    {
        public Sprite[] rubbleSprites;                         // đá rơi khi Ram đâm tường
        public int rubbleCount = 3;
        public float windupTime = 0.85f, chargeSpeed = 3.6f, maxRun = 1.8f, impactRadius = 1.1f;
        public float chargeEveryMin = 5f, chargeEveryMax = 8f, chargeMinDistance = 1.2f;
        [Header("Húc (pha 1)")]
        public float goreRange = 0.32f, goreWindup = 0.4f, goreSpeed = 3.2f, goreTime = 0.22f, goreCooldown = 1.3f;
        [Header("Rampage (pha 2)")]
        public float rampageSpeed = 4.2f, rampageAimChance = 0.35f, rampageJitter = 35f;
        public float rampageBreathEvery = 10f, rampageBreath = 1.4f;
        [Tooltip("Hướng bật phải còn ít nhất bấy nhiêu đv đường trống (chống kẹt góc)")]
        public float rampageMinRun = 1.2f;
        public int rampageRubble = 2;
        [Header("Da Ether / nước")]
        public float armorDamageMul = 0.5f;
        public float wallStunTime = 1.2f;
        public float quenchStun = 2.5f, quenchStunP2 = 2.0f, quenchDamageMul = 1.5f;
        public float soakedTime = 10f, soakedSlow = 0.2f;
        [Header("Vệt độc / quá tải")]
        public float trailLife = 4f, trailRadius = 0.24f, trailEvery = 0.09f;
        public int overchargeAbsorb = 8;
        public float overchargeTime = 6f, overchargeSpeedMul = 1.25f, overchargeWindupMul = 0.7f;

        private struct Trail { public Vector2 p; public float born; public SpriteRenderer sr; }
        private readonly List<Trail> trails = new List<Trail>();
        private float nextTrail, nextTrailHurt, nextDrip;
        private int absorbed;
        private float overchargedUntil, soakedUntil, lastQuench, nextNudge, nextCharge, nextGore, nextBodyHit;
        private bool engaged, stunnedByWater, rampHinted, quenchHinted, soakHinted, armorHinted, overHinted, wallHinted;
        private SpriteRenderer aura, chargeLine;
        private float lastBounceAt = -9f;
        private Collider2D lastWall;                                   // vật vừa dò trúng (log chẩn đoán trong Editor)
        private Rect arena; private bool hasArena;                      // khung khu boss: ArenaSeal xích chân boss cách mép 0,3 đv
        private EnemyAI ai;
        private bool ignoringPlayer;
        private static Sprite pixel;

        private bool Overcharged => Time.time < overchargedUntil;
        private bool Soaked => Time.time < soakedUntil;
        private float SpeedMul => (Soaked ? 1f - soakedSlow : 1f) * (Overcharged ? overchargeSpeedMul : 1f);
        private float Windup(float w) => w * (Overcharged ? overchargeWindupMul : 1f);

        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (!engaged) Engage();
            if (ai.Player == null) yield break;
            Vector2 pos = transform.position, pp = ai.Player.position;

            if (SluiceMechanism.Wet(pos)) { yield return Quench(); yield break; }       // bước vào nước

            if (ai.InPhase2) { yield return Rampage(); yield break; }

            float dist = (pp - pos).magnitude;
            if (Time.time >= nextCharge && dist >= chargeMinDistance)
            {
                yield return Charge();
                nextCharge = Time.time + Random.Range(chargeEveryMin, chargeEveryMax);
                yield break;
            }
            float gap = ai.GapToPlayer();
            if (gap <= goreRange && Time.time >= nextGore)
            {
                yield return Gore();
                nextGore = Time.time + goreCooldown;
                yield break;
            }
            // đi: nhanh hơn Ren; sát người thì đứng lại (không ủi Ren vào tường)
            Vector2 to = pp - pos;
            Vector2 step = gap <= 0.1f ? Vector2.zero : to.normalized * ai.moveSpeed * SpeedMul;
            ai.FaceX(to.x);
            ai.Desired = step;
            ai.PlayState(step == Vector2.zero ? "Idle" : "Move", false);
            Absorb(pos);
            Nudge();
        }

        private void Engage()
        {
            engaged = true;
            nextCharge = Time.time + 2.5f;
            lastQuench = Time.time;
            nextNudge = Time.time + 25f;
            SluiceMechanism.Alert = true;
            var med = MapEncounterDirector.Instance;
            hasArena = med != null && med.TryGetZoneAt(transform.position, out _, out arena);
            ai.Health.Damaged += OnDamaged;
            ai.Health.Died += _ => { SluiceMechanism.Alert = false; IgnorePlayer(false); };
            StartCoroutine(Tutorial());
        }

        // ------------------------------------------------------------------ chỉ dẫn
        private IEnumerator Tutorial()
        {
            yield return new WaitForSeconds(2.7f);
            if (ai != null && !ai.Health.IsDead)
                Hints.Show("Its Ether hide turns steel. Strike a glowing valve on the north wall - cold water breaks it.", 5f);
        }

        private void OnDamaged(Health h, float amount)
        {
            if (armorHinted || Soaked || stunnedByWater || h.IsDead) return;
            armorHinted = true;
            Hints.Show("Your blade barely bites. Soak it with a sluice valve first.", 3f);
        }

        /// <summary>Lâu chưa dập được Ram -> nhắc lại cách thắng (tối thiểu 25 giây một lần).</summary>
        private void Nudge()
        {
            if (Time.time < nextNudge || Time.time - lastQuench < 25f) return;
            nextNudge = Time.time + 25f;
            Hints.Show("Lure it across a channel, then strike the glowing valve!", 4f);
        }

        // ------------------------------------------------------------------ pha 1: húc
        private IEnumerator Gore()
        {
            ai.Desired = Vector2.zero;
            ai.PlayState("ChargeBegin");
            ai.FaceX(ai.Player.position.x - transform.position.x);
            yield return ai.Flash(Windup(goreWindup), new Color(1f, 0.45f, 0.35f));
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 dir = ((Vector2)ai.Player.position - (Vector2)transform.position).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("ChargeRun");
            IgnorePlayer(true);
            bool hit = false;
            for (float t = 0f; t < goreTime && !ai.Health.IsDead; t += Time.deltaTime)
            {
                ai.Desired = dir * goreSpeed * SpeedMul;
                if (!hit && ai.GapToPlayer() <= 0.08f) { ai.Hurt(ai.damage, transform.position, 3f); hit = true; }
                yield return null;
                if (SluiceMechanism.Wet(transform.position)) { ai.Desired = Vector2.zero; IgnorePlayer(false); yield return Quench(dir); yield break; }
            }
            ai.Desired = Vector2.zero;
            IgnorePlayer(false);
            ai.PlayState("ChargeRecover");
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ pha 1: lao có nhắm
        private IEnumerator Charge()
        {
            SfxHooks.BossMove(this, "Charge"); // 19/11 (§335): âm
            ai.Desired = Vector2.zero;
            ai.PlayState("ChargeBegin");
            var line = chargeLine = MakeLine();
            float t = 0f, w = Windup(windupTime), speed = chargeSpeed * SpeedMul;
            Vector2 dir = Vector2.right;
            while (t < w && !ai.Health.IsDead)
            {
                if (ai.Player != null && t < w * 0.75f)
                    dir = ((Vector2)ai.Player.position - (Vector2)transform.position).normalized;
                ai.FaceX(dir.x);
                PlaceLine(line, dir, speed * maxRun);
                if (SluiceMechanism.Wet(transform.position)) break;           // đang gồng mà nước tràn tới chân
                t += Time.deltaTime;
                yield return null;
            }
            DropLine();
            if (ai.Health.IsDead) yield break;
            if (SluiceMechanism.Wet(transform.position)) { yield return Quench(); yield break; }

            ai.PlayState("ChargeRun");
            IgnorePlayer(true);
            bool hitPlayer = false, wall = false, wet = false;
            float run = 0f, stall = 0f;
            Vector2 last = transform.position;
            while (run < maxRun && !ai.Health.IsDead)
            {
                ai.Desired = dir * speed;
                if (Time.time >= nextTrail) { nextTrail = Time.time + trailEvery; DropTrail(transform.position); }
                if (!hitPlayer && ai.GapToPlayer() <= 0.08f) { ai.Hurt(ai.damage, transform.position, 4f); hitPlayer = true; }
                run += Time.deltaTime;
                yield return null;
                if (SluiceMechanism.Wet(transform.position)) { wet = true; break; }
                if (Stalled(ref last, ref stall, run, speed)) { wall = true; break; }
            }
            ai.Desired = Vector2.zero;
            IgnorePlayer(false);
            if (wet) { yield return Quench(dir); yield break; }
            if (wall)
            {
                Vector2 at = (Vector2)transform.position + dir * 0.35f;
                Impact(at, dir, rubbleCount, true);
                if (!wallHinted) { wallHinted = true; Hints.Show("The wall barely slows it. Use the water.", 2.5f); }
                ai.PlayState("ChargeRecover");
                yield return ai.WaitFor(0.3f);
                ai.PlayState("Hit");
                yield return ai.Flash(wallStunTime, new Color(1f, 1f, 0.55f));
            }
            else
            {
                ai.PlayState("ChargeRecover");
                yield return ai.WaitFor(0.5f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ pha 2: lao liên tục, bật tường như lò xo
        private IEnumerator Rampage()
        {
            SfxHooks.BossMove(this, "Rampage"); // 19/11 (§335): âm
            if (!rampHinted)
            {
                rampHinted = true;
                Hints.Show("The Ram goes berserk and will not stop! Only the water can bring it down.", 4f);
            }
            // gồng ngắn mở đầu, hướng về Ren
            ai.Desired = Vector2.zero;
            ai.PlayState("ChargeBegin");
            Vector2 dir = ai.Player != null ? ((Vector2)ai.Player.position - (Vector2)transform.position).normalized : Vector2.right;
            var line = chargeLine = MakeLine();
            for (float t = 0f, w = Windup(0.55f); t < w && !ai.Health.IsDead; t += Time.deltaTime)
            {
                ai.FaceX(dir.x);
                PlaceLine(line, dir, 2.5f);
                yield return null;
            }
            DropLine();
            if (ai.Health.IsDead) yield break;

            IgnorePlayer(true);
            ai.PlayState("ChargeRun");
            float breathAt = Time.time + rampageBreathEvery, run = 0f, stall = 0f;
            Vector2 last = transform.position;
            int bounces = 0;
            while (!ai.Health.IsDead)
            {
                float speed = rampageSpeed * SpeedMul;
                // 06/11 (b): dò trước bằng khối tròn cỡ thân Ram -> bật đúng lúc chạm tường (không đợi kẹt mới biết)
                // 06/11 (f): tính tầm dò MỘT lần, so có dung sai — trước đây biểu thức tính 2 lần lệch số lẻ cuối (độ chính xác float)
                // -> "đường trống" bị coi là đâm tường mỗi khung -> Ram quay tại chỗ giữa phòng (log: không vật nào chặn).
                float look = speed * Time.deltaTime + 0.06f;
                bool hitAhead = FreeDistance(dir, look, out Vector2 n, out Vector2 at) < look - 0.001f;
                bool hitWall = hitAhead;
                if (!hitAhead)
                {
                    ai.Desired = dir * speed;
                    ai.FaceX(dir.x);
                    if (Time.time >= nextTrail) { nextTrail = Time.time + trailEvery; DropTrail(transform.position); }
                        if (Time.time >= nextBodyHit && ai.GapToPlayer() <= 0.08f) { ai.Hurt(ai.damage, transform.position, 3.5f); nextBodyHit = Time.time + 0.6f; }
                    run += Time.deltaTime;
                    yield return null;
                    if (SluiceMechanism.Wet(transform.position)) { ai.Desired = Vector2.zero; IgnorePlayer(false); yield return Quench(dir); yield break; }
                    // dự phòng: bị chặn bởi thứ dò không ra (vật động...) -> coi như tường, pháp tuyến ngược hướng lao
                    if (Stalled(ref last, ref stall, run, speed)) { hitWall = true; n = -dir; at = (Vector2)transform.position + dir * 0.3f; }
                }
                if (!hitWall) continue;

                // đâm tường: đá rơi, khựng ngắn, chọn hướng bật mới còn đường trống
                bool rocks = Time.time - lastBounceAt >= 0.4f;               // đâm dồn dập (góc hẹp) thì không gọi thêm đá
#if UNITY_EDITOR
                if (!rocks) Debug.Log("[Ram] bật dồn dập: " + (!hitAhead ? "kẹt (Stalled)" : lastWall != null ? lastWall.name + " (lớp " + LayerMask.LayerToName(lastWall.gameObject.layer) + ")" : "mép khung đấu trường")
                                      + " tại " + (Vector2)transform.position + " hướng " + dir + " khung " + (hasArena ? arena.ToString() : "không có")
                                      + " thân r=" + CastRadius.ToString("0.00") + " tâm " + CastOrigin);
#endif
                lastBounceAt = Time.time;
                if (rocks) Impact(at, dir, rampageRubble, (bounces++ % 2) == 0);
                else { var mm = MapMechanics.Instance; if (mm != null) mm.RamImpact(at, impactRadius); }
                ai.Desired = Vector2.zero;
                ai.PlayState("ChargeRecover");
                yield return ai.WaitFor(0.18f);
                if (ai.Health.IsDead) break;
                dir = ChooseBounce(dir, n);
                ai.FaceX(dir.x);
                run = 0f; stall = 0f; last = transform.position;

                if (Time.time >= breathAt)
                {
                    // thở dốc: cửa sổ nghỉ (vẫn còn da Ether nếu chưa ướt)
                    ai.PlayState("Hit");
                    yield return ai.Flash(rampageBreath, new Color(1f, 1f, 0.6f));
                    breathAt = Time.time + rampageBreathEvery;
                    if (ai.Health.IsDead) break;
                    if (ai.Player != null) dir = ChooseBounce(((Vector2)ai.Player.position - (Vector2)transform.position).normalized, Vector2.zero);
                    ai.PlayState("ChargeBegin");
                    yield return ai.WaitFor(Windup(0.35f));
                }
                ai.PlayState("ChargeRun");
            }
            ai.Desired = Vector2.zero;
            IgnorePlayer(false);
        }

        // ------------------------------------------------------------------ dò tường / chọn hướng bật (06/11 b)
        /// <summary>Tâm + bán kính khối dò (theo collider thân Ram).</summary>
        private Vector2 CastOrigin => ai.BodyCollider != null ? (Vector2)ai.BodyCollider.bounds.center : (Vector2)transform.position;
        private float CastRadius => ai.BodyCollider != null ? Mathf.Clamp(Mathf.Min(ai.BodyCollider.bounds.extents.x, ai.BodyCollider.bounds.extents.y * 2.5f), 0.1f, 0.4f) : 0.2f;

        /// <summary>Vật Ram đâm vào mà bật ra: CHỈ vật tĩnh (tường, cột, rào, mép nước). 06/11 (d): bỏ mọi thân động (Ren, quái, vật bị đẩy)
        /// và cả cây object của Ren — trước đây collider của Ren lọt lọc `Health` -> Ram coi Ren là tường, quay tại chỗ cạnh Ren.</summary>
        private bool Solid(Collider2D c)
        {
            if (c == null || c.isTrigger) return false;
            var rb = c.attachedRigidbody;
            if (rb != null && (rb == ai.Rb || rb.bodyType == RigidbodyType2D.Dynamic)) return false;
            if (ai.Player != null && (c.transform == ai.Player || c.transform.IsChildOf(ai.Player) || ai.Player.IsChildOf(c.transform))) return false;
            if (c.GetComponentInParent<EnemyAI>() != null) return false;                 // quái khác: không bật
            var hp = c.GetComponentInParent<Health>();
            return hp == null || !hp.IsPlayer;                                          // Ren: lao xuyên qua
        }

        /// <summary>Khoảng trống theo hướng `dir` (tối đa `max`) trước khi khối thân Ram chạm vật rắn. Chỉ tính mặt chắn ngược hướng đi
        /// (bỏ qua bức tường vừa rời ra — khối dò bắt đầu chồng lên nó).</summary>
        private float FreeDistance(Vector2 dir, float max, out Vector2 normal, out Vector2 point)
        {
            Vector2 o = CastOrigin;
            normal = -dir; point = o + dir * max;
            float best = max;
            foreach (var h in Physics2D.CircleCastAll(o, CastRadius, dir, max))
            {
                if (!Solid(h.collider)) continue;
                Vector2 n = h.normal;
                if (h.distance <= 0.001f)
                {
                    // 06/11 (e): khối dò đang CHỒNG lên vật (thân Ram đè tấm chắn đấu trường / tường): pháp tuyến thật = từ điểm gần nhất
                    // trên vật về tâm. Đi vào vật -> bị chặn ngay; đi ra -> bỏ qua. (Bản d bỏ qua hết -> Ram chọn hướng đâm vào tường, quay tại chỗ.)
                    Vector2 cp = h.collider.ClosestPoint(o);
                    n = o - cp;
                    if (n.sqrMagnitude < 1e-6f) n = hasArena ? arena.center - o : -dir;   // tâm đã lọt hẳn vào vật: hướng thoát = về giữa khu
                    n.Normalize();
                }
                if (Vector2.Dot(n, dir) > -0.1f) continue;                 // mặt cùng chiều đi / đang rời ra: không chắn
                if (h.distance < best) { best = h.distance; normal = n; point = h.point; lastWall = h.collider; }
            }
            // khung xích của đấu trường cũng là tường (không có collider riêng ở đúng chỗ chân boss bị giữ)
            if (hasArena)
            {
                Vector2 f = transform.position;
                const float m = 0.34f;                                      // ArenaSeal giữ chân boss cách mép 0,3 đv
                if (dir.x > 0.0001f) Edge((arena.xMax - m - f.x) / dir.x, Vector2.left, f, dir, ref best, ref normal, ref point);
                if (dir.x < -0.0001f) Edge((arena.xMin + m - f.x) / dir.x, Vector2.right, f, dir, ref best, ref normal, ref point);
                if (dir.y > 0.0001f) Edge((arena.yMax - m - f.y) / dir.y, Vector2.down, f, dir, ref best, ref normal, ref point);
                if (dir.y < -0.0001f) Edge((arena.yMin + m - f.y) / dir.y, Vector2.up, f, dir, ref best, ref normal, ref point);
            }
            return best;
        }

        private void Edge(float t, Vector2 n, Vector2 from, Vector2 dir, ref float best, ref Vector2 normal, ref Vector2 point)
        {
            t = Mathf.Max(0f, t);                                           // đã ở ngoài mép mà còn đi ra -> chặn ngay
            if (t >= best) return;
            best = t; normal = n; point = from + dir * t; lastWall = null;
        }

        /// <summary>Hướng bật: ưu tiên (đôi lúc) về phía Ren, còn lại phản xạ lệch ngẫu nhiên; chỉ nhận hướng còn ≥ `rampageMinRun`
        /// đường trống và không cắm vào mặt vừa đâm. Không hướng nào đạt (góc chết) -> quét 16 hướng lấy hướng thoáng nhất.</summary>
        private Vector2 ChooseBounce(Vector2 dir, Vector2 normal)
        {
            bool hasNormal = normal.sqrMagnitude > 0.01f;
            Vector2 baseDir = hasNormal ? Vector2.Reflect(dir, normal) : dir;
            var tries = new List<Vector2>(10);
            if (ai.Player != null && (Random.value < rampageAimChance || !hasNormal))
                tries.Add(Rotate(((Vector2)ai.Player.position - (Vector2)transform.position).normalized, Random.Range(-15f, 15f)));
            float j = Random.Range(-rampageJitter, rampageJitter);
            foreach (float off in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f }) tries.Add(Rotate(baseDir, j + off));
            foreach (var t in tries)
            {
                var d = t.normalized;
                if (hasNormal && Vector2.Dot(d, normal) < 0.2f) continue;
                if (FreeDistance(d, rampageMinRun, out _, out _) >= rampageMinRun - 0.001f) return d;
            }
            Vector2 open = -dir; float far = -1f;
            for (int k = 0; k < 16; k++)
            {
                var d = Rotate(Vector2.right, k * 22.5f + Random.Range(-5f, 5f));
                float f = FreeDistance(d, 6f, out _, out _);
                if (f > far) { far = f; open = d; }
            }
            return open;
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Đang lao mà gần như đứng yên = đụng tường.</summary>
        private bool Stalled(ref Vector2 last, ref float stall, float run, float speed)
        {
            Vector2 now = transform.position;
            float moved = (now - last).magnitude;
            last = now;
            stall = (run > 0.15f && moved < speed * Time.deltaTime * 0.25f) ? stall + Time.deltaTime : 0f;
            return stall > 0.06f;
        }

        private void Impact(Vector2 at, Vector2 dir, int rocks, bool aimOne)
        {
            var mm = MapMechanics.Instance;
            if (mm != null) mm.RamImpact(at, impactRadius);
            Rubble(at, rocks, aimOne);
            CameraFollow.KickMain(dir, 0.08f, 0.25f);
            SoulRequiem.Core.Sfx.Play("lg_wall_crash", at); // 19/11 (§337): âm: đâm vách -> trần nứt, đá sắp rơi
            if (rocks > 0) SoulRequiem.Core.Sfx.Play("ceiling_crack", at);
        }

        // ------------------------------------------------------------------ bị nước dập
        private IEnumerator Quench(Vector2 slide = default)
        {
            stunnedByWater = true;
            ai.Desired = slide * 0.6f;                                        // trượt thêm một đoạn trên nền ướt rồi khuỵu
            lastQuench = Time.time;
            overchargedUntil = 0f;
            absorbed = 0;
            StartCoroutine(Steam());
            CameraFollow.KickMain(slide == default ? Vector2.down : slide, 0.1f, 0.3f);
            if (!quenchHinted) { quenchHinted = true; Hints.Show("Steam hisses off its hide - the Ram is down! Strike now!", 3f); }
            ai.PlayState("ChargeRecover");
            yield return ai.WaitFor(0.2f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Hit");
            yield return ai.Flash(ai.InPhase2 ? quenchStunP2 : quenchStun, new Color(0.7f, 0.9f, 1f));
            stunnedByWater = false;
            soakedUntil = Time.time + soakedTime;
            if (!soakHinted) { soakHinted = true; Hints.Show("Soaked through - its hide can't shield it for a while!", 3f); }
            ai.PlayState("Idle");
        }

        /// <summary>Hơi nước trắng bốc lên từ thân Ram.</summary>
        private IEnumerator Steam()
        {
            SfxHooks.BossMove(this, "Steam"); // 19/11 (§335): âm
            int layer = ai.Body != null ? ai.Body.sortingLayerID : 0, order = ai.Body != null ? ai.Body.sortingOrder + 2 : 3;
            var puffs = new List<(SpriteRenderer sr, Vector2 from, Vector2 vel, float born)>();
            float k0 = 0.22f / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (t < 0.9f && puffs.Count < 18 && Random.value < 0.5f)
                {
                    Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.05f, 0.3f));
                    var sr = EtherVfx.Spawn("RamSteam", EtherVfx.Orb, p, new Color(0.92f, 0.96f, 1f, 0.7f), layer, order);
                    puffs.Add((sr, p, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.35f, 0.6f)), Time.time));
                }
                foreach (var q in puffs)
                {
                    if (q.sr == null) continue;
                    float age = Time.time - q.born, u = Mathf.Clamp01(age / 0.8f);
                    q.sr.transform.position = q.from + q.vel * age;
                    float k = k0 * (1f + u * 1.6f);
                    q.sr.transform.localScale = new Vector3(k, k, 1f);
                    q.sr.color = new Color(0.92f, 0.96f, 1f, 0.7f * (1f - u));
                }
                yield return null;
            }
            foreach (var q in puffs) if (q.sr != null) Destroy(q.sr.gameObject);
        }

        /// <summary>Giọt nước nhỏ rơi khỏi thân khi đang ướt sũng (dấu hiệu "mất da Ether").</summary>
        private void Drip()
        {
            if (Time.time < nextDrip || ai == null || ai.Body == null) return;
            nextDrip = Time.time + 0.12f;
            Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 0.45f));
            var sr = EtherVfx.Spawn("RamDrip", EtherVfx.Orb, p, new Color(0.6f, 0.85f, 1f, 0.85f), ai.Body.sortingLayerID, ai.Body.sortingOrder + 1);
            float k = 0.05f / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            sr.transform.localScale = new Vector3(k, k * 1.4f, 1f);
            StartCoroutine(Fall(sr, p));
        }

        private static IEnumerator Fall(SpriteRenderer sr, Vector2 from)
        {
            for (float t = 0f; t < 0.4f && sr != null; t += Time.deltaTime)
            {
                sr.transform.position = from + Vector2.down * (0.9f * t);
                sr.color = new Color(0.6f, 0.85f, 1f, 0.85f * (1f - t / 0.4f));
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        // ------------------------------------------------------------------ mỗi khung: da Ether, vệt độc, quá tải
        private void Update()
        {
            if (engaged && ai != null && ai.Health != null && !ai.Health.IsDead)
            {
                ai.Health.DamageTakenMul = stunnedByWater ? quenchDamageMul : Soaked ? 1f : armorDamageMul;
                if (Soaked) Drip();
            }
            UpdateAura();
            if (trails.Count == 0) return;
            float now = Time.time;
            Vector2 pp = ai != null && ai.Player != null ? (Vector2)ai.Player.position : new Vector2(9999f, 9999f);
            bool inTrail = false;
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                var t = trails[i];
                float age = now - t.born;
                bool washed = SluiceMechanism.Wet(t.p);                       // nước xả rửa trôi vệt độc
                if (age >= trailLife || washed || t.sr == null) { if (t.sr != null) Destroy(t.sr.gameObject); trails.RemoveAt(i); continue; }
                float a = age < 0.15f ? age / 0.15f : age > trailLife - 1f ? (trailLife - age) : 1f;
                t.sr.color = new Color(0.55f, 1f, 0.35f, 0.55f * a);
                Vector2 d = pp - t.p;
                if (new Vector2(d.x, d.y / 0.6f).magnitude < trailRadius) inTrail = true;
            }
            if (inTrail && now >= nextTrailHurt && ai != null && !ai.Health.IsDead)
            {
                nextTrailHurt = now + 0.5f;
                ai.Hurt(ai.damage * 0.22f, pp, 0f);
            }
        }

        /// <summary>Ram đi bộ qua vệt độc của chính nó -> hút lại; đủ số đốm thì quá tải.</summary>
        private void Absorb(Vector2 pos)
        {
            if (Overcharged) return;
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                if ((trails[i].p - pos).sqrMagnitude > 0.3f * 0.3f) continue;
                if (trails[i].sr != null) Destroy(trails[i].sr.gameObject);
                trails.RemoveAt(i);
                absorbed++;
            }
            if (absorbed < overchargeAbsorb) return;
            absorbed = 0;
            overchargedUntil = Time.time + overchargeTime;
            if (!overHinted) { overHinted = true; Hints.Show("It drinks its own ichor and surges! Wash the trails away with the sluices.", 3.5f); }
            StartCoroutine(ai.Flash(0.35f, new Color(0.6f, 1f, 0.4f)));
        }

        private void UpdateAura()
        {
            bool on = ai != null && !ai.Health.IsDead && Overcharged;
            if (!on) { if (aura != null) aura.enabled = false; return; }
            if (aura == null)
            {
                aura = EtherVfx.Spawn("RamOvercharge", EtherVfx.Orb, transform.position, new Color(0.55f, 1f, 0.35f, 0.4f),
                                      ai.Body != null ? ai.Body.sortingLayerID : 0, ai.Body != null ? ai.Body.sortingOrder - 1 : 0);
            }
            aura.enabled = true;
            aura.transform.position = transform.position;
            float k = (0.9f + 0.12f * Mathf.Sin(Time.time * 9f)) / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            aura.transform.localScale = new Vector3(k, k * 0.55f, 1f);
            aura.color = new Color(0.55f, 1f, 0.35f, 0.3f + 0.15f * Mathf.Sin(Time.time * 9f));
        }

        private void DropTrail(Vector2 p)
        {
            var sr = EtherVfx.Spawn("RamTrail", EtherVfx.Orb, p, new Color(0.55f, 1f, 0.35f, 0f),
                                    ai.Body != null ? ai.Body.sortingLayerID : 0, ai.Body != null ? ai.Body.sortingOrder - 3 : 0);
            float k = trailRadius * 2.4f / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            sr.transform.localScale = new Vector3(k, k * 0.6f, 1f);
            trails.Add(new Trail { p = p, born = Time.time, sr = sr });
        }

        private void ClearTrails()
        {
            foreach (var t in trails) if (t.sr != null) Destroy(t.sr.gameObject);
            trails.Clear();
        }

        // ------------------------------------------------------------------ va chạm với Ren
        /// <summary>Bật / tắt va chạm vật lý giữa Ram và Ren (lúc lao: xuyên qua sau khi gây sát thương -> không ép Ren vào tường).</summary>
        private void IgnorePlayer(bool on)
        {
            if (ignoringPlayer == on || ai == null || ai.Player == null) return;
            ignoringPlayer = on;
            var mine = GetComponentsInChildren<Collider2D>();
            foreach (var pc in ai.Player.GetComponentsInChildren<Collider2D>())
                foreach (var c in mine)
                    if (pc != null && c != null) Physics2D.IgnoreCollision(c, pc, on);
        }

        private void LateUpdate()
        {
            if (chargeLine != null && (ai == null || ai.Health == null || ai.Health.IsDead)) DropLine();
        }

        private void OnDisable()
        {
            DropLine();
            IgnorePlayer(false);
            SluiceMechanism.Alert = false;
            if (ai != null && ai.Health != null) ai.Health.DamageTakenMul = 1f;
            if (aura != null) aura.enabled = false;
            ClearTrails();
        }

        private void OnDestroy()
        {
            ClearTrails();
            if (aura != null) Destroy(aura.gameObject);
        }

        /// Đá trần rơi: `aimOne` = một tảng nhắm chỗ Ren đang đứng (có vòng báo trước), còn lại rải quanh chỗ đâm.
        private void Rubble(Vector2 impact, int n, bool aimOne)
        {
            if (rubbleSprites == null || rubbleSprites.Length == 0 || n <= 0) return;
            int order = ai.Body != null ? ai.Body.sortingOrder : 2;
            Vector2 p0 = ai.Player != null ? (Vector2)ai.Player.position : impact;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = i == 0 && aimOne ? p0 : impact + Random.insideUnitCircle * 1.4f;
                var g = GroundStrike.Spawn(p, 0.9f + 0.12f * i, 0.34f, ai.damage * 0.8f, null, 0, 14f, order,
                                           new Color(0.85f, 0.75f, 0.55f));
                g.shell = rubbleSprites[Random.Range(0, rubbleSprites.Length)];
                g.shellFrom = p + Vector2.up * 3.2f;
                g.shellArc = 0f;
                g.leaveSprite = g.shell;
                g.leaveSeconds = 2.5f;
            }
        }

        // ------------------------------------------------------------------ vạch cảnh báo
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
            var go = new GameObject("RamChargeLine");
            var s = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(s);
            s.sprite = pixel;
            s.sortingOrder = ai.Body != null ? ai.Body.sortingOrder - 1 : 1;
            return s;
        }

        private void PlaceLine(SpriteRenderer line, Vector2 dir, float length)
        {
            if (line == null) return;
            line.transform.position = transform.position;
            line.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(length, 0.12f, 1f);
            line.color = Overcharged ? new Color(0.55f, 1f, 0.35f, Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? 0.6f : 0.25f)
                                     : new Color(1f, 0.25f, 0.2f, Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? 0.55f : 0.25f);
        }

        private void DropLine()
        {
            if (chargeLine != null) Destroy(chargeLine.gameObject);
            chargeLine = null;
        }
    }
}
