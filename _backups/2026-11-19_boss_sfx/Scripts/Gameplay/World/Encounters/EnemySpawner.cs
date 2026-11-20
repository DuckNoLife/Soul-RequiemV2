using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Tạo quái từ tên (cột enemy trong Tiled) theo bảng prefab của MapEncounterDirector (Inspector: Enemies).
    /// Không biết gì về khu / đợt — việc đó của director.
    /// </summary>
    internal sealed class EnemySpawner
    {
        private readonly List<MapEncounterDirector.EnemyEntry> entries;
        private readonly float tileSize;

        public EnemySpawner(List<MapEncounterDirector.EnemyEntry> entries, float tileSize)
        {
            this.entries = entries;
            this.tileSize = tileSize;
        }

        /// <summary>Prefab theo tên (không phân biệt hoa thường); không có -> null.</summary>
        public GameObject Prefab(string enemyName)
        {
            foreach (var en in entries)
                if (en != null && en.prefab != null && string.Equals(en.enemyName, enemyName, System.StringComparison.OrdinalIgnoreCase))
                    return en.prefab;
            return null;
        }

        /// <summary>Sinh một quái. Thiếu prefab -> cảnh báo, trả về null.</summary>
        /// <param name="what">Mô tả cho dòng cảnh báo (vd "quái", "quái triệu hồi").</param>
        public EnemyAI Create(string enemyName, Vector3 pos, string what = "quái")
        {
            var prefab = Prefab(enemyName);
            if (prefab == null) { Debug.LogWarning($"Chưa có prefab cho {what} '{enemyName}'."); return null; }
            return Object.Instantiate(prefab, pos, Quaternion.identity).GetComponent<EnemyAI>();
        }

        /// <summary>Đặt một hang sinh quái (Cave_Spawner) theo thuộc tính của điểm sinh.</summary>
        public CaveSpawner CreateCave(SpawnPoint p)
        {
            var cavePrefab = Prefab(MapSchema.CaveSpawner);
            var go = cavePrefab != null ? Object.Instantiate(cavePrefab, p.pos, Quaternion.identity) : new GameObject(MapSchema.CaveSpawner);
            go.transform.position = p.pos;
            var cave = go.GetComponent<CaveSpawner>();
            if (cave == null) cave = go.AddComponent<CaveSpawner>();
            cave.perWave = p.props.GetInt(MapSchema.Prop.PerWave, cave.perWave);
            cave.waves = p.props.GetInt(MapSchema.Prop.Waves, cave.waves);
            cave.triggerRadius = p.props.GetFloat(MapSchema.Prop.TriggerRadiusTiles, 7f) * tileSize;
            var pool = new List<GameObject>();
            foreach (var n in p.props.GetString(MapSchema.Prop.Spawns, "Spider|Snake").Split('|'))
            {
                var pf = Prefab(n.Trim());
                if (pf != null) pool.Add(pf);
            }
            cave.spawnPool = pool.ToArray();
            return cave;
        }
    }
}
