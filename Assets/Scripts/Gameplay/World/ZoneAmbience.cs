using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Ambient light per area: each object in the Tiled 'zones' layer can declare
    ///   ambient        - Global Light intensity while the player is inside the area (e.g. 0.42)
    ///   ambient_color  - ambient colour (#rrggbb)
    ///   ambient_fight  - intensity while the area is in combat (fight started, not cleared) - boss arenas get darker
    /// Outside every area (corridors) corridorIntensity / corridorColor are used. Smooth transition between areas.
    /// Map2SceneBuilder adds this component next to MapEncounterDirector.
    /// </summary>
    public class ZoneAmbience : MonoBehaviour
    {
        public float corridorIntensity = 0.36f;
        public Color corridorColor = new Color(0.72f, 0.80f, 0.92f);
        [Tooltip("Ambient transition time between two areas (seconds)")] public float blendSeconds = 1.1f;

        private class Z { public string id; public Rect rect; public float amb = -1f, fight = -1f; public Color col; public bool hasCol; }

        private readonly List<Z> zones = new List<Z>();
        private Light2D global;
        private Transform player;
        private MapEncounterDirector dir;

        /// <summary>Current ambient brightness (0..1+) - SoulLantern uses it to dim itself in bright places.</summary>
        public static float CurrentLuminance { get; private set; } = 1f;
        /// <summary>Extra ambient multiplier (the "lights out" miniboss sets it &lt; 1, remember to restore it to 1).</summary>
        public static float DimMul = 1f;
        /// <summary>Ambient colour multiplier (The Unwritten domain changes tone with the Vessel's phase). Remember to restore Color.white.</summary>
        public static Color ColorMul = Color.white;

        private IEnumerator Start()
        {
            DimMul = 1f;
            ColorMul = Color.white;
            yield return null;                               // wait for the Director to read the map
            dir = MapEncounterDirector.Instance;
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global) { global = l; break; }
            var pc = FindFirstObjectByType<PlayerController>();
            if (global == null || pc == null || dir == null || dir.mapRoot == null) { enabled = false; yield break; }
            player = pc.transform;

            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.transform.parent == null || so.transform.parent.name != "zones") continue;
                var col = so.GetComponent<Collider2D>();
                if (col == null) continue;
                var props = so.GetComponent<SuperCustomProperties>();
                var z = new Z { id = so.m_TiledName, rect = new Rect(col.bounds.min, col.bounds.size) };
                z.amb = Float(props, "ambient", -1f);
                z.fight = Float(props, "ambient_fight", -1f);
                string c = Str(props, "ambient_color", "");
                z.hasCol = !string.IsNullOrEmpty(c) && ColorUtility.TryParseHtmlString(c, out z.col);
                if (z.amb >= 0f || z.hasCol) zones.Add(z);
            }
            // small areas first (the Cistern Gate sits right next to a large area) so small areas win when they overlap
            zones.Sort((a, b) => (a.rect.width * a.rect.height).CompareTo(b.rect.width * b.rect.height));
            Debug.Log($"[ZoneAmbience] {zones.Count} areas with custom ambient light.");
        }

        private void Update()
        {
            if (global == null || player == null) return;
            Vector2 p = player.position;
            float ti = corridorIntensity; Color tc = corridorColor;
            foreach (var z in zones)
            {
                if (!z.rect.Contains(p)) continue;
                if (z.amb >= 0f) ti = z.amb;
                if (z.hasCol) tc = z.col;
                if (z.fight >= 0f && dir.Flags.Has(z.id + "_started") && !dir.Flags.Has(z.id + "_cleared")) ti = z.fight;
                break;
            }
            ti *= DimMul;
            tc = new Color(Mathf.Min(1f, tc.r * ColorMul.r), Mathf.Min(1f, tc.g * ColorMul.g), Mathf.Min(1f, tc.b * ColorMul.b), tc.a);
            float k = 1f - Mathf.Exp(-Time.deltaTime * 3f / Mathf.Max(0.05f, blendSeconds));
            global.intensity = Mathf.Lerp(global.intensity, ti, k);
            global.color = Color.Lerp(global.color, tc, k);
            var c = global.color;
            CurrentLuminance = global.intensity * (0.3f * c.r + 0.59f * c.g + 0.11f * c.b);
        }

        private void OnDisable()
        {
            CurrentLuminance = 1f;
        }

        // (calls TiledProps - the only place that reads Tiled properties)
        static string Str(SuperCustomProperties p, string name, string fallback) => p.GetString(name, fallback);
        static float Float(SuperCustomProperties p, string name, float fallback) => p.GetFloat(name, fallback);
    }
}
