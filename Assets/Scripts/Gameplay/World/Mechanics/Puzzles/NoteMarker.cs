using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Gold glow ring + 3 sparkles at the foot of a puzzle note (notes with `show_when`), until it is read.
    /// The sprite is drawn in code (no image needed).
    /// </summary>
    internal static class NoteMarker
    {
        private static Sprite ring, spark;

        /// <summary>Create the glow ring (hidden) at the foot of object &lt;name&gt;_item (or `itemName`, if given) or the note area centre.</summary>
        public static GameObject Create(string noteName, Rect area, MechanismContext ctx, string itemName = null)
        {
            Vector2 foot = area.center;
            float top = foot.y + 0.3f;
            if (ctx.Named.TryGetValue(string.IsNullOrEmpty(itemName) ? noteName + "_item" : itemName, out var items))
                foreach (var go in items)
                {
                    var r = go != null ? go.GetComponentInChildren<SpriteRenderer>(true) : null;
                    if (r == null) continue;
                    foot = new Vector2(r.bounds.center.x, r.bounds.min.y);
                    top = r.bounds.max.y;
                }
            return CreateAt(foot, top, noteName);
        }

        /// <summary>Glow ring (hidden) at `foot` (object feet), sparkles at height `top` - also used outside mechanisms (pickups).</summary>
        /// <summary>Turn the gold ring under marked objects back on (off by default).</summary>
        public static bool ShowRing = false;

        public static GameObject CreateAt(Vector2 foot, float top, string name)
        {
            var root = new GameObject("NoteMarker " + name);
            root.transform.position = foot;
            var ringSr = new GameObject("ring").AddComponent<SpriteRenderer>();
            ringSr.transform.SetParent(root.transform, false);
            ringSr.sprite = Ring; ringSr.sortingOrder = 1; ringSr.color = new Color(1f, 0.82f, 0.35f, 0.8f);
            ringSr.enabled = ShowRing;                          // no GOLD RING under marked objects; only the sparkles + a soft light remain
            for (int i = 0; i < 3; i++)
            {
                var sp = new GameObject("spark" + i).AddComponent<SpriteRenderer>();
                sp.transform.SetParent(root.transform, false);
                sp.sprite = Spark; sp.sortingOrder = 6; sp.color = new Color(1f, 0.9f, 0.5f, 1f);
                sp.transform.localPosition = new Vector3(0f, top - foot.y + 0.05f, 0f);
            }
            var l = root.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(1f, 0.8f, 0.4f);
            l.pointLightOuterRadius = 1.1f; l.pointLightInnerRadius = 0.15f; l.intensity = 1f;
            root.SetActive(false);
            return root;
        }

        public static void Animate(GameObject marker)
        {
            float t = Time.time * 2.4f;
            var tr = marker.transform;
            var ringT = tr.GetChild(0);
            float k = 0.5f + 0.5f * Mathf.Sin(t);
            ringT.localScale = new Vector3(1f + 0.15f * k, (1f + 0.15f * k) * 0.55f, 1f);
            ringT.GetComponent<SpriteRenderer>().color = new Color(1f, 0.82f, 0.35f, 0.45f + 0.4f * k);
            for (int i = 1; i < tr.childCount; i++)
            {
                var sp = tr.GetChild(i);
                float a = t * 0.6f + i * 2.094f;
                sp.localPosition = new Vector3(Mathf.Cos(a) * 0.22f, sp.localPosition.y, 0f);
                float tw = 0.5f + 0.5f * Mathf.Sin(t * 1.7f + i * 1.3f);
                sp.localScale = Vector3.one * (0.6f + 0.6f * tw);
                sp.GetComponent<SpriteRenderer>().color = new Color(1f, 0.9f, 0.5f, 0.4f + 0.6f * tw);
            }
            var l = marker.GetComponent<Light2D>();
            if (l != null) l.intensity = 0.7f + 0.6f * k;
        }

        private static Sprite Ring
        {
            get
            {
                if (ring != null) return ring;
                const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n]; float c = (n - 1) / 2f;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    byte a = r > 1f ? (byte)0 : r > 0.86f ? (byte)255 : r > 0.74f ? (byte)90 : (byte)0;
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px); tex.Apply();
                return ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);   // diameter 0.64 units = 2 tiles
            }
        }

        private static Sprite Spark
        {
            get
            {
                if (spark != null) return spark;
                const int n = 7; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    int d = Mathf.Abs(x - 3) + Mathf.Abs(y - 3);
                    bool on = (x == 3 || y == 3) ? d <= 3 : d <= 1;
                    px[y * n + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
                tex.SetPixels32(px); tex.Apply();
                return spark = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
