#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Test commands (DevCheats: F5 - also grants every key of the map; Shift+F3 - pass area). Only in the Editor / Development Build.</summary>
    public partial class MapMechanics
    {
        /// <summary>Set every flag that puzzles / obstacles produce (the Map 2 crank included).</summary>
        public void DebugSolveAll()
        {
            if (ctx == null) return;
            ctx.Flags.Set("lever_handle");
            foreach (var m in mechs) m.DebugSolve();
            Hints.Show("[TEST] All puzzles and barricades solved", 2f);
        }

        /// <summary>Solve every mechanism / puzzle / minigame touching area `area` (Shift+F3 pass area).</summary>
        public void DebugSolveIn(Rect area)
        {
            if (ctx == null) return;
            foreach (var m in mechs) if (m != null && m.Rect.Overlaps(area)) m.DebugSolve();
        }

        /// <summary>Mechanisms that are BOSS FIGHTS (F4 to boss / F10 BOSSES panel): Absalom (absalom_hall), Burlap's Final Exam (drill_yard station 2).</summary>
        internal List<Mechanism> DebugBossArenas()
        {
            var list = new List<Mechanism>();
            foreach (var m in mechs)
            {
                if (m == null) continue;
                if (m is AbsalomHallMechanism) list.Add(m);
                else if (m is DrillYardMechanism d && d.Station == 2) list.Add(m);
            }
            return list;
        }

        internal Mechanism DebugFind(string name)
        {
            foreach (var m in mechs) if (m != null && m.Name == name) return m;
            return null;
        }

        /// <summary>F7 with no enemy boss - the running mechanism boss fight (Absalom) skips its current phase.</summary>
        public bool DebugBossShowcase()
        {
            foreach (var m in mechs) if (m is AbsalomHallMechanism a && a.DebugShowcase()) return true;
            foreach (var m in mechs) if (m is VesselDomainMechanism v && v.DebugShowcase()) return true;   // The Unwritten
            return false;
        }

        public bool DebugSkipBossPhase()
        {
            foreach (var m in mechs) if (m is AbsalomHallMechanism a && a.DebugSkipPhase()) return true;
            foreach (var m in mechs) if (m is VesselDomainMechanism v && v.DebugSkipAct()) return true;     // fill the resistance bar
            return false;
        }
    }
}
#endif
