using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Marks a normal enemy that needs an overhead HP bar (bosses / minibosses with treatAsBoss use the big HUD boss bar instead).
    /// RequiemHUD draws the bar + "hp/max" text for every enemy in All (always shown while alive and on screen).
    /// Head height comes from the Tight mesh of the Idle sprite (transparent cells ignored).
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        public static readonly List<EnemyHealthBar> All = new List<EnemyHealthBar>();

        public Health Health { get; private set; }
        private float top = 0.5f;

        /// World point just above the top of the head.
        public Vector3 HeadWorld => transform.position + Vector3.up * (top + 0.08f);

        public static void Attach(EnemyAI ai)
        {
            if (ai == null || ai.IsBoss || ai.GetComponent<EnemyHealthBar>() != null) return;
            ai.gameObject.AddComponent<EnemyHealthBar>();
        }

        private SpriteRenderer body;
        private bool init;

        private void Start()
        {
            Health = GetComponent<Health>();
            var ai = GetComponent<EnemyAI>();
            body = ai != null && ai.Body != null ? ai.Body : null;
            if (body == null)       // find the largest body sprite (skip drop shadows / child VFX)
                foreach (var r in GetComponentsInChildren<SpriteRenderer>())
                    if (r.sprite != null && (body == null || r.bounds.size.y > body.bounds.size.y)) body = r;
        }

        /// top of the head for the current frame (world bounds of the body sprite), smoothed so the bar does not jump with the animation.
        private void LateUpdate()
        {
            if (body == null || body.sprite == null) return;
            // top of the Tight mesh of the current frame (the drawn pixels only) - using the full 128 px frame bounds made the
            // Grave Worm bar float ~1 unit above its head, over the empty pit.
            float target = TightTop(body) - transform.position.y;
            if (!init) { top = target; init = true; }
            else top = Mathf.Lerp(top, target, 1f - Mathf.Exp(-Time.deltaTime * 4f));
        }

        private static readonly Dictionary<Sprite, float> tightTop = new Dictionary<Sprite, float>();
        /// World top of the drawn pixels (highest vertex of the sprite mesh; a FullRect mesh gives the same as bounds).
        private static float TightTop(SpriteRenderer r)
        {
            var s = r.sprite;
            if (!tightTop.TryGetValue(s, out float top))
            {
                top = float.MinValue;
                foreach (var v in s.vertices) if (v.y > top) top = v.y;
                if (tightTop.Count > 2048) tightTop.Clear();
                tightTop[s] = top;
            }
            return r.transform.TransformPoint(new Vector3(0f, top, 0f)).y;
        }
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }
    }
}
