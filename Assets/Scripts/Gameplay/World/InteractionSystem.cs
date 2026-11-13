using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Một vật tương tác được bằng phím Interact (mặc định T): bia, ghi chú, chuông, cần gạt...</summary>
    public class Interactable
    {
        public string id;
        public string verb = "Examine";
        public Func<string> verbNow;          // đổi chữ theo trạng thái (vd "Close" khi bảng đọc đang mở)
        public Rect rect;                     // vùng của vật (đơn vị thế giới)
        public Vector2 anchor;                // chỗ đặt nhắc phím "[T] Read"
        public Func<bool> available;          // null = luôn dùng được
        public Action use;
        public bool clue = true;              // vật dẫn truyện: có đốm sáng lấp lánh tới khi xem lần đầu
        public bool seen;
        public float range = 0.55f;           // khoảng cách tới mép vùng để nhấn được
        /// <summary>04/11: chỉ đọc / xem (biển, ghi chú, bảng giải đố) -> khoá khi đang giao chiến. Cần gạt, van, tay quay vẫn dùng được.</summary>
        public bool calmOnly;

        internal Light2D glow;
        internal Transform mark;
        internal SpriteRenderer[] sparks;

        public bool Available => available == null || available();
        public string Verb => verbNow != null ? verbNow() : verb;
    }

    /// <summary>
    /// 28/10: mọi tương tác trong map đi qua đây.
    ///   - Lại gần (trong `range` của vùng) -> vật được chọn, sáng mạnh, HUD hiện "[T] Read / Examine / Ring...".
    ///   - Trong 3,2 đv: vật thở sáng nhẹ. Manh mối chưa xem: đốm sáng vàng lấp lánh phía trên (thấy từ xa 8 đv).
    ///   - Nhấn Interact -> use(). Đòn chém KHÔNG còn kích hoạt chuông / cần gạt / bàn điều khiển (chỉ van, vật cản gỗ).
    /// Director đăng ký bia/biển (trigger "sign"), MapMechanics đăng ký ghi chú, khoá ký tự, chuông, cần gạt...
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        public static InteractionSystem Instance { get; private set; }
        public static Interactable Focus { get; private set; }

        private const float GlowRange = 3.2f, MarkRange = 8f;
        private readonly List<Interactable> items = new List<Interactable>();
        private Transform player;
        private Health playerHealth;
        private static Sprite star, dot;

        public static InteractionSystem Ensure()
        {
            if (Instance == null) Instance = new GameObject("InteractionSystem").AddComponent<InteractionSystem>();
            return Instance;
        }

        public static Interactable Add(Interactable it)
        {
            var s = Ensure();
            s.items.Add(it);
            s.BuildVisuals(it);
            return it;
        }

        private void Awake() { Instance = this; Focus = null; }
        private void OnDestroy() { if (Instance == this) { Instance = null; Focus = null; } }

        private void BuildVisuals(Interactable it)
        {
            var go = new GameObject("Interact " + it.id);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(it.rect.center.x, it.rect.center.y, 0f);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = Mathf.Clamp(Mathf.Max(it.rect.width, it.rect.height) * 0.6f, 0.6f, 1.4f);
            l.pointLightInnerRadius = 0.05f;
            l.falloffIntensity = 0.6f;
            l.color = new Color(1f, 0.86f, 0.55f);
            l.intensity = 0f;
            it.glow = l;

            if (!it.clue) return;
            var m = new GameObject("Clue mark " + it.id).transform;
            m.SetParent(transform, false);
            m.position = new Vector3(it.anchor.x, it.anchor.y + 0.14f, 0f);
            it.sparks = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var sr = new GameObject("s" + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(m, false);
                sr.sprite = i == 0 ? Star : Dot;
                sr.sortingOrder = 300;
                sr.color = new Color(1f, 0.88f, 0.5f, 1f);
                it.sparks[i] = sr;
            }
            it.mark = m;
            m.gameObject.SetActive(false);
        }

        private static float DistToRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private void Update()
        {
            if (player == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc == null) return;
                player = pc.transform;
                playerHealth = pc.GetComponent<Health>();
            }
            bool blocked = GameInput.Blocked || MapMechanics.ConsoleActive || (playerHealth != null && playerHealth.IsDead);
            // 03/11 (chạy thử Map 2): đứng ở điểm nghỉ thì [T] = nghỉ — biển đọc gần đó (vd hub_south cạnh nến ở Sluice Hall) không giành phím
            var med = MapEncounterDirector.Instance;
            if (med != null && med.AtCheckpoint) blocked = true;
            bool fighting = med != null && med.InCombat;              // 04/11: giữa trận không mở biển / ghi chú (bấm nhầm)
            Vector2 p = (Vector2)player.position + Vector2.up * 0.2f;

            Interactable best = null;
            float bestScore = float.MaxValue;
            if (!blocked)
                foreach (var it in items)
                {
                    if (!it.Available || (fighting && it.calmOnly)) continue;
                    float edge = DistToRect(it.rect, p);
                    if (edge > it.range) continue;
                    // đứng trong nhiều vùng chồng nhau: ưu tiên vật gần tâm hơn
                    float score = edge + 0.15f * Vector2.Distance(p, it.rect.center);
                    if (score < bestScore) { bestScore = score; best = it; }
                }
            Focus = best;

            bool calm = GameSettings.ReduceFlashing;
            float t = Time.time;
            foreach (var it in items)
            {
                bool av = it.Available && !(fighting && it.calmOnly);
                float d = Vector2.Distance(p, it.rect.center);
                float edge = DistToRect(it.rect, p);
                float wave = calm ? 0f : Mathf.Sin(t * 2.5f + it.rect.x);
                float target = !av ? 0f
                             : it == Focus ? 1.4f + (calm ? 0f : 0.3f * Mathf.Sin(t * 6f))
                             : edge < GlowRange ? (it.seen ? 0.3f : 0.6f) + 0.2f * wave : 0f;
                if (it.glow != null) it.glow.intensity = Mathf.MoveTowards(it.glow.intensity, target, Time.deltaTime * 4f);

                if (it.mark == null) continue;
                bool show = av && !it.seen && d < MarkRange && it != Focus;
                if (it.mark.gameObject.activeSelf != show) it.mark.gameObject.SetActive(show);
                if (!show) continue;
                float bob = calm ? 0f : 0.035f * Mathf.Sin(t * 2.2f + it.rect.y);
                it.mark.position = new Vector3(it.anchor.x, it.anchor.y + 0.14f + bob, 0f);
                float tw = calm ? 1f : 0.5f + 0.5f * Mathf.Sin(t * 3.1f + it.rect.x);
                it.sparks[0].transform.localScale = Vector3.one * (0.85f + 0.3f * tw);
                it.sparks[0].color = new Color(1f, 0.9f, 0.55f, 0.65f + 0.35f * tw);
                for (int i = 1; i < 3; i++)
                {
                    float a = t * 1.6f + i * Mathf.PI;
                    it.sparks[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 0.09f, Mathf.Sin(a) * 0.05f - 0.02f, 0f);
                    float k = 0.5f + 0.5f * Mathf.Sin(t * 4f + i * 2f);
                    it.sparks[i].color = new Color(1f, 0.9f, 0.6f, calm ? 0.8f : k);
                }
            }

            if (Focus != null && GameInput.Pressed(GameAction.Interact))
            {
                var f = Focus;
                f.seen = true;
                f.use?.Invoke();
            }
        }

        // sao 4 cánh 9x9 px và chấm 3x3 px (1 px ảnh = 1 px game ở PPU 100)
        private static Sprite Star
        {
            get
            {
                if (star != null) return star;
                const int n = 9;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int dx = Mathf.Abs(x - 4), dy = Mathf.Abs(y - 4);
                        bool on = (dx == 0 && dy <= 4) || (dy == 0 && dx <= 4) || (dx + dy <= 2);
                        bool core = dx + dy <= 1;
                        px[y * n + x] = on ? (core ? new Color32(255, 255, 255, 255) : new Color32(255, 230, 160, 255)) : new Color32(0, 0, 0, 0);
                    }
                tex.SetPixels32(px); tex.Apply();
                return star = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        private static Sprite Dot
        {
            get
            {
                if (dot != null) return dot;
                const int n = 3;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                for (int i = 0; i < px.Length; i++) px[i] = (i == 4 || i % 2 == 1) ? new Color32(255, 240, 190, 255) : new Color32(0, 0, 0, 0);
                tex.SetPixels32(px); tex.Apply();
                return dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
