using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ITEM POPPING OUT OF A CHEST.
    /// The item image (Resources/VFX/props/pickup_&lt;item&gt;, like PickupItemVisual) rises from the chest lid for 0.45 s (growing),
    /// hovers 0.35 s, then curves into Ren's chest in 0.35 s (shrinking) -> flash + pickup sound. Total `Duration` seconds (game time).
    /// Call: PickupPop.Launch(chest lid position, Ren, "skill_scroll", sorting layer, sorting order). Missing image -> null (nothing happens).
    /// Used by: MapTriggerHandler (pickups inside chests - Hob's chest, Map 1) and ChestMechanism (chests with skill_id - Map 2).
    /// </summary>
    public sealed class PickupPop : MonoBehaviour
    {
        public const float Duration = 1.15f;
        private const float Rise = 0.45f, Hold = 0.35f, Fly = 0.35f, Lift = 0.55f;

        private SpriteRenderer sr;
        private Vector2 from;
        private Transform target;
        private float t;
        private int layerId, order;

        public static PickupPop Launch(Vector2 from, Transform target, string item, int sortingLayerId, int sortingOrder)
        {
            var s = PickupItemVisual.SpriteOf(item);
            if (s == null) return null;
            var go = new GameObject("Pickup Pop " + item);
            go.transform.position = new Vector3(from.x, from.y, 0f);
            go.transform.localScale = Vector3.one * 0.3f;
            var p = go.AddComponent<PickupPop>();
            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sprite = s;
            p.sr.sortingLayerID = sortingLayerId;
            p.sr.sortingOrder = sortingOrder;
            p.from = from; p.target = target;
            p.layerId = sortingLayerId; p.order = sortingOrder;
            Sfx.Play(item == "skill_scroll" ? "cs_page" : "pickup_item", from, 0.7f);
            SpriteVfx.Spawn("duo_ice_burst", from + Vector2.up * 0.2f, 0.35f, sortingLayerId, sortingOrder - 1, false, 16f, false);
            return p;
        }

        private void Update()
        {
            t += Time.deltaTime;
            Vector2 hold = from + Vector2.up * Lift;
            Vector2 pos;
            float sc;
            if (t < Rise)
            {
                float u = t / Rise, e = 1f - (1f - u) * (1f - u);                    // fast out, slowing down
                pos = Vector2.Lerp(from, hold, e);
                sc = Mathf.Lerp(0.3f, 1.15f, e);
            }
            else if (t < Rise + Hold)
            {
                float u = (t - Rise) / Hold;
                pos = hold + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.04f;
                sc = Mathf.Lerp(1.15f, 1f, u);
            }
            else
            {
                float u = Mathf.Clamp01((t - Rise - Hold) / Fly);
                Vector2 dst = target != null ? (Vector2)target.position + Vector2.up * 0.45f : hold;
                pos = Vector2.Lerp(hold, dst, u * u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.25f;
                sc = Mathf.Lerp(1f, 0.35f, u);
                if (u >= 1f)
                {
                    Sfx.Play2D("pickup_item");
                    SpriteVfx.Spawn("duo_ice_burst", dst, 0.4f, layerId, order, false, 16f, false);
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position = new Vector3(pos.x, pos.y, 0f);
            transform.localScale = Vector3.one * sc;
        }
    }
}
