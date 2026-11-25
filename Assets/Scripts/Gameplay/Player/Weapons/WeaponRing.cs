using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SWORD RACK — the katanas Ren owns and the one equipped.
    ///   • Owning a sword = flag `weapon_&lt;id&gt;` (a map flag, carried across maps by GameSession like `book_` flags, saved by SaveGame).
    ///   • Equipped sword: <see cref="Equipped"/> (static, lives for the session) + stored in the save.
    ///   • Every sword lives in Resources/Weapons/*.asset — picked up via Tiled pickups `item = weapon`, `weapon_id = &lt;id&gt;`.
    /// The starting sword (Ronin's Katana) is always owned.
    /// </summary>
    public static class WeaponRing
    {
        public const string Prefix = "weapon_";
        private static WeaponDefinition[] all;

        /// <summary>Every katana in the game (order: the starting sword first, then by name).</summary>
        public static IReadOnlyList<WeaponDefinition> All
        {
            get
            {
                if (all == null)
                {
                    var list = new List<WeaponDefinition>(Resources.LoadAll<WeaponDefinition>("Weapons"));
                    list.Sort((a, b) => a == null || b == null ? 0
                        : a.id == DefaultWeaponId ? -1 : b.id == DefaultWeaponId ? 1 : string.CompareOrdinal(a.displayName, b.displayName));
                    all = list.ToArray();
                }
                return all;
            }
        }

        public static string DefaultWeaponId
        {
            get { var d = WeaponDefinition.LoadDefault(); return d != null ? d.id : "katana"; }
        }

        public static WeaponDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var w in All) if (w != null && w.id == id) return w;
            return null;
        }

        public static string Flag(string id) => Prefix + id;
        public static bool Has(WorldFlags f, string id) =>
            !string.IsNullOrEmpty(id) && (id == DefaultWeaponId || (f != null && f.Has(Flag(id))));

        /// <summary>Owned swords (in <see cref="All"/> order).</summary>
        public static List<WeaponDefinition> Owned(WorldFlags f)
        {
            var list = new List<WeaponDefinition>();
            foreach (var w in All) if (w != null && Has(f, w.id)) list.Add(w);
            return list;
        }

        /// <summary>Id of the equipped sword (carried across maps, saved). Empty = the starting sword.</summary>
        public static string Equipped { get; private set; } = "";

        public static void SetEquipped(string id) => Equipped = id ?? "";

        /// <summary>New game: forget the previous run's equipped sword (static data lives for the whole game process).</summary>
        public static void Reset() => Equipped = "";

        /// <summary>A sword was picked up: set the flag + show a message.</summary>
        /// <summary>`announce` = false -> no message at the bottom of the screen (the caller shows its own, e.g. at the foot of a note panel).</summary>
        public static void Give(WorldFlags f, string id, string line, bool announce = true)
        {
            var w = Find(id);
            if (f == null || w == null) { Debug.LogWarning($"[WeaponRing] no katana id '{id}' in Resources/Weapons."); return; }
            if (f.Has(Flag(id))) return;
            SoulRequiem.Core.Sfx.Play2D("pickup_weapon");   // sound
            f.Set(Flag(id));
            if (!announce) return;
            string msg = string.IsNullOrEmpty(line) ? $"Obtained: {w.displayName}." : line;
            Hints.Show(msg + "  [Inventory]", Mathf.Max(5f, msg.Length / 15f));
        }

        // ---- summary numbers for the inventory panel (0..1 to draw bars) -------------------------------------------------
        /// <summary>Total damage of a full 3-hit combo.</summary>
        public static float ComboDamage(WeaponDefinition w)
        {
            float d = 0f;
            if (w != null && w.combo != null) foreach (var s in w.combo) d += s.damage;
            return d;
        }

        /// <summary>Time of a full 3-hit combo (chained early at the cancel frame) + the end-of-combo rest.</summary>
        public static float ComboTime(WeaponDefinition w)
        {
            if (w == null || w.combo == null || w.combo.Length == 0) return 1f;
            float t = 0f;
            for (int i = 0; i < w.combo.Length; i++) t += i < w.combo.Length - 1 ? w.combo[i].CancelTime : w.combo[i].Length;
            return t + w.finisherRecovery;
        }

        public static float Dps(WeaponDefinition w)
        {
            float t = ComboTime(w);
            return t > 0.01f ? ComboDamage(w) / t : 0f;
        }

        public static float Reach(WeaponDefinition w)
        {
            float r = 0f;
            if (w != null && w.combo != null) foreach (var s in w.combo) r = Mathf.Max(r, s.range);
            return r;
        }

        public static float Impact(WeaponDefinition w)
        {
            float k = 0f;
            if (w != null && w.combo != null) foreach (var s in w.combo) k = Mathf.Max(k, s.knockback);
            return k;
        }

        /// <summary>Hits / second when chaining continuously.</summary>
        public static float Rate(WeaponDefinition w)
        {
            float t = ComboTime(w);
            return t > 0.01f && w != null && w.combo != null ? w.combo.Length / t : 0f;
        }
    }
}
