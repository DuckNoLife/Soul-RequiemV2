#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// 07/11: menu "Soul Requiem" duy nhất (tiếng Anh), gom lệnh của các builder theo nhóm Scenes / Enemies / Player.
    /// Các builder không còn [MenuItem] riêng — thêm lệnh mới thì thêm ở ĐÂY (một chỗ, có thứ tự + vạch ngăn nhóm).
    /// Đã bỏ: "Gắn animation Dash vào Animator" (PlayerDashAnimatorSetup) — Build Player Animator đã dựng Dash (Blend Tree 4 hướng).
    /// </summary>
    public static class SoulRequiemMenu
    {
        const string Root = "Soul Requiem/";
        const string Map1Scene = "Assets/Scenes/Map1_GreystonePlains.unity";

        static bool Ready()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("Soul Requiem", "Exit Play Mode first.", "OK");
            return false;
        }

        // ------------------------------------------------------------------ Scenes
        [MenuItem(Root + "Scenes/Build Title Scene", false, 100)]
        static void BuildTitle() { if (Ready()) TitleSceneBuilder.Build(); }

        [MenuItem(Root + "Scenes/Build Map 1b Scene (Hermit's Hollow)", false, 110)]
        static void BuildMap1bScene() { if (Ready()) Map2SceneBuilder.BuildMap1b(); }

        [MenuItem(Root + "Scenes/Build Map 2 Scene (Undercity of Vael)", false, 111)]
        static void BuildMap2Scene() { if (Ready()) Map2SceneBuilder.Build(); }

        [MenuItem(Root + "Scenes/Build Map 3 Scene (Vale of Graves)", false, 112)]
        static void BuildMap3Scene() { if (Ready()) Map2SceneBuilder.BuildMap3(); }

        [MenuItem(Root + "Scenes/Build Map 3b Scene (King's Barrow)", false, 113)]
        static void BuildMap3bScene() { if (Ready()) Map2SceneBuilder.BuildMap3b(); }

        [MenuItem(Root + "Scenes/Build Map 4 Scene (Catacombs)", false, 114)]
        static void BuildMap4Scene() { if (Ready()) Map2SceneBuilder.BuildMap4(); }

        /// <summary>Dựng lại mọi scene map theo đúng thứ tự phụ thuộc (Map 2 chép từ Map 1, Map 3 từ Map 2, Map 3b từ Map 3).</summary>
        [MenuItem(Root + "Scenes/Build All Map Scenes", false, 130)]
        static void BuildAllScenes()
        {
            if (!Ready()) return;
            Map2SceneBuilder.Build();
            Map2SceneBuilder.BuildMap3();
            Map2SceneBuilder.BuildMap3b();
            Map2SceneBuilder.BuildMap1b();   // 16/11 (§261)
            // 16/11: Map 4 cũ đã bỏ (§234) — không dựng lại cho tới khi có Map 4 mới
            Debug.Log("[Soul Requiem] All map scenes rebuilt (Map 2, Map 3, Map 3b, Map 1b).");
        }

        // ------------------------------------------------------------------ Enemies
        /// <summary>Builder Map 1 gắn director / HUD vào scene ĐANG MỞ -> mở scene Map 1 trước (trước đây chạy nhầm scene là hỏng).</summary>
        [MenuItem(Root + "Enemies/Build Map 1 Enemies", false, 200)]
        static void BuildMap1Enemies()
        {
            if (!Ready()) return;
            if (EditorSceneManager.GetActiveScene().path != Map1Scene)
            {
                if (!File.Exists(Map1Scene)) { Debug.LogError("[Soul Requiem] Missing " + Map1Scene); return; }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(Map1Scene, OpenSceneMode.Single);
            }
            Map1EnemyBuilder.BuildAll();
        }

        [MenuItem(Root + "Enemies/Build Map 2 Enemies", false, 201)]
        static void BuildMap2Enemies() { if (Ready()) Map2EnemyBuilder.BuildAllMenu(); }

        [MenuItem(Root + "Enemies/Build Map 3 Enemies", false, 202)]
        static void BuildMap3Enemies() { if (Ready()) Map3EnemyBuilder.BuildAllMenu(); }

        [MenuItem(Root + "Enemies/Build All Enemies", false, 220)]
        static void BuildAllEnemies()
        {
            if (!Ready()) return;
            BuildMap1Enemies();
            Map2EnemyBuilder.BuildAllMenu();
            Map3EnemyBuilder.BuildAllMenu();
            Debug.Log("[Soul Requiem] Enemies rebuilt for Map 1, Map 2, Map 3.");
        }

        [MenuItem(Root + "Enemies/Create Missing Enemy Definitions", false, 240)]
        static void CreateDefinitions() { if (Ready()) EnemyDefinitionTools.CreateFromPrefabs(); }

        [MenuItem(Root + "Enemies/Check and Repair Enemy Animators", false, 241)]
        static void RepairAnimators() { if (Ready()) EnemyAnimatorDoctor.Run(); }

        // ------------------------------------------------------------------ Player (trên scene đang mở)
        /// <summary>Đủ 3 bước theo thứ tự: Animator -> pivot ở chân (xếp lớp) -> collider ở chân. Lưu scene sau khi chạy.</summary>
        [MenuItem(Root + "Player/Rebuild Player (Open Scene)", false, 300)]
        static void RebuildPlayer()
        {
            if (!Ready()) return;
            RenAnimatorBuilder.Run();                                   // 09/11 (§201): Ren v2 (tay không + lớp vũ khí)
            PlayerFeetSorting.Run();
            PlayerFeetSorting.FeetCollider();
            Debug.Log("[Soul Requiem] Player rebuilt: animator, feet sorting, feet collider. Save the scene (Ctrl+S).");
        }

        [MenuItem(Root + "Player/Build Ren Animator (v2)", false, 319)]
        static void RenAnimator() { if (Ready()) RenAnimatorBuilder.Run(); }

        [MenuItem(Root + "Player/Build Player Animator (legacy Iaido)", false, 320)]
        static void PlayerAnimator() { if (Ready()) PlayerAnimatorBuilder.Run(); }

        [MenuItem(Root + "Player/Sort Player by Feet (Sprite Pivots)", false, 321)]
        static void PlayerFeet() { if (Ready()) PlayerFeetSorting.Run(); }

        [MenuItem(Root + "Player/Fit Player Collider to Feet", false, 322)]
        static void PlayerCollider() { if (Ready()) PlayerFeetSorting.FeetCollider(); }
    }
}
#endif
