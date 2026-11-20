using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§362): MỤC TIÊU TẠM của bảng nhiệm vụ HUD — một cơ quan đang diễn ra (vd minigame Burlap's Proving Grounds) đặt tiêu đề / dòng đích /
    /// điểm đích (mũi tên + số mét + chấm minimap) / tiến độ n/N thay cho bước nhiệm vụ chính; Clear() trả lại nhiệm vụ chính.
    /// QuestTracker.Evaluate đọc (mỗi 0,25 s).
    /// </summary>
    public static class QuestGuide
    {
        public static bool Active { get; private set; }
        public static string Title = "", Label = "";
        public static bool HasTarget;
        public static Vector3 World;
        public static int Done, Total;

        public static void Set(string title, string label, Vector2? target, int done = 0, int total = 0)
        {
            Active = true; Title = title ?? ""; Label = label ?? "";
            HasTarget = target.HasValue;
            if (target.HasValue) World = target.Value;
            Done = done; Total = total;
        }

        public static void Clear() { Active = false; HasTarget = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = false; HasTarget = false; }
    }
}
