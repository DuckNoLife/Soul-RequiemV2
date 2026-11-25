#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// Tools for EnemyDefinition (enemy stats as assets).
    ///   Menu "Soul Requiem ▸ Enemies ▸ Create Missing Enemy Definitions": for each enemy prefab in Assets/Enemies/*/Prefabs without an asset,
    ///   create Assets/Enemies/&lt;MapN&gt;/Definitions/&lt;prefab name&gt;.asset from the prefab's CURRENT stats (keeps every manual tweak),
    ///   then link it to EnemyAI.definition. Safe to rerun (existing assets are left untouched).
    ///   The "Enemies ▸ Map N" builders call <see cref="ApplyOrCreate"/>: an existing asset WINS (overwrites the values written in the builder code),
    ///   otherwise it is created from the builder values.
    /// </summary>
    public static class EnemyDefinitionTools
    {
        private const string EnemiesRoot = "Assets/Enemies";

        internal static void CreateFromPrefabs()
        {
            int created = 0, linked = 0, total = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemiesRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                int cut = path.IndexOf("/Prefabs/");
                if (cut < 0) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var ai = root.GetComponent<EnemyAI>();
                    if (ai == null) continue;
                    total++;
                    string defPath = DefinitionPath(path.Substring(0, cut), root.name);
                    var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(defPath);
                    if (def == null) { def = Create(defPath, ai); created++; }
                    if (ai.definition != def)
                    {
                        ai.definition = def;
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        linked++;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Soul Requiem] EnemyDefinition: {total} enemy prefabs, created {created} assets, linked {linked} prefabs.");
            EditorUtility.DisplayDialog("Soul Requiem",
                $"{total} enemy prefabs.\nCreated {created} EnemyDefinitions, linked {linked} prefabs.\n\nAssets are in Assets/Enemies/<MapN>/Definitions/.", "OK");
        }

        /// <summary>
        /// Called by the builders right before saving a prefab: asset exists -> write the asset stats onto the enemy; missing -> create the asset from the enemy.
        /// Always links EnemyAI.definition.
        /// </summary>
        public static void ApplyOrCreate(GameObject go, string key, string outRoot)
        {
            var ai = go.GetComponent<EnemyAI>();
            if (ai == null) return;
            string defPath = DefinitionPath(outRoot, key);
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(defPath);
            if (def == null) def = Create(defPath, ai);
            else def.ApplyTo(ai);
            ai.definition = def;
        }

        private static string DefinitionPath(string mapRoot, string key) => $"{mapRoot}/Definitions/{key}.asset";

        private static EnemyDefinition Create(string defPath, EnemyAI ai)
        {
            string folder = Path.GetDirectoryName(defPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.CaptureFrom(ai);
            AssetDatabase.CreateAsset(def, defPath);
            return def;
        }
    }
}
#endif
