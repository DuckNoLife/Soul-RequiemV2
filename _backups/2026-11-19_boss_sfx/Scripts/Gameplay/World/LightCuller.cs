using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 05/11: cắt đèn map ngoài khung camera (hiệu năng, nhất là mobile). Map 2 có ~120 đèn Light2D.
    /// Cứ 0,2 giây: đèn điểm của map (tên "Light ..." do Dựng scene tạo) nằm ngoài khung camera + lề (bán kính đèn + margin) -> Light2D tắt hẳn
    /// và không chạy hoạt ảnh; vào lại khung -> sáng theo đúng trạng thái cờ (LightAnimator giữ trạng thái).
    /// Đèn không có LightAnimator: bộ cắt nhớ nó đang bật hay tắt lúc cắt và chỉ bật lại cái nó đã tự tắt (không gắn thêm component —
    /// boss Map 3 Morrow chỉnh thẳng cường độ / tắt đèn trụ). Đèn tạo lúc chạy (boss, trụ phong ấn, cổ vật...) không đụng tới. Tự tạo khi scene có MapEncounterDirector.
    /// Số liệu cho bảng test (F1): Total, Active.
    /// </summary>
    public class LightCuller : MonoBehaviour
    {
        public float interval = 0.2f;
        [Tooltip("Lề thêm ngoài bán kính đèn (đơn vị) — đèn bật sớm hơn một chút trước khi vào khung")]
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
                    else if (e.wasOn) e.l.enabled = true;          // chỉ bật lại đèn do chính bộ cắt tắt
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
