using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// pit - COLLAPSING FLOOR (Crypt strip "pit trap2-2x2" -> Resources/VFX/trap_pit2: frame 0 = intact floor, last frame = dark hole).
    /// The object area is fully tiled with `size`-tile slabs (default 2 = 64 px, must match the frame size of the `sprite` strip).
    ///   Ren steps on a slab -> it cracks + shakes for `warn_seconds` (default 0.45) -> collapses over `fall_seconds` (0.35).
    ///   If Ren's feet are still on the slab when it has collapsed (or he steps into an open hole) -> FALLS: loses `damage` (14, cannot be rolled through),
    ///   black screen, then stands again at `return_at` ("x,y" Tiled tile; empty = middle of the area's bottom edge) with text `text`.
    ///   The hole stays open `open_seconds` (2.5), then the stones fit back together (frames reversed over 0.6 s), resting `rearm` (0.8) before it can collapse again.
    ///   Enemies do not collapse the floor. `stop_when` = flag: the floor hardens, no more collapsing. `hint` shows the first time Ren gets close.
    /// Design goal: punish players who rush / stop on strange floor in front of a door - walking straight across is fine.
    /// </summary>
    public sealed class PitTrapMechanism : Mechanism
    {
        private Sprite[] frames;
        private SpriteRenderer[] pieces;
        private Rect[] rects;
        private int[] state;          // 0 intact, 1 cracked (warning), 2 collapsed / hole open, 3 fitting back together
        private float[] since;        // time the state was entered (state 0: time it may collapse again)
        private Vector3[] basePos;    // original position (world) of each slab - shakes around this
        private bool announced;
        private float nextFall;

        private float Warn => Float("warn_seconds", 0.45f);
        private float FallTime => Float("fall_seconds", 0.35f);
        private float OpenTime => Float("open_seconds", 2.5f);
        private const float CloseTime = 0.6f;

        public override void Init()
        {
            frames = TrapArt.Frames(Str("sprite", "trap_pit2"));
            float ts = 0.32f * Mathf.Max(1, Int("size", 2));
            int nx = Mathf.Max(1, Mathf.RoundToInt(Rect.width / ts));
            int ny = Mathf.Max(1, Mathf.RoundToInt(Rect.height / ts));
            int n = nx * ny;
            pieces = new SpriteRenderer[n]; rects = new Rect[n]; state = new int[n]; since = new float[n]; basePos = new Vector3[n];
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    var bl = new Vector2(Rect.xMin + i * ts, Rect.yMin + j * ts);
                    rects[k] = new Rect(bl.x + ts * 0.12f, bl.y + ts * 0.12f, ts * 0.76f, ts * 0.76f);   // leave a margin: standing right at the rim does not count
                    basePos[k] = new Vector3(bl.x, bl.y, 0f);
                    if (frames.Length == 0) continue;
                    pieces[k] = TrapArt.MakeRenderer(Ctx.Host.transform, "Pit " + Name + " " + k, bl, Ctx.Player, -7);   // lies under the feet
                    pieces[k].sprite = frames[0];
                }
        }

        private bool Stopped
        {
            get { string w = Str("stop_when", ""); return w.Length > 0 && Ctx.Flags.HasAll(w); }
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            bool near = DistanceTo(playerPos) < 1.0f;
            if (near && !announced) { announced = true; string h = Str("hint", ""); if (h.Length > 0) Hints.Show(h, 2.5f); }
            if (!near) announced = false;

            float now = Time.time;
            int last = Mathf.Max(0, frames.Length - 1);
            int crackEnd = Mathf.Max(1, Mathf.RoundToInt(last * 0.3f));      // crack frame (no stones falling yet)
            bool stopped = Stopped;
            for (int k = 0; k < rects.Length; k++)
            {
                bool on = rects[k].Contains(playerPos);
                switch (state[k])
                {
                    case 0:
                        if (!stopped && on && now >= since[k]) { state[k] = 1; since[k] = now; Sfx.Play("trap_pit_crack", rects[k].center); }
                        Show(k, 0);
                        break;
                    case 1:
                    {
                        float p = (now - since[k]) / Mathf.Max(0.05f, Warn);
                        Show(k, Mathf.RoundToInt(Mathf.Lerp(0, crackEnd, Mathf.Clamp01(p))));
                        if (pieces[k] != null) pieces[k].transform.position = Jitter(k, 0.012f);
                        if (p >= 1f) { state[k] = 2; since[k] = now; Sfx.Play("trap_pit_collapse", rects[k].center); }
                        break;
                    }
                    case 2:
                    {
                        float e = now - since[k];
                        Show(k, Mathf.RoundToInt(Mathf.Lerp(crackEnd, last, Mathf.Clamp01(e / Mathf.Max(0.05f, FallTime)))));
                        if (pieces[k] != null) pieces[k].transform.position = basePos[k];
                        if (on && e >= FallTime * 0.6f) Fall();
                        if (e >= FallTime + OpenTime) { state[k] = 3; since[k] = now; }
                        break;
                    }
                    case 3:
                    {
                        float p = (now - since[k]) / CloseTime;
                        Show(k, Mathf.RoundToInt(Mathf.Lerp(last, 0, Mathf.Clamp01(p))));
                        if (on && p < 0.5f) Fall();                                  // hole still more than half open: still falls
                        if (p >= 1f) { state[k] = 0; since[k] = now + Float("rearm", 0.8f); }
                        break;
                    }
                }
            }
        }

        private void Show(int k, int frame)
        {
            if (pieces[k] == null || frames.Length == 0) return;
            pieces[k].sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        }

        private Vector3 Jitter(int k, float amp) =>
            basePos[k] + new Vector3(Mathf.Sin(Time.time * 70f + k) * amp, Mathf.Cos(Time.time * 83f + k * 2f) * amp * 0.5f, 0f);

        /// <summary>Ren falls into the hole: loses HP (cannot be rolled through), black screen, stands again at return_at.</summary>
        private void Fall()
        {
            if (Time.time < nextFall || SceneTransition.Busy || Ctx.Player == null) return;
            nextFall = Time.time + 2f;
            TrapArt.HurtPlayerCrush(Ctx, Float("damage", 14f), Vector2.down, 0f);
            if (Ctx.PlayerHealth != null && Ctx.PlayerHealth.IsDead) return;   // if he dies, leave it to the respawn flow
            Vector3 at = Ctx.TilePoint(Str("return_at", ""), new Vector2(Rect.center.x, Rect.yMin - 0.4f));
            SceneTransition.Warp(Ctx.Player, at, Str("text", "The floor gives way beneath you."), 0.04f);
        }
    }
}
