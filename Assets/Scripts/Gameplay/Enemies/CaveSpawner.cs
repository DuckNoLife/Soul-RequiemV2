using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Mysterious cave: when the player comes close it spawns N random Spiders or Snakes (max 3 per wave).
    /// The next wave only comes out after the previous one is all dead. Done = every wave spawned and none left alive.
    /// </summary>
    public class CaveSpawner : MonoBehaviour
    {
        public GameObject[] spawnPool;          // Spider, Snake
        public int perWave = 3;
        public int waves = 2;
        public float triggerRadius = 2.2f;      // Unity units (7 tiles)
        public float spawnRadius = 0.5f;
        [Tooltip("Enemies emerge in front of the cave mouth (offset from the cave base)")]
        public Vector2 spawnOffset = new Vector2(0f, -0.3f);
        public float delayBetweenWaves = 1.5f;

        public bool Done { get; private set; }
        public bool Activated { get; private set; }
        public int AliveCount { get { alive.RemoveAll(e => e == null || e.Health.IsDead); return alive.Count; } }

        private readonly List<EnemyAI> alive = new List<EnemyAI>();
        /// <summary>Enemies spawned so far / total to spawn (used by the quest board kill counter).</summary>
        public int Spawned { get; private set; }
        public int Total => spawnPool != null && spawnPool.Length > 0 ? perWave * waves : 0;
        private Transform player;

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        private void Update()
        {
            if (Activated || player == null) return;
            if (((Vector2)(player.position - transform.position)).sqrMagnitude <= triggerRadius * triggerRadius)
            {
                Activated = true;
                StartCoroutine(Run());
            }
        }

        private IEnumerator Run()
        {
            for (int w = 0; w < waves; w++)
            {
                for (int i = 0; i < perWave; i++)
                {
                    if (spawnPool == null || spawnPool.Length == 0) break;
                    var prefab = spawnPool[Random.Range(0, spawnPool.Length)];
                    Vector2 off = Random.insideUnitCircle * spawnRadius;
                    off.y = -Mathf.Abs(off.y) * 0.5f;     // spread only toward the front of the cave mouth
                    var go = Instantiate(prefab, transform.position + (Vector3)(spawnOffset + off), Quaternion.identity);
                    var ai = go.GetComponent<EnemyAI>();
                    if (ai != null) { alive.Add(ai); Spawned++; }
                    yield return new WaitForSeconds(0.25f);
                }
                while (AliveCount > 0) yield return null;
                if (w < waves - 1) yield return new WaitForSeconds(delayBetweenWaves);
            }
            Done = true;
        }

        /// <summary>Dev cheat (DevCheats): treat the cave as having spawned every wave.</summary>
        public void DebugFinish()
        {
            StopAllCoroutines();
            Activated = true;
            Done = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
