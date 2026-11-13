#if UNITY_EDITOR || DEVELOPMENT_BUILD   // công cụ chẩn đoán — không có trong bản phát hành
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
    /// Đo thời gian vào map (chỉ chạy khi có file Logs/claude_preview/loadprof.on — xoá file để tắt).
    /// Ghi vào Console / Editor.log các dòng "[LoadProfile]":
    ///   - mốc: bắt đầu chạy (sau domain reload) -> trước nạp scene -> sau nạp scene (Awake xong) -> khung 1 (Start xong) -> khung 30;
    ///   - 8 khung chậm nhất trong 10 giây đầu (ms + số lần biên dịch shader + GC alloc trong khung đó);
    ///   - số đèn Light2D đang bật / có bóng đổ, số Tilemap, số GameObject.
    /// Dùng để tìm vì sao một map vào Play Mode chậm (05/11).
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
            sb.AppendLine($"  trước nạp scene: {tBeforeScene:0} ms | sau nạp scene (Awake): {tAfterScene:0} ms | khung 1 (Start): {tFrame1:0} ms | khung 30: {tFrame30:0} ms");
            sb.AppendLine($"  10 s đầu: {frames.Count} khung");
            var sorted = new List<Frame>(frames);
            sorted.Sort((a, b) => b.ms.CompareTo(a.ms));
            for (int k = 0; k < Mathf.Min(8, sorted.Count); k++)
            {
                var f = sorted[k];
                sb.AppendLine($"  khung {f.i,4}: {f.ms,8:0.0} ms  (lúc {f.at:0} ms, shader biên dịch {f.shaders}, GC {f.gc / 1024} KB)");
            }
            int lights = 0, shadowLights = 0;
            // DevTools không tham chiếu URP -> nhận Light2D theo tên kiểu, đọc shadowsEnabled bằng reflection
            foreach (var l in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            {
                if (l == null || !l.isActiveAndEnabled || l.GetType().Name != "Light2D") continue;
                lights++;
                var pr = l.GetType().GetProperty("shadowsEnabled");
                if (pr != null && pr.GetValue(l) is bool sh && sh) shadowLights++;
            }
            int tilemaps = FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).Length;
            int objects = FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            sb.AppendLine($"  Light2D bật: {lights} (có bóng đổ: {shadowLights}) | Tilemap: {tilemaps} | GameObject: {objects}");
            Debug.Log(sb.ToString());
            Destroy(gameObject);
        }
    }
}
#endif
