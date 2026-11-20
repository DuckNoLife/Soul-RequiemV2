using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§190): Grave Worm — sâu mộ ĐÀO HẦM PHỤC KÍCH. Bộ hình Crypt không có hoạt ảnh bò (trước đây mượn Idle làm Move -> đuổi theo mà
    /// trượt như tượng; gặp hố nứt ở Sinking Fields thì đứng im hẳn vì không có đường đi tới Ren bên kia hố).
    ///   - Ren trong tầm cắn: chui nhanh rồi bật lên cắn tại chỗ (Attack = appear-atk, như cũ).
    ///   - Ren ngoài tầm: chui xuống đất (Retreat, khung 0-11) -> đi ngầm, vệt bụi đất nổi từng nhịp theo đường đi (thấy được, đi dưới
    ///     hố thì không nổi bụi) -> tới DƯỚI CHÂN Ren (chỗ đứng được gần nhất) -> đất rung `emergeWarn` giây (khung 0-15 của Attack,
    ///     chưa đánh trúng được) -> trồi lên cắn. Lăn / bước ra khỏi chỗ đất rung là né.
    ///   - Cắn xong đứng trên mặt đất `cooldown` giây (cửa sổ đánh trả); đào lại sau `burrowCooldown` giây.
    ///   - Dưới đất: tắt va chạm + thân + thanh máu (không trúng đòn). Chỉ trồi trong ~90% `leashRange` quanh hang.
    /// Counter: nhìn vệt bụi + đất rung dưới chân thì lăn đi; đánh nó lúc vừa cắn xong; hoặc tránh xa hang.
    /// </summary>
    public class WormSkills : MonoBehaviour, IEnemySkills
    {
        public string burrowState = "Retreat";
        [Tooltip("Thời gian chui xuống (Retreat 20 fps: khung 0-11)")] public float burrowTime = 0.6f;
        [Tooltip("Tốc độ đi ngầm (đv/giây)")] public float tunnelSpeed = 1.5f;
        public float minTunnel = 0.45f, maxTunnel = 2.2f;
        [Tooltip("Đất rung trước khi trồi (Attack 22 fps: khung 0-15)")] public float emergeWarn = 0.73f;
        public float burrowCooldown = 2.5f;
        [Tooltip("Ren xa hơn khoảng này (đv) thì không đào theo")] public float maxChase = 4.5f;
        [Tooltip("Đòn trồi lên: trúng khi khoảng hở <= attackRange + giá trị này (Ren đã lăn / bước ra thì hụt)")] public float emergeReach = 0.05f;
        public string trailVfx = "m3_dash_dust";
        public float trailEvery = 0.22f, trailScale = 0.6f;

        private EnemyAI self;
        private EnemyHealthBar bar;
        private Vector2 home;
        private bool hasHome;
        private float nextBurrow;

        private void Awake()
        {
            self = GetComponent<EnemyAI>();
            var h = GetComponent<Health>();
            if (h != null) h.Died += _ => { if (self != null && self.Body != null) self.Body.enabled = true; };   // chết dưới đất (Burn) vẫn thấy xác
        }

        public IEnumerator Act(EnemyAI ai)
        {
            if (ai.Player == null) yield break;
            if (!hasHome) { home = transform.position; hasHome = true; nextBurrow = Time.time + 0.6f; }
            if (ai.GapToPlayer() <= ai.attackRange) { yield return Bite(ai, false); yield break; }

            Vector2 p = ai.Player.position;
            // 19/11 (§340): bị gọi (quái sót / triệu hồi) -> đào hầm theo Ren bất kể xa (mỗi lượt tối đa maxTunnel giây, lượt sau đi tiếp)
            bool reachable = ai.Summoned || ((p - home).magnitude <= Leash(ai) + 0.6f && (p - (Vector2)transform.position).magnitude <= maxChase);
            if (Time.time >= nextBurrow && reachable) { yield return Tunnel(ai); yield break; }

            ai.Desired = Vector2.zero;            // không bò: đứng chờ ở chỗ vừa trồi
            ai.PlayState("Idle", false);
            yield return ai.WaitFor(0.15f);
        }

        private float Leash(EnemyAI ai) => ai.leashRange > 0f ? ai.leashRange * 0.9f : 99f;

        // ------------------------------------------------------------------ cắn (Attack: đất rung -> trồi -> cắn)
        private IEnumerator Bite(EnemyAI ai, bool emerging)
        {
            SfxHooks.BossMove(this, "Bite"); // 19/11 (§335): âm
            ai.Desired = Vector2.zero;
            ai.FaceX(ai.DirToPlayer.x);
            ai.PlayState("Attack");
            if (ai.Vfx != null) ai.Vfx.Windup(ai.DirToPlayer);
            if (emerging)
            {
                if (ai.Body != null) ai.Body.enabled = true;           // thấy đất rung (khung đầu của Attack)
                yield return ai.WaitFor(emergeWarn);
                if (ai.Health.IsDead) yield break;
                SetUnder(ai, false);                                   // trồi lên: đánh được
                yield return ai.WaitFor(Mathf.Max(0f, ai.windup - emergeWarn));
            }
            else yield return ai.WaitFor(ai.windup);
            if (ai.Health.IsDead) yield break;
            if (ai.Vfx != null) ai.Vfx.Swing(ai.DirToPlayer);
            float reach = emerging ? ai.attackRange + emergeReach : ai.attackRange + 0.2f;
            if (ai.GapToPlayer() <= reach) ai.HitPlayerNow(ai.damage);
            yield return ai.WaitFor(Mathf.Max(0f, ai.attackTime - ai.windup));
            ai.PlayState("Idle");
            yield return ai.WaitFor(ai.cooldown);
        }

        // ------------------------------------------------------------------ đào hầm
        private IEnumerator Tunnel(EnemyAI ai)
        {
            SfxHooks.BossMove(this, "Tunnel"); // 19/11 (§335): âm
            nextBurrow = Time.time + burrowCooldown;
            ai.Desired = Vector2.zero;
            if (!ai.HasAnimState(burrowState)) { yield return Bite(ai, false); yield break; }   // prefab cũ chưa dựng lại
            ai.PlayState(burrowState);
            yield return ai.WaitFor(burrowTime);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            SetUnder(ai, true);
            Puff(ai, transform.position);

            Vector2 from = transform.position;
            Vector2 dest = Destination(ai);
            float time = Mathf.Clamp(Vector2.Distance(from, dest) / tunnelSpeed, minTunnel, maxTunnel);
            float t = 0f, nextPuff = trailEvery;
            while (t < time && !ai.Health.IsDead)
            {
                if (t < time * 0.6f && ai.Player != null) dest = Destination(ai);   // bám theo Ren tới 60% đường, sau đó chốt chỗ trồi
                Vector2 at = Vector2.Lerp(from, dest, t / time);
                ai.TeleportTo(at);
                if (t >= nextPuff) { nextPuff = t + trailEvery; if (Free(at)) Puff(ai, at); }
                t += Time.deltaTime;
                yield return null;
            }
            if (ai.Health.IsDead) yield break;
            ai.TeleportTo(dest);
            yield return Bite(ai, true);
        }

        /// Chỗ trồi: dưới chân Ren (giữ trong vùng hang), né vật cản / nước (hố) — thử vòng quanh nếu chân Ren vướng.
        private Vector2 Destination(EnemyAI ai)
        {
            Vector2 p = ai.Player.position;
            Vector2 fromHome = p - home;
            float leash = Leash(ai);
            if (fromHome.magnitude > leash) p = home + fromHome.normalized * leash;
            if (Free(p)) return p;
            for (int ring = 1; ring <= 4; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float a = (k * 45f + ring * 22.5f) * Mathf.Deg2Rad;
                    Vector2 c = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.16f * ring;
                    if (Free(c)) return c;
                }
            return transform.position;
        }

        private bool Free(Vector2 p)
        {
            foreach (var c in Physics2D.OverlapCircleAll(p, 0.14f))
                if (c != null && (self == null || c != self.BodyCollider) && LineOfSight.IsSolid(c, true)) return false;
            return true;
        }

        private void Puff(EnemyAI ai, Vector2 at)
        {
            if (!string.IsNullOrEmpty(trailVfx)) SpriteVfx.Spawn(trailVfx, at, trailScale, ai.SortingLayer, ai.SortingOrder - 1, false, 14f);
        }

        private void SetUnder(EnemyAI ai, bool under)
        {
            if (ai.BodyCollider != null) ai.BodyCollider.enabled = !under;
            if (ai.Body != null) ai.Body.enabled = !under;
            if (bar == null) bar = GetComponent<EnemyHealthBar>();
            if (bar != null) bar.enabled = !under;
        }

        private void OnDisable()
        {
            if (self != null && !self.Health.IsDead && self.BodyCollider != null) self.BodyCollider.enabled = true;
        }
    }
}
