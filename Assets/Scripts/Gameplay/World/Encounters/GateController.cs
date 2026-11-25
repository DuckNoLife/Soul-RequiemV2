using System.Collections.Generic;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Opens / closes the map's gates. Three gate kinds (Tiled properties on objects of type "gate"):
    ///   close_on_enter = true : arena lock gate - closes when the area's fight starts, opens when cleared.
    ///   unlock_when = "a,b"   : opens once the flags are set.
    ///   (nothing)             : opens when area zone_id is cleared.
    /// starts_closed = false -> open from the start.
    /// </summary>
    internal sealed class GateController
    {
        private readonly List<MapGate> gates;

        public GateController(List<MapGate> gates)
        {
            this.gates = gates;
        }

        public IReadOnlyList<MapGate> All => gates;

        /// <summary>Set the initial state: gates with starts_closed = false start open.</summary>
        public void ApplyStartState()
        {
            foreach (var g in gates)
                if (!g.startsClosed) SetClosed(g, false);
        }

        public void SetClosed(MapGate g, bool closed)
        {
            bool animated = false, changed = false;
            foreach (var part in g.parts)
            {
                if (part == null) continue;
                // animated gate image -> always shown, plays the close / open frames instead of show / hide
                var anim = part.GetComponent<GateAnimator>();
                if (anim != null) { animated = true; part.SetActive(true); anim.SetClosed(closed); }
                else { if (part.activeSelf != closed) changed = true; part.SetActive(closed); }
            }
            // non-animated gate -> sound here (animated gates play their own in GateAnimator); skipped while the map loads
            if (!animated && changed && UnityEngine.Time.timeSinceLevelLoad > 1f) SfxHooks.Gate(g.name, closed, g.bounds.center);
        }

        public void Open(MapGate g)
        {
            g.opened = true;
            SetClosed(g, false);
        }

        /// <summary>Does the area have an arena lock gate.</summary>
        public bool HasArenaLock(string zoneId)
        {
            foreach (var g in gates) if (g.closeOnEnter && g.zoneId == zoneId) return true;
            return false;
        }

        /// <summary>Close (true) / open (false) every arena lock gate of the area.</summary>
        public void SetArenaClosed(string zoneId, bool closed)
        {
            foreach (var g in gates)
                if (g.closeOnEnter && g.zoneId == zoneId) SetClosed(g, closed);
        }

        /// <summary>Area restarted -> its arena lock gates open and close again when the fight restarts
        /// (skipping gates already marked "opened" in OpenForClearedZone left them stuck closed after a rematch).</summary>
        public void RearmArena(string zoneId)
        {
            foreach (var g in gates)
                if (g.closeOnEnter && g.zoneId == zoneId) { g.opened = false; SetClosed(g, false); }
        }

        /// <summary>Area just cleared: open its arena lock gates and its gates without conditions. Returns true if any gate opened.</summary>
        public bool OpenForClearedZone(string zoneId)
        {
            bool opened = false;
            foreach (var g in gates)
            {
                if (g.zoneId != zoneId || g.opened) continue;
                if (g.closeOnEnter || string.IsNullOrEmpty(g.unlockWhen)) { Open(g); opened = true; }
            }
            return opened;
        }

        /// <summary>Open every unlock_when gate whose flags are set. Returns true if any gate opened.</summary>
        public bool OpenUnlocked(WorldFlags flags)
        {
            bool any = false;
            foreach (var g in gates)
            {
                if (g.opened || g.closeOnEnter || string.IsNullOrEmpty(g.unlockWhen)) continue;
                if (!flags.HasAll(g.unlockWhen)) continue;
                Open(g);
                any = true;
            }
            return any;
        }
    }
}
