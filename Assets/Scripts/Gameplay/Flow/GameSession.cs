using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Data carried across map (scene) changes: HP, flasks, the map just left, the region name ahead, books, katanas, loadout, map group flags.
    /// Static, lives for the whole play session (no DontDestroyOnLoad needed).
    /// Written by: MapTriggerHandler (exits), SaveGame (Continue), DevCheats. Read by: MapEncounterDirector.Start, PlayerLoadout.
    /// </summary>
    public static class GameSession
    {
        public static bool HasCarry { get; private set; }
        public static float HpPercent { get; private set; } = 1f;
        public static int Potions { get; private set; }
        /// <summary>MAX flask count (chests / bronze flasks +1) — must be carried over, otherwise a new map, a death there
        /// and a save there would all reset it to 3.</summary>
        public static int MaxPotions { get; private set; }
        public static string FromMap { get; private set; } = "";
        public static string ArrivalTitle { get; private set; } = "";
        public static string ArrivalSubtitle { get; private set; } = "";

        public static void Capture(GameObject player, string fromMap, string arrivalTitle, string arrivalSubtitle)
        {
            HasCarry = true;
            FromMap = fromMap ?? "";
            ArrivalTitle = arrivalTitle ?? "";
            ArrivalSubtitle = arrivalSubtitle ?? "";
            var hp = player != null ? player.GetComponent<Health>() : null;
            HpPercent = hp != null ? Mathf.Clamp(hp.Percent, 0.35f, 1f) : 1f;      // never arrive in a new map below 35% HP
            var kit = player != null ? player.GetComponent<PlayerCombat>() : null;
            Potions = kit != null ? kit.Potions : 0;
            MaxPotions = kit != null ? kit.maxPotions : 0;
            // learned books (book_* flags) follow to the new map (map flags stay with their map, so they must be carried explicitly)
            var dir = MapEncounterDirector.Instance;
            if (dir != null && dir.Flags != null)
                foreach (var f in dir.Flags.All)
                    if (f.StartsWith("book_") || f.StartsWith(WeaponRing.Prefix)) Books.Add(f);   // owned katanas follow too
            // remember every flag of the map group being left (returning to the main map from a sub-map -> cleared zones / picked items stay)
            if (dir != null && dir.Flags != null && !string.IsNullOrEmpty(dir.FlagGroup))
            {
                GroupFlags[dir.FlagGroup] = new System.Collections.Generic.HashSet<string>(dir.Flags.All);
                GroupScene[dir.FlagGroup] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            }
            var lo = player != null ? player.GetComponent<PlayerLoadout>() : null;
            if (lo != null)
            {
                HasLoadout = true;
                Owned.Clear(); foreach (var o in lo.owned) if (o != null) Owned.Add(o.id);
                for (int i = 0; i < Equipped.Length; i++) Equipped[i] = i < PlayerLoadout.SlotCount && lo.slots[i] != null ? lo.slots[i].id : "";
            }
        }

        /// <summary>Learned books (book_* flags), carried across maps and restored into the new map's flags (MapEncounterDirector.Start).</summary>
        public static readonly System.Collections.Generic.HashSet<string> Books = new System.Collections.Generic.HashSet<string>();

        // ---- progress per map group (map property `flag_group`) + arrival point (exit property `arrive_at`)
        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> GroupFlags =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>();
        private static bool hasArrive;
        private static Vector2 arriveTile;

        /// <summary>Remembered flags of a map group (null if that group was never left this session).</summary>
        public static System.Collections.Generic.IEnumerable<string> RecallGroup(string group)
        {
            // test key "replay map / zone from scratch" -> the next map load neither restores (and also forgets) the group's remembered progress
            if (FreshNextMap)
            {
                FreshNextMap = false;
                if (!string.IsNullOrEmpty(group)) GroupFlags.Remove(group);
                return null;
            }
            return !string.IsNullOrEmpty(group) && GroupFlags.TryGetValue(group, out var f) ? f : null;
        }

        /// <summary>DevCheats sets true before reloading a map to replay it FROM SCRATCH (carried items / skills / katanas are kept).</summary>
        public static bool FreshNextMap;

        /// <summary>Arrival point set by an exit (Tiled tile of the target map); the target map's Director takes it once.</summary>
        public static void SetArrival(bool has, Vector2 tile) { hasArrive = has; arriveTile = tile; }

        public static bool TakeArrival(out Vector2 tile)
        {
            tile = arriveTile; bool h = hasArrive; hasArrive = false; return h;
        }

        /// <summary>Continue — forget the map group progress of the previous run in this session (trust only the save).</summary>
        public static void ForgetMaps() { GroupFlags.Clear(); GroupScene.Clear(); hasArrive = false; }

        // --- map group progress into / out of the save (the Chapters screen shows the right progress for old chapters)
        private static readonly System.Collections.Generic.Dictionary<string, string> GroupScene =
            new System.Collections.Generic.Dictionary<string, string>();

        public struct GroupSnap { public string id, scene; public string[] flags; }

        /// <summary>Map groups left during this session (flags + last scene) — SaveGame.Save merges them into the save.</summary>
        public static System.Collections.Generic.List<GroupSnap> SnapshotGroups()
        {
            var list = new System.Collections.Generic.List<GroupSnap>();
            foreach (var kv in GroupFlags)
            {
                GroupScene.TryGetValue(kv.Key, out var sc);
                list.Add(new GroupSnap { id = kv.Key, scene = sc ?? "", flags = new System.Collections.Generic.List<string>(kv.Value).ToArray() });
            }
            return list;
        }

        /// <summary>Load map group progress from the save (Continue / Chapters) -> taking an exit into that group's map shows the right flags.</summary>
        public static void RestoreGroups(SaveGame.Group[] groups)
        {
            if (groups == null) return;
            foreach (var g in groups)
            {
                if (g == null || string.IsNullOrEmpty(g.id) || g.flags == null) continue;
                GroupFlags[g.id] = new System.Collections.Generic.HashSet<string>(g.flags);
                if (!string.IsNullOrEmpty(g.scene)) GroupScene[g.id] = g.scene;
            }
        }

        /// <summary>New game — clear everything carried from the previous run (static data lives for the whole game process).</summary>
        public static void NewGame()
        {
            HasCarry = false; HasLoadout = false; MaxPotions = 0;
            GroupFlags.Clear(); GroupScene.Clear(); hasArrive = false;
            Owned.Clear();
            for (int i = 0; i < Equipped.Length; i++) Equipped[i] = "";
            Books.Clear();
            WeaponRing.Reset();
        }

        /// <summary>Set the carried skill loadout from the save (Continue).</summary>
        public static void SetLoadout(string[] owned, string[] equipped)
        {
            HasLoadout = true;
            Owned.Clear(); if (owned != null) Owned.AddRange(owned);
            for (int i = 0; i < Equipped.Length; i++) Equipped[i] = equipped != null && i < equipped.Length ? equipped[i] ?? "" : "";
        }

        // ---- skill loadout carried across maps (ids)
        public static bool HasLoadout { get; private set; }
        public static readonly System.Collections.Generic.List<string> Owned = new System.Collections.Generic.List<string>();
        public static readonly string[] Equipped = new string[PlayerLoadout.SlotCount];

        /// Apply the carried loadout (called by PlayerLoadout.Start).
        public static void ApplyLoadout(PlayerLoadout lo)
        {
            if (!HasLoadout || lo == null) return;
            foreach (var id in Owned) { var s = PlayerLoadout.Find(id); if (s != null && !lo.owned.Contains(s)) lo.owned.Add(s); }
            for (int i = 0; i < PlayerLoadout.SlotCount && i < Equipped.Length; i++)
                lo.slots[i] = string.IsNullOrEmpty(Equipped[i]) ? null : PlayerLoadout.Find(Equipped[i]);
            lo.NotifyChanged();
        }

        /// <summary>Apply the carried data to the character in the new scene (called once after the scene has loaded).</summary>
        public static void ApplyTo(GameObject player)
        {
            if (!HasCarry || player == null) return;
            var hp = player.GetComponent<Health>();
            if (hp != null) hp.SetCurrent(hp.Max * HpPercent);
            var kit = player.GetComponent<PlayerCombat>();
            if (kit != null)
            {
                if (MaxPotions > 0) kit.maxPotions = Mathf.Min(PlayerCombat.MaxFlaskSlots, Mathf.Max(kit.maxPotions, MaxPotions));   // capped at 5 slots
                kit.SetPotions(Potions);
            }
            HasCarry = false;
        }
    }
}
