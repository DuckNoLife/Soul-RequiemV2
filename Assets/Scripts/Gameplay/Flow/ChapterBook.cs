using System.Collections.Generic;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// CHAPTER list for the Chapters screen on the Title. One chapter = one main map plus its sub-maps with the same number
    /// (Map2 + Map2b, Map4 + 4m / 4d / 4n...). The chapter number comes from the scene name "Map&lt;number&gt;...".
    /// Taglines must not spoil the story.
    /// Hidden blades = katanas found in the chapter, counting only areas that EXIST (Eventide / Accord / Namesake are added when their areas are built).
    /// Read by: the Title Chapters screen, SaveGame (chapter progress).
    /// </summary>
    public static class ChapterBook
    {
        public sealed class Chapter
        {
            public int n;
            public string roman, name, entry, tagline, thumb;
            public string[] blades;
            /// <summary>Chapters present in the game (Chapter V not made yet -> "Coming soon").</summary>
            public bool Playable => !string.IsNullOrEmpty(entry);
        }

        public static readonly Chapter[] All =
        {
            new Chapter { n = 1, roman = "I", name = "Greystone Plains", entry = "Map1_GreystonePlains", thumb = "chapter_1",
                tagline = "Where the road began.", blades = new[] { "katana_oathbreaker" } },
            new Chapter { n = 2, roman = "II", name = "Undercity of Vael", entry = "Map2_UndercityOfVael", thumb = "chapter_2",
                tagline = "A city that buried its own.", blades = new[] { "katana_moonlit", "katana_ironcrown" } },
            new Chapter { n = 3, roman = "III", name = "Vale of Graves", entry = "Map3_ValeOfGraves", thumb = "chapter_3",
                tagline = "The dead still keep their oaths.", blades = new[] { "katana_crimson" } },
            new Chapter { n = 4, roman = "IV", name = "Catacombs of Naming", entry = "Map4_CatacombsOfNaming", thumb = "chapter_4",
                tagline = "Every name is written somewhere.", blades = new string[0] },
            // Chapter V (The Faint Lake) has no map yet -> not shown on the Chapters screen. When Map 5 exists: add n = 5 back, its entry, thumb "chapter_5".
        };

        public static Chapter Get(int n) => n >= 1 && n <= All.Length ? All[n - 1] : null;

        /// <summary>Chapter number of a scene ("Map3b_KingsBarrow" -> 3); 0 if not a map (Title...).</summary>
        public static int Of(string scene)
        {
            if (string.IsNullOrEmpty(scene) || scene.Length < 4 || !scene.StartsWith("Map")) return 0;
            char c = scene[3];
            if (c < '1' || c > '9') return 0;
            if (scene.Length > 4 && char.IsDigit(scene[4])) return 0;      // in case of Map10+
            return c - '0';
        }

        /// <summary>Chapter number if the scene is the chapter's ENTRY (main map), otherwise 0.</summary>
        public static int EntryChapter(string scene)
        {
            foreach (var c in All) if (c.Playable && c.entry == scene) return c.n;
            return 0;
        }

        private static readonly Dictionary<string, string> Areas = new Dictionary<string, string>
        {
            { "Map1_GreystonePlains", "Greystone Plains" }, { "Map1b_HermitsHollow", "Hermit's Hollow" },
            { "Map2_UndercityOfVael", "Undercity of Vael" }, { "Map2b_KrovenDeep", "Kroven Deep" },
            { "Map3_ValeOfGraves", "Vale of Graves" }, { "Map3b_KingsBarrow", "King's Barrow" },
            { "Map4_CatacombsOfNaming", "Catacombs of Naming" }, { "Map4m_TheMargin", "The Margin" },
            { "Map4d_DrillYard", "Drill Yard" }, { "Map4n_TheUnwritten", "The Unwritten" },
        };

        /// <summary>Display region name of a scene (falls back to the chapter name).</summary>
        public static string AreaName(string scene)
        {
            if (!string.IsNullOrEmpty(scene) && Areas.TryGetValue(scene, out var a)) return a;
            var c = Get(Of(scene));
            return c != null ? c.name : scene;
        }
    }
}
