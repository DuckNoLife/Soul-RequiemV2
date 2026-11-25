using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// One GHOST RANK of The Red Field boss (Map 3b) - a column of ghost soldiers from the top edge to the bottom edge of
    /// the arena, leaving ONE gap, then marching across the field.
    ///   1. Warning (`warn` seconds): soldiers fade in at the start edge, the gap shows a warm white light (safe lane).
    ///   2. March: the whole column moves along `dir` at `speed` (units / second), soldiers bob slightly. Touching Ren (outside the gap) = damage
    ///      ONCE per rank. ROLLING does not dodge it - only standing in the gap is safe.
    ///   3. After `travel` -> fade out and vanish.
    /// No physics collider (does not push / pin Ren against walls).
    /// </summary>
    public sealed class GhostRank : MonoBehaviour
    {
        private readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
        private readonly List<float> phase = new List<float>();
        private readonly List<float> baseY = new List<float>();
        private Vector2 dir;
        private float yMin, yMax, gapY, gapHalf, warn, speed, travel, damage, t0, moved;
        private bool hit, fading;
        private Light2D lane;
        private Health player;
        private float alpha;
        private float hitLo = float.NegativeInfinity, hitHi = float.PositiveInfinity;   // vertical hit band
        private Color tint = Color.white;

        public static GhostRank Spawn(float startX, float yMin, float yMax, float gapY, float gapHalf, Vector2 dir,
                                      float warn, float speed, float travel, float damage, float scale,
                                      int layer, int order, Color tint)
        {
            var go = new GameObject("Ghost Rank");
            go.transform.position = new Vector3(startX, (yMin + yMax) * 0.5f, 0f);
            var r = go.AddComponent<GhostRank>();
            r.yMin = yMin; r.yMax = yMax; r.gapY = gapY; r.gapHalf = gapHalf; r.dir = dir.x >= 0f ? Vector2.right : Vector2.left;
            r.warn = warn; r.speed = speed; r.travel = travel; r.damage = damage; r.tint = tint;
            var frames = SpriteVfx.Load("rf_ghost_E");
            Sprite sp = frames.Length > 0 ? frames[0] : null;
            float spacing = 0.27f;                                       // ~0.85 tiles: dense rank, no slipping between two soldiers
            int i = 0;
            for (float y = yMin; y <= yMax + 0.001f; y += spacing, i++)
            {
                if (Mathf.Abs(y - gapY) < gapHalf) continue;
                var g = new GameObject("Ghost");
                g.transform.SetParent(go.transform, false);
                g.transform.position = new Vector3(startX + Random.Range(-0.05f, 0.05f), y, 0f);
                g.transform.localScale = Vector3.one * scale;
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = sp; sr.flipX = r.dir.x < 0f;
                sr.sortingLayerID = layer; sr.sortingOrder = order; sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.color = new Color(tint.r, tint.g, tint.b, 0f);
                r.ghosts.Add(sr); r.phase.Add(Random.Range(0f, 6.28f)); r.baseY.Add(g.transform.localPosition.y);
            }
            // safe lane: warm light at the gap, right at the start edge
            var l = new GameObject("Rank Gap Light");
            l.transform.SetParent(go.transform, false);
            l.transform.position = new Vector3(startX, gapY, 0f);
            r.lane = l.AddComponent<Light2D>();
            r.lane.lightType = Light2D.LightType.Point;
            r.lane.color = new Color(1f, 0.9f, 0.7f);
            r.lane.pointLightOuterRadius = gapHalf * 2.2f; r.lane.pointLightInnerRadius = 0.05f;
            r.lane.intensity = 0f;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) r.player = pc.GetComponent<Health>();
            return r;
        }

        /// <summary>Only deal damage while Ren is inside the vertical band [lo, hi] (Marching Hall: does not touch Ren in the corridor).</summary>
        public GhostRank Bound(float lo, float hi) { hitLo = lo; hitHi = hi; return this; }

        private void Start() => t0 = Time.time;

        private void Update()
        {
            float t = Time.time - t0;
            if (t < warn)
            {
                alpha = Mathf.Lerp(0f, 0.55f, t / Mathf.Max(0.01f, warn));
                if (lane != null) lane.intensity = 1.1f * (0.6f + 0.4f * Mathf.Sin(t * 14f));
            }
            else if (!fading)
            {
                alpha = Mathf.MoveTowards(alpha, 0.85f, Time.deltaTime * 2f);
                float step = speed * Time.deltaTime;
                transform.position += (Vector3)(dir * step);
                moved += step;
                if (lane != null) lane.intensity = Mathf.MoveTowards(lane.intensity, 0f, Time.deltaTime * 3f);
                if (!hit) CheckHit();
                if (moved >= travel) fading = true;
            }
            else
            {
                alpha = Mathf.MoveTowards(alpha, 0f, Time.deltaTime * 2.5f);
                transform.position += (Vector3)(dir * speed * 0.5f * Time.deltaTime);
                if (alpha <= 0.001f) { Destroy(gameObject); return; }
            }
            for (int i = 0; i < ghosts.Count; i++)
            {
                var g = ghosts[i];
                if (g == null) continue;
                g.color = new Color(tint.r, tint.g, tint.b, alpha);
                var lp = g.transform.localPosition;
                g.transform.localPosition = new Vector3(lp.x, baseY[i] + Mathf.Sin(Time.time * 6f + phase[i]) * 0.012f, lp.z);   // slight bobbing
            }
        }

        private void CheckHit()
        {
            if (player == null || player.IsDead) return;
            Vector2 p = player.transform.position;
            float x = transform.position.x;
            if (Mathf.Abs(p.x - x) > 0.16f) return;
            if (p.y < hitLo || p.y > hitHi) return;                      // outside the band (e.g. standing in the corridor)
            // No top / bottom edge check - the field is walled and the rank covers the full height. A rank that stopped ~2 tiles short of the north wall
            // would leave a second "gap" next to the wall where the rank passed through Ren without damage.
            if (Mathf.Abs(p.y - gapY) < gapHalf - 0.06f) return;          // standing in the gap: safe
            // ROLLING (Q) cannot pass through - roll invulnerability is ignored (like mechanical traps), Riposte does not block it either.
            // Only standing in the GAP works. Shields / cutscenes still block.
            bool hurt;
            Health.IncomingRanged = true; Health.TrapIgnoresRoll = true;
            try { hurt = player.TakeDamage(damage, dir, 2.6f); }
            finally { Health.IncomingRanged = false; Health.TrapIgnoresRoll = false; }
            if (hurt)
            {
                hit = true;
                CameraFollow.KickMain(dir, 0.04f, 0.18f);
            }
        }
    }
}
