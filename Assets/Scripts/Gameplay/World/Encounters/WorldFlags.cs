using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// State flags of the current map: "z3_cleared", "valve_w", "bells_done", "book_purifier"...
    /// Puzzles, gates, pickups and quests all talk through flags - they never call each other directly.
    ///
    ///   Set    : flags.Set("valve_w");
    ///   Query  : flags.Has("valve_w")   |   flags.HasAll("valve_w,valve_e")   (empty list = true)
    ///   Listen : flags.Changed += f => ...;
    ///
    /// Automatic area flags: "&lt;zone_id&gt;_started" when the area's fight starts, "&lt;zone_id&gt;_cleared" when cleared.
    /// (The save system only needs to store <see cref="All"/>.)
    /// </summary>
    public sealed class WorldFlags
    {
        private readonly HashSet<string> flags = new HashSet<string>();
        private readonly Action<bool> afterSet;

        /// <summary>A NEW flag was just set (name already trimmed).</summary>
        public event Action<string> Changed;

        /// <param name="afterSet">Called after <see cref="Changed"/> for every new flag (argument = announce). The director uses it to open gates.</param>
        public WorldFlags(Action<bool> afterSet = null)
        {
            this.afterSet = afterSet;
        }

        /// <summary>Every flag currently set.</summary>
        public IReadOnlyCollection<string> All => flags;

        public bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag.Trim());

        /// <summary>Every flag in the list "a,b,c" is set. Empty list = true.</summary>
        public bool HasAll(string commaList)
        {
            if (string.IsNullOrEmpty(commaList)) return true;
            foreach (var f in commaList.Split(','))
            {
                // "a|b" = ANY one of the flags is enough (e.g. a blocking strip opens once the diary page is read OR an old save already took the key)
                if (f.IndexOf('|') < 0) { if (!Has(f.Trim())) return false; continue; }
                bool any = false;
                foreach (var g in f.Split('|')) if (Has(g.Trim())) { any = true; break; }
                if (!any) return false;
            }
            return true;
        }

        /// <summary>A flag was just CLEARED (player fell mid-fight with `reset_on_death`).</summary>
        public event Action<string> Removed;

        /// <summary>Clear a flag. Does nothing if not set.</summary>
        public void Unset(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Remove(flag.Trim())) return;
            Debug.Log($"[Map] Flag cleared: {flag}");
            Removed?.Invoke(flag.Trim());
        }

        /// <summary>Set a flag. Setting an existing flag does nothing.</summary>
        /// <param name="announce">true = if a gate opens because of this flag, show "Somewhere, a way opens."</param>
        public void Set(string flag, bool announce = false)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Add(flag.Trim())) return;
            Debug.Log($"[Map] Flag: {flag}");
            Changed?.Invoke(flag.Trim());
            afterSet?.Invoke(announce);
        }
    }
}
