using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356): BẢNG ĐIỂM của minigame THE DRILL YARD (Map 4) — dữ liệu tĩnh để HUD (RequiemHUD.UpdateDrill) vẽ.
    /// DrillYardMechanism ghi, HUD chỉ đọc. Active = false -> HUD ẩn bảng.
    /// </summary>
    public static class DrillBoard
    {
        public static bool Active;
        public static string Title = "";          // "ROUND 2/4 - ABOUT FACE!"
        public static int Score, Target;
        public static float Combo = 1f;
        public static float RoundLeft, RoundLength = 1f;
        public static int Hits;
        /// <summary>Dòng sự kiện vừa xảy ra ("PERFECT +120") + thời điểm (HUD cho nổi lên rồi mờ).</summary>
        public static string Pop = "";
        public static Color PopColor = Color.white;
        public static float PopTime = -99f;
        /// <summary>Kết quả cuối ("RANK B - PASSED"), hiện to giữa màn trong vài giây.</summary>
        public static string Result = "";
        public static Color ResultColor = Color.white;
        public static float ResultTime = -99f;

        public static void Say(string text, Color c) { Pop = text; PopColor = c; PopTime = Time.time; }
        public static void Finish(string text, Color c) { Result = text; ResultColor = c; ResultTime = Time.time; }

        public static void Reset(int target)
        {
            Active = true; Title = ""; Score = 0; Target = target; Combo = 1f; RoundLeft = 0f; RoundLength = 1f; Hits = 0;
            Pop = ""; PopTime = -99f;
        }
    }
}
