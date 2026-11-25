using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// stillness - MOTION SENSOR ZONE. Ren inside the zone and MOVING (dashing included) -> fires pulse `fires`
    /// (default = object name) to the traps whose `trigger` matches (see <see cref="TrapSignals"/>). Standing still = silence.
    /// Used by The Still Walk (Map 1b): the hermit's arrow shooters "only hear those who walk".
    ///   speed_min   speed (Unity units / second) that counts as "walking"; default 0.35 (Ren walks at ~2)
    ///   rearm       seconds between two pulses while Ren keeps walking; default 1.0
    ///   hint        text shown on first entering the zone (empty = none)
    ///   start_when / stop_when   flag layer like the other traps (stop_when -> off for good)
    /// Enemies walking in the zone do NOT trigger it (Ren only).
    /// </summary>
    public sealed class StillnessMechanism : HazardMechanismBase
    {
        private Vector2 last;
        private bool hasLast, announced;
        private float readyAt;

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = hasLast ? (playerPos - last).magnitude / dt : 0f;
            last = playerPos;
            hasLast = true;
            if (Done || !inside || !Armed()) return;

            if (!announced)
            {
                announced = true;
                string h = Str("hint", "");
                if (h.Length > 0) Hints.Show(h, 3f);
            }

            // instant moves (scene load / passage) give a huge speed -> skip that frame
            if (speed > 40f) return;
            if (speed < Float("speed_min", 0.35f) || Time.time < readyAt) return;
            readyAt = Time.time + Mathf.Max(0.2f, Float("rearm", 1.0f));
            TrapSignals.Fire(Str("fires", Name));
        }

        internal override void DebugSolve() { }
    }
}
