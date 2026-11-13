// Soul Requiem — chẩn đoán & sửa Animator của quái bị "Missing (Runtime Animator Controller)".
// Menu: Soul Requiem ▸ Enemies ▸ Check and Repair Enemy Animators
// 07/11: mọi map — với mỗi prefab trong Assets/Enemies/<MapN>/Prefabs: nếu Animator mất controller thì gán lại
// Animations/<tên>/<tên>.controller (nếu có), lưu prefab. Ghi báo cáo vào Logs/claude_preview/animator_doctor.txt.
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

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Kiểm tra & sửa Animator quái")]
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
                    if (anim == null) { sb.AppendLine($"{key}: (không có Animator)"); continue; }
                    string ctrlPath = $"{Root}/Animations/{key}/{key}.controller";
                    var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                    string have = anim.runtimeAnimatorController != null
                        ? AssetDatabase.GetAssetPath(anim.runtimeAnimatorController) : "MISSING";
                    string yaml = File.ReadAllText(path);
                    int i = yaml.IndexOf("m_Controller:");
                    string refGuid = i >= 0 ? yaml.Substring(i, Mathf.Min(90, yaml.Length - i)).Split('\n')[0] : "?";
                    sb.AppendLine($"{key}: đang trỏ = {have} | YAML {refGuid.Trim()} | controller thật GUID = " +
                                  (ctrl != null ? AssetDatabase.AssetPathToGUID(ctrlPath) : "không có file"));
                    if (ctrl == null) { if (anim.runtimeAnimatorController == null) noCtrl++; else okN++; continue; }
                    // Luôn gán lại + lưu: bản prefab đang nằm trong bộ nhớ Unity có thể còn giữ tham chiếu "Missing"
                    // (controller từng bị xoá rồi tạo lại khi Unity đang mở) dù file trên đĩa đã đúng.
                    anim.runtimeAnimatorController = ctrl;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixedN++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            // nạp lại toàn bộ controller + prefab quái từ đĩa để bỏ tham chiếu hỏng còn trong bộ nhớ
            foreach (var g in AssetDatabase.FindAssets("t:AnimatorController t:Prefab", new[] { EnemiesRoot }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
            string report = $"Đã gán lại + lưu {fixedN}, đúng sẵn {okN}, thiếu controller {noCtrl}\n" + sb;
            Directory.CreateDirectory("Logs/claude_preview");
            File.WriteAllText("Logs/claude_preview/animator_doctor.txt", report);
            Debug.Log("[Soul Requiem] Animator quái: " + report);
            EditorUtility.DisplayDialog("Soul Requiem", $"Enemy animators: repaired {fixedN}, already OK {okN}, missing controller {noCtrl}.\n" +
                                        "Details: Logs/claude_preview/animator_doctor.txt", "OK");
        }
    }
}
