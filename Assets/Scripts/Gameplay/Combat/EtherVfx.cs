using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Code-drawn visuals for the pure Ether Mantras (BLUE; corrupted Ether on the maps is green):
    /// glowing orb, shield ring, beam. No external images; sprites are created once and reused.
    /// </summary>
    public static class EtherVfx
    {
        public static readonly Color Blue = new Color(0.45f, 0.78f, 1f, 1f);
        public static readonly Color Core = new Color(0.88f, 0.96f, 1f, 1f);

        private static Sprite orb, ring, beam;

        /// <summary>Soft glowing orb 32x32 px (0.32 units at scale 1), centred pivot.</summary>
        public static Sprite Orb => orb != null ? orb : (orb = Make(32, 32, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            return Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
        }));

        /// <summary>Shield ring 64x64 px (0.64 units), bright rim + faint fill.</summary>
        public static Sprite Ring => ring != null ? ring : (ring = Make(64, 64, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float band = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.07f, 2f));
            float fill = d < 0.86f ? 0.12f * (d / 0.86f) : 0f;
            return Mathf.Clamp01(band + fill);
        }));

        /// <summary>Beam segment 32x16 px (0.32 x 0.16 units), pivot at the middle of the LEFT edge -> stretched along x.</summary>
        public static Sprite Beam => beam != null ? beam : (beam = Make(32, 16, new Vector2(0f, 0.5f), (u, v) =>
            Mathf.Exp(-Mathf.Pow(v / 0.45f, 2f))));

        public const float BeamWidthUnits = 0.32f;
        public const float BeamHeightUnits = 0.16f;

        /// <summary>Create an object with an unlit SpriteRenderer (always bright in the dark).</summary>
        public static SpriteRenderer Spawn(string name, Sprite sprite, Vector2 pos, Color color, int sortingLayer, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, parent != null ? parent.position.z : 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingLayerID = sortingLayer;
            r.sortingOrder = order;
            FxLighting.MakeUnlit(r);
            return r;
        }

        /// <summary>Rotate vector <paramref name="v"/> by <paramref name="degrees"/> (counter-clockwise).</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Turn <paramref name="from"/> toward <paramref name="to"/>, at most <paramref name="maxDegrees"/>.</summary>
        public static Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDegrees)
        {
            if (to.sqrMagnitude < 0.0001f) return from;
            float a = Vector2.SignedAngle(from, to);
            return Rotate(from, Mathf.Clamp(a, -maxDegrees, maxDegrees)).normalized;
        }

        // u, v in [-1, 1] (image centre = 0) -> opacity 0..1; white, tinted by SpriteRenderer.color
        private static Sprite Make(int w, int h, Vector2 pivot, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "EtherVfx" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha(u, v))));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 100f);
        }
    }
}
