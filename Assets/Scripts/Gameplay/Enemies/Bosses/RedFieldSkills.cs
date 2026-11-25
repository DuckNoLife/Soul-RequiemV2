using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Secret boss THE RED FIELD (Map 3b, opening the Borrow mausoleum -> Crimson Oath). Ghost of the battle 30 years before the Angel Bird
    /// (Miravel - Arden, 600,000 dead), woken by Vael as one mass of stacked soldier corpses. Never walks: stands in the middle of the field
    /// and fights with the whole arena. Every skill has a warning + a way to dodge:
    ///   • THE CHARGE  - a rank of ghost soldiers sweeps across the field from one edge, leaving ONE gap (a warm light shows it first). Stand in the gap.
    ///                   Phase 2: two ranks of the two armies (Miravel red / Arden grey) from both edges, offset in time. Phase 3: faster.
    ///   • SPEAR FIELD - spears burst from the ground chasing where Ren stands (red ring 0.9 s warning, GroundStrike). Standing still gets you hit.
    ///   • SLAM        - Ren right next to the mass: it crashes down, a BLOOD POOL spreads from between its feet, blood tendrils rise - standing in the pool = controls reversed 5 s.
    ///   • THE OATH BREAKS (damage window) - after each march the mass splits open to show its red sigil core (Open animation):
    ///                   takes x2 damage for a few seconds. Outside the window it takes only 45% (a whole army stacked together).
    /// Animations: Idle / Slam / Open (PixelLab object 45958e9d). Death: sinks into the ground + fades (done in code, no animation needed).
    /// </summary>
    public sealed class RedFieldSkills : BossSkillsBase
    {
        public const float FPS_SLAM = 11f, FPS_OPEN = 10f;
        [Header("Arena (Unity units, from the spawn point)")]
        // covers the WHOLE floor (from the first floor row under the north wall to the south wall) - stopping 2 tiles short of the north wall left a hiding spot
        public float halfWidth = 4.6f, top = 3.3f, bottom = 3.0f;      // field 30 x 20 tiles; the boss stands slightly north
        [Header("Damage")]
        public float rankDamage = 22f, spearDamage = 18f;
        [Tooltip("The slam deals no damage - movement controls are reversed for this many seconds")]
        public float confuseSeconds = 5f;
        public float armour = 0.45f, exposed = 2f;

        private int ranksSinceOpen;
        private Vector2 lastAim;
        private bool open, dying;

        private Color Red => new Color(1f, 0.25f, 0.25f, 1f);
        private static readonly Color Ember = new Color(1f, 0.82f, 0.45f, 1f);      // core burning inside the mass (damage window)
        /// tall mass - the charge point is in the middle of the mass (not the feet).
        protected override Vector2 CastPoint => SnareCenter() + Vector2.up * 0.6f;

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
            FirstTimeHints.Show("tut_red_field", "The dead march in ranks. Rolling will not save you - find the gap in the line.", 4.5f);
        }

        protected override int PhaseFor(float hpPercent) => hpPercent > 0.6f ? 1 : hpPercent > 0.3f ? 2 : 3;

        protected override IEnumerator OnPhase(int p)
        {
            ai.Desired = Vector2.zero;
            Announce(p == 2 ? "Arden Answers" : "The Last Oath", Red, 0.6f);
            yield return Telegraph(0.6f, Red);
            Fired();
            Boom(CastPoint, Red, 1.8f, 3);
            BossJuice.Burst("m3x_war_quake", SnareCenter(), 2.2f, Color.white, Layer, Order - 1, 12f);
            Hint(p == 2 ? "Arden answers. Both armies take the field." : "The oath is all that holds them together now.", 3f);
            yield return ai.WaitFor(0.5f);
        }

        protected override float Gcd() => phase == 1 ? 1.4f : phase == 2 ? 1.1f : 0.85f;

        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (dying) yield break;
            // pressed against the mass -> slam
            if (ai.GapToPlayer() < 0.85f && Ready("slam")) { yield return Slam(); yield break; }     // pool is x1.5 -> slams from farther away
            // two marches done -> expose the core (damage window)
            if (ranksSinceOpen >= 2 && Ready("open")) { yield return OathBreaks(); yield break; }
            if (ReadyG("charge")) { yield return Charge(); yield break; }
            if (ReadyG("spears")) { yield return SpearField(); yield break; }
            ai.PlayState("Idle", false);
            yield return ai.WaitFor(0.25f);
        }

        /// Stands in one spot: no corner escape needed. Pressed for long -> slam.
        protected override IEnumerator Reposition()
        {
            if (Ready("slam")) yield return Slam();
        }

        // ------------------------------------------------------------------ THE CHARGE
        private IEnumerator Charge()
        {
            Use("charge", phase == 1 ? 6.5f : phase == 2 ? 6f : 5f);
            ai.PlayState("Idle", false);
            Announce("March of the Dead", Red, 0.35f);
            yield return Telegraph(0.35f, Red);
            Fired();
            float yMin = center.y - bottom, yMax = center.y + top;
            float left = center.x - halfWidth - 0.1f, right = center.x + halfWidth + 0.1f;
            float travel = right - left;
            float speed = phase == 1 ? 1.75f : phase == 2 ? 1.95f : 2.3f;
            float warn = phase == 3 ? 0.9f : 1.1f;
            bool fromLeft = Random.value < 0.5f;
            // first gap: offset from Ren's row so Ren has to MOVE (cannot dodge by standing still)
            float py = Mathf.Clamp(PlayerPos.y, yMin + 0.4f, yMax - 0.4f);
            float gap1 = PickGap(yMin, yMax, py);
            Spawn(fromLeft, gap1, yMin, yMax, left, right, travel, speed, warn, new Color(1f, 0.82f, 0.82f));
            if (phase >= 2)
            {
                yield return ai.WaitFor(phase == 2 ? 1.6f : 1.2f);
                if (hp.IsDead) yield break;
                float gap2 = PickGap(yMin, yMax, gap1);                  // the other army, gap in a different place
                Spawn(!fromLeft, gap2, yMin, yMax, left, right, travel, speed, warn, new Color(0.72f, 0.8f, 0.95f));
            }
            ranksSinceOpen++;
            yield return ai.WaitFor(0.6f);
        }

        /// <summary>Blood pool centre = between the mass's feet (from the frame being drawn), not the pivot at the bottom edge.</summary>
        private Vector2 SnareCenter()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return Pos;
            var b = sr.bounds;
            return new Vector2(b.center.x, b.min.y + b.size.y * 0.2f);
        }

        /// <summary>Pool x1.5 - ~4.4 x 1.9 units (rf_blood_snare image x3), an ellipse flattened by the view angle.</summary>
        private static bool InSnare(Vector2 p, Vector2 c)
        {
            float dx = (p.x - c.x) / 2.18f, dy = (p.y - c.y) / 0.93f;
            return dx * dx + dy * dy <= 1f;
        }

        private void Confuse()
        {
            SoulRequiem.Core.GameInput.InvertedUntil = Time.time + confuseSeconds;
            var pl = ai != null ? ai.Player : null;
            if (pl != null)
            {
                var ring = SpriteVfx.SpawnAttached("rf_confuse_ring", pl, 1.5f, Layer, Order + 6, 12f, confuseSeconds);
                if (ring != null) ring.transform.position = pl.position + Vector3.up * 1.05f;
            }
            Hint("Six hundred thousand hands pull at you. Every step turns against you.", 2.4f);
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
                     .Bound(yMin - 0.6f, yMax + 0.6f);                         // covers the whole field, does not reach into the south corridor
            SoulRequiem.Core.Sfx.Play("rf_war_drum", new Vector2(x, gap)); SoulRequiem.Core.Sfx.Play("ghost_march", new Vector2(x, gap)); // sound
            // the rank appears - flash along the field edge + war drum screen shake
            for (int k = 0; k < 3; k++) BossJuice.Flash(new Vector2(x, Mathf.Lerp(yMin, yMax, (k + 0.5f) / 3f)), tint, 1.6f, 1.1f, 0.6f);
            BossJuice.Shake(1, fromLeft ? Vector2.right : Vector2.left);
        }

        // ------------------------------------------------------------------ SPEAR FIELD
        private IEnumerator SpearField()
        {
            Use("spears", phase == 1 ? 5f : 4f);
            ai.PlayState("Idle", false);
            Announce("Spear Field", Red, 0.3f);
            yield return Telegraph(0.3f, Red);
            Fired();
            var frames = SpriteVfx.Load("rf_spear");
            int n = phase == 1 ? 5 : phase == 2 ? 6 : 8;
            float gap = phase == 3 ? 0.22f : 0.3f;
            for (int i = 0; i < n && !hp.IsDead; i++)
            {
                // chases Ren: impact point = where Ren stands + offset along his running direction
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
            g.impactSfx = "spear_burst"; // sound
            g.knockback = 1.6f;
            g.bodyScale = 0.9f;
            g.impactLight = 1.1f;                                                    // spear strikes down with a fiery flash
            g.onHit = _ => BossJuice.Stop(0.04f);
        }

        // ------------------------------------------------------------------ SLAM
        private IEnumerator Slam()
        {
            Use("slam", 4f);
            Announce("Blood Snare", Red, 0.5f);
            yield return Telegraph(0.5f, Red);
            if (hp.IsDead) yield break;
            ai.PlayState("Slam");
            yield return ai.WaitFor(3f / FPS_SLAM);                   // frame 3: crash down
            SoulRequiem.Core.Sfx.Play("boss_slam", Pos);
            // no damaging wave (centred on the FEET, the ring sat far south of the mass and looked wrong).
            // Instead: a blood pool spreads from BETWEEN the mass's feet, blood tendrils + ghost skulls rise; Ren in the pool when they rise -> CONTROLS REVERSED 5 s, no HP loss.
            Vector2 c = SnareCenter();
            SpriteVfx.Spawn("rf_blood_snare", c, 3f, Layer, Order - 1, false, 14f);   // x1.5 range
            Fired();
            BossJuice.Burst("m3x_war_quake", c, 2.4f, Color.white, Layer, Order - 2, 12f);   // red ground cracks + dust under the pool
            Boom(c + Vector2.up * 0.3f, Red, 1.6f, 3);
            yield return ai.WaitFor(5f / 14f);                                 // frame 5: blood tendrils rise
            for (int i = 0; i < 6 && !hp.IsDead; i++)                           // window while the tendrils stand (~frames 5-14): stepping in also gets caught
            {
                if (InSnare(PlayerPos, c) && !SoulRequiem.Core.GameInput.Inverted) Confuse();
                yield return ai.WaitFor(0.1f);
            }
            yield return ai.WaitFor(0.2f);
            ai.PlayState("Idle", false);
        }

        // ------------------------------------------------------------------ THE OATH BREAKS (damage window)
        private IEnumerator OathBreaks()
        {
            SfxHooks.BossMove(this, "OathBreaks"); // sound
            Use("open", 2f);
            ranksSinceOpen = 0;
            open = true;
            ai.PlayState("Open");
            yield return ai.WaitFor(3f / FPS_OPEN);
            Hint("The dead part. Something burns at the heart of the mound - strike it!", 2.6f);
            hp.DamageTakenMul = exposed;
            float window = phase == 1 ? 3.6f : phase == 2 ? 3.1f : 2.6f;
            Announce("The Oath Breaks", Ember, window);                            // cast bar = damage window left
            BossJuice.Impact(CastPoint, Ember, 1.2f, 2, Layer, Order + 2);
            var heart = BossJuice.Light(CastPoint, Ember, 1.8f, 1.6f, window, transform);   // burning heart in the middle of the mass, fading = about to close
            yield return ai.Flash(window, new Color(1f, 0.9f, 0.55f));
            BossJuice.Clear(hp);
            hp.DamageTakenMul = armour;
            open = false;
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ death: the mass sinks and dissolves into the ground
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
            if (ai != null && ai.Body != null) ai.Body.flipX = false;      // the mass never flips left / right
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (hp != null && !open) hp.DamageTakenMul = armour;
        }
    }
}
