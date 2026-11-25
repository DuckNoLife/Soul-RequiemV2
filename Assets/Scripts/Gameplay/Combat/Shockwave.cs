using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Shockwave spreading from a point (toad landing...). A thin ring grows from 0 to `maxRadius` over `duration` seconds;
    /// a player standing right at the rim as it passes is hit once (a roll dodges it because Health ignores hits while invulnerable).
    /// </summary>
    public class Shockwave : MonoBehaviour
    {
        public float maxRadius = 2f, duration = 0.6f, damage = 10f, band = 0.16f, knockback = 2.5f;
        public Color color = new Color(0.75f, 1f, 0.55f, 0.9f);
        public int sortingOrder = 1;
        [Tooltip("Gaps in the ring (degrees, 0 = right, 90 = up): standing exactly in a gap lets the wave pass without a hit. Empty = closed ring.")]
        public float[] gapAngles;
        public float gapWidth = 40f;
        [Tooltip("Gaps only open once the ring has grown past this radius (measured as squashed distance) — e.g. the shadow BEHIND a pillar: in front of the pillar still hits.")]
        public float gapFrom = 0f;
        [Tooltip("Called once when the ring passes the player while the player stands in a gap (sheltered).")]
        public System.Action onSheltered;
        [Tooltip("Shelter condition (player's feet position) — e.g. standing inside a seal pillar's ring. True = the wave passes without a hit.")]
        public System.Func<Vector2, bool> shelter;
        [Tooltip("Called when the wave hits the player. Damage 0 + onHit = the wave only knocks back / applies a status.")]
        public System.Action<Health> onHit;
        [Header("PixelLab skin (optional)")]
        [Tooltip("PixelLab wave segment (tip toward +x), laid around the ring instead of the hand-drawn ring; gaps = segments dropped.")]
        public Sprite[] crest;
        public float crestSpacing = 0.2f, crestScale = 1f, crestFps = 12f;
        [Header("Hit detection only (visuals drawn by another VFX)")]
        [Tooltip("Hide its own ring — when the wave visual is a separate SpriteVfx (e.g. the Lantern Knight's ground_impact).")]
        public bool hidden;
        [Tooltip("Vertical squash of the ring (top-down perspective). 0.6 = hand-drawn ring; ground_impact ≈ 0.4.")]
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
            SoulRequiem.Core.Sfx.Play("shockwave", at); // sound
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
            float e = 1f - (1f - k) * (1f - k);                 // grows fast then slows down
            float r = Mathf.Max(0.01f, maxRadius * e);
            if (hidden) { }
            else if (crest != null && crest.Length > 0) DrawCrest(r, color.a * (1f - k * k));
            else
            {
                if (!gapShown && gapSprite != null && r >= gapFrom) { gapShown = true; sr.sprite = gapSprite; }   // ring just passed the pillar: open the gap
                transform.localScale = new Vector3(r, r * squash, 1f);  // squashed for the top-down perspective
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
                if (!landed) return;                       // rolled through: no hit, the wave goes on
                hitDone = true;
                if (damage <= 0f && knockback > 0f && d.sqrMagnitude > 0.0001f) playerHp.AddImpulse(d.normalized * knockback);
                onHit?.Invoke(playerHp);
            }
        }

        /// Lay the PixelLab wave segments around an ellipse of radius r (dropping segments inside gaps).
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

        /// Ring with gaps (a new image each time, cheap at 96x96). Gap edges get bright marks so the player sees the way out.
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

        /// Thin ring: 2 units across at scale 1 (scale = radius).
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
