using System.Collections.Generic;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PASSIVES catalogue — learned books (book_* flags, carried across maps by GameSession.Books). Always active,
    /// take no slot; the rest panel (Mantras) lists them under "Passives". New book: add one row here + an icon in Resources/UI/Requiem/&lt;icon&gt;.
    /// </summary>
    public static class PassiveCatalog
    {
        public struct Entry
        {
            public string flag, name, description, icon;
            public Entry(string flag, string name, string description, string icon) { this.flag = flag; this.name = name; this.description = description; this.icon = icon; }
        }

        public static readonly Entry[] All =
        {
            new Entry(TurnedBlade.Flag, "The Turned Blade",
                "Strike an enemy projectile just before it hits you and it flies back at its owner - faster, and twice as deadly.",
                "ic_passive_turned_blade"),
            new Entry("book_purifier", "The Purifier's Rite",
                "Earthshatter now also scatters any poison cloud it touches.",
                "ic_passive_purifier"),
            // the Kroven guard's sword manual (Map 2b The Kroven Deep, beside King Nolan's remains)
            new Entry(PlayerCombat.RampartFlag, "The Rampart Forms",
                "Stand your ground: after half a second without moving, you take 20% less damage until you step again.",
                "ic_trait_laststand"),
        };

        /// Learned passives (from the current map's flags; book flags are copied from GameSession.Books on entering the map).
        public static List<Entry> Learned(WorldFlags flags)
        {
            var list = new List<Entry>();
            foreach (var e in All)
                if ((flags != null && flags.Has(e.flag)) || GameSession.Books.Contains(e.flag)) list.Add(e);
            return list;
        }
    }
}
