using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 17/11 (§285): ĐIỂM HỒI SINH TỰ ĐỘNG. Không còn vòng sáng / "thắp lửa để lưu": game ghi ngầm một điểm mỗi khi Ren bước vào một khu
    /// (chỗ Ren đứng lúc vào khu) và khi đi qua điểm nghỉ. Gục -> tỉnh ở điểm đã ghi GẦN CHỖ GỤC NHẤT (ải chưa xong vẫn ưu tiên đầu ải).
    /// Không ghi điểm trong khu đấu trường có cổng khoá (boss) — gục trong trận boss thì tỉnh ở ngoài cổng, không kẹt trong đấu trường.
    /// </summary>
    public partial class MapEncounterDirector
    {
        private readonly List<Vector2> autoRespawns = new List<Vector2>();
        private string autoZoneId = "";
        private const float AutoRespawnMinGap = 0.6f;          // hai điểm gần hơn khoảng này thì gộp

        /// <summary>Ghi một điểm hồi sinh (đồng thời là điểm lưu khi thoát game).</summary>
        internal void AddAutoRespawn(Vector2 p)
        {
            for (int i = 0; i < autoRespawns.Count; i++)
                if ((autoRespawns[i] - p).sqrMagnitude < AutoRespawnMinGap * AutoRespawnMinGap) { autoRespawns[i] = p; return; }
            autoRespawns.Add(p);
        }

        private void TickAutoRespawn(Vector2 pos)
        {
            if (layout == null || respawning || playerHealth == null || playerHealth.IsDead) return;
            if (autoRespawns.Count == 0) autoRespawns.Add(respawnPoint);              // điểm xuất phát / điểm đến / bản lưu
            // khu nhỏ nhất chứa Ren
            EncounterZone best = null;
            float area = float.MaxValue;
            foreach (var z in layout.Zones.Values)
                if (z.rect.Contains(pos) && z.rect.width * z.rect.height < area) { area = z.rect.width * z.rect.height; best = z; }
            string id = best != null ? best.id : "";
            if (id == autoZoneId) return;
            autoZoneId = id;
            if (best == null) return;
            triggerHandler?.LightCheckpointsIn(best.id, best.rect);              // 17/11 (§289): vào khu = điểm nghỉ của khu sáng trên minimap
            if (gateCtl != null && gateCtl.HasArenaLock(best.id)) return;            // đấu trường khoá cổng: không hồi sinh bên trong
            if (best.started && !best.cleared && best.alive.Count > 0) return;      // đang bị vây: không ghi
            AddAutoRespawn(pos);
            SetRespawnPoint(pos);
            SaveProgress();
        }

        /// <summary>Gục: chọn điểm đã ghi gần chỗ gục nhất (khoảng cách thẳng). Không có điểm nào -> giữ điểm hiện tại.</summary>
        private void ChooseNearestRespawn(Vector2 diedAt)
        {
            if (autoRespawns.Count == 0) return;
            Vector2 pick = autoRespawns[0];
            float bestD = float.MaxValue;
            foreach (var p in autoRespawns)
            {
                float d = (p - diedAt).sqrMagnitude;
                if (d < bestD) { bestD = d; pick = p; }
            }
            SetRespawnPoint(pick);
        }
    }
}
