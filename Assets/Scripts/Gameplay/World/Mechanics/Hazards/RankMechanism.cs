using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ranks - THE MARCHING HALL (Map 3b, before The Red Field boss arena). Object area = hall floor. While Ren stands in
    /// the hall, GHOST SOLDIER RANKS (<see cref="GhostRank"/>, same attack as the boss) march across the hall one after another, each leaving ONE gap
    /// (shown first by a warm light). Surviving `count` ranks -> flag `sets` (the north door opens) and it stops for good. Leaving the hall midway (or dying)
    /// restarts the count.
    ///   count (8)  interval seconds between two ranks (2.3)  warn (1.0)  speed units/second (2.0)  gap_half (0.42)  damage (18)
    ///   double_from: from which rank there are TWO armies (the second rank from the other edge, `double_lag` 1.2 s later, gap elsewhere) - default 5
    ///   start_when / stop_when (base class)   hint: text on first entering   text: text when done   reset_text: text when running away
    /// </summary>
    public sealed class RankMechanism : HazardMechanismBase
    {
        private bool running, announced;
        private int spawned, pairedPending;
        private float nextAt, lastFinish, pairAt, pairGap;
        private bool pairFromLeft;

        private int Count => Mathf.Max(1, Int("count", 8));

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Init()
        {
            string sets = Str("sets", Name + "_held");
            if (Ctx.Flags.Has(sets)) Done = true;                 // save: the hall is already passed
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (!Armed()) return;
            if (!inside)
            {
                if (running && spawned > 0)
                    Hints.Show(Str("reset_text", "The ranks fall back into the dark. They will march again."), 2.5f);
                running = false; spawned = 0; pairedPending = 0;
                return;
            }
            if (!running)
            {
                running = true; spawned = 0; pairedPending = 0;
                nextAt = Time.time + 0.8f;
                if (!announced)
                {
                    announced = true;
                    Hints.Show(Str("hint", "The dead are mustering. Hold the hall until the last rank has passed. Rolling will not save you - find the gap."), 3.5f);
                }
            }
            float now = Time.time;
            if (pairedPending > 0 && now >= pairAt)
            {
                pairedPending = 0;
                Spawn(pairFromLeft, pairGap, new Color(0.72f, 0.8f, 0.95f));
            }
            if (spawned < Count && now >= nextAt)
            {
                bool fromLeft = Random.value < 0.5f;
                float g = PickGap(playerPos.y);
                Spawn(fromLeft, g, new Color(1f, 0.82f, 0.82f));
                spawned++;
                nextAt = now + Float("interval", 2.3f);
                if (spawned >= Int("double_from", 5) && spawned < Count)
                {
                    pairedPending = 1; pairAt = now + Float("double_lag", 1.2f);
                    pairFromLeft = !fromLeft; pairGap = PickGap(g);
                    nextAt += 0.8f;
                }
                if (spawned > 1) Hints.Show(string.Format(Str("progress_text", "The line holds. {0} of {1}."), spawned - 1, Count), 1.4f);
            }
            if (spawned >= Count && pairedPending == 0 && now >= lastFinish)
            {
                Done = true;
                running = false;
                Ctx.Flags.Set(Str("sets", Name + "_held"));
                Hints.Show(Str("text", "The last rank passes. Somewhere ahead, iron grinds against stone."), 3f);
                Sfx.Play("checkpoint", playerPos, 0.6f);
            }
        }

        private float PickGap(float avoid)
        {
            float lo = Rect.yMin + 0.35f, hi = Rect.yMax - 0.35f;
            for (int i = 0; i < 16; i++)
            {
                float g = Random.Range(lo, hi);
                float d = Mathf.Abs(g - avoid);
                if (d > 0.7f && d < 2.0f) return g;                  // must MOVE, but can reach it during the warning
            }
            return avoid > (lo + hi) * 0.5f ? Mathf.Max(lo, avoid - 1.1f) : Mathf.Min(hi, avoid + 1.1f);
        }

        private void Spawn(bool fromLeft, float gap, Color tint)
        {
            float left = Rect.xMin - 0.1f, right = Rect.xMax + 0.1f, travel = right - left;
            float speed = Float("speed", 2.0f), warn = Float("warn", 1.0f);
            int layer = 0, order = 2;
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) { layer = psr.sortingLayerID; order = psr.sortingOrder; }
            float x = fromLeft ? left : right;
            GhostRank.Spawn(x, Rect.yMin - 0.05f, Rect.yMax + 0.05f, gap, Float("gap_half", 0.42f), fromLeft ? Vector2.right : Vector2.left,
                            warn, speed, travel, Float("damage", 18f), 0.7f, layer, order, tint)
                     .Bound(Rect.yMin - 0.3f, Rect.yMax + 0.3f);
            lastFinish = Mathf.Max(lastFinish, Time.time + warn + travel / Mathf.Max(0.1f, speed));
            Sfx.Play("rf_war_drum", new Vector2(x, gap), 0.8f); Sfx.Play("ghost_march", new Vector2(x, gap), 0.8f); // sound
        }

        protected override void OnStopped() { running = false; }

        internal override void DebugSolve()
        {
            if (Done) return;
            Done = true;
            Ctx.Flags.Set(Str("sets", Name + "_held"));
        }
    }
}
