using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Saves progress at rest points (campfires / shrines). One save slot: a JSON file in Application.persistentDataPath.
    ///   Save : every time a rest point is lit / used (MapTriggerHandler, RequiemMenus) -> SaveGame.Save(director).
    ///   Load : Continue on the title screen -> SaveGame.RequestLoad() then load the scene; MapEncounterDirector.Start calls TryTake.
    ///   New  : New Game -> SaveGame.Delete().
    /// Saved: world flags (cleared zones, quests, seen tutorials, picked items "picked_*", lit rest points "lit_*"), respawn point,
    /// flask count / max. A zone in progress is not saved (re-entering restarts it — same as dying).
    /// Save v2 for the CHAPTERS screen:
    ///   * `groups`: flags of EACH visited map group (flag_group) + that group's last campfire -> revisiting an old chapter shows the right progress
    ///     (cleared zones, opened chests — cannot be looted again), continuing from the chapter's last campfire.
    ///   * `chapters`: chapters reached. Reaching a new chapter entry for the first time (including New Game entering Map 1) auto-saves (MapEncounterDirector).
    ///   * The Editor and builds use 2 different files: sharing one file in AppData\LocalLow made a build on the dev machine
    ///     see the Editor's save ("Continue shown without ever playing").
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 2;

        [Serializable]
        public class Group
        {
            public string id = "";             // the map's flag_group (default = scene name)
            public int chapter;                 // ChapterBook.Of(scene)
            public string scene = "";           // scene of the last campfire (hasPoint) / last scene visited
            public bool hasPoint;
            public float rx, ry;
            public string[] flags = new string[0];
        }

        [Serializable]
        public class Data
        {
            public int version = Version;
            public string scene = "";
            public string[] flags = new string[0];
            public float rx, ry;
            public int potions, maxPotions;
            public string[] owned = new string[0], equipped = new string[0];   // skill loadout (ids) — Continue keeps the skills
            public float healPercent;              // bronze flasks (flask_upgrade) make each drink heal more
            public string weapon = "";             // id of the equipped katana (weapon_* flags live in `flags`)
            public string savedAt = "";
            public Group[] groups = new Group[0];
            public int[] chapters = new int[0];

            /// <summary>Only used when loading from the Chapters screen — no campfire in the chapter -> stand at the map entry.</summary>
            [NonSerialized] public bool noPoint;
            /// <summary>Text shown when entering the map (instead of "Progress restored...").</summary>
            [NonSerialized] public string note = "";
        }

        private static bool loadRequested;
        private static Data pending;
#if UNITY_EDITOR
        private const string FileName = "soulrequiem_save.json";
#else
        private const string FileName = "soulrequiem_slot1.json";      // separate from the Editor's file
