using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: trạng thái dùng chung giữa cảnh diễn (Gameplay) và HUD (UI).
    /// Begin/End: đang diễn cảnh -> HUD mờ đi (máu, chiêu, bình, minimap, nhiệm vụ), chỉ giữ dải gợi ý + bảng tên boss.
    /// </summary>
    public static class CutsceneState
    {
        private static int depth;
        public static bool Active => depth > 0;
        public static void Begin() => depth++;
        public static void End() => depth = Mathf.Max(0, depth - 1);
        public static void Reset() => depth = 0;
    }

    /// <summary>
    /// 02/11: màn dừng hướng dẫn (game đứng yên, phím nhấp nháy, chờ người chơi bấm đúng phím). HUD vẽ theo các trường này.
    /// </summary>
    public static class TutorialPrompt
    {
        public static bool Active { get; private set; }
        public static string Title = "", Keys = "", Sub = "";

        public static void Show(string title, string keys, string sub)
        {
            Title = title; Keys = keys; Sub = sub; Active = true;
        }

        public static void Hide() => Active = false;

        /// <summary>Dừng game (timeScale 0), HUD nhấp nháy phím; chờ đúng phím rồi chạy tiếp và làm luôn hành động đó theo `dir`.
        /// Menu Pause mở giữa chừng: không nhận phím; đóng menu (trả timeScale = 1) thì dừng lại. Chạy bằng StartCoroutine.</summary>
        public static IEnumerator Run(PlayerCombat combat, GameAction a, string title, string keys, string sub, Vector2 dir)
        {
            Show(title, keys, sub);
            Time.timeScale = 0f;
            yield return null;                                       // không ăn phím của chính khung vừa dừng
            while (true)
            {
                GameInput.Blocked = true;
                if (!GameInput.MenuOpen)
                {
                    Time.timeScale = 0f;
                    if (GameInput.PressedRaw(a)) break;
                }
                yield return null;
            }
            Hide();
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            if (combat != null) combat.QueueAction(a, dir);
        }
    }
}
