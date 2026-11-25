// Soul Requiem — diagnose & repair enemy Animators showing "Missing (Runtime Animator Controller)".
// Menu: Soul Requiem ▸ Enemies ▸ Check and Repair Enemy Animators (declared in SoulRequiemMenu).
// Every map — for each prefab in Assets/Enemies/<MapN>/Prefabs: if the Animator lost its controller, reassign
// Animations/<name>/<name>.controller (if it exists) and save the prefab. Writes a report to Logs/claude_preview/animator_doctor.txt.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.EditorTools
{
    public static class EnemyAnimatorDoctor
    {
        const string EnemiesRoot = "Assets/Enemies";

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Exit Play Mode first.", "OK");
                return;
            }
            var sb = new StringBuilder();
            int fixedN = 0, okN = 0, noCtrl = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemiesRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                int cut = path.IndexOf("/Prefabs/");
                if (cut < 0) continue;
                string Root = path.Substring(0, cut);                       // Assets/Enemies/<MapN>
                string key = Path.GetFileNameWithoutExtension(path);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var anim = root.GetComponentInChildren<Animator>();
                    if (anim == null) { sb.AppendLine($"{key}: (no Animator)"); continue; }
                    string ctrlPath = $"{Root}/Animations/{key}/{key}.controller";
                    var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                    string have = anim.runtimeAnimatorController != null
                        ? AssetDatabase.GetAssetPath(anim.runtimeAnimatorController) : "MISSING";
                    string yaml = File.ReadAllText(path);
                    int i = yaml.IndexOf("m_Controller:");
                    string refGuid = i >= 0 ? yaml.Substring(i, Mathf.Min(90, yaml.Length - i)).Split('\n')[0] : "?";
                    sb.AppendLine($"{key}: points to = {have} | YAML {refGuid.Trim()} | real controller GUID = " +
                                  (ctrl != null ? AssetDatabase.AssetPathToGUID(ctrlPath) : "no file"));
                    if (ctrl == null) { if (anim.runtimeAnimatorController == null) noCtrl++; else okN++; continue; }
                    // Always reassign + save: the prefab copy in Unity's memory can still hold a "Missing" reference
                    // (controller deleted and recreated while Unity was open) even when the file on disk is correct.
                    anim.runtimeAnimatorController = ctrl;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixedN++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            // reload every enemy controller + prefab from disk to drop stale references still in memory
            foreach (var g in AssetDatabase.FindAssets("t:AnimatorController t:Prefab", new[] { EnemiesRoot }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
            string report = $"Reassigned + saved {fixedN}, already correct {okN}, missing controller {noCtrl}\n" + sb;
            Directory.CreateDirectory("Logs/claude_preview");
            File.WriteAllText("Logs/claude_preview/animator_doctor.txt", report);
            Debug.Log("[Soul Requiem] Enemy Animators: " + report);
            EditorUtility.DisplayDialog("Soul Requiem", $"Enemy animators: repaired {fixedN}, already OK {okN}, missing controller {noCtrl}.\n" +
                                        "Details: Logs/claude_preview/animator_doctor.txt", "OK");
        }
    }
}
