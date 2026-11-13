using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Làm đèn "sống": lửa nhấp nháy (flicker), axit/phép "thở" (pulse), và bật/tắt mờ dần thay vì tắt phụt.
    /// Map2SceneBuilder gắn component này theo thuộc tính Tiled của đèn: anim = flicker | pulse, anim_amount, anim_speed.
    /// MapMechanics gọi SetOn(bool) khi cờ lit_when / off_when đổi.
    /// 05/11: LightCuller gọi SetCulled khi đèn ra / vào khung camera — bị cắt thì Light2D tắt hẳn, không tính hoạt ảnh;
    /// trạng thái bật/tắt theo cờ vẫn giữ (vào lại khung thì sáng đúng như cơ quan muốn).
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class LightAnimator : MonoBehaviour
    {
        public enum Mode { Steady, Flicker, Pulse }

        public Mode mode = Mode.Steady;
        [Tooltip("Biên độ dao động cường độ (0.1 = ±10%)")] public float amount = 0.1f;
        [Tooltip("Flicker: tốc độ nhiễu. Pulse: số nhịp mỗi giây")] public float speed = 1f;
        public float phase;
        public float baseIntensity = 1f;
        [Tooltip("Thời gian mờ dần khi bật / tắt (giây)")] public float fadeSeconds = 1.2f;

        private Light2D l;
        private float fade = 1f, target = 1f;
        private bool culled;

        public Light2D Lamp => l;
        public bool Culled => culled;
        /// <summary>Đèn đang muốn sáng (theo cờ), bất kể có bị cắt hay không.</summary>
        public bool WantsOn => target > 0f;

        private void Awake()
        {
            l = GetComponent<Light2D>();
            if (baseIntensity <= 0f) baseIntensity = l.intensity;
            fade = target = l.enabled ? 1f : 0f;
            if (phase == 0f) phase = (transform.position.x * 13.7f + transform.position.y * 7.3f) % 10f;
        }

        /// <summary>Bật / tắt có mờ dần. Đèn tắt hẳn (enabled = false) khi đã mờ hết để không tốn hiệu năng.</summary>
        public void SetOn(bool on)
        {
            if (l == null) l = GetComponent<Light2D>();
            target = on ? 1f : 0f;
            if (culled) { fade = target; return; }                       // ngoài khung: không cần mờ dần
            if (on) l.enabled = true;
            enabled = true;
        }

        /// <summary>05/11 (LightCuller): ngoài khung camera -> tắt hẳn; vào lại -> sáng theo trạng thái cờ.</summary>
        public void SetCulled(bool c)
        {
            if (l == null) l = GetComponent<Light2D>();
            if (culled == c) return;
            culled = c;
            if (c) { fade = target; l.enabled = false; }
            else { l.enabled = target > 0f; if (l.enabled) l.intensity = baseIntensity * fade; }
        }

        private void Update()
        {
            if (culled) return;
            if (!Mathf.Approximately(fade, target))
            {
                fade = Mathf.MoveTowards(fade, target, Time.deltaTime / Mathf.Max(0.05f, fadeSeconds));
                if (fade <= 0f && target <= 0f) { l.enabled = false; return; }
            }
            if (!l.enabled) return;

            float t = Time.time;
            float mod = 1f;
            switch (mode)
            {
                case Mode.Flicker:
                    // 2 tầng nhiễu: rung nhanh của ngọn lửa + chao chậm, thỉnh thoảng hụt sáng
                    float fast = Mathf.PerlinNoise(t * speed, phase) * 2f - 1f;
                    float slow = Mathf.PerlinNoise(t * speed * 0.23f, phase + 31f) * 2f - 1f;
                    mod = 1f + amount * (0.65f * fast + 0.35f * slow);
                    break;
                case Mode.Pulse:
                    mod = 1f + amount * Mathf.Sin((t * speed + phase) * Mathf.PI * 2f);
                    break;
            }
            l.intensity = baseIntensity * fade * mod;
        }
    }
}
