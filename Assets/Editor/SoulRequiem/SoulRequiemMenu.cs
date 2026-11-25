#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulRequiem.Gameplay;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// "Soul Requiem" MENU — the ONLY place that declares the project's [MenuItem]s (builders have no menus of their own; add new commands here).
    ///   • Build Everything      : rebuilds the WHOLE game in dependency order (Title -> Map 1 -> Map 2 -> Map 3 -> Map 4, each map: scene -> enemies -> sub-maps),
    ///                             progress bar, per-step error capture, summary table (which step failed, how long), reopens the scene you were working on.
    ///   • Build Map/…           : ONE click for a whole map (scene + enemies + sub-maps + sync enemy lists to sub-maps). Build Current Map = the open map.
    ///   • Scenes/… , Enemies/… : single steps (advanced). All Scenes / All Enemies cover every map including Map 4, The Margin, Drill Yard, Title.
    ///   • Player/…              : builds the player in the open scene (Ren Animator, Ren Robe Animator, feet sorting, feet collider).
    ///   • Utilities/…           : quick scene opening, re-sort Build Settings (Title first, maps in story order), sync enemy lists to sub-maps.
    /// Calls: Map1/2/3/4EnemyBuilder, Map2SceneBuilder, Map2bBuilder, Map3bBuilder, TitleSceneBuilder, RenAnimatorBuilder, RenRobeAnimatorBuilder,
    /// PlayerFeetSorting, EnemyDefinitionTools, EnemyAnimatorDoctor.
    /// </summary>
    public static class SoulRequiemMenu
    {
        const string Root = "Soul Requiem/";
        const string MapsMenu = Root + "Build Map/";
        const string ScenesMenu = Root + "Scenes/";
        const string EnemiesMenu = Root + "Enemies/";
        const string PlayerMenu = Root + "Player/";
        const string UtilMenu = Root + "Utilities/";

        // ------------------------------------------------------------------ scene table (order = Build Settings order)
        const string Title = "Assets/Scenes/Title.unity";
        const string Map1 = "Assets/Scenes/Map1_GreystonePlains.unity";
        const string Map1b = "Assets/Scenes/Map1b_HermitsHollow.unity";
        const string Map2 = "Assets/Scenes/Map2_UndercityOfVael.unity";
        const string Map2b = "Assets/Scenes/Map2b_KrovenDeep.unity";
        const string Map3 = "Assets/Scenes/Map3_ValeOfGraves.unity";
        const string Map3b = "Assets/Scenes/Map3b_KingsBarrow.unity";
        const string Map4 = "Assets/Scenes/Map4_CatacombsOfNaming.unity";
        const string Map4m = "Assets/Scenes/Map4m_TheMargin.unity";
        const string Map4d = "Assets/Scenes/Map4d_DrillYard.unity";
        const string Map4n = "Assets/Scenes/Map4n_TheUnwritten.unity";   // domain pha 3 Absalom
        static readonly string[] SceneOrder = { Title, Map1, Map1b, Map2, Map2b, Map3, Map3b, Map4, Map4m, Map4d, Map4n };

        // ================================================================== BUILD EVERYTHING
        [MenuItem(Root + "Build Everything", false, 0)]
        static void BuildEverything()
        {
            if (!Ready()) return;
            if (!EditorUtility.DisplayDialog("Soul Requiem — Build Everything",
                    "Rebuild every scene, every enemy list and the Build Settings, in dependency order:\n\n" +
                    "Title → Map 1 (+1b) → Map 2 (+2b) → Map 3 (+3b) → Map 4 (+Margin, Drill Yard, The Unwritten)\n\n" +
                    "This takes a few minutes. Unsaved scene changes will be asked about first.", "Build", "Cancel")) return;
            var steps = new List<Step> { new Step("Title scene", TitleSceneBuilder.Build) };
            steps.AddRange(Map1Steps()); steps.AddRange(Map2Steps()); steps.AddRange(Map3Steps()); steps.AddRange(Map4Steps());
            steps.Add(new Step("Build Settings order", SyncBuildSettings));
            Run("Build Everything", steps);
        }

        // ================================================================== BUILD MAP (one click for a whole map)
        [MenuItem(MapsMenu + "Build Current Map", false, 100)]
        static void BuildCurrentMap()
        {
            if (!Ready()) return;
            string p = EditorSceneManager.GetActiveScene().path;
            if (p == Map1 || p == Map1b) Run("Map 1", Map1Steps());
            else if (p == Map2 || p == Map2b) Run("Map 2", Map2Steps());
            else if (p == Map3 || p == Map3b) Run("Map 3", Map3Steps());
            else if (p == Map4 || p == Map4m || p == Map4d || p == Map4n) Run("Map 4", Map4Steps());
            else if (p == Title) Run("Title", new List<Step> { new Step("Title scene", TitleSceneBuilder.Build) });
            else EditorUtility.DisplayDialog("Soul Requiem", "The open scene is not one of the game maps:\n" + (string.IsNullOrEmpty(p) ? "(unsaved scene)" : p), "OK");
        }

        [MenuItem(MapsMenu + "Map 1 - Greystone Plains (+ Hermit's Hollow)", false, 111)]
        static void BuildMap1All() { if (Ready()) Run("Map 1", Map1Steps()); }

        [MenuItem(MapsMenu + "Map 2 - Undercity of Vael (+ Kroven Deep)", false, 112)]
        static void BuildMap2All() { if (Ready()) Run("Map 2", Map2Steps()); }

        [MenuItem(MapsMenu + "Map 3 - Vale of Graves (+ King's Barrow)", false, 113)]
        static void BuildMap3All() { if (Ready()) Run("Map 3", Map3Steps()); }

        [MenuItem(MapsMenu + "Map 4 - Catacombs of Naming (+ Margin, Drill Yard, Unwritten)", false, 114)]
        static void BuildMap4All() { if (Ready()) Run("Map 4", Map4Steps()); }

        [MenuItem(MapsMenu + "Title Screen", false, 125)]
        static void BuildTitleAll() { if (Ready()) Run("Title", new List<Step> { new Step("Title scene", TitleSceneBuilder.Build) }); }

        // Map 1: hand-made scene (no scene builder) — enemies only; Map 1b is copied from Map 1.
        static List<Step> Map1Steps() => new List<Step>
        {
            new Step("Map 1 enemies", BuildMap1EnemiesCore),
            new Step("Map 1b scene (Hermit's Hollow)", Map2SceneBuilder.BuildMap1b),
            new Step("Map 1b enemy list ← Map 1", () => SyncEnemies(Map1, Map1b)),
        };

        static List<Step> Map2Steps() => new List<Step>
        {
            new Step("Map 2 scene (Undercity of Vael)", Map2SceneBuilder.Build),
            new Step("Map 2 enemies", Map2EnemyBuilder.BuildAll),
            new Step("Map 2b scene (Kroven Deep) + Last Guard", Map2SceneBuilder.BuildMap2b),
            new Step("Map 2b enemy list ← Map 2", () => SyncEnemies(Map2, Map2b)),
        };

        static List<Step> Map3Steps() => new List<Step>
        {
            new Step("Map 3 scene (Vale of Graves)", Map2SceneBuilder.BuildMap3),
            new Step("Map 3 enemies", Map3EnemyBuilder.BuildAll),
            new Step("Map 3b scene (King's Barrow) + Red Field", Map2SceneBuilder.BuildMap3b),
            new Step("Map 3b enemy list ← Map 3", () => SyncEnemies(Map3, Map3b)),
        };

        // the Map 4 scene step also builds the Map 4 enemies (Map4EnemyBuilder) once the scene can be opened.
        static List<Step> Map4Steps() => new List<Step>
        {
            new Step("Map 4 scene + enemies (Catacombs)", Map2SceneBuilder.BuildMap4),
            new Step("Map 4m scene (The Margin)", Map2SceneBuilder.BuildMap4Margin),
            new Step("Map 4d scene (Drill Yard)", Map2SceneBuilder.BuildMap4Drill),
            new Step("Map 4n scene (The Unwritten)", Map2SceneBuilder.BuildMap4Unwritten),
            new Step("Map 4m / 4d / 4n enemy lists ← Map 4", () => { SyncEnemies(Map4, Map4m); SyncEnemies(Map4, Map4d); SyncEnemies(Map4, Map4n); }),
        };

        // ================================================================== SCENES (single steps)
        [MenuItem(ScenesMenu + "Title", false, 200)]
        static void SceneTitle() { if (Ready()) TitleSceneBuilder.Build(); }

        [MenuItem(ScenesMenu + "Map 1b - Hermit's Hollow", false, 211)]
        static void SceneMap1b() { if (Ready()) Map2SceneBuilder.BuildMap1b(); }

        [MenuItem(ScenesMenu + "Map 2 - Undercity of Vael", false, 212)]
        static void SceneMap2() { if (Ready()) Map2SceneBuilder.Build(); }

        [MenuItem(ScenesMenu + "Map 2b - Kroven Deep", false, 213)]
        static void SceneMap2b() { if (Ready()) Map2SceneBuilder.BuildMap2b(); }

        [MenuItem(ScenesMenu + "Map 3 - Vale of Graves", false, 214)]
        static void SceneMap3() { if (Ready()) Map2SceneBuilder.BuildMap3(); }

        [MenuItem(ScenesMenu + "Map 3b - King's Barrow", false, 215)]
        static void SceneMap3b() { if (Ready()) Map2SceneBuilder.BuildMap3b(); }

        [MenuItem(ScenesMenu + "Map 4 - Catacombs of Naming (+ enemies)", false, 216)]
        static void SceneMap4() { if (Ready()) Map2SceneBuilder.BuildMap4(); }

        [MenuItem(ScenesMenu + "Map 4m - The Margin", false, 217)]
        static void SceneMap4m() { if (Ready()) Map2SceneBuilder.BuildMap4Margin(); }

        [MenuItem(ScenesMenu + "Map 4d - Drill Yard", false, 218)]
        static void SceneMap4d() { if (Ready()) Map2SceneBuilder.BuildMap4Drill(); }

        [MenuItem(ScenesMenu + "Map 4n - The Unwritten", false, 219)]
        static void SceneMap4n() { if (Ready()) Map2SceneBuilder.BuildMap4Unwritten(); }

        /// <summary>Every map scene, in copy order (Map 2 from Map 1, Map 3 from Map 2, 3b/4 from Map 3, 4m/4d/4n from Map 4).</summary>
        [MenuItem(ScenesMenu + "All Scenes", false, 230)]
        static void SceneAll()
        {
            if (!Ready()) return;
            Run("All Scenes", new List<Step>
            {
                new Step("Title", TitleSceneBuilder.Build),
                new Step("Map 1b", Map2SceneBuilder.BuildMap1b),
                new Step("Map 2", Map2SceneBuilder.Build),
                new Step("Map 2b", Map2SceneBuilder.BuildMap2b),
                new Step("Map 3", Map2SceneBuilder.BuildMap3),
                new Step("Map 3b", Map2SceneBuilder.BuildMap3b),
                new Step("Map 4 (+ enemies)", Map2SceneBuilder.BuildMap4),
                new Step("Map 4m", Map2SceneBuilder.BuildMap4Margin),
                new Step("Map 4d", Map2SceneBuilder.BuildMap4Drill),
                new Step("Map 4n", Map2SceneBuilder.BuildMap4Unwritten),
                new Step("Build Settings order", SyncBuildSettings),
            });
        }

        // ================================================================== ENEMIES (single steps)
        [MenuItem(EnemiesMenu + "Map 1", false, 300)]
        static void EnemiesMap1() { if (Ready()) BuildMap1EnemiesCore(); }

        [MenuItem(EnemiesMenu + "Map 2", false, 301)]
        static void EnemiesMap2() { if (Ready()) Map2EnemyBuilder.BuildAll(); }

        [MenuItem(EnemiesMenu + "Map 3", false, 302)]
        static void EnemiesMap3() { if (Ready()) Map3EnemyBuilder.BuildAll(); }

        [MenuItem(EnemiesMenu + "Map 4", false, 303)]
        static void EnemiesMap4() { if (Ready()) Map4EnemyBuilder.BuildAll(); }

        [MenuItem(EnemiesMenu + "All Enemies (Map 1-4)", false, 315)]
        static void EnemiesAll()
        {
            if (!Ready()) return;
            Run("All Enemies", new List<Step>
            {
                new Step("Map 1 enemies", BuildMap1EnemiesCore),
                new Step("Map 2 enemies", Map2EnemyBuilder.BuildAll),
                new Step("Map 3 enemies", Map3EnemyBuilder.BuildAll),
                new Step("Map 4 enemies", Map4EnemyBuilder.BuildAll),
                new Step("Sub-map enemy lists", SyncAllSubMaps),
            });
        }

        [MenuItem(EnemiesMenu + "Create Missing Enemy Definitions", false, 330)]
        static void CreateDefinitions() { if (Ready()) EnemyDefinitionTools.CreateFromPrefabs(); }

        [MenuItem(EnemiesMenu + "Check and Repair Enemy Animators", false, 331)]
        static void RepairAnimators() { if (Ready()) EnemyAnimatorDoctor.Run(); }

        /// <summary>The Map 1 builder attaches director / HUD to the OPEN scene -> open the Map 1 scene first (running it on the wrong scene breaks it).</summary>
        static void BuildMap1EnemiesCore()
        {
            if (EditorSceneManager.GetActiveScene().path != Map1)
            {
                if (!File.Exists(Map1)) { Debug.LogError("[Soul Requiem] Missing " + Map1); return; }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(Map1, OpenSceneMode.Single);
            }
            Map1EnemyBuilder.BuildAll();
        }

        // ================================================================== PLAYER (open scene)
        /// <summary>All 3 steps in order: Animator -> feet pivots (sorting) -> feet collider.</summary>
        [MenuItem(PlayerMenu + "Rebuild Player (Open Scene)", false, 400)]
        static void RebuildPlayer()
        {
            if (!Ready()) return;
            RenAnimatorBuilder.Run();                                   // Ren v2 (unarmed body + weapon layer)
            PlayerFeetSorting.Run();
            PlayerFeetSorting.FeetCollider();
            Debug.Log("[Soul Requiem] Player rebuilt: animator, feet sorting, feet collider. Save the scene (Ctrl+S).");
        }

        [MenuItem(PlayerMenu + "Ren Animator (v2)", false, 411)]
        static void RenAnimator() { if (Ready()) RenAnimatorBuilder.Run(); }

        [MenuItem(PlayerMenu + "Ren Robe Animator (Map 4)", false, 412)]
        static void RenRobeAnimator() { if (Ready()) RenRobeAnimatorBuilder.Run(); }

        [MenuItem(PlayerMenu + "Sort Player by Feet (Sprite Pivots)", false, 413)]
        static void PlayerFeet() { if (Ready()) PlayerFeetSorting.Run(); }

        [MenuItem(PlayerMenu + "Fit Player Collider to Feet", false, 414)]
        static void PlayerCollider() { if (Ready()) PlayerFeetSorting.FeetCollider(); }

        // ================================================================== UTILITIES
        [MenuItem(UtilMenu + "Open Scene/Title", false, 500)] static void OpenTitle() => Open(Title);
        [MenuItem(UtilMenu + "Open Scene/Map 1 - Greystone Plains", false, 511)] static void OpenMap1() => Open(Map1);
        [MenuItem(UtilMenu + "Open Scene/Map 1b - Hermit's Hollow", false, 512)] static void OpenMap1b() => Open(Map1b);
        [MenuItem(UtilMenu + "Open Scene/Map 2 - Undercity of Vael", false, 513)] static void OpenMap2() => Open(Map2);
        [MenuItem(UtilMenu + "Open Scene/Map 2b - Kroven Deep", false, 514)] static void OpenMap2b() => Open(Map2b);
        [MenuItem(UtilMenu + "Open Scene/Map 3 - Vale of Graves", false, 515)] static void OpenMap3() => Open(Map3);
        [MenuItem(UtilMenu + "Open Scene/Map 3b - King's Barrow", false, 516)] static void OpenMap3b() => Open(Map3b);
        [MenuItem(UtilMenu + "Open Scene/Map 4 - Catacombs of Naming", false, 517)] static void OpenMap4() => Open(Map4);
        [MenuItem(UtilMenu + "Open Scene/Map 4m - The Margin", false, 518)] static void OpenMap4m() => Open(Map4m);
        [MenuItem(UtilMenu + "Open Scene/Map 4d - Drill Yard", false, 519)] static void OpenMap4d() => Open(Map4d);
        [MenuItem(UtilMenu + "Open Scene/Map 4n - The Unwritten", false, 520)] static void OpenMap4n() => Open(Map4n);

        [MenuItem(UtilMenu + "Sort Build Settings (Title first)", false, 540)]
        static void SortBuildSettings() { if (Ready()) SyncBuildSettings(); }

        [MenuItem(UtilMenu + "Sync Enemy Lists to Sub-Maps", false, 541)]
        static void SyncSubMaps() { if (Ready() && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Run("Sync enemy lists", new List<Step> { new Step("Sub-map enemy lists", SyncAllSubMaps) }); }

        // ================================================================== infrastructure
        static bool Ready()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling) return true;
            EditorUtility.DisplayDialog("Soul Requiem", EditorApplication.isCompiling ? "Scripts are still compiling — try again in a moment." : "Exit Play Mode first.", "OK");
            return false;
        }

        static void Open(string path)
        {
            if (!Ready()) return;
            if (!File.Exists(path)) { EditorUtility.DisplayDialog("Soul Requiem", "Scene not built yet:\n" + path, "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        sealed class Step
        {
            public readonly string Name; public readonly Action Act;
            public Step(string name, Action act) { Name = name; Act = act; }
        }

        /// <summary>
        /// Run a chain of steps: progress bar (cancellable between steps), each step catches exceptions + counts Console errors, timed;
        /// at the end prints a summary table and reopens the scene the user was working on.
        /// </summary>
        static void Run(string title, List<Step> steps)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string back = EditorSceneManager.GetActiveScene().path;
            var report = new List<string>();
            int failed = 0, errors = 0;
            var total = Stopwatch.StartNew();
            Application.LogCallback hook = (msg, trace, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            Application.logMessageReceived += hook;
            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Soul Requiem — " + title, $"{i + 1}/{steps.Count}  {steps[i].Name}", (float)i / steps.Count))
                    { report.Add($"  ✗ cancelled before: {steps[i].Name}"); failed++; break; }
                    int before = errors;
                    var sw = Stopwatch.StartNew();
                    try { steps[i].Act(); }
                    catch (Exception e) { errors++; Debug.LogException(e); }
                    sw.Stop();
                    bool ok = errors == before;
                    if (!ok) failed++;
                    report.Add($"  {(ok ? "✓" : "✗")} {steps[i].Name}  ({sw.Elapsed.TotalSeconds:0.0}s{(ok ? "" : $", {errors - before} error(s)")})");
                }
            }
            finally
            {
                Application.logMessageReceived -= hook;
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
            total.Stop();
            if (!string.IsNullOrEmpty(back) && File.Exists(back) && EditorSceneManager.GetActiveScene().path != back)
                EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            string summary = $"[Soul Requiem] {title}: {steps.Count - failed}/{steps.Count} steps OK in {total.Elapsed.TotalSeconds:0}s\n" + string.Join("\n", report);
            if (failed == 0) Debug.Log(summary); else Debug.LogWarning(summary);
            EditorUtility.DisplayDialog("Soul Requiem — " + title,
                (failed == 0 ? "All steps finished." : $"{failed} step(s) reported errors — see the Console.") + "\n\n" + string.Join("\n", report), "OK");
        }

        /// <summary>Build Settings: Title first (startup scene), maps in story order; other scenes (if any) stay after them.</summary>
        static void SyncBuildSettings()
        {
            var old = EditorBuildSettings.scenes.ToList();
            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in SceneOrder) if (File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            foreach (var e in old) if (!SceneOrder.Contains(e.path) && File.Exists(e.path)) list.Add(e);
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Soul Requiem] Build Settings: " + string.Join(", ", list.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }

        static void SyncAllSubMaps()
        {
            SyncEnemies(Map1, Map1b);
            SyncEnemies(Map2, Map2b);
            SyncEnemies(Map3, Map3b);
            SyncEnemies(Map4, Map4m);
            SyncEnemies(Map4, Map4d);
            SyncEnemies(Map4, Map4n);
        }

        /// <summary>
        /// Sub-maps (1b, 2b, 3b, 4m, 4d) copy their parent map's scene ONCE when created, so enemies added to the parent later are missing there.
        /// Merge the parent's enemy list INTO the sub-map: same name -> update the prefab; new name -> add. The sub-map's own enemies (Last Guard,
        /// The Red Field...) are kept.
        /// </summary>
        static void SyncEnemies(string parent, string child)
        {
            if (!File.Exists(parent) || !File.Exists(child)) { Debug.LogWarning($"[Soul Requiem] Sync skipped (scene missing): {parent} -> {child}"); return; }
            EditorSceneManager.OpenScene(parent, OpenSceneMode.Single);
            var pd = Object.FindFirstObjectByType<MapEncounterDirector>();
            if (pd == null || pd.enemies == null) { Debug.LogWarning("[Soul Requiem] No MapEncounterDirector in " + parent); return; }
            var src = pd.enemies.Where(e => e != null && e.prefab != null && !string.IsNullOrEmpty(e.enemyName))
                                .Select(e => (e.enemyName, e.prefab)).ToList();
            var scene = EditorSceneManager.OpenScene(child, OpenSceneMode.Single);
            var cd = Object.FindFirstObjectByType<MapEncounterDirector>();
            if (cd == null) { Debug.LogWarning("[Soul Requiem] No MapEncounterDirector in " + child); return; }
            var list = cd.enemies != null ? new List<MapEncounterDirector.EnemyEntry>(cd.enemies) : new List<MapEncounterDirector.EnemyEntry>();
            int added = 0, updated = 0;
            foreach (var (name, prefab) in src)
            {
                var e = list.FirstOrDefault(x => x != null && x.enemyName == name);
                if (e == null) { list.Add(new MapEncounterDirector.EnemyEntry { enemyName = name, prefab = prefab }); added++; }
                else if (e.prefab != prefab) { e.prefab = prefab; updated++; }
            }
            if (added == 0 && updated == 0) return;
            Undo.RecordObject(cd, "Sync enemy list");
            cd.enemies = list;
            EditorUtility.SetDirty(cd);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Soul Requiem] {Path.GetFileNameWithoutExtension(child)} enemy list ← {Path.GetFileNameWithoutExtension(parent)}: +{added} new, {updated} updated.");
        }
    }
}
#endif
