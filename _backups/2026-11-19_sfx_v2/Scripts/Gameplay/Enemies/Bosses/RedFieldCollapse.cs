using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 18/11 (§330): khối xác The Red Field gục — bản sao hình boss lún dần xuống đất, rung nhẹ từng đợt, bụi đá bung ở chân,
    /// rồi tan hết. Boss gốc tự xoá sau ~2 s (EnemyAI.FadeAndDestroy) nên phần diễn chạy trên object riêng này.
    /// </summary>
    public sealed class RedFieldCollapse : MonoBehaviour
    {
        public float seconds = 2.8f;

        private IEnumerator Start()
        {
            var sr = GetComponent<SpriteRenderer>();
            Vector3 s0 = transform.localScale;
            Vector3 p0 = transform.position;
            float nextPuff = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float u = t / seconds;
                transform.localScale = new Vector3(s0.x * (1f + 0.08f * u), s0.y * Mathf.Lerp(1f, 0.18f, u * u), s0.z);
                transform.position = p0 + new Vector3(Mathf.Sin(t * 40f) * 0.012f * (1f - u), 0f, 0f);
                if (sr != null) sr.color = new Color(1f, 1f - 0.3f * u, 1f - 0.3f * u, Mathf.Clamp01(1.3f - u * 1.3f));
                if (t >= nextPuff)
                {
                    nextPuff = t + 0.35f;
                    CameraFollow.KickMain(Vector2.down, 0.035f * (1f - u), 0.25f);
                    Vector3 at = p0 + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.05f, 0.15f), 0f);
                    if (sr != null) StartCoroutine(CutsceneFx.Puff(at, sr.sortingLayerID, sr.sortingOrder + 1));
                }
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            Destroy(gameObject);
        }
    }
}
