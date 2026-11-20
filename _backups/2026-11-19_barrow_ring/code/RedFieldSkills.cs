using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 18/11 (§330): boss bí mật THE RED FIELD (Map 3b, mở lăng Borrow -> Crimson Oath). Hồn của trận năm 30 trước Angel Bird
    /// (Miravel – Arden, 600.000 người chết), bị Vael đánh thức thành một khối xác lính chồng lên nhau. Không đi lại: đứng giữa sân,
    /// đánh bằng cả đấu trường. Mọi chiêu có báo trước + cách né:
    ///   • THE CHARGE  — hàng lính hồn tràn ngang sân từ một mép, chừa MỘT khe (đốm sáng ấm báo trước). Đứng vào khe hoặc lăn xuyên qua.
    ///                   Pha 2: hai hàng hai phe (Miravel đỏ / Arden xám) từ hai mép, lệch nhịp. Pha 3: nhanh hơn.
    ///   • SPEAR FIELD — giáo trồi khỏi đất đuổi theo chỗ Ren đứng (vòng đỏ báo trước 0,9 s, GroundStrike). Đứng yên là trúng.
    ///   • SLAM        — Ren đứng sát khối xác quá lâu: khối xác chồm tới đè xuống (sóng vòng), đẩy Ren ra.
    ///   • THE OATH BREAKS (cửa sổ đánh) — sau mỗi đợt hành quân, khối xác tách ra để lộ lõi ấn đỏ (hoạt ảnh Open):
    ///                   nhận x2 sát thương vài giây. Ngoài cửa sổ chỉ nhận 45% (cả một đạo quân chồng lên nhau).
    /// Hoạt ảnh: Idle / Slam / Open (PixelLab object 45958e9d). Chết: lún xuống đất + tan mờ (dựng bằng code, không cần hoạt ảnh).
    /// </summary>
    public sealed class RedFieldSkills : BossSkillsBase
    {
        public const float FPS_SLAM = 11f, FPS_OPEN = 10f;
        [Header("Đấu trường (đơn vị Unity, tính từ chỗ sinh)")]
        // 18/11 (§331): phủ HẾT sàn (từ hàng sàn đầu dưới tường bắc tới sát tường nam) — trước đây hụt 2 ô dưới tường bắc = chỗ núp
        public float halfWidth = 4.6f, top = 3.3f, bottom = 3.0f;      // sân 30 x 20 ô; boss đứng hơi lệch bắc
        [Header("Sát thương")]
        public float rankDamage = 22f, spearDamage = 18f, slamDamage = 24f;
        public float armour = 0.45f, exposed = 2f;

        private int ranksSinceOpen;
        private Vector2 lastAim;
        private bool open, dying;

        private Color Red => new Color(1f, 0.25f, 0.25f, 1f);

        private void Start()
        {
            var h = GetComponent<Health>();
            if (h != null) { h.DamageTakenMul = armour; h.Died += OnDied; }
        }

        protected override void OnEngage()
        {
            arenaRadii = new Vector2(halfWidth, Mathf.Max(top, bottom));
            ai.moveSpeed = 0f;
            ai.Desired = Vector2.zero;
            hp.DamageTakenMul = armour;
            FirstTimeHints.Show("tut_red_field", "The dead march in ranks. Find the gap in the line - or roll through it.", 4.5f);
        }

        protected override int PhaseFor(float hpPercent) => hpPercent > 0.6f ? 1 : hpPercent > 0.3f ? 2 : 3;

        protected override IEnumerator OnPhase(int p)
        {
            ai.Desired = Vector2.zero;
            yield return Telegraph(0.6f, Red);
            Kick(Vector2.down, 0.06f, 0.4f);
            Hint(p == 2 ? "Arden answers. Both armies take the field." : "The oath is all that holds them together now.", 3f);
            yield return ai.WaitFor(0.5f);
        }

        protected override float Gcd() => phase == 1 ? 1.4f : phase == 2 ? 1.1f : 0.85f;

        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (dying) yield break;
            // áp sát khối xác -> đè
            if (ai.GapToPlayer() < 0.55f && Ready("slam")) { yield return Slam(); yield break; }
            // đủ hai đợt hành quân -> lộ lõi (cửa sổ đánh)
            if (ranksSinceOpen >= 2 && Ready("open")) { yield return OathBreaks(); yield break; }
            if (ReadyG("charge")) { yield return Charge(); yield break; }
            if (ReadyG("spears")) { yield return SpearField(); yield break; }
            ai.PlayState("Idle", false);
            yield return ai.WaitFor(0.25f);
        }

        /// Đứng yên một chỗ: không cần thoát góc. Bị áp sát lâu -> đè.
        protected override IEnumerator Reposition()
        {
            if (Ready("slam")) yield return Slam();
        }

        // ------------------------------------------------------------------ THE CHARGE
        private IEnumerator Charge()
        {
            Use("charge", phase == 1 ? 6.5f : phase == 2 ? 6f : 5f);
            ai.PlayState("Idle", false);
            yield return Telegraph(0.35f, Red);
            float yMin = center.y - bottom, yMax = center.y + top;
            float left = center.x - halfWidth - 0.1f, right = center.x + halfWidth + 0.1f;
            float travel = right - left;
            float speed = phase == 1 ? 1.75f : phase == 2 ? 1.95f : 2.3f;
            float warn = phase == 3 ? 0.9f : 1.1f;
            bool fromLeft = Random.value < 0.5f;
            // khe đầu: lệch khỏi hàng của Ren để Ren phải DI CHUYỂN (không đứng yên mà né được)
            float py = Mathf.Clamp(PlayerPos.y, yMin + 0.4f, yMax - 0.4f);
            float gap1 = PickGap(yMin, yMax, py);
            Spawn(fromLeft, gap1, yMin, yMax, left, right, travel, speed, warn, new Color(1f, 0.82f, 0.82f));
            if (phase >= 2)
            {
                yield return ai.WaitFor(phase == 2 ? 1.6f : 1.2f);
                if (hp.IsDead) yield break;
                float gap2 = PickGap(yMin, yMax, gap1);                  // phe kia, khe khác chỗ
                Spawn(!fromLeft, gap2, yMin, yMax, left, right, travel, speed, warn, new Color(0.72f, 0.8f, 0.95f));
            }
            ranksSinceOpen++;
            yield return ai.WaitFor(0.6f);
        }

        private static float PickGap(float yMin, float yMax, float avoid)
        {
            for (int i = 0; i < 12; i++)
            {
                float g = Random.Range(yMin + 0.35f, yMax - 0.35f);
                if (Mathf.Abs(g - avoid) > 0.9f) return g;
            }
            return avoid > (yMin + yMax) * 0.5f ? yMin + 0.4f : yMax - 0.4f;
        }

        private void Spawn(bool fromLeft, float gap, float yMin, float yMax, float left, float right, float travel, float speed, float warn, Color tint)
        {
            float x = fromLeft ? left : right;
            GhostRank.Spawn(x, yMin, yMax, gap, phase == 3 ? 0.3f : 0.36f, fromLeft ? Vector2.right : Vector2.left,
                            warn, speed, travel, rankDamage, 0.7f, Layer, Order, tint)
                     .Bound(yMin - 0.6f, yMax + 0.6f);                         // 18/11 (§331): phủ cả sân, không với ra hành lang nam
            SoulRequiem.Core.Sfx.Play("boss_slam", new Vector2(x, gap), 0.6f);     // tiếng trống trận (chưa có tiếng riêng)
        }

        // ------------------------------------------------------------------ SPEAR FIELD
        private IEnumerator SpearField()
        {
            Use("spears", phase == 1 ? 5f : 4f);
            ai.PlayState("Idle", false);
            yield return Telegraph(0.3f, Red);
            var frames = SpriteVfx.Load("rf_spear");
            int n = phase == 1 ? 5 : phase == 2 ? 6 : 8;
            float gap = phase == 3 ? 0.22f : 0.3f;
            for (int i = 0; i < n && !hp.IsDead; i++)
            {
                // đuổi theo Ren: điểm rơi = chỗ Ren đang đứng + lệch theo hướng đang chạy
                Vector2 now = PlayerPos;
                Vector2 vel = i == 0 ? Vector2.zero : (now - lastAim) / Mathf.Max(0.05f, gap);
                lastAim = now;
                Vector2 at = ClampField(now + Vector2.ClampMagnitude(vel, 1.2f) * 0.35f);
                Strike(at, frames);
                if (phase >= 2 && i % 2 == 1) Strike(ClampField(at + Random.insideUnitCircle.normalized * 0.7f), frames);
                yield return ai.WaitFor(gap);
            }
            yield return ai.WaitFor(0.4f);
        }

        private Vector2 ClampField(Vector2 p) =>
            new Vector2(Mathf.Clamp(p.x, center.x - halfWidth + 0.2f, center.x + halfWidth - 0.2f), Mathf.Clamp(p.y, center.y - bottom + 0.15f, center.y + top - 0.15f));

        private void Strike(Vector2 at, Sprite[] frames)
        {
            var g = GroundStrike.Spawn(at, 0.9f, 0.34f, spearDamage, frames, 1, 12f, Order, new Color(1f, 0.22f, 0.2f, 1f));
            g.knockback = 1.6f;
            g.bodyScale = 0.9f;
        }

        // ------------------------------------------------------------------ SLAM
        private IEnumerator Slam()
        {
            Use("slam", 4f);
            yield return Telegraph(0.5f, Red);
            if (hp.IsDead) yield break;
            ai.PlayState("Slam");
            yield return ai.WaitFor(3f / FPS_SLAM);                   // khung 3: đè xuống
            SoulRequiem.Core.Sfx.Play("boss_slam", Pos);
            var w = Wave(Pos + Vector2.down * 0.15f, 1.5f, 0.55f, slamDamage, "m3_wave_blood");
            w.knockback = 4f;
            Kick(Vector2.down, 0.07f, 0.3f);
            yield return ai.WaitFor(6f / FPS_SLAM);
            ai.PlayState("Idle", false);
        }

        // ------------------------------------------------------------------ THE OATH BREAKS (cửa sổ đánh)
        private IEnumerator OathBreaks()
        {
            Use("open", 2f);
            ranksSinceOpen = 0;
            open = true;
            ai.PlayState("Open");
            yield return ai.WaitFor(3f / FPS_OPEN);
            Hint("The dead part. Something burns at the heart of the mound - strike it!", 2.6f);
            hp.DamageTakenMul = exposed;
            float window = phase == 1 ? 3.6f : phase == 2 ? 3.1f : 2.6f;
            yield return ai.Flash(window, new Color(1f, 0.9f, 0.55f));
            hp.DamageTakenMul = armour;
            open = false;
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ chết: khối xác lún xuống, tan vào đất
        private void OnDied(Health h)
        {
            dying = true;
            var sr = ai != null ? ai.Body : GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;
            var go = new GameObject("Red Field Collapse");
            go.transform.position = sr.transform.position;
            go.transform.localScale = sr.transform.lossyScale;
            var copy = go.AddComponent<SpriteRenderer>();
            copy.sprite = sr.sprite; copy.sortingLayerID = sr.sortingLayerID; copy.sortingOrder = sr.sortingOrder;
            copy.spriteSortPoint = SpriteSortPoint.Pivot; copy.sharedMaterial = sr.sharedMaterial;
            sr.enabled = false;
            go.AddComponent<RedFieldCollapse>();
        }

        private void LateUpdate()
        {
            if (ai != null && ai.Body != null) ai.Body.flipX = false;      // khối xác không lật trái phải
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (hp != null && !open) hp.DamageTakenMul = armour;
        }
    }
}
