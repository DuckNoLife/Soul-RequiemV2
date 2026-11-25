#if UNITY_EDITOR || DEVELOPMENT_BUILD   // diagnostic tool — not in release builds
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SoulRequiem.DevTools
{
    /// <summary>
    /// Measures map load time (only runs when the file Logs/claude_preview/loadprof.on exists — delete it to turn off).
    /// Writes "[LoadProfile]" lines to the Console / Editor.log:
    ///   - milestones: start (after domain reload) -> before scene load -> after scene load (Awake done) -> frame 1 (Start done) -> frame 30;
    ///   - the 8 slowest frames in the first 10 seconds (ms + shader compile count + GC alloc in that frame);
    ///   - number of enabled Light2D / shadow-casting lights, Tilemaps, GameObjects.
    /// Use it to find out why a map is slow to enter Play Mode.
    /// </summary>
    public class LoadProfiler : MonoBehaviour
    {
        static Stopwatch sw;
        static double tBeforeScene, tAfterScene;
        static bool on;

        struct Frame { public int i; public double ms, at; public long shaders, gc; }
        readonly List<Frame> frames = new List<Frame>();
        double last, tFrame1 = -1, tFrame30 = -1;
        int n;
        ProfilerRecorder shaderRec, gcRec;

        static bool Enabled() => File.Exists(Path.Combine(Application.dataPath, "../Logs/claude_preview/loadprof.on"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Begin()
        {
            on = Enabled();
            if (!on) return;
            sw = Stopwatch.StartNew();
            Debug.Log("[LoadProfile] start (sau domain reload), scene = " + SceneManager.GetActiveScene().name);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeScene()
        {
            if (!on) return;
            tBeforeScene = sw.Elapsed.TotalMilliseconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterScene()
        {
            if (!on) return;
            tAfterScene = sw.Elapsed.TotalMilliseconds;
            var go = new GameObject("LoadProfiler");
            DontDestroyOnLoad(go);
            go.AddComponent<LoadProfiler>();
        }

        void OnEnable()
        {
            shaderRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shader.CreateGPUProgram");
            gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            last = sw.Elapsed.TotalMilliseconds;
        }

        void OnDisable()
        {
            shaderRec.Dispose();
            gcRec.Dispose();
        }

        void Update()
        {
            double now = sw.Elapsed.TotalMilliseconds;
            n++;
            frames.Add(new Frame
            {
                i = n, ms = now - last, at = now,
                shaders = shaderRec.Valid ? shaderRec.LastValue : -1,
                gc = gcRec.Valid ? gcRec.LastValue : -1,
            });
            last = now;
            if (n == 1) tFrame1 = now;
            if (n == 30) tFrame30 = now;
            if (now - tAfterScene > 10000.0) Report();
        }

        void Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[LoadProfile] scene " + SceneManager.GetActiveScene().name);
            sb.AppendLine($"  before scene load: {tBeforeScene:0} ms | after scene load (Awake): {tAfterScene:0} ms | frame 1 (Start): {tFrame1:0} ms | frame 30: {tFrame30:0} ms");
            sb.AppendLine($"  first 10 s: {frames.Count} frames");
            var sorted = new List<Frame>(frames);
            sorted.Sort((a, b) => b.ms.CompareTo(a.ms));
            for (int k = 0; k < Mathf.Min(8, sorted.Count); k++)
            {
                var f = sorted[k];
                sb.AppendLine($"  frame {f.i,4}: {f.ms,8:0.0} ms  (at {f.at:0} ms, shaders compiled {f.shaders}, GC {f.gc / 1024} KB)");
            }
            int lights = 0, shadowLights = 0;
            // DevTools does not reference URP -> finds Light2D by type name and reads shadowsEnabled via reflection
            foreach (var l in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            {
                if (l == null || !l.isActiveAndEnabled || l.GetType().Name != "Light2D") continue;
                lights++;
                var pr = l.GetType().GetProperty("shadowsEnabled");
                if (pr != null && pr.GetValue(l) is bool sh && sh) shadowLights++;
            }
            int tilemaps = FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).Length;
            int objects = FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            sb.AppendLine($"  Light2D on: {lights} (with shadows: {shadowLights}) | Tilemap: {tilemaps} | GameObject: {objects}");
            Debug.Log(sb.ToString());
            Destroy(gameObject);
        }
    }
}
#endif
