using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// Mờ dần rồi tự huỷ (mảnh vụn để lại sau đòn đánh xuống đất).
    public class FadeAndDie : MonoBehaviour
    {
        public float seconds = 2f;
        private float t0;
        private SpriteRenderer sr;
        private void Start() { t0 = Time.time; sr = GetComponent<SpriteRenderer>(); }
        private void Update()
        {
            float k = (Time.time - t0) / Mathf.Max(0.01f, seconds);
            if (sr != null) { var c = sr.color; c.a = Mathf.Clamp01(1.5f - 1.5f * k); sr.color = c; }
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
