using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Gây sát thương lên quái + cảm giác lực khi trúng (dừng hình ngắn, giật camera) + VFX trúng đòn / dưới chân.
    /// Dùng chung cho đòn chém (<see cref="PlayerMelee"/>) và kỹ năng tháo lắp (SkillDefinition qua API của PlayerCombat).
    /// </summary>
    internal sealed class PlayerStrikes
    {
        private readonly PlayerCombat owner;
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
        private CameraFollow camFollow;
        private static bool hitStopping;

        public PlayerStrikes(PlayerCombat owner) { this.owner = owner; }

        // ------------------------------------------------------------------ sát thương
        /// <summary>Mọi quái có collider trong vòng tròn (và <paramref name="accept"/> đồng ý) nhận sát thương; trả về số quái trúng.</summary>
        public int Damage(Vector2 center, float radius, float dmg, float knock, Func<Collider2D, bool> accept)
        {
            hits.Clear();
            Physics2D.OverlapCircle(center, radius, filter, hits);
            var done = new HashSet<Health>();
            int landedCount = 0;
            foreach (var c in hits)
            {
                if (accept != null && !accept(c)) continue;
                var ai = c.GetComponentInParent<EnemyAI>();
                if (ai == null || ai.Health == null || done.Contains(ai.Health)) continue;
                // 31/10: đá / tường / đồ vật chắn giữa CHÂN người chơi và quái -> không chém xuyên (lấy chân: hông có thể nằm trong collider đá)
                if (!LineOfSight.CanSee(owner.transform.position, c)) continue;
                done.Add(ai.Health);                                                          // xét mỗi quái 1 lần / nhát
                Vector2 dir = (Vector2)ai.transform.position - (Vector2)owner.transform.position;
                float hpBefore = ai.Health.Current;
                bool landed = ai.Health.TakeDamage(dmg, dir, knock);
                // 13/11: chỉ tính là TRÚNG khi thật sự mất máu. Trước đây đòn chạm quái đang bất tử (mai rùa, nằm choáng, bỏ chạy...)
                // vẫn bắn máu + dừng hình + giật camera -> người chơi thấy "trúng mà không mất máu".
                if (owner.debugHitbox && !landed)
                    Debug.Log($"[Strike] {ai.name}: chạm nhưng KHÔNG nhận đòn (bất tử={ai.Health.Invulnerable}, chết={ai.Health.IsDead}, máu {hpBefore:0.#}/{ai.Health.Max:0})");
                if (!landed) continue;
                landedCount++;
                // VFX trúng đòn ở thân quái (collider quái chỉ là vệt chân -> kéo lên phía giữa hình quái);
                // máu bắn theo hướng đòn (ảnh gốc bắn sang phải)
                if (owner.showVfx)
                {
                    Vector2 p = c.bounds.center;
                    var body = ai.GetComponentInChildren<SpriteRenderer>();
                    if (body != null) p.y = Mathf.Lerp(p.y, body.bounds.center.y, 0.6f);
                    SpriteVfx.Spawn("hit", p, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder + 2, dir.x < 0f);
                }
            }
            return landedCount;                                                               // 13/11: chỉ đòn thật sự gây sát thương
        }

        // ------------------------------------------------------------------ cảm giác lực
        /// <summary>Đòn chém trúng: dừng hình + giật camera theo hướng chém.</summary>
        public void Impact(Vector2 d) => Impact(d, 1f);

        /// <summary>09/11 (§201): trúng đòn với hệ số lực (nhát bổ cuối combo dừng hình / giật camera mạnh hơn).</summary>
        public void Impact(Vector2 d, float mul)
        {
            if (owner.hitStop > 0f) owner.StartCoroutine(HitStopRoutine(owner.hitStop * Mathf.Max(0.2f, mul)));
            Kick(d, Mathf.Max(0.2f, mul), 0.16f);
        }

        /// <summary>Giật camera (nhân cameraKick của Inspector và cài đặt rung màn hình).</summary>
        public void Kick(Vector2 dir, float mul, float duration)
        {
            if (owner.cameraKick <= 0f) return;
            if (camFollow == null && Camera.main != null) camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow != null) camFollow.Kick(dir, owner.cameraKick * mul, duration);
        }

        /// <summary>Dừng hình rất ngắn (hitStop giây thật). Không chồng lên pause / dừng hình khác.</summary>
        public void HitStop()
        {
            if (owner.hitStop > 0f) owner.StartCoroutine(HitStopRoutine(owner.hitStop));
        }

        private static IEnumerator HitStopRoutine(float seconds)
        {
            if (hitStopping || Time.timeScale < 0.99f) yield break;
            hitStopping = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
            hitStopping = false;
        }

        /// <summary>Người chơi bị tắt giữa lúc dừng hình: trả lại tốc độ thời gian.</summary>
        public static void ReleaseHitStop()
        {
            if (!hitStopping) return;
            hitStopping = false;
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ VFX dưới chân
        /// <summary>VFX dưới chân (Resources/VFX/&lt;name&gt;); tắt VFX thì vẽ vòng tròn mờ báo tầm.</summary>
        public void GroundVfx(string name, Vector2 c, float scale, Color fallback, float radius)
        {
            if (owner.showVfx) SpriteVfx.Spawn(name, c, scale, owner.VfxLayer, owner.PlayerOrder - 1);
            else owner.StartCoroutine(Swing(c, radius, fallback));
        }

        private static IEnumerator Swing(Vector2 center, float radius, Color color)
        {
            var go = new GameObject("fx_swing");
            go.transform.position = center;
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sprite = CircleSprite;
            r.color = color;
            r.sortingOrder = 50;
            float t = 0f;
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                float s = radius * 2f * Mathf.Lerp(0.7f, 1f, t / 0.12f);
                go.transform.localScale = new Vector3(s, s, 1f);
                r.color = new Color(color.r, color.g, color.b, color.a * (1f - t / 0.12f));
                yield return null;
            }
            UnityEngine.Object.Destroy(go);
        }

        private static Sprite circle;
        private static Sprite CircleSprite
        {
            get
            {
                if (circle != null) return circle;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        byte a = (byte)(d > 1f ? 0 : d > 0.85f ? 255 : 90);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 đơn vị đường kính
                return circle;
            }
        }
    }
}
