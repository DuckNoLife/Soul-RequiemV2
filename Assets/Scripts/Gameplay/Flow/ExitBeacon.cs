using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Blinking beacon at an exit once it is open (e.g. the cracked altar after defeating Grok).</summary>
    public class ExitBeacon : MonoBehaviour
    {
        public float baseIntensity = 0.75f, pulse = 0.3f, speed = 2.2f;
        private Light2D light2d;

        public static ExitBeacon Create(Vector3 pos, Color color, float radius)
        {
            var go = new GameObject("Exit Beacon");
            go.transform.position = pos;
            var b = go.AddComponent<ExitBeacon>();
            b.light2d = go.AddComponent<Light2D>();
            b.light2d.lightType = Light2D.LightType.Point;
            b.light2d.color = color;
            b.light2d.pointLightOuterRadius = radius;
            b.light2d.pointLightInnerRadius = radius * 0.2f;
            return b;
        }

        private void Update()
        {
            if (light2d != null) light2d.intensity = baseIntensity + Mathf.Sin(Time.time * speed) * pulse;
        }
    }
}
