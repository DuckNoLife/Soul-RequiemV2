using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Khung chung cho boss Map 3 (Morrow, Isolde). Giải quyết lỗi cũ "boss bắn xa cứ lùi thẳng -> kẹt góc -> bị đánh dễ":
    ///   1. ĐẤU TRƯỜNG: boss nhớ tâm + bán kính elip đấu trường (lấy ở chỗ sinh). Mọi điểm đến đều kẹp trong elip.
    ///   2. KHÔNG LÙI THẲNG: khi chờ chiêu, boss đi vòng (strafe) quanh người chơi ở tầm ưa thích, hướng lệch dần về tâm.
    ///   3. CHỐNG KẸT / CHỐNG ĐÈ: sát mép elip quá lâu, đứng kẹt không nhích, hoặc bị người chơi áp sát liên tục
    ///      -> chiêu tái định vị riêng của boss (Morrow: Vespers Step; Isolde: Mist Step) tới một điểm xa người chơi.
    ///   4. CẬN CHIẾN: người chơi trong tầm gần -> đòn cận chiến riêng (Bell Sweep / Blood Lash) thay vì lùi.
    ///   5. NHỊP: luôn có chiêu sẵn; không chiêu nào sẵn thì strafe tối đa 0,7 s rồi chọn lại -> không bao giờ đứng ì.
    ///   6. BREAK: khi người chơi giải đúng cơ chế môi trường, boss quỳ (Stagger) và nhận x2 sát thương vài giây.
    /// Lớp con viết Think() (chọn chiêu) và Reposition() (cách thoát góc).
    /// </summary>
    public abstract class BossSkillsBase : MonoBehaviour, IEnemySkills
    {
        [Header("Đấu trường (đơn vị Unity, tính từ chỗ sinh)")]
        public Vector2 arenaRadii = new Vector2(2.9f, 1.9f);
        public Vector2 arenaOffset = Vector2.zero;
        [Header("Tầm")]
        public float meleeRange = 0.55f;          // khoảng hở thân-thân để ra đòn cận chiến
        public float preferredRange = 1.7f;       // tầm ưa thích khi strafe
        [Header("Chống kẹt")]
        public float edgeLimit = 0.86f, edgeTimeMax = 1.2f, pinnedTimeMax = 2.2f, stuckTimeMax = 0.7f, repositionCooldown = 3f;

        protected EnemyAI ai;
        protected Health hp;
        protected Vector2 center;
        protected bool engaged;
        protected bool busy;                       // đang ra chiêu (không tính kẹt)
        protected int phase = 1;
        protected float breakUntil;
        protected readonly Dictionary<string, float> ready = new Dictionary<string, float>();

        private float edgeT, pinT, stuckT, nextReposition;
        private float nextGlobal;                  // nhịp nghỉ chung giữa hai chiêu (không tính cận chiến)
        private bool usedSkill;
        private Vector2 lastPos;
        private int strafeSign = 1;
        private float strafeFlipAt;

        protected int Layer => ai != null ? ai.SortingLayer : 0;
        protected int Order => ai != null ? ai.SortingOrder : 2;
        protected Vector2 Pos => transform.position;
        protected Vector2 PlayerPos => ai != null && ai.Player != null ? (Vector2)ai.Player.position : Pos;
        protected bool Broken => Time.time < breakUntil;

        // ------------------------------------------------------------------ vòng đời
        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (hp == null) hp = ai.Health;
            if (!engaged)
            {
                engaged = true;
                center = (Vector2)transform.position + arenaOffset;
                lastPos = Pos;
                OnEngage();
            }
            if (ai.Player == null) yield break;
            int newPhase = PhaseFor(hp.Percent);
            if (newPhase > phase) { phase = newPhase; busy = true; yield return OnPhase(phase); busy = false; yield break; }
            if (!Broken && NeedsReposition())
            {
                nextReposition = Time.time + repositionCooldown;
                busy = true; yield return Reposition(); busy = false;
                nextGlobal = Time.time + Gcd();
                ResetTrackers();
                yield break;
            }
            busy = true;
            usedSkill = false;
            yield return Think();
            busy = false;
            if (usedSkill) nextGlobal = Time.time + Gcd();       // sau mỗi chiêu: boss chỉ đi vòng / cận chiến một nhịp -> cửa sổ phản công
        }

        protected abstract IEnumerator Think();
        protected abstract IEnumerator Reposition();
        protected virtual void OnEngage() { }
        protected virtual int PhaseFor(float hpPercent) => 1;
        protected virtual IEnumerator OnPhase(int p) { yield break; }

        protected virtual void Update()
        {
            if (ai == null || !engaged || hp == null || hp.IsDead) return;
            float dt = Time.deltaTime;
            // sát mép đấu trường
            edgeT = EllipseR(Pos) > edgeLimit ? edgeT + dt : Mathf.Max(0f, edgeT - dt * 2f);
            // bị áp sát liên tục (người chơi "đè" boss)
            pinT = ai.GapToPlayer() < meleeRange * 0.8f ? pinT + dt : Mathf.Max(0f, pinT - dt * 1.5f);
            // muốn đi mà không nhích (kẹt vật cản)
            float moved = (Pos - lastPos).magnitude;
            lastPos = Pos;
            bool wants = ai.Desired.sqrMagnitude > 0.04f;
            stuckT = wants && moved < ai.Desired.magnitude * dt * 0.25f ? stuckT + dt : 0f;
        }

        private bool NeedsReposition() =>
            Time.time >= nextReposition && (edgeT > edgeTimeMax || pinT > pinnedTimeMax || stuckT > stuckTimeMax);

        protected void ResetTrackers() { edgeT = pinT = stuckT = 0f; lastPos = Pos; }

        // ------------------------------------------------------------------ đấu trường
        /// 0 = tâm, 1 = mép elip.
        protected float EllipseR(Vector2 p)
        {
            Vector2 d = p - center;
            return Mathf.Sqrt((d.x * d.x) / (arenaRadii.x * arenaRadii.x) + (d.y * d.y) / (arenaRadii.y * arenaRadii.y));
        }

        protected Vector2 ClampArena(Vector2 p, float k = 0.8f)
        {
            float r = EllipseR(p);
            return r <= k || r < 0.0001f ? p : center + (p - center) * (k / r);
        }

        /// Điểm trong đấu trường xa người chơi nhất trong n ứng viên (thoát góc, dịch chuyển).
        protected Vector2 FarPoint(float k = 0.65f, int n = 12, IList<Vector2> extra = null)
        {
            Vector2 best = center; float bestD = -1f;
            var cands = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                cands.Add(center + new Vector2(Mathf.Cos(a) * arenaRadii.x, Mathf.Sin(a) * arenaRadii.y) * k);
            }
            cands.Add(center);
            if (extra != null) cands.AddRange(extra);
            foreach (var c in cands)
            {
                float d = (c - PlayerPos).magnitude;
                if (d > bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// Đi vòng quanh người chơi ở tầm ưa thích (không lùi thẳng), kéo dần về tâm khi ra xa.
        protected IEnumerator Stalk(float seconds, float speedMul = 1f)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !hp.IsDead && ai.Player != null)
            {
                if (Time.time > strafeFlipAt) { strafeSign = Random.value < 0.5f ? -1 : 1; strafeFlipAt = Time.time + Random.Range(1.2f, 2.4f); }
                Vector2 toP = PlayerPos - Pos;
                float d = toP.magnitude;
                Vector2 tangent = Vector2.Perpendicular(toP.normalized) * strafeSign;
                Vector2 radial = d > preferredRange * 1.15f ? toP.normalized : d < preferredRange * 0.7f ? tangent * 0.3f : Vector2.zero;
                Vector2 toCenter = (center - Pos) * Mathf.Clamp01(EllipseR(Pos) - 0.5f) * 1.6f;
                Vector2 v = (tangent * 0.8f + radial + toCenter);
                Vector2 next = ClampArena(Pos + v.normalized * 0.4f, 0.82f);
                ai.Desired = (next - Pos).normalized * ai.moveSpeed * speedMul;
                ai.FaceX(toP.x);
                ai.PlayState("Move", false);
                yield return null;
            }
            ai.Desired = Vector2.zero;
        }

        // ------------------------------------------------------------------ chiêu: tiện ích
        protected bool Ready(string key) => !ready.TryGetValue(key, out float t) || Time.time >= t;
        /// Ready + đã qua nhịp nghỉ chung (dùng cho mọi chiêu trừ cận chiến).
        protected bool ReadyG(string key) => Ready(key) && Time.time >= nextGlobal;
        protected void Use(string key, float cd)
        {
            ready[key] = Time.time + cd * CooldownMul();
            if (cd > 0.5f) usedSkill = true;
        }
        protected virtual float CooldownMul() => 1f;
        /// Nhịp nghỉ sau mỗi chiêu (giây): pha 1 dài, pha 3 ngắn.
        protected virtual float Gcd() => phase == 1 ? 1.7f : phase == 2 ? 1.35f : 1.05f;

        /// Trạng thái của người chơi (làm chậm, phong ấn chiêu, chặn hồi máu, choáng).
        protected PlayerStatus PS => ai != null && ai.Player != null ? PlayerStatus.Of(ai.Player) : null;

        protected void Hint(string text, float s = 2.5f)
        {
            Hints.Show(text, s);
        }

        protected static void Kick(Vector2 dir, float amp, float dur) => CameraFollow.KickMain(dir, amp, dur);

        /// Break: quỳ, nhận x2 sát thương trong `seconds`. Gọi khi người chơi giải đúng cơ chế đấu trường.
        protected IEnumerator BreakRoutine(float seconds, string text)
        {
            breakUntil = Time.time + seconds;
            ai.Desired = Vector2.zero;
            Hint(text, 2.5f);
            SpriteVfx.Spawn("m3_break", Pos + Vector2.up * 0.5f, 0.7f, Layer, Order + 3, false, 14f);
            Kick(Vector2.down, 0.05f, 0.25f);
            hp.DamageTakenMul = 2f;
            ai.PlayState(ai.HasAnimState("Stagger") ? "Stagger" : "Idle");
            yield return ai.Flash(seconds, new Color(1f, 1f, 0.6f));
            hp.DamageTakenMul = 1f;
            ai.PlayState("Idle");
        }

        /// Mờ dần thân boss tới alpha `target` (tan biến / hiện lại).
        protected IEnumerator Fade(float target, float seconds)
        {
            var sr = ai.Body;
            if (sr == null) yield break;
            float a0 = sr.color.a, t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var c = sr.color; c.a = Mathf.Lerp(a0, target, t / Mathf.Max(0.0001f, seconds)); sr.color = c;
                yield return null;
            }
            var cc = sr.color; cc.a = target; sr.color = cc;
        }

        /// Ẩn hẳn boss (không va chạm, không nhận đòn) — dùng khi trốn vào quan tài / hoá dơi.
        protected void SetHidden(bool hidden)
        {
            hp.Invulnerable = hidden;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = !hidden;
            if (ai.Body != null) { var c = ai.Body.color; c.a = hidden ? 0f : 1f; ai.Body.color = c; }
        }

        /// Nhịp "khoá trắng" ngắn trước đòn lớn: người chơi đọc được đúng lúc phải né.
        protected IEnumerator Telegraph(float seconds, Color c) => ai.Flash(seconds, c);

        // ------------------------------------------------------------------ VFX PixelLab dùng chung
        /// Sóng vòng có vỏ PixelLab (mảnh sóng m3_wave_hymn / m3_wave_blood xếp quanh vòng).
        protected Shockwave Wave(Vector2 at, float r, float dur, float dmg, string crestName, float alpha = 1f)
        {
            var w = Shockwave.Spawn(at, r, dur, dmg, Order - 1, new Color(1f, 1f, 1f, alpha));
            var f = SpriteVfx.Load(crestName);
            if (f.Length > 0) w.crest = f;
            return w;
        }

        /// Hiệu ứng dạng tia (ảnh PixelLab vẽ nằm ngang, gốc bên trái) kéo từ a tới b. nativeLen = chiều dài ảnh (đơn vị).
        protected SpriteVfx Ray(string name, Vector2 a, Vector2 b, float nativeLen, float thick, int order, float fps, float loopSeconds = 0f)
        {
            var v = SpriteVfx.Spawn(name, a, 1f, Layer, order, false, fps);
            if (v == null) return null;
            if (loopSeconds > 0f) v.Looping(loopSeconds);
            PlaceRay(v, a, b, nativeLen, thick);
            return v;
        }

        protected static void PlaceRay(SpriteVfx v, Vector2 a, Vector2 b, float nativeLen, float thick)
        {
            if (v == null) return;
            Vector2 d = b - a;
            v.transform.position = a;
            v.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            v.transform.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude / nativeLen), thick, 1f);
        }

        /// Một hạt (giọt máu...) bay vòng nhẹ từ `from` tới vật `to` rồi tan — dòng máu bị hút.
        protected IEnumerator FlyMote(string name, Vector2 from, Transform to, Vector2 toOffset, float seconds, float scale)
        {
            var v = SpriteVfx.Spawn(name, from, scale, Layer, Order + 1, false, 12f);
            if (v == null) yield break;
            v.Looping(seconds + 0.3f);
            float t = 0f;
            while (t < seconds && v != null && to != null)
            {
                t += Time.deltaTime;
                float k = t / seconds;
                v.transform.position = Vector2.Lerp(from, (Vector2)to.position + toOffset, k * k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 0.2f;
                yield return null;
            }
            if (v != null) v.Stop();
        }

        /// Khoảng cách từ p tới đoạn ab.
        protected static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        protected static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        protected virtual void OnDisable()
        {
            if (hp != null) { hp.DamageTakenMul = 1f; hp.Invulnerable = false; }
        }
    }
}
