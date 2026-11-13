using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§197): launcher — MÁY BẮN gắn tường (đặt vùng object đúng ô mặt tường; đạn bay ra từ mép vùng theo `dir`).
    ///   kind   arrow (lỗ bắn tên: nhanh, nhẹ) | fire (đầu tượng phun cầu lửa: chậm, cháy) | fire_hole (lỗ tường phun lửa)
    ///          | ice (ống băng: làm chậm Ren `chill` (0,55) trong `chill_seconds` (2))
    ///   dir    down (mặc định) | up | left | right      mode cycle (mặc định: `period`, `offset`) | trigger (`trigger` = tín hiệu)
    ///   warn_seconds (hình báo trước), speed (ô / giây), damage, range (ô), burst + burst_gap (bắn nhiều phát / lượt)
    ///   disable_on + disable_seconds, start_when, stop_when (lớp nền). Đạn trúng cả quái; chặn được bằng bàn lật, cột, tường.
    /// </summary>
    public sealed class LauncherMechanism : HazardMechanismBase
    {
        private struct Kind { public string warn, fire, fly, end; public int fireFrame; public float speed, damage; public bool spin; }

        private Kind k;
        private Sprite[] warnFrames, fireFrames, fly, end;
        private SpriteRenderer sr;
        private TrapListener trigger, disabler;
        private float disabledUntil, shotStart = -99f;
        private int shotsLeft, shownFireFrame = -1;
        private float nextBurst;
        private float lastCycle = -1f;

        private static Kind Of(string kind)
        {
            switch (kind)
            {
                case "fire": return new Kind { warn = "", fire = "trap_face_fire", fly = "trap_fireball", end = "trap_fireball_end", fireFrame = 4, speed = 3.5f, damage = 14f };
                case "fire_hole": return new Kind { warn = "", fire = "trap_hole_fire", fly = "trap_fireball", end = "trap_fireball_end", fireFrame = 3, speed = 4f, damage = 12f };
                case "ice": return new Kind { warn = "", fire = "trap_hole_ice", fly = "trap_ice", end = "trap_ice_end", fireFrame = 4, speed = 4f, damage = 8f };
                default: return new Kind { warn = "trap_arrow_prep", fire = "trap_arrow_launch", fly = "trap_arrow_fly", end = "trap_arrow_stick", fireFrame = 1, speed = 7f, damage = 9f, spin = true };
            }
        }

        private Vector2 Dir
        {
            get
            {
                switch (Str("dir", "down"))
                {
                    case "up": return Vector2.up;
                    case "left": return Vector2.left;
                    case "right": return Vector2.right;
                    default: return Vector2.down;
                }
            }
        }

        public override void Init()
        {
            k = Of(Str("kind", "arrow"));
            warnFrames = TrapArt.Frames(k.warn);
            fireFrames = TrapArt.Frames(k.fire);
            fly = TrapArt.Frames(k.fly);
            end = TrapArt.Frames(k.end);
            var idle = warnFrames.Length > 0 ? warnFrames[0] : fireFrames.Length > 0 ? fireFrames[0] : null;
            if (idle != null)
            {
                sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Launcher " + Name, new Vector3(Rect.center.x, Rect.yMin, 0f), Ctx.Player, 0);
                sr.sprite = idle;
                sr.flipX = Props.GetBool("flip", false);
            }
            trigger = new TrapListener(Str("trigger", ""));
            disabler = new TrapListener(Str("disable_on", ""));
        }

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (disabler.Poll()) disabledUntil = Time.time + Float("disable_seconds", 6f);
            bool fired = trigger.Poll();
            if (!Armed() || Time.time < disabledUntil) return;
            float now = Time.time;
            if (Str("mode", "cycle") == "trigger") { if (fired) Begin(now); }
            else
            {
                float period = Mathf.Max(0.8f, Float("period", 3f));
                float c = Mathf.Floor((now + Float("offset", 0f)) / period);
                if (c != lastCycle) { if (lastCycle >= 0f) Begin(now); lastCycle = c; }
            }
            if (shotsLeft > 0 && now >= nextBurst && now - shotStart >= Warn)
            {
                shotsLeft--;
                nextBurst = now + Float("burst_gap", 0.35f);
                Shoot();
            }
        }

        private float Warn => Float("warn_seconds", k.warn.Length > 0 ? 0.8f : 0.45f);

        private void Begin(float now)
        {
            if (shotsLeft > 0) return;
            shotStart = now;
            shotsLeft = Mathf.Max(1, Int("burst", 1));
            nextBurst = now;
        }

        private void Shoot()
        {
            if (fly.Length == 0) return;
            Vector2 d = Dir;
            var r = Rect;
            Vector2 muzzle = new Vector2(
                d.x > 0 ? r.xMax : d.x < 0 ? r.xMin : r.center.x,
                d.y > 0 ? r.yMax : d.y < 0 ? r.yMin : r.center.y) + d * 0.06f;
            var go = new GameObject("TrapShot " + Name);
            go.transform.position = muzzle;
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = fly[0];
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) { s.sortingLayerID = psr.sortingLayerID; s.sortingOrder = psr.sortingOrder + 1; }
            var p = go.AddComponent<Projectile>();
            p.flyFrames = fly;
            p.explodeFrames = end;
            p.fps = 14f;
            p.spinToDirection = k.spin;
            p.knockback = 1.2f;
            p.hitsEnemies = true;
            string kind = Str("kind", "arrow");
            if (kind == "fire" || kind == "fire_hole") { p.burnDps = Float("burn_dps", 4f); p.burnSeconds = Float("burn_seconds", 2.5f); }
            if (kind == "ice") { p.chillMul = Float("chill", 0.55f); p.chillSeconds = Float("chill_seconds", 2f); }
            float speed = Float("speed", k.speed) * 0.32f;
            p.lifetime = Float("range", 14f) * 0.32f / Mathf.Max(0.1f, speed);
            p.Launch(d, speed, Float("damage", k.damage));
        }

        public override void Animate()
        {
            if (sr == null) return;
            float e = Time.time - shotStart;
            if (k.warn.Length > 0)                                       // máy bắn tên: chuẩn bị (warn) -> bật lẫy
            {
                float warn = Warn;
                if (e < warn && warnFrames.Length > 0) sr.sprite = warnFrames[Mathf.Clamp((int)(e / warn * warnFrames.Length), 0, warnFrames.Length - 1)];
                else if (e < warn + 0.3f && fireFrames.Length > 0) sr.sprite = fireFrames[Mathf.Clamp((int)((e - warn) / 0.3f * fireFrames.Length), 0, fireFrames.Length - 1)];
                else if (warnFrames.Length > 0) sr.sprite = warnFrames[0];
                return;
            }
            if (fireFrames.Length == 0) return;                          // tượng / lỗ tường: một dải, khung `fireFrame` = lúc nhả đạn
            float pre = Warn, perFrame = pre / Mathf.Max(1, k.fireFrame);
            int f = e < pre ? (int)(e / perFrame) : k.fireFrame + (int)((e - pre) / 0.07f);
            if (f >= fireFrames.Length || e < 0f) f = 0;
            if (f != shownFireFrame) { shownFireFrame = f; sr.sprite = fireFrames[f]; }
        }

        internal override void DebugSolve() { }
    }
}
