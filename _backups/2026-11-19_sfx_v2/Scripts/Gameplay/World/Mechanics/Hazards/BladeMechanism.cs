using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§197): blade — LƯỠI CHẠY THEO RAY. Vùng object = đường chạy (cạnh dài = trục); lưỡi đi lại giữa hai đầu.
    ///   kind   saw (lưỡi cưa Crypt trong rãnh sàn, nhỏ) | blades (lưỡi xoay 4 cánh Old Prison trên ray sắt, to)
    ///   speed (ô / giây, mặc định saw 3, blades 2), pause (giây dừng ở mỗi đầu, 0,3), offset (lệch pha 0..1 quãng đường)
    ///   damage (người chơi, 12), enemy_damage (30), knock (3)
    ///   disable_on + disable_seconds (lưỡi đứng yên, không cắt), start_when, stop_when (lớp nền).
    /// </summary>
    public sealed class BladeMechanism : HazardMechanismBase
    {
        private Sprite[] spin;
        private SpriteRenderer sr;
        private TrapListener disabler;
        private float disabledUntil, nextHurt, travel;
        private readonly List<Health> found = new List<Health>();
        private readonly Dictionary<Health, float> enemyHitAt = new Dictionary<Health, float>();

        private bool Horizontal => Rect.width >= Rect.height;
        private bool Saw => Str("kind", "saw") != "blades";

        public override void Init()
        {
            spin = TrapArt.Frames(Saw ? (Horizontal ? "trap_saw" : "trap_saw_v") : "trap_blades");
            BuildTrack();
            if (spin.Length > 0)
            {
                sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Blade " + Name, Rect.center, Ctx.Player, 0);
                sr.sprite = spin[0];
            }
            disabler = new TrapListener(Str("disable_on", ""));
            travel = Mathf.Repeat(Float("offset", 0f), 1f) * 2f;
        }

        /// <summary>Vẽ rãnh (saw) hoặc ray sắt (blades) dọc trục.</summary>
        private void BuildTrack()
        {
            const float ts = 0.32f;
            int n = Mathf.Max(2, Mathf.RoundToInt((Horizontal ? Rect.width : Rect.height) / ts));
            Vector2 a = Horizontal ? new Vector2(Rect.xMin + ts / 2f, Rect.center.y) : new Vector2(Rect.center.x, Rect.yMax - ts / 2f);
            Vector2 step = Horizontal ? new Vector2(ts, 0f) : new Vector2(0f, -ts);
            if (Saw)
            {
                var trail = TrapArt.Frames(Horizontal ? "trap_saw_trail" : "trap_saw_trail_v");
                if (trail.Length == 0) return;
                // 18/11 (§321): rãnh nằm GỌN trong vùng bẫy. Trước đây bước mảnh tính theo bề NGANG ảnh cả với rãnh dọc
                // + mảnh cuối đặt quá đầu ray -> rãnh thò ra ngoài tường ~1,5 ô. Nay: chia đều số mảnh trên chiều dài vùng,
                // vùng ngắn hơn một mảnh thì co mảnh lại.
                float total = Horizontal ? Rect.width : Rect.height;
                const float pad = 0.06f;
                float use = Mathf.Max(0.05f, total - 2f * pad);
                float piece = (Horizontal ? trail[0].bounds.size.x : trail[0].bounds.size.y) * 0.8f;   // vạch rãnh ~80% khung ảnh
                float k = use < piece ? use / piece : 1f;
                float len = piece * k;
                int count = Mathf.Max(1, Mathf.CeilToInt(use / len - 0.05f));
                Vector2 start = Horizontal ? new Vector2(Rect.xMin + pad, Rect.center.y) : new Vector2(Rect.center.x, Rect.yMax - pad);
                Vector2 axis = Horizontal ? Vector2.right : Vector2.down;
                for (int i = 0; i < count; i++)
                {
                    float c = count == 1 ? use / 2f : Mathf.Lerp(len / 2f, use - len / 2f, i / (float)(count - 1));
                    var r = TrapArt.MakeRenderer(Ctx.Host.transform, "SawTrail " + Name, start + axis * c, Ctx.Player, -7);
                    r.sprite = trail[0];
                    if (k < 1f) r.transform.localScale = Horizontal ? new Vector3(k, 1f, 1f) : new Vector3(1f, k, 1f);
                }
                return;
            }
            for (int i = 0; i < n; i++)
            {
                string piece = Horizontal ? (i == 0 ? "trap_rail_r" : i == n - 1 ? "trap_rail_l" : "trap_rail_lr")
                                          : (i == 0 ? "trap_rail_b" : i == n - 1 ? "trap_rail_t" : "trap_rail_tb");
                var f = TrapArt.Frames(piece);
                if (f.Length == 0) continue;
                var r = TrapArt.MakeRenderer(Ctx.Host.transform, "Rail " + Name, a + step * i, Ctx.Player, -7);
                r.sprite = f[0];
            }
        }

        private Vector2 PosAt(float u)        // u 0..2: đi rồi về
        {
            float t = u <= 1f ? u : 2f - u;
            const float inset = 0.16f;
            Vector2 a = Horizontal ? new Vector2(Rect.xMin + inset, Rect.center.y) : new Vector2(Rect.center.x, Rect.yMax - inset);
            Vector2 b = Horizontal ? new Vector2(Rect.xMax - inset, Rect.center.y) : new Vector2(Rect.center.x, Rect.yMin + inset);
            return Vector2.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
        }

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (disabler.Poll()) disabledUntil = Time.time + Float("disable_seconds", 6f);
            if (sr == null || !Armed() || Time.time < disabledUntil) return;
            float len = Mathf.Max(0.3f, (Horizontal ? Rect.width : Rect.height) - 0.32f);
            float speed = Float("speed", Saw ? 3f : 2f) * 0.32f;
            float pause = Float("pause", 0.3f);
            // thời gian một chiều + dừng ở đầu: quy về tham số u
            float leg = len / Mathf.Max(0.05f, speed);
            travel = Mathf.Repeat(travel + Time.deltaTime / (leg + pause), 2f);
            float local = Mathf.Repeat(travel, 1f) * (leg + pause);
            float u01 = Mathf.Clamp01((local - pause) / leg);
            float u = travel < 1f ? u01 : 1f + u01;
            Vector2 pos = PosAt(u);
            sr.transform.position = pos;
            if (Saw) SoulRequiem.Core.Sfx.Loop(this, "trap_saw", sr.transform);   // 17/11 (§294): âm (thôi gọi 0.3 s là tắt)
            sr.sprite = spin[(int)(Time.time * (Saw ? 18f : 12f)) % spin.Length];

            float radius = Saw ? 0.2f : 0.38f;
            float now = Time.time;
            if (Vector2.Distance(playerPos, pos) < radius && now >= nextHurt)
            {
                nextHurt = now + 0.5f;
                TrapArt.HurtPlayerCrush(Ctx, Float("damage", 12f), playerPos - pos, Float("knock", 3f));
            }
            TrapArt.EnemiesIn(new Rect(pos - Vector2.one * radius, Vector2.one * radius * 2f), found);
            foreach (var h in found)
            {
                if (enemyHitAt.TryGetValue(h, out float t) && now < t) continue;
                enemyHitAt[h] = now + 0.6f;
                h.TakeDamage(Float("enemy_damage", 30f), (Vector2)h.transform.position - pos, Float("knock", 3f));
            }
        }

        internal override void DebugSolve() { }
    }
}
