#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// 16/11 (§281/§283): quái riêng của Map 2b The Kroven Deep — boss nhỏ The Last Guard (PixelLab a3d65a38, pro theo style Lantern Knight 49d0004b).
    /// Sprite: Assets/Enemies/Map2b/Sprites/Last_Guard/Last_Guard_&lt;State&gt;[_S|_N].png (dải ngang ô vuông, hướng east; _S nhìn xuống, _N nhìn lên)
    /// + Last_Guard_meta.txt "cell cell pivotY pivotYS pivotYN" (xuất bằng _tools/kroven_2b/import_last_guard.py).
    /// Gọi từ Map2SceneBuilder.BuildMap2b sau khi chép scene: dựng prefab rồi THÊM vào danh sách quái của director trong scene đang mở.
    /// </summary>
    public static class Map2bBuilder
    {
        const string OutRoot = "Assets/Enemies/Map2b";
        const string Key = "Last_Guard";

        // 17/11 (§284) v3: fps khớp hằng số FPS_* trong LastGuardSkills (đòn canh theo khung hình)
        static readonly (string state, float fps, bool loop)[] States =
        {
            ("Idle", 6, true), ("Move", 8, true), ("Aim", LastGuardSkills.FPS_AIM, false), ("Stuck", LastGuardSkills.FPS_STUCK, false),
            ("Pull", 14, false), ("Charge", LastGuardSkills.FPS_CHARGE, true), ("Bash", LastGuardSkills.FPS_BASH, false),
            ("Dazed", 8, true), ("Stagger", 9, false), ("Death", 9, false),
        };
        // pha 2 "Last Stand": bỏ khiên, giáo hai tay — controller riêng, cùng tên state, LastGuardSkills đổi controller lúc vào pha 2
        static readonly (string state, float fps, bool loop)[] StatesLS =
        {
            ("Idle", 6, true), ("Move", 9, true), ("Thrust", LastGuardSkills.FPS_THRUST2, false), ("Whirl", LastGuardSkills.FPS_WHIRL, false),
            ("Leap", LastGuardSkills.FPS_LEAP, false), ("Death", 9, false),
        };

        public static void AddLastGuard()
        {
            string dir = OutRoot + "/Sprites/" + Key + "/";
            var ctrl = BuildSet(dir, Key, States, out Sprite first);
            var ctrlLS = BuildSet(dir, Key + "_LS", StatesLS, out _);
            if (ctrl == null) return;
            // khiên rơi xuống sàn khi vào pha 2
            string shieldPath = dir + Key + "_ShieldDropped.png";
            Sprite shield = null;
            if (File.Exists(shieldPath))
            {
                AssetDatabase.ImportAsset(shieldPath, ImportAssetOptions.ForceUpdate);
                var ti = AssetImporter.GetAtPath(shieldPath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 100f;
                    ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false;
                    var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts); ts.spriteAlignment = (int)SpriteAlignment.BottomCenter; ti.SetTextureSettings(ts);
                    ti.SaveAndReimport();
                }
                shield = AssetDatabase.LoadAssetAtPath<Sprite>(shieldPath);
            }

            var pc = Object.FindFirstObjectByType<PlayerController>();
            var psr = pc != null ? pc.GetComponentInChildren<SpriteRenderer>() : null;
            int order = psr != null ? psr.sortingOrder : 2;

            var go = new GameObject(Key);
            GameObject prefab;
            try
            {
                const float scale = 1.35f;          // 16/11 (§283): bản vẽ lại theo style Lantern Knight, thân ~111 px -> ~150 px trong game (~1,45 lần Ren)
                go.transform.localScale = Vector3.one * scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = first; sr.sortingOrder = order; sr.spriteSortPoint = SpriteSortPoint.Pivot;
                go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic; rb.gravityScale = 0f; rb.freezeRotation = true; rb.mass = 30f;
                rb.linearDamping = 5f; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var col = go.AddComponent<CapsuleCollider2D>();
                col.direction = CapsuleDirection2D.Horizontal;
                col.size = new Vector2(0.44f, 0.16f) / scale;
                col.offset = new Vector2(0f, col.size.y * 0.35f);
                var hp = go.AddComponent<Health>();
                var so = new SerializedObject(hp);
                so.FindProperty("maxHp").floatValue = 620f;
                so.FindProperty("isPlayer").boolValue = false;
                so.FindProperty("immuneToCrowdControl").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                var ai = go.AddComponent<EnemyAI>();
                ai.displayName = "The Last Guard";
                ai.kind = EnemyKind.Custom;
                ai.treatAsBoss = true; ai.phase2At = 0.5f; ai.leashRange = 0f;
                ai.moveSpeed = 0.9f; ai.damage = 16; ai.aggroRange = 4.5f; ai.attackRange = 0.4f; ai.cooldown = 0.4f; ai.knockback = 2.5f;
                var lgs = go.AddComponent<LastGuardSkills>();
                lgs.lastStandController = ctrlLS; lgs.droppedShield = shield;
                EnemyDefinitionTools.ApplyOrCreate(go, Key, OutRoot);
                ai.attackTime = Mathf.Max(0.6f, ai.windup + 0.1f);
                prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{Key}.prefab");
            }
            finally { Object.DestroyImmediate(go); }

            var director = Object.FindFirstObjectByType<MapEncounterDirector>();
            if (director == null) { Debug.LogError("[Soul Requiem] Scene đang mở chưa có MapEncounterDirector."); return; }
            Undo.RecordObject(director, "Map 2b enemies");
            var list = director.enemies != null ? new List<MapEncounterDirector.EnemyEntry>(director.enemies) : new List<MapEncounterDirector.EnemyEntry>();
            list.RemoveAll(e => e.enemyName == Key);
            list.Add(new MapEncounterDirector.EnemyEntry { enemyName = Key, prefab = prefab });
            director.enemies = list;
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            EditorSceneManager.SaveScene(director.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Soul Requiem] Map 2b: đã dựng The Last Guard + thêm vào danh sách quái.");
        }

        /// Cắt mọi dải &lt;prefix&gt;_&lt;State&gt;[_S|_N].png theo &lt;prefix&gt;_meta.txt, dựng clip + controller &lt;prefix&gt;.controller.
        static AnimatorController BuildSet(string dir, string prefix, (string state, float fps, bool loop)[] table, out Sprite first)
        {
            first = null;
            var metaPath = dir + prefix + "_meta.txt";
            if (!File.Exists(metaPath)) { Debug.LogError("[Soul Requiem] Thiếu " + metaPath + " (chạy _tools/kroven_2b/import_last_guard.py)."); return null; }
            var t = File.ReadAllText(metaPath).Trim().Split(' ');
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            int cell = int.Parse(t[0]);
            float py = float.Parse(t[2], ci), pyS = float.Parse(t[3], ci), pyN = float.Parse(t[4], ci);
            var anims = new List<(string state, string file, float fps, bool loop)>();
            foreach (var s in table)
                foreach (var (suf, pv) in new[] { ("", py), ("_S", pyS), ("_N", pyN) })
                {
                    string f = dir + prefix + "_" + s.state + suf + ".png";
                    if (!File.Exists(f)) continue;
                    Slice(f, cell, pv);
                    anims.Add((s.state + suf, f, s.fps, s.loop));
                }
            Directory.CreateDirectory(OutRoot + "/Animations/" + Key);
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            Directory.CreateDirectory(OutRoot + "/Definitions");
            AssetDatabase.Refresh();
            var states = new List<(string, AnimationClip)>();
            foreach (var a in anims)
            {
                var frames = AssetDatabase.LoadAllAssetsAtPath(a.file).OfType<Sprite>().OrderBy(x => x.rect.x).ToArray();
                if (frames.Length == 0) continue;
                if (a.state == "Idle") first = frames[0];
                states.Add((a.state, Clip($"{OutRoot}/Animations/{Key}/{prefix}_{a.state}", frames, a.fps, a.loop)));
            }
            return Controller($"{OutRoot}/Animations/{Key}/{prefix}.controller", states);
        }

        // ------------------------------------------------------------------ cắt sprite / clip / controller (như Map3EnemyBuilder)
        internal static void Slice(string path, int cell, float pivotY)   // 18/11 (§330): Map3bBuilder dùng chung
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Không tìm thấy " + path); return; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 8192;
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int count = Mathf.Max(1, w / cell);
            string baseName = Path.GetFileNameWithoutExtension(path);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();
            var old = new Dictionary<string, GUID>();
            foreach (var r in dp.GetSpriteRects()) if (!old.ContainsKey(r.name)) old[r.name] = r.spriteID;
            var rects = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                string n = $"{baseName}_{i}";
                rects.Add(new SpriteRect
                {
                    name = n, rect = new Rect(i * cell, 0, cell, h), alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, pivotY), border = Vector4.zero,
                    spriteID = old.TryGetValue(n, out var id) ? id : GUID.Generate()
                });
            }
            dp.SetSpriteRects(rects.ToArray());
            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null) nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            dp.Apply();
            importer.SaveAndReimport();
        }

        internal static AnimationClip Clip(string pathNoExt, Sprite[] frames, float fps, bool loop)
        {
            string p = pathNoExt + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.frameRate = fps;
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (isNew) AssetDatabase.CreateAsset(clip, p); else EditorUtility.SetDirty(clip);
            return clip;
        }

        internal static AnimatorController Controller(string p, List<(string state, AnimationClip clip)> states)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
            if (ctrl == null)
            {
                if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(p);
            }
            if (ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer");
            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states.ToArray()) sm.RemoveState(cs.state);
            foreach (var (state, clip) in states)
            {
                var st = sm.AddState(state);
                st.motion = clip;
                st.writeDefaultValues = false;
                if (state == "Idle") sm.defaultState = st;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssetIfDirty(ctrl);
            return ctrl;
        }
    }
}
#endif
