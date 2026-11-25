using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// (1) HEAD REACH — the head must only reach the map wall, not dive deep into the arena. Measures the jaw bottom with the head at rest,
    /// computes the max source px the head may lower so the jaw only touches the NORTH EDGE of the arena (Floor.yMax + `head_reach_pad`), and writes it to
    /// VesselRig.MaxDipPx (cap for both HeadDip and Lunge) and HeadLungePx (Lunge = lower exactly to the map wall, max `head_lunge_px`).
    /// (2) loopFx: long looping SpriteVfx (flamethrower, spinning flames, frost breath, ice lances…) — ClearField stops them all, so a coroutine stopped
    /// mid-way at phase end can no longer leave e.g. a Spitfire Trap on the arena.
    /// Called by: TickFight (TrackLungeReach), ClearField (StopLoopFx).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private float reachPx = -1f;
        private readonly List<SpriteVfx> loopFx = new List<SpriteVfx>();

        private void TrackLungeReach()
        {
            if (rig == null) return;
            if (rig.Lunge < 0.01f && rig.HeadDip < 0.01f && rig.JawOpen < 0.05f)
            {
                float wpp = Mathf.Abs(rig.PointWorld(Vector2.zero).y - rig.PointWorld(new Vector2(0f, 100f)).y) / 100f;   // world units per source px
                if (wpp > 1e-5f)
                {
                    float edge = Floor.yMax + Float("head_reach_pad", 0f);
                    reachPx = Mathf.Max(0f, (rig.JawBottomY - edge) / wpp);
                }
            }
            if (reachPx < 0f) return;
            rig.MaxDipPx = reachPx;
            rig.HeadLungePx = Mathf.Min(reachPx, Float("head_lunge_px", 400f));
        }

        private void StopLoopFx()
        {
            foreach (var v in loopFx) if (v != null) v.Stop();
            loopFx.Clear();
        }
    }
}
