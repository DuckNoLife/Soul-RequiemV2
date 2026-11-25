using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// blade - BLADE ON A TRACK. Object area = the track (long side = axis); the blade travels back and forth between both ends.
    ///   kind   saw (small Crypt saw blade in a floor groove) | blades (large 4-bladed Old Prison spinner on an iron rail)
    ///   speed (tiles / second, default saw 3, blades 2), pause (seconds stopped at each end, 0.3), offset (phase offset 0..1 of the path)
    ///   damage (player, 12), knock (3). Traps never hurt enemies.
    ///   disable_on + disable_seconds (blade stops, does not cut), start_when, stop_when (base class).
    /// </summary>
    public sealed class BladeMechanism : HazardMechanismBase
    {
        private Sprite[] spin;
        private SpriteRenderer sr;
        private TrapListener disabler;
        private float disabledUntil, nextHurt, travel;

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

        /// <summary>Draw the groove (saw) or iron rail (blades) along the axis.</summary>
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
                // the groove stays INSIDE the trap area: the piece count is spread evenly over the area length,
                // and the pieces shrink when the area is shorter than one piece (stepping by the image width even for vertical grooves,
                // with the last piece past the rail end, made the groove stick ~1.5 tiles out of the wall).
                float total = Horizontal ? Rect.width : Rect.height;
                const float pad = 0.06f;
                float use = Mathf.Max(0.05f, total - 2f * pad);
                float piece = (Horizontal ? trail[0].bounds.size.x : trail[0].bounds.size.y) * 0.8f;   // groove line ~80% of the image frame
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

        private Vector2 PosAt(float u)        // u 0..2: there and back
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
            // one-way time + pause at the end: mapped to parameter u
            float leg = len / Mathf.Max(0.05f, speed);
            travel = Mathf.Repeat(travel + Time.deltaTime / (leg + pause), 2f);
            float local = Mathf.Repeat(travel, 1f) * (leg + pause);
            float u01 = Mathf.Clamp01((local - pause) / leg);
            float u = travel < 1f ? u01 : 1f + u01;
            Vector2 pos = PosAt(u);
            sr.transform.position = pos;
            SoulRequiem.Core.Sfx.Loop(this, Saw ? "trap_saw" : "trap_blade", sr.transform);   // sound (stops 0.3 s after the last call)
            sr.sprite = spin[(int)(Time.time * (Saw ? 18f : 12f)) % spin.Length];

            float radius = Saw ? 0.2f : 0.38f;
            float now = Time.time;
            if (Vector2.Distance(playerPos, pos) < radius && now >= nextHurt)
            {
                nextHurt = now + 0.5f;
                TrapArt.HurtPlayerCrush(Ctx, Float("damage", 12f), playerPos - pos, Float("knock", 3f));
            }
            // traps do NOT damage enemies - only Ren gets hit.
        }

        internal override void DebugSolve() { }
    }
}
