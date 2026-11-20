using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 18/11 (§330): một HÀNG LÍNH HỒN của boss The Red Field (Map 3b) — một cột lính ma đứng dọc từ mép trên tới mép dưới
    /// đấu trường, chừa MỘT khe hở, rồi hành quân ngang qua sân.
    ///   1. Báo trước (`warn` giây): lính hiện mờ dần ở mép xuất phát, khe hở có một đốm sáng trắng ấm (làn an toàn).
    ///   2. Hành quân: cả cột tiến theo `dir` với `speed` (đơn vị / giây), lính nhấp nhô nhẹ. Chạm Ren (ngoài khe) = sát thương
    ///      MỘT lần cho cả hàng. Lăn (bất tử) xuyên qua được — Health.TakeDamage trả false khi bất tử.
    ///   3. Đi hết `travel` -> mờ rồi biến mất.
    /// Không có va chạm vật lý (không đẩy / không kẹt Ren vào tường).
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
        private float hitLo = float.NegativeInfinity, hitHi = float.PositiveInfinity;   // 18/11 (§331): dải dọc nhận đòn
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
            float spacing = 0.27f;                                       // ~0,85 ô: hàng dày, không lọt qua giữa hai lính
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
            // làn an toàn: đốm sáng ấm ở khe hở, ngay mép xuất phát
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

        /// <summary>18/11 (§331): chỉ gây sát thương khi Ren đứng trong dải dọc [lo, hi] (sảnh Marching Hall: không chạm Ren ở hành lang).</summary>
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
                g.transform.localPosition = new Vector3(lp.x, baseY[i] + Mathf.Sin(Time.time * 6f + phase[i]) * 0.012f, lp.z);   // nhấp nhô nhẹ
            }
        }

        private void CheckHit()
        {
            if (player == null || player.IsDead) return;
            Vector2 p = player.transform.position;
            float x = transform.position.x;
            if (Mathf.Abs(p.x - x) > 0.16f) return;
            if (p.y < hitLo || p.y > hitHi) return;                      // 18/11 (§331): ngoài dải (vd đứng ở hành lang)
            // 18/11 (§331): KHÔNG xét mép trên / dưới — sân có tường bao, hàng phủ hết bề cao. Trước đây hàng hụt ~2 ô dưới tường bắc
            // -> Ren đứng sát tường là "khe" thứ hai, hàng lính đi xuyên qua mà không mất máu.
            if (Mathf.Abs(p.y - gapY) < gapHalf - 0.06f) return;          // đứng trong khe: an toàn
            if (player.TakeDamage(damage, dir, 2.6f))
            {
                hit = true;
                CameraFollow.KickMain(dir, 0.04f, 0.18f);
            }
        }
    }
}
