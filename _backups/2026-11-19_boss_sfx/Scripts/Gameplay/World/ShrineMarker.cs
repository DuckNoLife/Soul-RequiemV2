using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11 (v2): điểm nghỉ = ĐỐNG LỬA TRẠI được làm nổi (không vẽ thêm vòng phù văn): lửa của điểm nghỉ sáng hơn lửa trại thường,
    /// quầng sáng ấm phập phồng toả ra quanh lửa và tia lửa bay lên — người chơi tự nhận ra "lửa sáng = chỗ nghỉ".
    /// Bước vào lần đầu (Kindle): bùng sáng, lửa chuyển sang lửa linh hồn xanh lam và giữ như vậy (đã thắp).
    /// MapTriggerHandler tạo cho mỗi trigger checkpoint.
    /// </summary>
    public class ShrineMarker : MonoBehaviour
    {
        private static readonly Color Warm = new Color(1f, 0.72f, 0.38f), Spirit = new Color(0.45f, 0.85f, 1f);
        private Light2D glow;
        private SpriteRenderer[] flames = new SpriteRenderer[0];
        private Color[] flameBase = new Color[0];
        private bool lit;
        private float k, nextSpark;
        private int layer, order;

        /// <summary>02/11: mọi lửa trại nghỉ trong map (HUD vẽ biểu tượng trên minimap / bản đồ lớn: cam = chưa thắp, xanh = đã thắp).</summary>
        public static readonly System.Collections.Generic.List<ShrineMarker> All = new System.Collections.Generic.List<ShrineMarker>();
        /// <summary>02/11: dòng giải thích lần đầu thắp — HUD ghép vào ô nhắc [T] (một ô duy nhất, không hiện gợi ý thứ hai).</summary>
        public static string Notice = "";
        /// <summary>02/11: chữ ô nhắc nghỉ theo điểm nghỉ đang đứng (thuộc tính Tiled `rest_noun`, mặc định "shrine"; Map 1 = "campfire").</summary>
        public static string RestLabel = "Rest at the shrine";
        /// <summary>02/11: tên điểm nghỉ của map đang chơi ("shrine" / "campfire") — chữ màn chết, menu nghỉ, gợi ý dùng chung.</summary>
        public static string Noun = "shrine";
        public static float NoticeUntil;
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        public static ShrineMarker Create(Rect area, Transform mapRoot, int sortingLayerId)
        {
            // ngọn lửa của điểm nghỉ = tile object có hoạt ảnh gần vùng nghỉ (trong 1.6 đv)
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            if (mapRoot != null)
                foreach (var a in mapRoot.GetComponentsInChildren<TileObjectAnimator>(true))
                {
                    var r = a.GetComponent<SpriteRenderer>();
                    if (r == null) continue;
                    Vector2 p = r.bounds.center;
                    float dx = Mathf.Max(area.xMin - p.x, 0f, p.x - area.xMax), dy = Mathf.Max(area.yMin - p.y, 0f, p.y - area.yMax);
                    if (dx * dx + dy * dy < 0.4f * 0.4f) list.Add(r);     // 02/11: vùng nghỉ giờ bao quanh đống lửa -> chỉ lấy lửa / khói nằm trong
                }
            Vector2 at = area.center;                                       // vùng nghỉ đặt tâm trên đống lửa

            var go = new GameObject("Shrine Marker");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var m = go.AddComponent<ShrineMarker>();
            m.flames = list.ToArray();
            m.flameBase = new Color[m.flames.Length];
            for (int i = 0; i < m.flames.Length; i++) m.flameBase[i] = m.flames[i].color;
            m.layer = sortingLayerId;
            m.order = list.Count > 0 ? list[0].sortingOrder + 1 : 3;
            m.glow = go.AddComponent<Light2D>();
            m.glow.lightType = Light2D.LightType.Point;
            m.glow.pointLightOuterRadius = 1.7f;
            m.glow.pointLightInnerRadius = 0.2f;
            m.Apply();
            return m;
        }

        public bool Lit => lit;

        /// <summary>04/11: khung bao các ngọn lửa / nến của điểm nghỉ (đặt vòng sáng đúng dưới chân cụm nến). false = không có.</summary>
        public bool TryFlameBounds(out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var f in flames)
            {
                if (f == null) continue;
                if (!any) { b = f.bounds; any = true; } else b.Encapsulate(f.bounds);
            }
            return any;
        }

        /// <summary>02/11: đã thắp từ bản lưu — sáng xanh luôn, không bùng VFX.</summary>
        public void SetLit() => lit = true;

        /// <summary>Thắp điểm nghỉ (lần đầu bước vào): bùng sáng, lửa hoá xanh.</summary>
        public void Kindle()
        {
            if (lit) return;
            lit = true;
            SoulRequiem.Core.Sfx.Play("checkpoint", transform.position);   // 17/11 (§294): âm
            var fx = SpriteVfx.Spawn("m3_heal_lantern", transform.position + new Vector3(0f, -0.1f, 0f), 1.1f, layer, 40, false, 14f);
            if (fx != null) fx.Tint(new Color(0.6f, 0.9f, 1f, 1f));
        }

        private void Update()
        {
            k = Mathf.MoveTowards(k, lit ? 1f : 0f, Time.deltaTime * 1.5f);
            Apply();
            // tia lửa bay lên (nhỏ, thưa) — thứ lửa trại thường không có
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + Random.Range(0.35f, 0.7f);
                var s = SpriteVfx.Spawn("m3_hymn_pop", transform.position + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(0.05f, 0.25f), 0f),
                                        0.25f, layer, order, false, 12f);
                if (s != null) s.Tint(lit ? new Color(0.6f, 0.95f, 1f, 0.9f) : new Color(1f, 0.8f, 0.45f, 0.9f));
            }
        }

        private void Apply()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
            if (glow != null)
            {
                glow.color = Color.Lerp(Warm, Spirit, k);
                glow.intensity = Mathf.Lerp(0.75f, 1.05f, k) + pulse * 0.25f;
            }
            for (int i = 0; i < flames.Length; i++)
                if (flames[i] != null)
                {
                    // chưa thắp: lửa sáng rực hơn lửa thường; đã thắp: lửa linh hồn xanh lam
                    Color bright = new Color(1f, 0.95f, 0.8f, flameBase[i].a);
                    Color spirit = new Color(Spirit.r, Spirit.g, Spirit.b, flameBase[i].a);
                    flames[i].color = Color.Lerp(bright, spirit, k);
                }
        }
    }
}