#endif
        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists => File.Exists(FilePath);

        /// <summary>Scene of the save (empty if none / corrupt).</summary>
        public static string SavedScene
        {
            get { var d = Read(); return d != null ? d.scene : ""; }
        }

        /// <summary>Read the save (null if none / corrupt / no scene).</summary>
        public static Data Peek()
        {
            var d = Read();
            return d != null && !string.IsNullOrEmpty(d.scene) ? d : null;
        }

        /// <summary>Continue: the next map entry loads the save (if it is the same scene). Pass `overrideData` to load a custom build (Chapters).</summary>
        public static void RequestLoad(Data overrideData = null) { loadRequested = true; pending = overrideData; }

        public static void Delete()
        {
            loadRequested = false;
            pending = null;
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not delete the save: {e.Message}"); }
        }

        /// <summary>Take the save for this scene if the player chose Continue / Chapters (only once).</summary>
        public static bool TryTake(string scene, out Data data)
        {
            data = null;
            if (!loadRequested) return false;
            loadRequested = false;
            data = pending ?? Read();
            pending = null;
            return data != null && data.scene == scene;
        }

        // ----------------------------------------------------------------- chapters

        /// <summary>Chapters reached. Old saves (v1, no `chapters`): every chapter from 1 to the chapter of the saved scene.</summary>
        public static HashSet<int> ReachedChapters(Data d)
        {
            var set = new HashSet<int>();
            if (d == null) return set;
            if (d.chapters != null) foreach (var c in d.chapters) if (c > 0) set.Add(c);
            if (d.groups != null) foreach (var g in d.groups) if (g != null && g.chapter > 0) set.Add(g.chapter);
            int cur = ChapterBook.Of(d.scene);
            if (cur > 0)
            {
                set.Add(cur);
                if (d.version < 2) for (int i = 1; i < cur; i++) set.Add(i);
            }
            return set;
        }

        /// <summary>Flag group representing chapter n (groups with a campfire first, then the main map's group). Old saves: built from the save itself.</summary>
        public static Group GroupFor(Data d, int n)
        {
            if (d == null) return null;
            Group best = null;
            var ch = ChapterBook.Get(n);
            if (d.groups != null)
                foreach (var g in d.groups)
                {
                    if (g == null || g.chapter != n) continue;
                    if (best == null || Score(g, d, ch) > Score(best, d, ch)) best = g;
                }
            if (best == null && ChapterBook.Of(d.scene) == n)
                best = new Group { id = d.scene, chapter = n, scene = d.scene, hasPoint = true, rx = d.rx, ry = d.ry, flags = d.flags };
            return best;
        }

        // a campfire wins; with equal kind, the group containing the saved scene / main map wins
        private static int Score(Group x, Data d, ChapterBook.Chapter ch) =>
            (x.hasPoint ? 4 : 0) + (x.scene == d.scene ? 2 : 0) + (ch != null && x.scene == ch.entry ? 1 : 0);

        /// <summary>Every flag of the save (all groups) — used to count hidden blades found.</summary>
        public static HashSet<string> AllFlags(Data d)
        {
            var set = new HashSet<string>();
            if (d == null) return set;
            if (d.flags != null) set.UnionWith(d.flags);
            if (d.groups != null) foreach (var g in d.groups) if (g != null && g.flags != null) set.UnionWith(g.flags);
            return set;
        }

        /// <summary>The scene is the entry of a chapter NOT yet in the save -> should auto-save (MapEncounterDirector.Start).</summary>
        public static bool IsNewChapter(string scene)
        {
            int c = ChapterBook.EntryChapter(scene);
            if (c <= 0) return false;
            var d = Read();
            return d == null || !ReachedChapters(d).Contains(c);
        }

        /// <summary>
        /// Build the load data for the Chapters screen: chapter n's flags + books / katanas (book_*, weapon_*) of the whole journey, loadout / flasks / sword as in the save,
        /// standing at the chapter's last campfire (otherwise the main map entry). Null if the chapter was never reached / does not exist.
        /// </summary>
        public static Data BuildChapterLoad(int n)
        {
            var save = Peek();
            var ch = ChapterBook.Get(n);
            if (save == null || ch == null || !ch.Playable || !ReachedChapters(save).Contains(n)) return null;
            var g = GroupFor(save, n);
            var d = JsonUtility.FromJson<Data>(JsonUtility.ToJson(save));
            var flags = new HashSet<string>();
            if (g != null && g.flags != null) flags.UnionWith(g.flags);
            foreach (var f in AllFlags(save))
                if (f.StartsWith("book_") || f.StartsWith(WeaponRing.Prefix)) flags.Add(f);
            d.flags = new List<string>(flags).ToArray();
            bool point = g != null && g.hasPoint && !string.IsNullOrEmpty(g.scene) && Application.CanStreamedLevelBeLoaded(g.scene);
            d.scene = point ? g.scene : ch.entry;
            d.rx = point ? g.rx : 0f;
            d.ry = point ? g.ry : 0f;
            d.noPoint = !point;
            d.note = $"Chapter {ch.roman} - {ch.name}";
            return d;
        }

        // ------------------------------------------------------------------ save

        public static void Save(MapEncounterDirector dir, Vector2 respawn)
        {
            if (dir == null || dir.Player == null) return;
            var kit = dir.Player.GetComponent<PlayerCombat>();
            var d = new Data
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                rx = respawn.x, ry = respawn.y,
                potions = kit != null ? kit.Potions : 0,
                maxPotions = kit != null ? kit.maxPotions : 3,
                healPercent = kit != null ? kit.healPercent : 0f,
                weapon = kit != null && kit.Weapon != null ? kit.Weapon.id : "",
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            var lo = dir.Player.GetComponent<PlayerLoadout>();
            if (lo != null)
            {
                var ow = new List<string>();
                foreach (var o in lo.owned) if (o != null) ow.Add(o.id);
                d.owned = ow.ToArray();
                d.equipped = new string[PlayerLoadout.SlotCount];
                for (int i = 0; i < PlayerLoadout.SlotCount; i++) d.equipped[i] = lo.slots[i] != null ? lo.slots[i].id : "";
            }
            d.flags = new List<string>(dir.Flags.All).ToArray();

            // merge map group progress — old save < groups visited this session (GameSession) < current group (newest)
            var old = Read();
            var map = new Dictionary<string, Group>();
            if (old != null && old.groups != null)
                foreach (var g in old.groups) if (g != null && !string.IsNullOrEmpty(g.id)) map[g.id] = g;
            foreach (var s in GameSession.SnapshotGroups())
            {
                if (!map.TryGetValue(s.id, out var g)) { g = new Group { id = s.id }; map[s.id] = g; }
                g.flags = s.flags;
                if (g.chapter <= 0) g.chapter = ChapterBook.Of(s.scene);
                if (!g.hasPoint && !string.IsNullOrEmpty(s.scene)) g.scene = s.scene;
            }
            string gid = string.IsNullOrEmpty(dir.FlagGroup) ? d.scene : dir.FlagGroup;
            map[gid] = new Group { id = gid, chapter = ChapterBook.Of(d.scene), scene = d.scene, hasPoint = true, rx = respawn.x, ry = respawn.y, flags = d.flags };
            d.groups = new List<Group>(map.Values).ToArray();
            var reached = ReachedChapters(old);
            int cur = ChapterBook.Of(d.scene);
            if (cur > 0) reached.Add(cur);
            var chs = new List<int>(reached);
            chs.Sort();
            d.chapters = chs.ToArray();

            try { File.WriteAllText(FilePath, JsonUtility.ToJson(d)); Debug.Log($"[Save] Saved ({d.flags.Length} flags, {d.groups.Length} map groups, chapters {string.Join(",", chs)}) -> {FilePath}"); }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not save: {e.Message}"); }
        }

        private static Data Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var d = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                if (d == null) return null;
                // v1 files have no version field -> JsonUtility keeps the default (2). Old saves are recognised by having neither `chapters` nor `groups`.
                if ((d.chapters == null || d.chapters.Length == 0) && (d.groups == null || d.groups.Length == 0)) d.version = 1;
                return d;
            }
            catch (Exception e) { Debug.LogWarning($"[Save] Corrupt save: {e.Message}"); return null; }
        }
    }
}
