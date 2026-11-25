using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// IMAGE OF A GROUND PICKUP (flask, flask upgrade, blue potion, skill scroll) so the item itself is visible, not just a glow.
    /// Image Resources/VFX/props/pickup_&lt;item&gt;.png (PixelLab, feet at the bottom). Bobs gently, flashes in rhythm, shadow underneath.
    /// Child of the glow (ExitBeacon) -> when the glow is destroyed after pickup, the image goes with it.
    /// </summary>
    public sealed class PickupItemVisual : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private SpriteRenderer sr;
        private Vector3 basePos;
        private float phase;

        /// <summary>Create the image of item `item` with its feet at `foot` (world). Missing image -> null.</summary>
        public static PickupItemVisual Attach(Transform parent, Vector2 foot, string item, int sortingLayerId, int sortingOrder)
        {
            var s = Load(item);
            if (s == null) return null;
            var go = new GameObject("Pickup Item " + item);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(foot.x, foot.y, 0f);
            var shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(go.transform, false);
            shadow.sprite = DrillChalk.Shadow();
            shadow.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            shadow.color = new Color(1f, 1f, 1f, 0.55f);
            shadow.sortingLayerID = sortingLayerId; shadow.sortingOrder = sortingOrder - 1;
            var v = go.AddComponent<PickupItemVisual>();
            var body = new GameObject("item").AddComponent<SpriteRenderer>();
            body.transform.SetParent(go.transform, false);
            body.sprite = s;
            body.sortingLayerID = sortingLayerId; body.sortingOrder = sortingOrder;
            v.sr = body;
            v.basePos = body.transform.localPosition;
            v.phase = Random.value * 10f;
            return v;
        }

        private static Sprite Load(string item)
        {
            if (string.IsNullOrEmpty(item)) return null;
            if (cache.TryGetValue(item, out var s)) return s;
            var all = Resources.LoadAll<Sprite>("VFX/props/pickup_" + item);
            if (all.Length > 0) s = all[0];
            else
            {
                var tex = Resources.Load<Texture2D>("VFX/props/pickup_" + item);
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
                }
            }
            cache[item] = s;
            return s;
        }

        /// <summary>Item image (shared with PickupPop).</summary>
        internal static Sprite SpriteOf(string item) => Load(item);

        private void Update()
        {
            if (sr == null) return;
            float t = Time.time + phase;
            sr.transform.localPosition = basePos + new Vector3(0f, 0.03f + Mathf.Sin(t * 2.4f) * 0.025f, 0f);
            float g = 0.88f + 0.12f * Mathf.Max(0f, Mathf.Sin(t * 1.7f));   // gentle flash in rhythm
            sr.color = new Color(g + 0.12f, g + 0.12f, g + 0.12f, 1f);
        }
    }
}
