using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Culls map lights outside the camera frame (performance, especially on mobile). Map 2 has ~120 Light2D lights.
    /// Every 0.2 seconds: map point lights (named "Light ..." by the scene builder) outside the camera frame + margin (light radius + margin) -> Light2D fully off
    /// and no animation; back in frame -> shines according to the flag state (LightAnimator keeps it).
    /// Lights without a LightAnimator: the culler remembers whether it was on when culled and only re-enables lights it turned off itself (no extra component -
    /// the Map 3 boss Morrow sets pillar light intensity / turns them off directly). Lights created at runtime (bosses, seal pillars, relics...) are left alone. Created automatically when the scene has a MapEncounterDirector.
    /// Numbers for the test panel (F1): Total, Active.
    /// </summary>
    public class LightCuller : MonoBehaviour
    {
        public float interval = 0.2f;
        [Tooltip("Extra margin beyond the light radius (units) - lights turn on a little before entering the frame")]
        public float margin = 1f;

        public static int Total { get; private set; }
        public static int Active { get; private set; }

        private sealed class Entry { public Light2D l; public LightAnimator la; public bool culled, wasOn; }
        private readonly List<Entry> lights = new List<Entry>();
        private float next;
        private Camera cam;
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { if (hooked) SceneManager.sceneLoaded -= OnSceneLoaded; hooked = false; Total = Active = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!hooked) { SceneManager.sceneLoaded += OnSceneLoaded; hooked = true; }
            Ensure();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();

        private static void Ensure()
        {
            if (FindFirstObjectByType<MapEncounterDirector>() != null && FindFirstObjectByType<LightCuller>() == null)
                new GameObject("LightCuller").AddComponent<LightCuller>();
        }

        private void Start()
        {
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.lightType != Light2D.LightType.Point || !l.name.StartsWith("Light ")) continue;
                lights.Add(new Entry { l = l, la = l.GetComponent<LightAnimator>() });
            }
            Total = lights.Count;
        }

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            if (cam == null) cam = Camera.main;
            if (cam == null || !cam.orthographic) return;
            Vector2 c = cam.transform.position;
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            int active = 0;
            for (int i = lights.Count - 1; i >= 0; i--)
            {
                var e = lights[i];
                if (e.l == null) { lights.RemoveAt(i); continue; }
                Vector2 p = e.l.transform.position;
                float r = e.l.pointLightOuterRadius + margin;
                bool cull = !(Mathf.Abs(p.x - c.x) <= hw + r && Mathf.Abs(p.y - c.y) <= hh + r);
                if (e.la != null) e.la.SetCulled(cull);
                else if (cull != e.culled)
                {
                    if (cull) { e.wasOn = e.l.enabled; e.l.enabled = false; }
                    else if (e.wasOn) e.l.enabled = true;          // only re-enable lights the culler turned off itself
                    e.culled = cull;
                }
                if (e.l.enabled) active++;
            }
            Total = lights.Count;
            Active = active;
        }

        private void OnDestroy() { Total = Active = 0; }
    }
}
