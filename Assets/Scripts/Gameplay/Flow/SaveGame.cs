using System;
using System.IO;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: lưu tiến trình tại điểm nghỉ (lửa trại / đền). Một ô lưu duy nhất: file JSON trong Application.persistentDataPath.
    ///   Lưu  : mỗi lần thắp / nghỉ ở điểm nghỉ (MapTriggerHandler, RequiemMenus) -> SaveGame.Save(director).
    ///   Nạp  : màn hình tiêu đề chọn Continue -> SaveGame.RequestLoad() rồi tải scene; MapEncounterDirector.Start gọi TryTake.
    ///   Mới  : New Game -> SaveGame.Delete().
    /// Lưu: cờ thế giới (khu đã dọn, nhiệm vụ, hướng dẫn đã xem, đồ đã nhặt "picked_*", điểm nghỉ đã thắp "lit_*"), điểm hồi sinh,
    /// số bình / bình tối đa. Khu đang đánh dở không lưu (vào lại thì đánh lại từ đầu — giống lúc gục).
    /// </summary>
    public static class SaveGame
    {
        [Serializable]
        public class Data
        {
            public int version = 1;
            public string scene = "";
            public string[] flags = new string[0];
            public float rx, ry;
            public int potions, maxPotions;
            public string[] owned = new string[0], equipped = new string[0];   // 04/11: bộ chiêu (id) — Continue không còn mất chiêu
            public float healPercent;              // bình đồng (flask_upgrade) làm mỗi lần uống hồi nhiều hơn
            public string weapon = "";             // 10/11 (§217): id thanh katana đang đeo (cờ weapon_* nằm trong `flags`)
            public string savedAt = "";
        }

        private static bool loadRequested;
        private static string FilePath => Path.Combine(Application.persistentDataPath, "soulrequiem_save.json");

        public static bool Exists => File.Exists(FilePath);

        /// <summary>Scene của bản lưu (rỗng nếu không có / hỏng).</summary>
        public static string SavedScene
        {
            get { var d = Read(); return d != null ? d.scene : ""; }
        }

        /// <summary>Continue: lần vào map kế tiếp sẽ nạp bản lưu (nếu cùng scene).</summary>
        public static void RequestLoad() => loadRequested = true;

        public static void Delete()
        {
            loadRequested = false;
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning($"[Save] Không xoá được bản lưu: {e.Message}"); }
        }

        /// <summary>Lấy bản lưu cho scene này nếu người chơi chọn Continue (chỉ một lần).</summary>
        public static bool TryTake(string scene, out Data data)
        {
            data = null;
            if (!loadRequested) return false;
            loadRequested = false;
            data = Read();
            return data != null && data.scene == scene;
        }

        public static void Save(MapEncounterDirector dir, Vector2 respawn)
        {
            if (dir == null || dir.Player == null) return;
            var kit = dir.Player.GetComponent<PlayerCombat>();
            var d = new Data
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                rx = respawn.x, ry = respawn.y,
                potions = kit != null ? kit.Potions : 0,
                maxPotions = kit != null ? kit.maxPotions : 3,
                healPercent = kit != null ? kit.healPercent : 0f,
                weapon = kit != null && kit.Weapon != null ? kit.Weapon.id : "",
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            var lo = dir.Player.GetComponent<PlayerLoadout>();
            if (lo != null)
            {
                var ow = new System.Collections.Generic.List<string>();
                foreach (var o in lo.owned) if (o != null) ow.Add(o.id);
                d.owned = ow.ToArray();
                d.equipped = new string[PlayerLoadout.SlotCount];
                for (int i = 0; i < PlayerLoadout.SlotCount; i++) d.equipped[i] = lo.slots[i] != null ? lo.slots[i].id : "";
            }
            var list = new System.Collections.Generic.List<string>(dir.Flags.All);
            d.flags = list.ToArray();
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(d)); Debug.Log($"[Save] Đã lưu ({d.flags.Length} cờ) -> {FilePath}"); }
            catch (Exception e) { Debug.LogWarning($"[Save] Không lưu được: {e.Message}"); }
        }

        private static Data Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
            }
            catch (Exception e) { Debug.LogWarning($"[Save] Bản lưu hỏng: {e.Message}"); return null; }
        }
    }
}
