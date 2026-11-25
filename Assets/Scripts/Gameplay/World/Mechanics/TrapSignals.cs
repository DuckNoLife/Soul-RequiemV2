using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Signal pulses between mechanisms (pressure plates / levers -> traps). Unlike WorldFlags: not saved, fire and forget.
    /// Traps POLL (no event subscription) through <see cref="Count"/>: how many times that name has fired - compared with the count seen last time.
    /// Tiled properties: senders `fires` = "name1,name2"; listeners `trigger` (run one round) / `disable_on` (temporarily off).
    /// </summary>
    public static class TrapSignals
    {
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public static void Fire(string names)
        {
            if (string.IsNullOrEmpty(names)) return;
            foreach (var raw in names.Split(','))
            {
                string n = raw.Trim();
                if (n.Length == 0) continue;
                counts.TryGetValue(n, out int c);
                counts[n] = c + 1;
            }
        }

        /// <summary>Total number of pulses fired by every name in the list "a,b".</summary>
        public static int Count(string names)
        {
            if (string.IsNullOrEmpty(names)) return 0;
            int sum = 0;
            foreach (var raw in names.Split(','))
                if (counts.TryGetValue(raw.Trim(), out int c)) sum += c;
            return sum;
        }
    }

    /// <summary>Listener for a signal list: <see cref="Poll"/> returns true when there is a new pulse since the last poll.</summary>
    public sealed class TrapListener
    {
        private readonly string names;
        private int seen;

        public TrapListener(string names)
        {
            this.names = names ?? "";
            seen = TrapSignals.Count(this.names);
        }

        public bool Active => names.Length > 0;

        public bool Poll()
        {
            if (names.Length == 0) return false;
            int c = TrapSignals.Count(names);
            if (c == seen) return false;
            seen = c;
            return true;
        }
    }
}
