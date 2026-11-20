using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Thành tựu (bản tạm): lưu PlayerPrefs "ach_&lt;id&gt;" = 1 và báo lên màn hình.
    /// Sau này thay bằng hệ thống thành tựu thật (danh sách, biểu tượng, Steam...) — chỉ cần giữ API Unlock/Has.
    /// </summary>
    public static class Achievements
    {
        public static event System.Action<string, string> Unlocked;

        public static bool Has(string id) => PlayerPrefs.GetInt("ach_" + id, 0) == 1;

        public static void Unlock(string id, string title)
        {
            bool first = !Has(id);
            if (first)
            {
                PlayerPrefs.SetInt("ach_" + id, 1);
                PlayerPrefs.Save();
            }
            Debug.Log($"[Achievement] {id}: {title}{(first ? " (mới)" : "")}");
            Hints.Show("ACHIEVEMENT UNLOCKED: " + title, 6f);
            Unlocked?.Invoke(id, title);
        }
    }
}
