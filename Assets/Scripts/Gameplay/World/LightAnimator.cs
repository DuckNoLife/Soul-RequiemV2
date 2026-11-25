using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Makes lights "alive": flickering fire (flicker), "breathing" acid/magic (pulse), and fading on/off instead of snapping.
    /// Map2SceneBuilder adds this component from the light's Tiled properties: anim = flicker | pulse, anim_amount, anim_speed.
    /// MapMechanics calls SetOn(bool) when the lit_when / off_when flags change.
    /// LightCuller calls SetCulled when the light leaves / enters the camera frame - culled lights turn the Light2D fully off and skip animation;
    /// the flag on/off state is kept (back in frame, it shines exactly as the mechanism wants).
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class LightAnimator : MonoBehaviour
    {
        public enum Mode { Steady, Flicker, Pulse }

        public Mode mode = Mode.Steady;
        [Tooltip("Intensity oscillation amplitude (0.1 = ±10%)")] public float amount = 0.1f;
        [Tooltip("Flicker: noise speed. Pulse: beats per second")] public float speed = 1f;
        public float phase;
        public float baseIntensity = 1f;
        [Tooltip("Fade time when turning on / off (seconds)")] public float fadeSeconds = 1.2f;

        private Light2D l;
        private float fade = 1f, target = 1f;
        private bool culled;

        public Light2D Lamp => l;
        public bool Culled => culled;
        /// <summary>The light wants to be on (by flag), whether culled or not.</summary>
        public bool WantsOn => target > 0f;

        private void Awake()
        {
            l = GetComponent<Light2D>();
            if (baseIntensity <= 0f) baseIntensity = l.intensity;
            fade = target = l.enabled ? 1f : 0f;
            if (phase == 0f) phase = (transform.position.x * 13.7f + transform.position.y * 7.3f) % 10f;
        }

        /// <summary>Turn on / off with a fade. The light is fully disabled (enabled = false) once faded out, to save performance.</summary>
        public void SetOn(bool on)
        {
            if (l == null) l = GetComponent<Light2D>();
            target = on ? 1f : 0f;
            if (culled) { fade = target; return; }                       // out of frame: no fade needed
            if (on) l.enabled = true;
            enabled = true;
        }

        /// <summary>Out of the camera frame -> fully off; back in -> shines according to the flag state.</summary>
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
                    // 2 noise layers: fast flame jitter + slow sway, occasional dips
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
