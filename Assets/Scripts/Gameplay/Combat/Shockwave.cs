using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Sóng chấn động lan ra từ một điểm (cóc tiếp đất...). Vòng mỏng nở từ 0 tới `maxRadius` trong `duration` giây;
    /// người chơi đứng đúng mép vòng lúc nó đi qua thì trúng 1 lần (lăn/Roll né được vì Health bỏ qua khi bất tử).
    /// </summary>
    public class Shockwave : MonoBehaviour
    {
        public float maxRadius = 2f, duration = 0.6f, damage = 10f, band = 0.16f, knockback = 2.5f;
        public Color color = new Color(0.75f, 1f, 0.55f, 0.9f);
        public int sortingOrder = 1;
        [Tooltip("Khe hở trên vòng (độ, 0 = phải, 90 = lên): đứng đúng khe thì sóng đi qua không trúng. Rỗng = vòng kín.")]
        public float[] gapAngles;
        public float gapWidth = 40f;
        [Tooltip("Khe chỉ mở khi vòng đã nở quá bán kính này (đo như khoảng cách dẹt) — vd bóng che phía SAU một trụ: trước trụ vẫn trúng.")]
        public float gapFrom = 0f;
        [Tooltip("Gọi một lần khi vòng đi qua chỗ người chơi mà người chơi đang đứng trong khe (được che).")]
        public System.Action onSheltered;
        [Tooltip("05/11: điều kiện được che (vị trí chân người chơi) — vd đứng trong vòng ấn của trụ phong ấn. Đúng = sóng đi qua không trúng.")]
        public System.Func<Vector2, bool> shelter;
        [Tooltip("Gọi khi sóng trúng người chơi. Sát thương 0 + onHit = sóng chỉ đẩy lùi / gây trạng thái.")]
        public System.Action<Health> onHit;
        [Header("Vỏ PixelLab (tuỳ chọn)")]
        [Tooltip("Mảnh sóng PixelLab (mũi hướng +x), xếp quanh vòng thay cho vòng vẽ tay; khe hở = bỏ mảnh.")]
        public Sprite[] crest;
        public float crestSpacing = 0.2f, crestScale = 1f, crestFps = 12f;
        [Header("Chỉ tính trúng (hình do VFX khác vẽ)")]
        [Tooltip("Ẩn vòng của chính nó — dùng khi hình sóng là một SpriteVfx riêng (vd ground_impact của Lantern Knight).")]
        public bool hidden;
        [Tooltip("Độ dẹt của vòng theo trục dọc (phối cảnh top-down). 0,6 = vòng vẽ tay; ground_impact ≈ 0,4.")]
        public float squash = 0.6f;
        private readonly List<SpriteRenderer> crestPool = new List<SpriteRenderer>();

        private float t0;
        private bool hitDone, gapShown;
        private SpriteRenderer sr;
        private Sprite gapSprite;
        private static Sprite ringSprite;
        private static Health playerHp;

        public static Shockwave Spawn(Vector2 at, float maxRadius, float duration, float damage, int order, Color color)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = at;
            SoulRequiem.Core.Sfx.Play("shockwave", at); // 19/11 (§335): âm
            var s = go.AddComponent<Shockwave>();
            s.maxRadius = maxRadius; s.duration = duration; s.damage = damage; s.sortingOrder = order; s.color = color;
            return s;
        }

        private void Start()
        {
            t0 = Time.time;
            sr = gameObject.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.enabled = !hidden;
            bool gaps = gapAngles != null && gapAngles.Length > 0;
            if (gaps) gapSprite = GapRing(gapAngles, gapWidth);
            gapShown = gaps && gapFrom <= 0f;
            if (crest == null || crest.Length == 0)
                sr.sprite = gapShown ? gapSprite : RingSprite;
            sr.sortingOrder = sortingOrder;
            sr.color = color;
            transform.localScale = new Vector3(0.01f, 0.01f * squash, 1f);
            if (playerHp == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerHp = pc.GetComponent<Health>();
            }
        }

        private void Update()
        {
            float k = (Time.time - t0) / Mathf.Max(0.01f, duration);
            if (k >= 1f) { Destroy(gameObject); return; }
            float e = 1f - (1f - k) * (1f - k);                 // nở nhanh rồi chậm lại
            float r = Mathf.Max(0.01f, maxRadius * e);
            if (hidden) { }
            else if (crest != null && crest.Length > 0) DrawCrest(r, color.a * (1f - k * k));
            else
            {
                if (!gapShown && gapSprite != null && r >= gapFrom) { gapShown = true; sr.sprite = gapSprite; }   // vòng vừa vượt qua trụ: lộ khe
                transform.localScale = new Vector3(r, r * squash, 1f);  // dẹt theo phối cảnh top-down
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
            }
            if (hitDone || playerHp == null || playerHp.IsDead || (damage <= 0f && onHit == null)) return;
            Vector2 d = (Vector2)playerHp.transform.position - (Vector2)transform.position;
            float dist = new Vector2(d.x, d.y / squash).magnitude;
            bool covered = (r >= gapFrom && InGap(d)) || (shelter != null && shelter(playerHp.transform.position));
            if (Mathf.Abs(dist - r) <= band + 0.08f && covered && onSheltered != null) { var cb = onSheltered; onSheltered = null; cb(); }
            if (Mathf.Abs(dist - r) <= band + 0.08f && !covered)
            {
                bool landed = damage > 0f ? playerHp.TakeDamage(damage, d, knockback) : !playerHp.Invulnerable;
                if (!landed) return;                       // lăn qua: không trúng, sóng đi tiếp
                hitDone = true;
                if (damage <= 0f && knockback > 0f && d.sqrMagnitude > 0.0001f) playerHp.AddImpulse(d.normalized * knockback);
                onHit?.Invoke(playerHp);
            }
        }

        /// Xếp các mảnh sóng PixelLab quanh elip bán kính r (bỏ mảnh nằm trong khe).
        private void DrawCrest(float r, float alpha)
        {
            transform.localScale = Vector3.one;
            float per = 2f * Mathf.PI * r * Mathf.Sqrt((1f + 0.36f) * 0.5f);
            int n = Mathf.Clamp(Mathf.CeilToInt(per / Mathf.Max(0.05f, crestSpacing)), 6, 180);
            while (crestPool.Count < n)
            {
                var g = new GameObject("Crest");
                g.transform.SetParent(transform, false);
                var c = g.AddComponent<SpriteRenderer>();
                FxLighting.MakeUnlit(c);
                c.sortingOrder = sortingOrder;
                g.transform.localScale = Vector3.one * crestScale;
                crestPool.Add(c);
            }
            int f0 = Mathf.FloorToInt((Time.time - t0) * crestFps);
            for (int i = 0; i < crestPool.Count; i++)
            {
                var c = crestPool[i];
                if (i >= n) { c.enabled = false; continue; }
                float a = i * 360f / n;
                if (InGapAngle(a)) { c.enabled = false; continue; }
                float rad = a * Mathf.Deg2Rad;
                c.enabled = true;
                c.transform.localPosition = new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r * 0.6f, 0f);
                c.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Mathf.Sin(rad) / 0.6f, Mathf.Cos(rad)) * Mathf.Rad2Deg);
                c.sprite = crest[(f0 + i) % crest.Length];
                c.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private bool InGapAngle(float a)
        {
            if (gapAngles == null) return false;
            foreach (var g in gapAngles) if (Mathf.Abs(Mathf.DeltaAngle(a, g)) <= gapWidth * 0.5f) return true;
            return false;
        }

        private bool InGap(Vector2 d)
        {
            if (gapAngles == null) return false;
            float a = Mathf.Atan2(d.y / squash, d.x) * Mathf.Rad2Deg;
            foreach (var g in gapAngles) if (Mathf.Abs(Mathf.DeltaAngle(a, g)) <= gapWidth * 0.5f) return true;
            return false;
        }

        /// Vòng có khe (mỗi lần tạo một ảnh riêng, 96x96 nên rẻ). Mép khe có vạch sáng để người chơi thấy lối thoát.
        private static Sprite GapRing(float[] gaps, float width)
        {
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    byte a = r > 1f ? (byte)0 : r > 0.9f ? (byte)255 : r > 0.82f ? (byte)110 : (byte)0;
                    if (a > 0)
                    {
                        float ang = Mathf.Atan2(y - c, x - c) * Mathf.Rad2Deg;
                        foreach (var g in gaps)
                        {
                            float dd = Mathf.Abs(Mathf.DeltaAngle(ang, g));
                            if (dd <= width * 0.5f) { a = 0; break; }
                        }
                    }
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
        }

        /// Vòng mỏng: đường kính 2 đơn vị ở scale 1 (scale = bán kính).
        private static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null) return ringSprite;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                float c = (n - 1) / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        byte a = r > 1f ? (byte)0 : r > 0.9f ? (byte)255 : r > 0.82f ? (byte)110 : (byte)0;
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
                return ringSprite;
            }
        }
    }
}
