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
    /// Menu: Soul Requiem ▸ Dựng scene Map 2 (Sewers)
    /// 1. Sao Map1_GreystonePlains thành Assets/Scenes/Map2_UndercityOfVael.unity (lần đầu), mở scene đó.
    /// 2. Gỡ map cũ (mọi SuperMap), đặt prefab map2_undercity_of_vael.tmx vào đúng chỗ, gán cho EncounterDirector.
    /// 3. Ánh sáng cống ngầm: Global Light 2D tối xuống, mỗi object 'light' trong lớp 'mechanics' thành Light2D điểm.
    /// 4. Thêm Map1_GreystonePlains + Map2_UndercityOfVael vào Build Settings để lối ra Map 1 chuyển được sang Map 2.
    /// Chạy lại bao nhiêu lần cũng được (đèn cũ trong nhóm "Map2 Lights" được dựng lại).
    /// </summary>
    public static class Map2SceneBuilder
    {
        const string SrcScene = "Assets/Scenes/Map1_GreystonePlains.unity";
        const string DstScene = "Assets/Scenes/Map2_UndercityOfVael.unity";
        const string MapPathV2 = "Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx";
        const float GlobalIntensity = 0.40f;   // lúc chơi ZoneAmbience đổi theo từng khu

        // 17/10: Map 2 chính thức = bản thiết kế lại "Malric's Warren" (map2_undercity_of_vael.tmx). Bản cũ vẫn dựng được bằng menu (bản cũ).
        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Dựng scene Map 2 (Sewers)")]
        public static void Build()
        {
            // 17/11 (§286): lối xuống Kroven ở Records Vault — ảnh cầu thang chìm + song sắt (SR-relics) phải là ảnh pixel trước khi nhập map
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

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Dựng scene Map 3 (Vale of Graves)")]
        public static void BuildMap3()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            FixPixelTextures(Map3Pack + "/TiledMap Editor");
            FixPixelTextures(Map3Pack + "/Tilesets");
            FixPixelTextures(Map3Pack + "/Props");
            AssetDatabase.ImportAsset(Map3Path, ImportAssetOptions.ForceUpdate);
            BuildScene(DstScene, Map3Scene, Map3Path, "map3_vale_of_graves", 0.42f, new Color(0.70f, 0.78f, 0.95f));   // đêm trăng xanh
        }

        // ------------------------------------------------------------------ 07/11: Map 3b — hầm bí mật King's Barrow (tách khỏi Map 3)
        const string Map3bScene = "Assets/Scenes/Map3b_KingsBarrow.unity";
        const string Map3bPath = Map3Pack + "/TiledMap Editor/Maps/map3b_kings_barrow.tmx";

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Dựng scene Map 3b (King's Barrow)")]
        public static void BuildMap3b()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            if (!File.Exists(Map3Scene)) { Debug.LogError("Dựng Map 3 trước (scene hầm chép từ scene Map 3)."); return; }
            FixPixelTextures(Map3Pack + "/TiledMap Editor");
            AssetDatabase.ImportAsset(Map3Pack + "/TiledMap Editor/Tilesets/SR-barrow.tsx", ImportAssetOptions.ForceUpdate);   // 18/11 (§330)
            AssetDatabase.ImportAsset(Map3bPath, ImportAssetOptions.ForceUpdate);
            BuildScene(Map3Scene, Map3bScene, Map3bPath, "map3b_kings_barrow", 0.25f, new Color(0.55f, 0.60f, 0.85f));   // hầm tối, chỉ nến + giếng trời
            // ngoài tường phòng mộ là nền camera: đen như lòng đất (map nhỏ hơn khung hình -> camera đứng giữa, lộ nền)
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map3bScene)
            {
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.035f, 1f);
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
            if (EditorSceneManager.GetActiveScene().path == Map3bScene) Map3bBuilder.AddRedField();   // 18/11 (§330): boss bí mật The Red Field
        }

        // ------------------------------------------------------------------ 16/11 (§261): Map 1b — Hermit's Hollow (khu bí mật Oathbreaker)
        const string Map1Scene = "Assets/Scenes/Map1_GreystonePlains.unity";
        const string Map1Pack = "Assets/GamePackage/ERW - Grass Land 2.0 v2.1";
        const string Map1bScene = "Assets/Scenes/Map1b_HermitsHollow.unity";
        const string Map1bPath = Map1Pack + "/TiledMap Editor/Maps/map1b_hermits_hollow.tmx";

        /// Chép scene Map 1 (đủ director, HUD, danh sách quái Map 1: Spider, Lizard, Cave_Spawner...) rồi thay map bằng map1b.
        /// Cùng nhóm cờ với Map 1 (thuộc tính map flag_group = Map1_GreystonePlains).
        public static void BuildMap1b()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            if (!File.Exists(Map1Scene)) { Debug.LogError("Thiếu scene Map 1 (" + Map1Scene + ")."); return; }
            FixPixelTextures(Map1Pack + "/Props/SR-mine");
            AssetDatabase.ImportAsset(Map1Pack + "/TiledMap Editor/Tilesets/SR-mine.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map1bPath, ImportAssetOptions.ForceUpdate);
            BuildScene(Map1Scene, Map1bScene, Map1bPath, "map1b_hermits_hollow", 0.25f, new Color(0.80f, 0.74f, 0.66f));   // mỏ tối, ánh đất ấm
            var cam = Camera.main;
            if (cam != null && EditorSceneManager.GetActiveScene().path == Map1bScene)
            {
                cam.backgroundColor = new Color(0.035f, 0.025f, 0.02f, 1f);   // ngoài vách = lòng đất
                EditorUtility.SetDirty(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                EditorSceneManager.SaveScene(cam.gameObject.scene);
            }
        }

        // ------------------------------------------------------------------ 16/11 (§281): Map 2b — The Kroven Deep (khu bí mật Ironcrown)
        const string Map2bScene = "Assets/Scenes/Map2b_KrovenDeep.unity";
        const string Map2bPath = "Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2b_kroven_deep.tmx";

        /// Chép scene Map 2 (đủ director, HUD, quái Map 2) rồi thay map bằng map2b; thêm boss The Last Guard (Map2bBuilder).
        /// Cùng nhóm cờ với Map 2 (thuộc tính map flag_group = Map2_UndercityOfVael).
        public static void BuildMap2b()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            if (!File.Exists(DstScene)) { Debug.LogError("Thiếu scene Map 2 (" + DstScene + ")."); return; }
            FixPixelTextures("Assets/GamePackage/EPIC RPG World - Sewers V1.5/Props/SR-relics");
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-relic-ironcrown.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-kroven.tsx", ImportAssetOptions.ForceUpdate);   // 17/11 (§284): vòm cầu thang đá lối lên
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-down-kroven.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-stair-grate-kroven.tsx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Map2bPath, ImportAssetOptions.ForceUpdate);
            BuildScene(DstScene, Map2bScene, Map2bPath, "map2b_kroven_deep", 0.24f, new Color(0.62f, 0.74f, 0.70f));   // bể chứa tối, ánh rêu Ether
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

        // ------------------------------------------------------------------ 08/11 (§192): Map 4 — The Catacombs of Naming (BẢN ĐỒ THÔ)
        const string Map4Scene = "Assets/Scenes/Map4_CatacombsOfNaming.unity";
        const string Map4Prison = "Assets/GamePackage/EPIC RPG World Pack - Old Prison V1.7.1";
        const string Map4Crypt = "Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6";
        // 17/11 (§304): map MỚI sinh bằng script (_tools/map4_1117/write_real.py) — bố cục ngục + hầm mộ, cổng có hoạt ảnh, logic phần 1
        const string Map4Path = Map4Crypt + "/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx";

        /// Chép scene Map 3 (đủ director, HUD, danh sách quái Map 3 dùng tạm) rồi thay map bằng map4_catacombs_of_naming.tmx.
        /// 17/11 (§304): bản TẠM để chạy thử logic phần 1 (cổng, tời, chìa, ấn, điểm nghỉ) — chưa có quái riêng / cảnh thật (phần 3–4).
        public static void BuildMap4()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            if (!File.Exists(Map3Scene)) { Debug.LogError("Dựng Map 3 trước (scene Map 4 chép từ scene Map 3)."); return; }
            FixPixelTextures(Map4Prison + "/Tilesets");
            FixPixelTextures(Map4Prison + "/Props");            // §198: đồ ngục Old Prison (thùng, bàn, máy tra tấn, lửa ma)
            FixPixelTextures(Map4Crypt + "/Tilesets");
            FixPixelTextures(Map4Crypt + "/Props");             // anh do vat + anh dong (duoc, nen) cua goi Crypt
            AssetDatabase.ImportAsset(Map4Crypt + "/TiledMap Editor/Tilesets/SR-m4-art.tsx", ImportAssetOptions.ForceUpdate);   // §303: cửa boss 128x96
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
            if (EditorSceneManager.GetActiveScene().path == Map4Scene) Map4EnemyBuilder.BuildAll();   // 17/11 (§307): danh sách quái Map 4
        }

        /// Ảnh tileset to (1568x2016...) bị nén + giới hạn 2048 -> mờ, lệch màu. Ép Point, không nén, tối đa 8192.
        static void FixPixelTextures(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            int n = 0;
            AssetDatabase.StartAssetEditing();                 // gom lại: 1 lần import, không reimport tmx sau mỗi ảnh
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
            if (n > 0) Debug.Log($"[Soul Requiem] Sửa import {n} ảnh trong {folder}.");
        }

        static void BuildScene(string srcScene, string dstScene, string mapPath, string mapName) =>
            BuildScene(srcScene, dstScene, mapPath, mapName, GlobalIntensity, new Color(0.78f, 0.86f, 0.95f));

        static void BuildScene(string srcScene, string dstScene, string mapPath, string mapName, float globalIntensity, Color globalColor)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(dstScene)) AssetDatabase.CopyAsset(srcScene, dstScene);
            var scene = EditorSceneManager.OpenScene(dstScene, OpenSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mapPath);
            if (prefab == null) { Debug.LogError("Chưa import được " + mapPath + " (Ctrl+R rồi thử lại)."); return; }

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
                if (dir.GetComponent<MapMechanics>() == null) dir.gameObject.AddComponent<MapMechanics>();   // van, đèn, ống phun, câu đố...
                if (dir.GetComponent<ZoneAmbience>() == null) dir.gameObject.AddComponent<ZoneAmbience>();   // ánh nền theo khu
            }
            else Debug.LogError("Scene không có MapEncounterDirector.");

            // ---------------- ánh sáng
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
                l.intensity = Float(props, "intensity", 1.1f);   // đèn nền theo vùng: bán kính lớn, cường độ thấp
                l.color = c;
                l.falloffIntensity = Float(props, "falloff_strength", 0.5f);
                // vùng sáng theo hình (mặt axit): object hình chữ nhật/đa giác + shape=area -> Freeform light
                if (Str(props, "shape", "") == "area" && col != null)
                {
                    l.lightType = Light2D.LightType.Freeform;
                    l.SetShapePath(AreaPath(col, go.transform.position));
                    l.shapeLightFalloffSize = Float(props, "falloff", 2.5f) * 0.32f;
                }
                // đèn tín hiệu / đèn theo cờ: MapMechanics bật tắt lúc chơi
                if (!string.IsNullOrEmpty(Str(props, "lit_when", ""))) l.enabled = false;
                // lửa nhấp nháy / axit thở; đèn theo cờ cũng cần để mờ dần khi bật tắt
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
            Debug.Log($"[Soul Requiem] Đã dựng {dstScene}: {mapName}, {n} đèn đuốc, ánh sáng nền {globalIntensity}.");
        }

        /// <summary>Đường bao vùng sáng Freeform (toạ độ cục bộ quanh tâm đèn) từ collider của object Tiled.</summary>
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

        /// <summary>Thêm scene vào Build Settings (cần để chuyển map bằng SceneManager.LoadScene).</summary>
        static void AddToBuild(string path)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var e in list) if (e.path == path) { e.enabled = true; EditorBuildSettings.scenes = list.ToArray(); return; }
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Soul Requiem] Đã thêm vào Build Settings: " + path);
        }

        // (gọi về TiledProps — nơi duy nhất đọc thuộc tính Tiled)
        static string Str(SuperCustomProperties p, string name, string fallback) => p.GetString(name, fallback);
        static float Float(SuperCustomProperties p, string name, float fallback) => p.GetFloat(name, fallback);
    }
}
#endif
