using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Creates enemies by name (enemy column in Tiled) from MapEncounterDirector's prefab table (Inspector: Enemies).
    /// Knows nothing about areas / waves - that is the director's job.
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

        /// <summary>Prefab by name (case-insensitive); none -> null.</summary>
        public GameObject Prefab(string enemyName)
        {
            foreach (var en in entries)
                if (en != null && en.prefab != null && string.Equals(en.enemyName, enemyName, System.StringComparison.OrdinalIgnoreCase))
                    return en.prefab;
            return null;
        }

        /// <summary>Spawn one enemy. Missing prefab -> warning, returns null.</summary>
        /// <param name="what">Description for the warning line (e.g. "enemy", "summoned enemy").</param>
        public EnemyAI Create(string enemyName, Vector3 pos, string what = "enemy")
        {
            var prefab = Prefab(enemyName);
            if (prefab == null) { Debug.LogWarning($"No prefab for {what} '{enemyName}'."); return null; }
            return Object.Instantiate(prefab, pos, Quaternion.identity).GetComponent<EnemyAI>();
        }

        /// <summary>Place an enemy spawning cave (Cave_Spawner) from the spawn point's properties.</summary>
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
