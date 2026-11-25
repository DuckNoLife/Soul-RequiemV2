#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// Map 3b secret boss — THE RED FIELD (PixelLab object 45958e9d, a mound of soldiers' corpses from both sides). Does not walk, one direction.
    /// Sprites: Assets/Enemies/Map3b/Sprites/Red_Field/Red_Field_&lt;Idle|Slam|Open&gt;.png (horizontal strips of 192 cells) + Red_Field_meta.txt
    /// (exported by _tools/barrow_1118/assets.py). Called by Map2SceneBuilder.BuildMap3b after the scene copy: builds the prefab then ADDS it to
    /// the director's enemy list in the open scene.
    /// </summary>
    public static class Map3bBuilder
    {
        const string OutRoot = "Assets/Enemies/Map3b";
        const string Key = "Red_Field";

        static readonly (string state, float fps, bool loop)[] States =
        {
            ("Idle", 6, true), ("Slam", RedFieldSkills.FPS_SLAM, false), ("Open", RedFieldSkills.FPS_OPEN, false),
        };

        public static void AddRedField()
        {
            string dir = OutRoot + "/Sprites/" + Key + "/";
            var metaPath = dir + Key + "_meta.txt";
            if (!File.Exists(metaPath)) { Debug.LogError("[Soul Requiem] Missing " + metaPath + " (run _tools/barrow_1118/assets.py)."); return; }
            var t = File.ReadAllText(metaPath).Trim().Split(' ');
            int cell = int.Parse(t[0]);
            float py = float.Parse(t[2], System.Globalization.CultureInfo.InvariantCulture);
            Directory.CreateDirectory(OutRoot + "/Animations/" + Key);
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            Directory.CreateDirectory(OutRoot + "/Definitions");
            var states = new List<(string, AnimationClip)>();
            Sprite first = null;
            foreach (var s in States)
            {
                string f = dir + Key + "_" + s.state + ".png";
                if (!File.Exists(f)) { Debug.LogWarning("[Soul Requiem] Missing " + f); continue; }
                Map2bBuilder.Slice(f, cell, py);
                var frames = AssetDatabase.LoadAllAssetsAtPath(f).OfType<Sprite>().OrderBy(x => x.rect.x).ToArray();
                if (frames.Length == 0) continue;
                if (s.state == "Idle") first = frames[0];
                states.Add((s.state, Map2bBuilder.Clip($"{OutRoot}/Animations/{Key}/{Key}_{s.state}", frames, s.fps, s.loop)));
            }
            var ctrl = Map2bBuilder.Controller($"{OutRoot}/Animations/{Key}/{Key}.controller", states);
            if (ctrl == null || first == null) return;

            var pc = Object.FindFirstObjectByType<PlayerController>();
            var psr = pc != null ? pc.GetComponentInChildren<SpriteRenderer>() : null;
            int order = psr != null ? psr.sortingOrder : 2;

            var go = new GameObject(Key);
            GameObject prefab;
            try
            {
                const float scale = 1.3f;           // corpse mound ~168 px -> ~220 px in game (~2x Ren)
                go.transform.localScale = Vector3.one * scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = first; sr.sortingOrder = order; sr.spriteSortPoint = SpriteSortPoint.Pivot;
                go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;                 // does not walk, cannot be pushed
                rb.gravityScale = 0f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                var col = go.AddComponent<CapsuleCollider2D>();
                col.direction = CapsuleDirection2D.Horizontal;
                col.size = new Vector2(1.25f, 0.42f) / scale;            // mound base (wide): Ren cannot walk through
                col.offset = new Vector2(0f, col.size.y * 0.45f);
                var hp = go.AddComponent<Health>();
                var so = new SerializedObject(hp);
                so.FindProperty("maxHp").floatValue = 900f;
                so.FindProperty("isPlayer").boolValue = false;
                so.FindProperty("immuneToCrowdControl").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                var ai = go.AddComponent<EnemyAI>();
                ai.displayName = "The Red Field";
                ai.kind = EnemyKind.Custom;
                ai.treatAsBoss = true; ai.phase2At = 0.6f; ai.leashRange = 0f;
                ai.moveSpeed = 0f; ai.damage = 22; ai.aggroRange = 9f; ai.attackRange = 0.6f; ai.cooldown = 0.3f; ai.knockback = 3f;
                go.AddComponent<RedFieldSkills>();
                EnemyDefinitionTools.ApplyOrCreate(go, Key, OutRoot);
                ai.attackTime = Mathf.Max(0.6f, ai.windup + 0.1f);
                prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{Key}.prefab");
            }
            finally { Object.DestroyImmediate(go); }

            var director = Object.FindFirstObjectByType<MapEncounterDirector>();
            if (director == null) { Debug.LogError("[Soul Requiem] The open scene has no MapEncounterDirector."); return; }
            Undo.RecordObject(director, "Map 3b enemies");
            var list = director.enemies != null ? new List<MapEncounterDirector.EnemyEntry>(director.enemies) : new List<MapEncounterDirector.EnemyEntry>();
            list.RemoveAll(e => e.enemyName == Key);
            list.Add(new MapEncounterDirector.EnemyEntry { enemyName = Key, prefab = prefab });
            director.enemies = list;
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            EditorSceneManager.SaveScene(director.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Soul Requiem] Map 3b: built The Red Field + added it to the enemy list.");
        }
    }
}
#endif
