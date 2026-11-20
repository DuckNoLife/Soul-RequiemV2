#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// 17/11 (§307): Menu Soul Requiem ▸ Enemies ▸ Build Map 4 Enemies (BuildMap4 cũng tự gọi sau khi dựng scene).
    /// Quái The Catacombs of Naming (Map 4) theo bảng người dùng chốt (§296):
    ///   Bone_Jailer     = Tiny Swords Skull (PPU 200 như quái TS Map 1)           — 3 con ập vào khi Ren cầm lại katana (Cell Rows)
    ///   Choir_Cutthroat = Tiny Swords Thief                                       — Blood Pit, Scriptorium (+ The Registrar gọi viện)
    ///   Pit_Bat         = Tiny Swords Giant Bat (không có hoạt ảnh chết -> đứng hình rồi tan)
    ///   Lantern_Warden  = MEGAPACK Knight_LVL4 (chỉ Idle/Move/Dash/Dashend -> Dash = đòn húc LungeSkills; lửa tím đã đổi ĐỎ:
    ///                     Assets/Enemies/Map4/Sprites/Lantern_Warden, tô bằng script — giữ nguyên ảnh gốc của gói)
    ///   Hollow_Cantor   = MEGAPACK Sorcerer_LVL4 (không có hoạt ảnh đánh -> Dash_Begin làm động tác niệm, bắn đạn thánh ca Hymn_Bolt Map 3)
    ///   The_Registrar   = MEGAPACK GameMaster (miniboss giữ Ấn 1: đứng yên, ATK, Upgrade = pha 2, chết = tan biến) — RegistrarSkills
    ///   Lantern_Knight  = DÙNG LẠI prefab Map 3 (giữ Ấn 2, Reliquary)
    /// Mở scene Map4_CatacombsOfNaming, gán đúng danh sách quái Map 4 vào MapEncounterDirector. Chạy lại bao nhiêu lần cũng được.
    /// </summary>
    public static class Map4EnemyBuilder
    {
        const string OutRoot = "Assets/Enemies/Map4";
        const string Scene = "Assets/Scenes/Map4_CatacombsOfNaming.unity";
        const string Map3Prefabs = "Assets/Enemies/Map3/Prefabs/";
        static readonly string[] Reuse = { "Lantern_Knight" };

        const string TSE = "Assets/GamePackage/Tiny Swords (Enemy Pack)/Enemy Pack/";
        const string MEGA = "Assets/GamePackage/CHARACTER MEGAPACK/";
        const string WARDEN = OutRoot + "/Sprites/Lantern_Warden/";

        class Anim
        {
            public string state, file; public float fps; public bool loop; public int frameW; public float pivotY;
            public Anim(string state, string file, float fps, bool loop, int frameW = 0, float pivotY = -1f)
            { this.state = state; this.file = file; this.fps = fps; this.loop = loop; this.frameW = frameW; this.pivotY = pivotY; }
        }

        class Def
        {
            public string key, display;
            public float ppu = 100f, pivotX = 0.5f, pivotY;
            public Color tint = Color.white;
            public float scale = 1f;
            public Anim[] anims;
            public Vector2 collider;
            public float mass = 1f, hp;
            public bool immuneCC;
            public EnemyKind kind;
            public string projectile;            // "hymn"
            public Action<EnemyAI> tune;
            public Action<GameObject> extra;
        }

        // pivotY đo bằng script (đáy hình chân / bóng của khung Idle): Skull 0.323, Thief 0.312, Giant Bat 0.255, Knight 0.0, Sorcerer 0.0, GameMaster 0.128
        static Def[] Defs => new[]
        {
            new Def {
                key = "Bone_Jailer", display = "Bone Jailer", ppu = 200, pivotY = 0.323f, scale = 1.2f, collider = new Vector2(0.24f, 0.10f), hp = 70, mass = 2f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", TSE + "Skull/Skull_Idle.png", 8, true, 192), new Anim("Move", TSE + "Skull/Skull_Run.png", 10, true, 192),
                                new Anim("Attack", TSE + "Skull/Skull_Attack.png", 12, false, 192) },
                tune = e => { e.moveSpeed = 1.25f; e.damage = 12; e.windup = 0.45f; e.cooldown = 1.2f; e.attackRange = 0.18f; e.aggroRange = 3.6f; e.knockback = 1.5f; }
            },
            new Def {
                key = "Choir_Cutthroat", display = "Choir Cutthroat", ppu = 200, pivotY = 0.312f, scale = 1.15f, collider = new Vector2(0.22f, 0.09f), hp = 50, mass = 1.5f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", TSE + "Thief/Thief_Idle.png", 8, true, 192), new Anim("Move", TSE + "Thief/Thief_Run.png", 12, true, 192),
                                new Anim("Attack", TSE + "Thief/Thief_Attack.png", 14, false, 192) },
                tune = e => { e.moveSpeed = 1.9f; e.damage = 10; e.windup = 0.3f; e.cooldown = 0.9f; e.attackRange = 0.16f; e.aggroRange = 4.0f;
                              e.comboHits = 2; e.comboGap = 0.18f; e.poisonPercent = 0.015f; e.poisonDuration = 4f; }
            },
            new Def {
                key = "Pit_Bat", display = "Pit Bat", ppu = 200, pivotY = 0.255f, scale = 1.15f, collider = new Vector2(0.22f, 0.09f), hp = 45, mass = 1f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", TSE + "Giant Bat/Giant Bat_Idle.png", 10, true, 192), new Anim("Move", TSE + "Giant Bat/Giant Bat_Move.png", 12, true, 192),
                                new Anim("Attack", TSE + "Giant Bat/Giant Bat_Attack.png", 16, false, 192) },
                tune = e => { e.moveSpeed = 2.1f; e.damage = 8; e.windup = 0.3f; e.cooldown = 1.0f; e.attackRange = 0.14f; e.aggroRange = 4.4f; }   // 18/11: bỏ floating — floating = GHIM tại chỗ sinh (kiểu Plague Eye), làm dơi đứng im không đuổi
            },
            new Def {
                key = "Lantern_Warden", display = "Lantern Warden", pivotY = 0.02f, scale = 0.95f, collider = new Vector2(0.30f, 0.12f), hp = 150, mass = 8f,
                kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", WARDEN + "Idle_2x1.png", 4, true, 87), new Anim("Move", WARDEN + "Move_24x1.png", 18, true, 87),
                                new Anim("Prepare", WARDEN + "Idle_2x1.png", 10, true, 87), new Anim("Dash", WARDEN + "Dash_2x1.png", 12, true, 87),
                                new Anim("Recover", WARDEN + "Dashend_6x1.png", 12, false, 87) },
                tune = e => { e.moveSpeed = 0.9f; e.damage = 16; e.aggroRange = 3.6f; e.knockback = 2.5f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.meleeState = ""; k.lungeRange = 1.7f; k.lungeSpeed = 3.6f; k.prepTime = 0.55f; k.recoverTime = 0.7f; }
            },
            new Def {
                key = "Hollow_Cantor", display = "Hollow Cantor", pivotY = 0.0f, scale = 1.05f, collider = new Vector2(0.20f, 0.08f), hp = 60, mass = 1.5f,
                kind = EnemyKind.Ranged, projectile = "hymn",
                anims = new[] { new Anim("Idle", MEGA + "Sorcerer_LVL4/Idle_2x1.png", 4, true, 62), new Anim("Move", MEGA + "Sorcerer_LVL4/Move_24x1.png", 18, true, 62),
                                new Anim("Attack", MEGA + "Sorcerer_LVL4/Dash_Begin_4x1.png", 8, false, 62) },
                tune = e => { e.moveSpeed = 1.0f; e.damage = 9; e.windup = 0.5f; e.cooldown = 1.7f; e.aggroRange = 4.4f; e.keepDistance = 2.2f;
                              e.projectileSpeed = 2.3f; e.projectileLifetime = 2.8f; }
            },
            new Def {
                key = "The_Registrar", display = "The Registrar", pivotY = 0.128f, scale = 0.75f, collider = new Vector2(0.34f, 0.12f), hp = 480, mass = 60f,
                immuneCC = true, kind = EnemyKind.Custom, projectile = "hymn",
                anims = new[] { new Anim("Idle", MEGA + "GameMaster/Idle_48x1.png", 16, true, 173), new Anim("Attack", MEGA + "GameMaster/ATK_48x1.png", 40, false, 173),
                                new Anim("Upgrade", MEGA + "GameMaster/Upgrade_40x1.png", 16, false, 173), new Anim("Move", MEGA + "GameMaster/Idle_48x1.png", 16, true, 173) },
                tune = e => { e.treatAsBoss = true; e.phase2At = 0.5f; e.leashRange = 0f; e.moveSpeed = 0f; e.damage = 14; e.aggroRange = 6f; e.attackRange = 0.3f; e.cooldown = 0.5f; },
                extra = go => { var k = go.AddComponent<RegistrarSkills>(); k.arenaRadii = new Vector2(2.6f, 1.6f); k.meleeRange = 0.5f; k.preferredRange = 1.8f; k.upgradeSeconds = 40f / 16f; }
            },
        };

        public static void BuildAllMenu() => BuildAll();

        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var d in Defs)
                    foreach (var a in d.anims) Slice(a.file, d.ppu, a.pivotY >= 0f ? a.pivotY : d.pivotY, d.pivotX, a.frameW);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Directory.CreateDirectory(OutRoot + "/Animations");
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(Scene)) { Debug.LogError("Chưa có " + Scene + " (chạy menu Build Map 4 Scene trước)."); return; }
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            int order = PlayerSortingOrder();
            var hymn = AssetDatabase.LoadAssetAtPath<GameObject>(Map3Prefabs + "Hymn_Bolt.prefab");      // đạn thánh ca dựng ở Map 3
            if (hymn == null) Debug.LogWarning("Thiếu " + Map3Prefabs + "Hymn_Bolt.prefab (chạy Build Map 3 Enemies một lần).");
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var d in Defs) prefabs[d.key] = BuildEnemy(d, order, d.projectile == "hymn" ? hymn : null);
            foreach (var key in Reuse)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(Map3Prefabs + key + ".prefab");
                if (p != null) prefabs[key] = p; else Debug.LogWarning("Thiếu prefab Map 3: " + key);
            }
            var dir = UnityEngine.Object.FindFirstObjectByType<MapEncounterDirector>();
            if (dir == null) { Debug.LogError("Scene Map 4 chưa có MapEncounterDirector."); return; }
            Undo.RecordObject(dir, "Map 4 enemies");
            var list = new List<MapEncounterDirector.EnemyEntry>();          // Map 4 chỉ dùng quái của nó (scene chép từ Map 3)
            foreach (var kv in prefabs) list.Add(new MapEncounterDirector.EnemyEntry { enemyName = kv.Key, prefab = kv.Value });
            dir.enemies = list;
            EditorUtility.SetDirty(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Soul Requiem] Đã dựng {Defs.Length} prefab quái Map 4 ({OutRoot}/Prefabs) + gán {prefabs.Count} loại vào Map4_CatacombsOfNaming.");
        }

        // ------------------------------------------------------------------ cắt sprite (giống Map3EnemyBuilder)
        static void Slice(string path, float ppu, float pivotY, float pivotX = 0.5f, int frameW = 0)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Không tìm thấy " + path); return; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 8192;
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int frame = frameW > 0 ? frameW : h, count = Mathf.Max(1, w / frame);
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
                    name = n, rect = new Rect(i * frame, 0, frame, h), alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(pivotX, pivotY), border = Vector4.zero,
                    spriteID = old.TryGetValue(n, out var id) ? id : GUID.Generate()
                });
            }
            dp.SetSpriteRects(rects.ToArray());
            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null) nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            dp.Apply();
            importer.SaveAndReimport();
        }

        static Sprite[] Sprites(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();

        static AnimationClip Clip(string folder, string name, Sprite[] frames, float fps, bool loop)
        {
            string p = $"{folder}/{name}.anim";
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

        static AnimatorController Controller(string folder, string name, List<(string state, AnimationClip clip)> states)
        {
            string p = $"{folder}/{name}.controller";
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

        static GameObject BuildEnemy(Def d, int order, GameObject projectile)
        {
            string animFolder = $"{OutRoot}/Animations/{d.key}";
            Directory.CreateDirectory(animFolder);
            AssetDatabase.Refresh();
            var states = new List<(string, AnimationClip)>();
            float attackLen = 0.6f;
            Sprite first = null;
            foreach (var a in d.anims)
            {
                var frames = Sprites(a.file);
                if (frames.Length == 0) { Debug.LogWarning("Chưa cắt được " + a.file); continue; }
                if (a.state == "Idle") first = frames[0];
                var clip = Clip(animFolder, $"{d.key}_{a.state}", frames, a.fps, a.loop);
                if (a.state == "Attack") attackLen = frames.Length / a.fps;
                states.Add((a.state, clip));
            }
            var ctrl = Controller(animFolder, d.key, states);
            var go = new GameObject(d.key);
            try
            {
                go.transform.localScale = Vector3.one * d.scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = first;
                sr.sortingOrder = order;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.color = d.tint;
                go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.mass = d.mass;
                rb.linearDamping = 5f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var col = go.AddComponent<CapsuleCollider2D>();
                col.direction = CapsuleDirection2D.Horizontal;
                col.size = d.collider / Mathf.Max(0.01f, d.scale);           // collider tính theo thế giới, không phóng theo scale
                col.offset = new Vector2(0f, col.size.y * 0.35f);
                var hp = go.AddComponent<Health>();
                var so = new SerializedObject(hp);
                so.FindProperty("maxHp").floatValue = d.hp;
                so.FindProperty("isPlayer").boolValue = false;
                so.FindProperty("immuneToCrowdControl").boolValue = d.immuneCC;
                so.ApplyModifiedPropertiesWithoutUndo();
                var ai = go.AddComponent<EnemyAI>();
                ai.displayName = d.display;
                ai.kind = d.kind;
                d.tune?.Invoke(ai);
                ai.attackTime = Mathf.Max(attackLen, ai.windup + 0.1f);
                if (projectile != null) ai.projectilePrefab = projectile;
                d.extra?.Invoke(go);
                EnemyDefinitionTools.ApplyOrCreate(go, d.key, OutRoot);            // asset Definitions/<key> thắng giá trị trong code
                ai.attackTime = Mathf.Max(attackLen, ai.windup + 0.1f);
                if (VfxCfg.TryGetValue(d.key, out var vcfg)) vcfg(go.AddComponent<EnemyVfx>());
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{d.key}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ---------------------------------------------------------------- VFX (dùng bộ m3_* đã có trong Resources/VFX)
        const string VFX = "Assets/Resources/VFX/";
        static readonly Dictionary<string, Action<EnemyVfx>> VfxCfg = new Dictionary<string, Action<EnemyVfx>>
        {
            ["Bone_Jailer"] = v => { v.swing = "m3_slash_steel"; v.windup = "m3_windup"; v.death = "m3_death_soul"; v.chestY = 0.25f; },
            ["Choir_Cutthroat"] = v => { v.swing = "m3_claw"; v.windup = "m3_windup"; v.death = "m3_death_soul"; v.chestY = 0.25f; },
            ["Pit_Bat"] = v => { v.swing = "m3_claw"; v.death = "m3_death_blood"; v.chestY = 0.3f; },
            ["Lantern_Warden"] = v => { v.windup = "m3_windup"; v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.chestY = 0.35f; },
            ["Hollow_Cantor"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.3f; },
            ["The_Registrar"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.45f; },
        };

        static GameObject BuildProjectile(string name, string fly, string boom, int order, float radius, Color tint, bool spin, float scale = 1f)
        {
            var flyF = Sprites(fly);
            var boomF = Sprites(boom);
            var go = new GameObject(name);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = flyF.Length > 0 ? flyF[0] : null;
                sr.sortingOrder = order;
                sr.color = tint;
                var p = go.AddComponent<Projectile>();
                p.radius = radius;
                p.flyFrames = flyF;
                p.explodeFrames = boom == fly ? new Sprite[0] : boomF;      // mũi tên: ảnh vỡ riêng (VFX) nếu có
                p.fps = 12f;
                p.spinToDirection = spin;
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{name}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static int PlayerSortingOrder()
        {
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var sr = pc != null ? pc.GetComponentInChildren<SpriteRenderer>() : null;
            return sr != null ? sr.sortingOrder : 2;
        }
    }
}
#endif
