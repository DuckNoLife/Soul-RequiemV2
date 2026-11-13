using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Ánh sáng nền theo khu: mỗi object trong lớp 'zones' của Tiled có thể khai báo
    ///   ambient        – cường độ Global Light khi người chơi ở trong khu (vd. 0.42)
    ///   ambient_color  – màu ánh nền (#rrggbb)
    ///   ambient_fight  – cường độ khi khu đang đánh (đã vào trận, chưa dọn sạch) — đấu trường boss tối lại
    /// Ngoài mọi khu (hành lang) dùng corridorIntensity / corridorColor. Chuyển mượt khi đổi khu.
    /// Map2SceneBuilder gắn component này cạnh MapEncounterDirector.
    /// </summary>
    public class ZoneAmbience : MonoBehaviour
    {
        public float corridorIntensity = 0.36f;
        public Color corridorColor = new Color(0.72f, 0.80f, 0.92f);
        [Tooltip("Thời gian chuyển ánh nền giữa hai khu (giây)")] public float blendSeconds = 1.1f;

        private class Z { public string id; public Rect rect; public float amb = -1f, fight = -1f; public Color col; public bool hasCol; }

        private readonly List<Z> zones = new List<Z>();
        private Light2D global;
        private Transform player;
        private MapEncounterDirector dir;

        /// <summary>Độ sáng nền hiện tại (0..1+) — SoulLantern dùng để tự giảm ở chỗ sáng.</summary>
        public static float CurrentLuminance { get; private set; } = 1f;
        /// <summary>Nhân thêm vào ánh nền (miniboss "dập đèn" đặt &lt; 1, nhớ trả về 1).</summary>
        public static float DimMul = 1f;

        private IEnumerator Start()
        {
            DimMul = 1f;
            yield return null;                               // đợi Director đọc map
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
            // khu nhỏ trước (cổng Cistern Gate nằm sát khu lớn) để khu nhỏ được ưu tiên khi chồng nhau
            zones.Sort((a, b) => (a.rect.width * a.rect.height).CompareTo(b.rect.width * b.rect.height));
            Debug.Log($"[ZoneAmbience] {zones.Count} khu có ánh nền riêng.");
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

        // (gọi về TiledProps — nơi duy nhất đọc thuộc tính Tiled)
        static string Str(SuperCustomProperties p, string name, string fallback) => p.GetString(name, fallback);
        static float Float(SuperCustomProperties p, string name, float fallback) => p.GetFloat(name, fallback);
    }
}
