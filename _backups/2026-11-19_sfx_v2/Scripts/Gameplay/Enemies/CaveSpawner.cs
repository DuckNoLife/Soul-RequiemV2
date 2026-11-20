using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Hang động bí ẩn : khi người chơi tới gần, triệu hồi ngẫu nhiên N con Nhện hoặc Rắn. Max = 3
    /// Đợt sau chỉ ra khi đợt trước chết hết. Done = đã ra đủ số đợt và không còn con nào sống.
    /// </summary>
    public class CaveSpawner : MonoBehaviour
    {
        public GameObject[] spawnPool;          // Spider, Snake
        public int perWave = 3;
        public int waves = 2;
        public float triggerRadius = 2.2f;      // đơn vị Unity (7 ô)
        public float spawnRadius = 0.5f;
        [Tooltip("Quái chui ra ở trước miệng hang (lệch so với chân hang)")]
        public Vector2 spawnOffset = new Vector2(0f, -0.3f);
        public float delayBetweenWaves = 1.5f;

        public bool Done { get; private set; }
        public bool Activated { get; private set; }
        public int AliveCount { get { alive.RemoveAll(e => e == null || e.Health.IsDead); return alive.Count; } }

        private readonly List<EnemyAI> alive = new List<EnemyAI>();
        /// <summary>02/11: số quái đã ra / tổng sẽ ra (bảng nhiệm vụ đếm quái).</summary>
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
                    off.y = -Mathf.Abs(off.y) * 0.5f;     // chỉ tỏa ra phía trước miệng hang
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

        /// <summary>Dùng cho phím test F2: coi như hang đã ra hết quái.</summary>
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
