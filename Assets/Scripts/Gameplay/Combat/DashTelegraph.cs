using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// DASH / CHARGE WARNING AREA shared by every boss (a thin 4 px line was hard to see, did not show when the charge
    /// starts, and its length did not match the real charge).
    /// Drawn in code (1:1 pixels at PPU 100, no external images):
    ///   • a LANE exactly as wide as the hit area (boss body + Ren body) — shows how far to step aside;
    ///   • faint lane fill + 2 bright EDGES;
    ///   • a CHARGE STRIP running from the origin to the lane end over the exact wind-up time — full strip = charge (readable timing);
    ///   • ">" ARROWS drifting in the charge direction; a TARGET MARK (ring + X) where the boss will stop;
    ///   • last 25%: direction locked (if the skill tracks) -> fast blinking + brighter = about to charge.
    /// Usage: var tg = DashTelegraph.Spawn(...); (while winding up: tg.Aim(...) to change direction) -> on the charge: tg.Release().
    /// Forgot Release (boss died mid-way) -> destroys itself after the wind-up + 0.6 s.
    /// DashTelegraph.Reach: the real charge distance (stops before walls / pillars, ignores triggers / the boss itself / Ren / other enemies).
    /// </summary>
    public sealed class DashTelegraph : MonoBehaviour
    {
        private SpriteRenderer fill, charge, edgeA, edgeB, endMark;
        private readonly List<SpriteRenderer> chevrons = new List<SpriteRenderer>();
        private Color color;
        private float length, width, seconds, born, releasedAt = -1f;
        private const float Spacing = 0.34f, Edge = 0.03f;
        private static Sprite pixel, chevron, target;

        // ------------------------------------------------------------------ create / control
        /// <param name="from">lane origin (boss feet)</param>
        /// <param name="dir">charge direction</param>
        /// <param name="length">real charge distance (take it from Reach)</param>
        /// <param name="width">lane width = hit area (see WidthFor)</param>
        /// <param name="seconds">wind-up time — the charge strip fills exactly when the charge starts</param>
        public static DashTelegraph Spawn(Vector2 from, Vector2 dir, float length, float width, Color color, float seconds, int sortingLayer, int sortingOrder)
        {
            Build();
            var go = new GameObject("DashTelegraph");
            var t = go.AddComponent<DashTelegraph>();
            t.color = color; t.seconds = Mathf.Max(0.05f, seconds); t.born = Time.time; t.width = Mathf.Max(0.2f, width);
            t.fill = t.Part("fill", pixel, sortingLayer, sortingOrder);
            t.charge = t.Part("charge", pixel, sortingLayer, sortingOrder);
            t.edgeA = t.Part("edge", pixel, sortingLayer, sortingOrder + 1);
            t.edgeB = t.Part("edge", pixel, sortingLayer, sortingOrder + 1);
            t.endMark = t.Part("end", target, sortingLayer, sortingOrder + 1);
            t.sortLayer = sortingLayer; t.sortOrder = sortingOrder + 1;
            t.Aim(from, dir, length);
            t.LateUpdate();
            return t;
        }

        private int sortLayer, sortOrder;

        /// <summary>Change origin / direction / length (skills tracking Ren during the wind-up).</summary>
        public void Aim(Vector2 from, Vector2 dir, float len)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            length = Mathf.Max(0.3f, len);
            transform.position = from;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>Charge starts: the lane flashes then fades fast (0.15 s) and self-destroys.</summary>
        public void Release()
        {
            if (releasedAt >= 0f) return;
            releasedAt = Time.time;
        }

        // ------------------------------------------------------------------ draw every frame
        private void LateUpdate()
        {
            float age = Time.time - born;
            if (releasedAt < 0f && age > seconds + 0.6f) releasedAt = Time.time;   // Release forgotten
            float fade = 1f;
            if (releasedAt >= 0f)
            {
                float k = (Time.time - releasedAt) / 0.15f;
                if (k >= 1f) { Destroy(gameObject); return; }
                fade = 1f - k;
            }
            float u = Mathf.Clamp01(age / seconds);
            bool late = u > 0.75f;
            float blink = late ? (Mathf.Repeat(Time.time * 14f, 1f) < 0.5f ? 1f : 0.55f) : 0.85f + 0.15f * Mathf.Sin(Time.time * 9f);
            if (releasedAt >= 0f) blink = 1.3f;                                     // flash on the charge
            float half = width * 0.5f;
            // lane fill + charge strip (origin on the left, stretched along local x)
            Place(fill, 0f, 0f, length, width, Alpha(0.16f * blink * fade));
            Place(charge, 0f, 0f, length * u, width, Alpha((late ? 0.34f : 0.24f) * fade));
            Place(edgeA, 0f, half - Edge * 0.5f, length, Edge, Alpha(0.9f * blink * fade));
            Place(edgeB, 0f, -half + Edge * 0.5f, length, Edge, Alpha(0.9f * blink * fade));
            // target mark
            endMark.transform.localPosition = new Vector3(length, 0f, 0f);
            endMark.transform.localRotation = Quaternion.Euler(0f, 0f, -transform.eulerAngles.z);   // ring always upright
            float es = Mathf.Max(0.6f, width / 0.36f) * (late ? 1f + 0.08f * Mathf.Sin(Time.time * 30f) : 1f);
            endMark.transform.localScale = new Vector3(es, es * 0.75f, 1f);
            endMark.color = Alpha(0.95f * blink * fade);
            // arrows drift in the charge direction (faster as the charge nears)
            int n = Mathf.Max(1, Mathf.FloorToInt((length - 0.25f) / Spacing));
            while (chevrons.Count < n) chevrons.Add(Part("chev", chevron, sortLayer, sortOrder));
            float shift = Mathf.Repeat(age * (late ? 2.4f : 1.2f), Spacing);
            float cs = Mathf.Clamp(width / 0.5f, 0.7f, 1.6f);
            for (int i = 0; i < chevrons.Count; i++)
            {
                var c = chevrons[i];
                float x = 0.15f + i * Spacing + shift;
                bool on = i < n && x < length - 0.15f;
                c.enabled = on;
                if (!on) continue;
                c.transform.localPosition = new Vector3(x, 0f, 0f);
                c.transform.localScale = new Vector3(cs, cs, 1f);
                float edgeFade = Mathf.Clamp01(Mathf.Min(x - 0.1f, length - x) / 0.25f);
                c.color = Alpha(0.85f * edgeFade * blink * fade);
            }
        }

        private Color Alpha(float a) => new Color(color.r, color.g, color.b, Mathf.Clamp01(a) * color.a);

        private static void Place(SpriteRenderer r, float x, float y, float sx, float sy, Color c)
        {
            r.enabled = sx > 0.001f;
            r.transform.localPosition = new Vector3(x, y, 0f);
            r.transform.localScale = new Vector3(Mathf.Max(0.0001f, sx), sy, 1f);
            r.color = c;
        }

        private SpriteRenderer Part(string name, Sprite s, int layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sortingLayerID = layer;
            r.sortingOrder = order;
            FxLighting.MakeUnlit(r);
            return r;
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Lane width from the boss collider: body width + ~Ren's body (at least `min`).</summary>
        public static float WidthFor(Collider2D body, float min = 0.6f)
        {
            if (body == null) return min;
            return Mathf.Max(min, body.bounds.size.x + 0.3f);
        }

        /// <summary>Real charge distance along `dir`, at most `want`: stops before the first solid obstacle (wall, pillar, prop) — ignores triggers,
        /// the boss itself, Ren, other enemies. `radius` = half the boss body width (circle cast).</summary>
        public static float Reach(Vector2 from, Vector2 dir, float want, float radius, Collider2D self, Transform player)
        {
            if (dir.sqrMagnitude < 0.0001f) return want;
            float best = want;
            foreach (var h in Physics2D.CircleCastAll(from, Mathf.Max(0.05f, radius), dir.normalized, want))
            {
                if (h.collider == null || h.collider.isTrigger || h.collider == self) continue;
                if (player != null && h.collider.transform.IsChildOf(player)) continue;
                if (h.collider.GetComponentInParent<EnemyAI>() != null) continue;
                if (h.distance <= 0.01f) continue;                                     // already touching (standing against a wall) — skip
                best = Mathf.Min(best, h.distance);
            }
            return Mathf.Max(0.3f, best);
        }

        /// <summary>Charge distance "to Ren and slide past" — never too short / too long.</summary>
        public static float WantReach(Vector2 from, Vector2 playerPos, float overshoot, float min, float max)
            => Mathf.Clamp((playerPos - from).magnitude + overshoot, min, max);

        // ------------------------------------------------------------------ code-drawn sprites
        private static void Build()
        {
            if (pixel != null) return;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "dash_px" };
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px); t.Apply(false, true);
            pixel = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);    // 1 u, origin at the left edge

            // ">" arrow 12 x 14 px, 3 px stroke + 1 px dark outline
            const int W = 12, H = 14;
            var c = new Color32[W * H];
            var ink = new Color32(255, 255, 255, 255); var dark = new Color32(30, 10, 10, 200);
            var mask = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float cy = (H - 1) * 0.5f, dy = Mathf.Abs(y - cy);
                    float xLine = 2f + (cy - dy) * 0.95f;                                     // arrow tip on the right, at mid height
                    if (x >= xLine - 1.5f && x <= xLine + 1.5f && dy <= cy - 0.5f) mask[y * W + x] = true;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (mask[i]) { c[i] = ink; continue; }
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                        for (int dx = -1; dx <= 1 && !edge; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < W && yy < H && mask[yy * W + xx]) edge = true;
                        }
                    if (edge) c[i] = dark;
                }
            var ct = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "dash_chevron" };
            ct.SetPixels32(c); ct.Apply(false, true);
            chevron = Sprite.Create(ct, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);

            // target mark: 2 px ring + X in the middle, 36 px
            const int N = 36;
            var e = new Color32[N * N];
            float m = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - m) * (x - m) + (y - m) * (y - m));
                    bool ring = d > 14.5f && d < 17f;
                    bool cross = (Mathf.Abs((x - m) - (y - m)) < 1.6f || Mathf.Abs((x - m) + (y - m)) < 1.6f) && d < 8.5f;
                    if (ring || cross) e[y * N + x] = ink;
                    else if (d >= 17f && d < 18f) e[y * N + x] = dark;
                }
            var et = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "dash_target" };
            et.SetPixels32(e); et.Apply(false, true);
            target = Sprite.Create(et, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
