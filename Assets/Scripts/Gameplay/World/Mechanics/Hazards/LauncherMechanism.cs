using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// launcher - WALL-MOUNTED SHOOTER (place the object area exactly on the wall-face tiles; projectiles leave from the area edge along `dir`).
    ///   kind   arrow (arrow slit: fast, light) | fire (statue head spitting fireballs: slow, burning) | fire_hole (wall hole spitting fire)
    ///          | ice (ice pipe: slows Ren by `chill` (0.55) for `chill_seconds` (2))
    ///   dir    down (default) | up | left | right      mode cycle (default: `period`, `offset`) | trigger (`trigger` = signal)
    ///   warn_seconds (warning image), speed (tiles / second), damage, range (tiles), burst + burst_gap (several shots per round)
    ///   disable_on + disable_seconds, start_when, stop_when (base class). Projectiles pass through enemies; blocked by flipped tables, pillars, walls.
    ///   `art` = suffix of the slit sprite set (e.g. mine -> trap_arrow_prep_mine: brown rock slit for the Map 1b mine walls); missing images -> the original set.
    ///   Projectiles ignore the wall block holding the muzzle (otherwise a freshly spawned arrow touched the wall collider and stuck at once, as if the trap were broken).
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
        private bool mounted;                 // image rotated against the wall
        private Vector2 wallBack;             // wall edge (back of the image)
        private float artDepth;               // image depth along the firing direction

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
            warnFrames = Skin(k.warn);
            fireFrames = Skin(k.fire);
            fly = TrapArt.Frames(k.fly);
            end = TrapArt.Frames(k.end);
            var idle = warnFrames.Length > 0 ? warnFrames[0] : fireFrames.Length > 0 ? fireFrames[0] : null;
            if (idle != null)
            {
                sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Launcher " + Name, new Vector3(Rect.center.x, Rect.yMin, 0f), Ctx.Player, 0);
                sr.sprite = idle;
                sr.flipX = Props.GetBool("flip", false);
                MountOnWall(idle);
            }
            trigger = new TrapListener(Str("trigger", ""));
            disabler = new TrapListener(Str("disable_on", ""));
        }

        /// <summary>Shooters on WEST / EAST / SOUTH walls (dir right / left / up): rotate the image so the muzzle faces the firing direction
        /// (the original art points down), the image BACK sits against the area edge opposite the firing direction (= the wall face), and the muzzle moves with the image.
        /// North walls (dir down) stay as they are. Turn off with rotate_art = false.</summary>
        private void MountOnWall(Sprite s)
        {
            Vector2 d = Dir;
            if (sr == null || d == Vector2.down || !Props.GetBool("rotate_art", true)) return;
            artDepth = s.bounds.size.y;
            float rot = d.x > 0f ? 90f : d.x < 0f ? -90f : 180f;
            wallBack = new Vector2(d.x > 0f ? Rect.xMin : d.x < 0f ? Rect.xMax : Rect.center.x,
                                   d.y > 0f ? Rect.yMin : Rect.center.y);
            sr.transform.SetPositionAndRotation(wallBack + d * artDepth, Quaternion.Euler(0f, 0f, rot));
            sr.flipX = false;
            mounted = true;
        }

        /// <summary>Slit sprite set by `art` (suffix), otherwise the original.</summary>
        private Sprite[] Skin(string name)
        {
            string art = Str("art", "");
            if (name.Length > 0 && art.Length > 0)
            {
                var f = TrapArt.Frames(name + "_" + art);
                if (f.Length > 0) return f;
            }
            return TrapArt.Frames(name);
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
            string kindS = Str("kind", "arrow"); // sound
            SoulRequiem.Core.Sfx.Play(kindS.StartsWith("fire") ? "trap_fire" : kindS == "ice" ? "trap_ice" : "trap_arrow", Rect.center);
            Vector2 d = Dir;
            var r = Rect;
            Vector2 muzzle = new Vector2(
                d.x > 0 ? r.xMax : d.x < 0 ? r.xMin : r.center.x,
                d.y > 0 ? r.yMax : d.y < 0 ? r.yMin : r.center.y) + d * 0.06f;
            if (mounted) muzzle = wallBack + d * (artDepth * 0.6f + 0.06f);   // muzzle follows the rotated image
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
            p.hitsEnemies = false;                                          // trap arrows / fireballs do NOT hit enemies (they fly through)
            p.keepRotationOnEnd = k.spin;                                   // vertically fired arrows stick in vertically
            // ignore the wall / rock face holding the muzzle (objects without Health around the muzzle at spawn time)
            var wall = Physics2D.OverlapCircleAll(muzzle, 0.2f);
            var skip = new System.Collections.Generic.List<Collider2D>();
            foreach (var c in wall) if (c != null && !c.isTrigger && c.GetComponentInParent<Health>() == null) skip.Add(c);
            if (skip.Count > 0) p.Ignore(skip);
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
            if (k.warn.Length > 0)                                       // arrow shooter: prepare (warn) -> release
            {
                float warn = Warn;
                if (e < warn && warnFrames.Length > 0) sr.sprite = warnFrames[Mathf.Clamp((int)(e / warn * warnFrames.Length), 0, warnFrames.Length - 1)];
                else if (e < warn + 0.3f && fireFrames.Length > 0) sr.sprite = fireFrames[Mathf.Clamp((int)((e - warn) / 0.3f * fireFrames.Length), 0, fireFrames.Length - 1)];
                else if (warnFrames.Length > 0) sr.sprite = warnFrames[0];
                return;
            }
            if (fireFrames.Length == 0) return;                          // statue / wall hole: one strip, frame `fireFrame` = the moment it fires
            float pre = Warn, perFrame = pre / Mathf.Max(1, k.fireFrame);
            int f = e < pre ? (int)(e / perFrame) : k.fireFrame + (int)((e - pre) / 0.07f);
            if (f >= fireFrames.Length || e < 0f) f = 0;
            if (f != shownFireFrame) { shownFireFrame = f; sr.sprite = fireFrames[f]; }
        }

        internal override void DebugSolve() { }
    }
}
