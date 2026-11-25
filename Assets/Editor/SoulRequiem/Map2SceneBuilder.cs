#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using SuperTiled2Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// SCENE BUILDER for every map scene copied from another one (despite the file name it builds Map 2, 1b, 2b, 3b, 4, 4m, 4n and 4d).
    /// Menu: Soul Requiem ▸ Scenes ▸ … (declared in SoulRequiemMenu; also run by Build Map / Build Everything).
    /// Map 2 example:
    /// 1. Copy Map1_GreystonePlains to Assets/Scenes/Map2_UndercityOfVael.unity (first time), open that scene.
    /// 2. Remove the old map (every SuperMap), place the map2_undercity_of_vael.tmx prefab, assign it to the EncounterDirector.
    /// 3. Sewer lighting: darker Global Light 2D, every 'light' object in the 'mechanics' layer becomes a point Light2D.
    /// 4. Add Map1_GreystonePlains + Map2_UndercityOfVael to Build Settings so the Map 1 exit can load Map 2.
    /// Safe to rerun any number of times (old lights in the "Map2 Lights" group are rebuilt).
    /// Calls: Map2bBuilder / Map3bBuilder (sub-map bosses), TiledProps (Tiled properties).
    /// </summary>
    public static class Map2SceneBuilder
    {
        const string SrcScene = "Assets/Scenes/Map1_GreystonePlains.unity";
        const string DstScene = "Assets/Scenes/Map2_UndercityOfVael.unity";
        const string MapPathV2 = "Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx";
        const float GlobalIntensity = 0.40f;   // at runtime ZoneAmbience changes per zone

        // Map 2 = the redesigned "Malric's Warren" layout (map2_undercity_of_vael.tmx).
        public static void Build()
        {
            // way down to Kroven in the Records Vault — the sunken stairs + iron bars images (SR-relics) must be pixel images before the map is imported
            FixPixelTextures("Assets/GamePackage/EPIC RPG World - Sewers V1.5/Props/SR-relics");
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-down-kroven.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-grate-kroven.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(MapPathV2, ImportAssetOptions.ForceUpdate);
            BuildScene(SrcScene, DstScene, MapPathV2, "map2_undercity_of_vael");
        }


        // ------------------------------------------------------------------ Map 3: The Vale of Graves
        const string Map3Scene = "Assets/Scenes/Map3_ValeOfGraves.unity";
        const string Map3Pack = "Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6";
        const string Map3Path = Map3Pack + "/TiledMap Editor/Maps/map3_vale_of_graves.tmx";

        public static void BuildMap3()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            FixPixelTextures(Map3Pack + "/TiledMap Editor");
            FixPixelTextures(Map3Pack + "/Tilesets");
            FixPixelTextures(Map3Pack + "/Props");
            AssetDatabase.ImportAsset(Map3Path, ImportAssetOptions.ForceUpdate);
            BuildScene(DstScene, Map3Scene, Map3Path, "map3_vale_of_graves", 0.42f, new Color(0.70f, 0.78f, 0.95f));   // blue moonlit night
        }

        // ------------------------------------------------------------------ Map 3b — King's Barrow secret crypt (split off Map 3)
        const string Map3bScene = "Assets/Scenes/Map3b_KingsBarrow.unity";
        const string Map3bPath = Map3Pack + "/TiledMap Editor/Maps/map3b_kings_barrow.tmx";

        public static void BuildMap3b()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!File.Exists(Map3Scene)) { Debug.LogError("Build Map 3 first (the crypt scene is copied from the Map 3 scene)."); return; }
            FixPixelTextures(Map3Pack + "/TiledMap Editor");
            AssetDatabase.ImportAsset(Map3Pack + "/TiledMap Editor/Tilesets/SR-barrow.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map3bPath, ImportAssetOptions.ForceUpdate);
            BuildScene(Map3Scene, Map3bScene, Map3bPath, "map3b_kings_barrow", 0.25f, new Color(0.55f, 0.60f, 0.85f));   // dark crypt, only candles + skylight
            // outside the tomb walls is the camera background: black as the earth (the map is smaller than the view -> the camera centres and shows the background)
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map3bScene)
            {
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.035f, 1f);
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
            if (EditorSceneManager.GetActiveScene().path == Map3bScene) Map3bBuilder.AddRedField();   // secret boss The Red Field
        }

        // ------------------------------------------------------------------ Map 1b — Hermit's Hollow (Oathbreaker secret area)
        const string Map1Scene = "Assets/Scenes/Map1_GreystonePlains.unity";
        const string Map1Pack = "Assets/GamePackage/ERW - Grass Land 2.0 v2.1";
        const string Map1bScene = "Assets/Scenes/Map1b_HermitsHollow.unity";
        const string Map1bPath = Map1Pack + "/TiledMap Editor/Maps/map1b_hermits_hollow.tmx";

        /// Copy the Map 1 scene (director, HUD, Map 1 enemy list: Spider, Lizard, Cave_Spawner...) then swap the map for map1b.
        /// Same flag group as Map 1 (map property flag_group = Map1_GreystonePlains).
        public static void BuildMap1b()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!File.Exists(Map1Scene)) { Debug.LogError("Missing Map 1 scene (" + Map1Scene + ")."); return; }
            FixPixelTextures(Map1Pack + "/Props/SR-mine");
            AssetDatabase.ImportAsset(Map1Pack + "/TiledMap Editor/Tilesets/SR-mine.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map1bPath, ImportAssetOptions.ForceUpdate);
            BuildScene(Map1Scene, Map1bScene, Map1bPath, "map1b_hermits_hollow", 0.25f, new Color(0.80f, 0.74f, 0.66f));   // dark mine, warm earth glow
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map1bScene)
            {
                cam.backgroundColor = new Color(0.035f, 0.025f, 0.02f, 1f);   // outside the walls = solid earth
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
        }

        // ------------------------------------------------------------------ Map 2b — The Kroven Deep (Ironcrown secret area)
        const string Map2bScene = "Assets/Scenes/Map2b_KrovenDeep.unity";
        const string Map2bPath = "Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2b_kroven_deep.tmx";

        /// Copy the Map 2 scene (director, HUD, Map 2 enemies) then swap the map for map2b; add the boss The Last Guard (Map2bBuilder).
        /// Same flag group as Map 2 (map property flag_group = Map2_UndercityOfVael).
        public static void BuildMap2b()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!File.Exists(DstScene)) { Debug.LogError("Missing Map 2 scene (" + DstScene + ")."); return; }
            FixPixelTextures("Assets/GamePackage/EPIC RPG World - Sewers V1.5/Props/SR-relics");
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-relic-ironcrown.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-kroven.tsx", ImportAssetOptions.ForceUpdate);   // stone stair arch on the way up
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-down-kroven.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-grate-kroven.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map2bPath, ImportAssetOptions.ForceUpdate);
            BuildScene(DstScene, Map2bScene, Map2bPath, "map2b_kroven_deep", 0.24f, new Color(0.62f, 0.74f, 0.70f));   // dark cistern, Ether moss glow
            if (EditorSceneManager.GetActiveScene().path != Map2bScene) return;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.02f, 0.03f, 0.03f, 1f);
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
            Map2bBuilder.AddLastGuard();
        }

        // ------------------------------------------------------------------ Map 4 — The Catacombs of Naming
        const string Map4Scene = "Assets/Scenes/Map4_CatacombsOfNaming.unity";
        const string Map4Prison = "Assets/GamePackage/EPIC RPG World Pack - Old Prison V1.7.1";
        const string Map4Crypt = "Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6";
        // map generated by script (_tools/map4_1117/write_real.py) — prison + crypt layout, animated gates
        const string Map4Path = Map4Crypt + "/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx";

        /// Copy the Map 3 scene (director, HUD) then swap the map for map4_catacombs_of_naming.tmx.
        /// The Map 4 enemy list is assigned afterwards by Map4EnemyBuilder (Enemies ▸ Map 4).
        public static void BuildMap4()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!File.Exists(Map3Scene)) { Debug.LogError("Build Map 3 first (the Map 4 scene is copied from the Map 3 scene)."); return; }
            FixPixelTextures(Map4Prison + "/Tilesets");
            FixPixelTextures(Map4Prison + "/Props");            // Old Prison props (barrels, tables, torture devices, ghost fire)
            FixPixelTextures(Map4Crypt + "/Tilesets");
            FixPixelTextures(Map4Crypt + "/Props");             // Crypt pack props + animated images (torches, candles)
            AssetDatabase.ImportAsset(Map4Crypt + "/TiledMap Editor/Tilesets/SR-m4-art.tsx", ImportAssetOptions.ForceUpdate);   // boss door 128x96
            AssetDatabase.ImportAsset(Map4Path, ImportAssetOptions.ForceUpdate);
            BuildScene(Map3Scene, Map4Scene, Map4Path, "map4_catacombs", 0.3f, new Color(0.70f, 0.74f, 0.95f));
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map4Scene)
            {
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.035f, 1f);
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
            if (EditorSceneManager.GetActiveScene().path == Map4Scene) Map4EnemyBuilder.BuildAll();   // Map 4 enemy list
        }

        // ------------------------------------------------------------------ Map 4m — The Margin (The Registrar's own arena)
        const string Map4MarginScene = "Assets/Scenes/Map4m_TheMargin.unity";
        const string Map4MarginPath = Map4Crypt + "/TiledMap Editor/Maps/map4m_the_margin.tmx";

        /// <summary>Copy the Map 4 scene (director, HUD, Map 4 enemy list) then swap the map for map4m_the_margin.tmx (one ledger page in the void).
        /// Same flag group as Map 4 (map property flag_group = Map4_CatacombsOfNaming). Needs Map 4 (+ Enemies ▸ Map 4) built first.</summary>
        public static void BuildMap4Margin()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!File.Exists(Map4Scene)) { Debug.LogError("Build Map 4 first (The Margin scene is copied from the Map 4 scene)."); return; }
            FixPixelTextures(Map4Crypt + "/Tilesets");
            FixPixelTextures(Map4Crypt + "/Props/SR-trimmed-anim");   // violet candle flames (new images)
            AssetDatabase.ImportAsset(Map4Crypt + "/TiledMap Editor/Tilesets/SR-margin.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map4Crypt + "/TiledMap Editor/Tilesets/SR-margin-art.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map4Crypt + "/TiledMap Editor/Tilesets/SR-animT-candle-violet.tsx", ImportAssetOptions.ForceUpdate);   // violet candle flames
            AssetDatabase.ImportAsset(Map4MarginPath, ImportAssetOptions.ForceUpdate);
            BuildScene(Map4Scene, Map4MarginScene, Map4MarginPath, "map4m_the_margin", 0.24f, new Color(0.62f, 0.55f, 0.9f));   // dark page, violet glow
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map4MarginScene)
            {
                cam.backgroundColor = new Color(0.015f, 0.012f, 0.03f, 1f);   // outside the page = void
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
        }

        // ------------------------------------------------------------------ Map 4n — The Unwritten (domain pha 3 Absalom, boss The Crystal Vessel)
        const string Volcano = "Assets/GamePackage/Epic RPG World - Volcano V1.6";
        const string Map4UnwrittenScene = "Assets/Scenes/Map4n_TheUnwritten.unity";
        const string Map4UnwrittenPath = Volcano + "/TiledMap Editor/Maps/map4n_the_unwritten.tmx";

        /// <summary>Copy the Map 4 scene (director, HUD, Map 4 enemy list) then swap the map for map4n_the_unwritten.tmx (an island floating in the void).
        /// Same flag group as Map 4 (flag_group = Map4_CatacombsOfNaming); Mantras are sealed until flag vessel_named (map property).
        /// Map generated by _tools/s402/gen_domain.py, assets by _tools/s402/make_assets.py + _tools/s406/make_void_deco.py. Needs Map 4 built first.</summary>
        public static void BuildMap4Unwritten()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!File.Exists(Map4Scene)) { Debug.LogError("Build Map 4 first (The Unwritten scene is copied from the Map 4 scene)."); return; }
            if (!File.Exists(Map4UnwrittenPath)) { Debug.LogError("Missing " + Map4UnwrittenPath + " - run python3 _tools/s402/gen_domain.py at the project root."); return; }
            FixPixelTextures(Volcano + "/Tilesets/SR-void");          // void island tileset (2048x2048) — uncompressed, Point filter
            FixPixelTextures(Volcano + "/Props/SR-void");             // recolourable rocks / crystals
            FixPixelTextures("Assets/Resources/VFX/void");            // watchtowers, rock islands, element crystals, light cones, nebula
            FixPixelTextures("Assets/Resources/VFX/vessel");          // / The Crystal Vessel parts
            foreach (var tsx in new[] { "SR-void-main", "SR-void-shadow", "SR-void-props-tiles", "SR-void-props-sprites", "SR-void-deco" })
                AssetDatabase.ImportAsset(Volcano + "/TiledMap Editor/Tilesets/" + tsx + ".tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map4UnwrittenPath, ImportAssetOptions.ForceUpdate);
            BuildScene(Map4Scene, Map4UnwrittenScene, Map4UnwrittenPath, "map4n_the_unwritten", 0.22f, new Color(0.54f, 0.42f, 0.82f));   // violet void
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map4UnwrittenScene)
            {
                cam.backgroundColor = new Color(0.035f, 0.025f, 0.06f, 1f);   // void background (outside the nebula / stars)
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
        }

        // ------------------------------------------------------------------ Map 4d — Burlap's Proving Grounds (drill yard split off Map 4)
        const string Map4DrillScene = "Assets/Scenes/Map4d_DrillYard.unity";
        const string Map4DrillPath = Map4Crypt + "/TiledMap Editor/Maps/map4d_drill_yard.tmx";

        /// <summary>Copy the Map 4 scene (director, HUD, Map 4 enemy list) then swap the map for map4d_drill_yard.tmx (3 drill yards linked by doors).
        /// Same flag group as Map 4 (flag_group = Map4_CatacombsOfNaming), the sword is still taken / Mantras sealed like Map 4. Needs Map 4 (+ Enemies ▸ Map 4) built first.</summary>
        public static void BuildMap4Drill()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!File.Exists(Map4Scene)) { Debug.LogError("Build Map 4 first (the Drill Yard scene is copied from the Map 4 scene)."); return; }
            FixPixelTextures(Map4Prison + "/Tilesets");
            FixPixelTextures(Map4Crypt + "/Tilesets");
            AssetDatabase.ImportAsset(Map4Crypt + "/TiledMap Editor/Tilesets/SR-drill-art.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map4DrillPath, ImportAssetOptions.ForceUpdate);
            BuildScene(Map4Scene, Map4DrillScene, Map4DrillPath, "map4d_drill_yard", 0.3f, new Color(0.70f, 0.74f, 0.95f));
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map4DrillScene)
            {
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.035f, 1f);
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
        }

        /// Large tileset images (1568x2016...) get compressed + capped at 2048 -> blurry, colour shift. Force Point, no compression, max 8192.
        static void FixPixelTextures(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            int n = 0;
            AssetDatabase.StartAssetEditing();                 // batched: one import pass, no tmx reimport after each image
            try {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                if (ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed
                    && ti.maxTextureSize >= 8192 && !ti.mipmapEnabled) continue;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 8192;
                ti.mipmapEnabled = false;
                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();
                n++;
            }
            } finally { AssetDatabase.StopAssetEditing(); }
            if (n > 0) Debug.Log($"[Soul Requiem] Fixed import settings of {n} images in {folder}.");
        }

        static void BuildScene(string srcScene, string dstScene, string mapPath, string mapName) =>
            BuildScene(srcScene, dstScene, mapPath, mapName, GlobalIntensity, new Color(0.78f, 0.86f, 0.95f));

        static void BuildScene(string srcScene, string dstScene, string mapPath, string mapName, float globalIntensity, Color globalColor)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(dstScene)) AssetDatabase.CopyAsset(srcScene, dstScene);
            var scene = EditorSceneManager.OpenScene(dstScene, OpenSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mapPath);
            if (prefab == null) { Debug.LogError("Could not import " + mapPath + " (Ctrl+R and try again)."); return; }

            Vector3 pos = Vector3.zero;
            foreach (var old in Object.FindObjectsByType<SuperMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                pos = old.transform.position;
                Object.DestroyImmediate(old.gameObject);
            }
            var map = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            map.transform.position = pos;
            map.name = mapName;

            var dir = Object.FindFirstObjectByType<MapEncounterDirector>();
            if (dir != null)
            {
                dir.mapRoot = map.transform;
                dir.movePlayerToStart = true;
                EditorUtility.SetDirty(dir);
                if (dir.GetComponent<MapMechanics>() == null) dir.gameObject.AddComponent<MapMechanics>();   // valves, lights, nozzles, puzzles...
                if (dir.GetComponent<ZoneAmbience>() == null) dir.gameObject.AddComponent<ZoneAmbience>();   // ambient light per zone
            }
            else Debug.LogError("The scene has no MapEncounterDirector.");

            // ---------------- lighting
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global) { l.intensity = globalIntensity; l.color = globalColor; EditorUtility.SetDirty(l); }
            var old2 = GameObject.Find("Map2 Lights");
            if (old2 != null) Object.DestroyImmediate(old2);
            var root = new GameObject("Map2 Lights");
            int n = 0;
            foreach (var so in map.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.transform.parent == null || so.transform.parent.name != "mechanics" || (so.m_Type != "light" && so.m_Type != "lamp")) continue;
                var props = so.GetComponent<SuperCustomProperties>();
                float radius = Float(props, "radius", 5f) * 0.32f;
                Color c = ColorUtility.TryParseHtmlString(Str(props, "color", "#ffb860"), out var cc) ? cc : new Color(1f, 0.72f, 0.38f);
                var col = so.GetComponent<Collider2D>();
                Vector3 p = col != null ? (Vector3)col.bounds.center : so.transform.position;
                var go = new GameObject("Light " + so.m_TiledName);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(p.x, p.y, 0f);
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightOuterRadius = radius;
                l.pointLightInnerRadius = radius * Float(props, "inner", 0.25f);
                l.intensity = Float(props, "intensity", 1.1f);   // zone fill lights: large radius, low intensity
                l.color = c;
                l.falloffIntensity = Float(props, "falloff_strength", 0.5f);
                // shaped lit areas (acid surface): rectangle / polygon object + shape=area -> Freeform light
                if (Str(props, "shape", "") == "area" && col != null)
                {
                    l.lightType = Light2D.LightType.Freeform;
                    l.SetShapePath(AreaPath(col, go.transform.position));
                    l.shapeLightFalloffSize = Float(props, "falloff", 2.5f) * 0.32f;
                }
                // signal lights / flag-driven lights: MapMechanics toggles them at runtime
                if (!string.IsNullOrEmpty(Str(props, "lit_when", ""))) l.enabled = false;
                // flickering fire / breathing acid; flag-driven lights also need it to fade when toggled
                string anim = Str(props, "anim", "");
                if (anim != "" || Str(props, "lit_when", "") != "" || Str(props, "off_when", "") != "")
                {
                    var la = go.AddComponent<LightAnimator>();
                    la.mode = anim == "flicker" ? LightAnimator.Mode.Flicker : anim == "pulse" ? LightAnimator.Mode.Pulse : LightAnimator.Mode.Steady;
                    la.amount = Float(props, "anim_amount", anim == "flicker" ? 0.14f : 0.12f);
                    la.speed = Float(props, "anim_speed", anim == "flicker" ? 7f : 0.35f);
                    la.baseIntensity = l.intensity;
                }
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (File.Exists(SrcScene)) AddToBuild(SrcScene);
            AddToBuild(dstScene);
            Debug.Log($"[Soul Requiem] Built {dstScene}: {mapName}, {n} torch lights, ambient light {globalIntensity}.");
        }

        /// <summary>Freeform light outline (local coordinates around the light centre) from the Tiled object's collider.</summary>
        static Vector3[] AreaPath(Collider2D col, Vector3 center)
        {
            if (col is PolygonCollider2D pc && pc.points.Length >= 3)
            {
                var pts = new Vector3[pc.points.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = (Vector3)(Vector2)pc.transform.TransformPoint(pc.points[i] + pc.offset) - center;
                return pts;
            }
            var b = col.bounds;
            return new[] { new Vector3(b.min.x, b.min.y) - center, new Vector3(b.max.x, b.min.y) - center, new Vector3(b.max.x, b.max.y) - center, new Vector3(b.min.x, b.max.y) - center };
        }

        /// <summary>Add a scene to Build Settings (needed to change maps via SceneManager.LoadScene).</summary>
        static void AddToBuild(string path)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var e in list) if (e.path == path) { e.enabled = true; EditorBuildSettings.scenes = list.ToArray(); return; }
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Soul Requiem] Added to Build Settings: " + path);
        }

        // (delegates to TiledProps — the only place that reads Tiled properties)
        static string Str(SuperCustomProperties p, string name, string fallback) => p.GetString(name, fallback);
        static float Float(SuperCustomProperties p, string name, float fallback) => p.GetFloat(name, fallback);
    }
}
#endif
