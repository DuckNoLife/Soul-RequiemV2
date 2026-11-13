#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// Công cụ cho EnemyDefinition (chỉ số quái dạng asset).
    ///   Menu "Soul Requiem ▸ Quái ▸ Tạo EnemyDefinition từ prefab": mỗi prefab quái trong Assets/Enemies/*/Prefabs chưa có asset
    ///   -> tạo Assets/Enemies/&lt;MapN&gt;/Definitions/&lt;tên prefab&gt;.asset từ chỉ số HIỆN TẠI của prefab (giữ mọi chỉnh tay),
    ///   rồi gắn vào EnemyAI.definition. Chạy lại an toàn (asset đã có thì giữ nguyên).
    ///   Các menu "Dựng quái Map N" gọi <see cref="ApplyOrCreate"/>: asset đã có thì ASSET THẮNG (ghi đè giá trị viết trong code builder),
    ///   chưa có thì tạo từ giá trị builder.
    /// </summary>
    public static class EnemyDefinitionTools
    {
        private const string EnemiesRoot = "Assets/Enemies";

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Quái/Tạo EnemyDefinition từ prefab")]
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
            Debug.Log($"[Soul Requiem] EnemyDefinition: {total} prefab quái, tạo mới {created} asset, gắn {linked} prefab.");
            EditorUtility.DisplayDialog("Soul Requiem",
                $"{total} prefab quái.\nTạo mới {created} EnemyDefinition, gắn vào {linked} prefab.\n\nAsset nằm ở Assets/Enemies/<MapN>/Definitions/.", "OK");
        }

        /// <summary>
        /// Gọi từ builder ngay trước khi lưu prefab: asset có sẵn -> ghi chỉ số asset lên quái; chưa có -> tạo asset từ quái.
        /// Luôn gắn EnemyAI.definition.
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
