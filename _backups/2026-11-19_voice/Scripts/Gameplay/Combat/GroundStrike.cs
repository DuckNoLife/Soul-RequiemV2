using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Đòn đánh xuống một điểm trên mặt đất:
    ///   1. vòng cảnh báo hiện ở điểm rơi (tối thiểu ~0,6 giây theo quy tắc bẫy chung);
    ///   2. (tùy chọn) đạn pháo bay theo vòng cung từ nòng súng tới điểm rơi trong lúc cảnh báo;
    ///   3. phát hoạt ảnh va chạm, gây sát thương MỘT lần nếu người chơi đứng trong bán kính.
    /// Dùng cho: tia bùn xanh + gai đá của Greystone Seer, đạn pháo của Camp Gunner.
    /// Lướt (i-frame) né được vì Health.TakeDamage bỏ qua khi người chơi đang bất tử.
    /// </summary>
    public class GroundStrike : MonoBehaviour
    {
        public float warnTime = 0.6f;
        public float radius = 0.3f;
        public float damage = 10f;
        public float knockback = 0f;
        public float stunSeconds = 0f;
        [Tooltip("Gọi khi đòn trúng người chơi (sát thương 0 = chỉ gây hiệu ứng trạng thái).")]
        public System.Action<Health> onHit;
        public Color warnColor = new Color(1f, 0.45f, 0.25f, 1f);

        [Header("Hoạt ảnh va chạm")]
        public Sprite[] frames;
        public int impactFrame = 0;          // khung hình gây sát thương
        public float fps = 14f;
        public int sortingOrder = 2;
        public float bodyScale = 1f;         // phóng to hoạt ảnh va chạm (không ảnh hưởng vòng cảnh báo)
        public Color bodyColor = Color.white; // nhuộm hoạt ảnh va chạm
        [Tooltip("Hình để lại trên sàn sau khi rơi (vd. tảng đá), mờ dần trong leaveSeconds")]
        public Sprite leaveSprite;
        public float leaveSeconds = 0f;
        public float shellScale = 1f;
        public Color shellColor = Color.white;

        [Header("Đạn bay vòng cung (tùy chọn)")]
        public Sprite shell;
        public Vector2 shellFrom;
        public float shellArc = 0.9f;

        private SpriteRenderer ring, body, shellSr;
        private float t0, impactAt;
        private bool impacting, hitDone;
        private Vector2 at;

        private static Sprite ringSprite;
        private static Health playerHp;

        public static GroundStrike Spawn(Vector2 at, float warn, float radius, float damage, Sprite[] frames, int impactFrame,
                                         float fps, int order, Color warnColor)
        {
            FirstTimeHints.Show("tut_circle", "A red circle marks where something will land - get out of it before it strikes.", 4.5f);   // 02/11
            var go = new GameObject("GroundStrike");
            go.transform.position = at;
            var g = go.AddComponent<GroundStrike>();
            g.warnTime = warn; g.radius = radius; g.damage = damage; g.frames = frames; g.impactFrame = impactFrame;
            g.fps = fps; g.sortingOrder = order; g.warnColor = warnColor;
            return g;
        }

        private void Start()
        {
            at = transform.position;
            t0 = Time.time;

            var r = new GameObject("Warning");
            r.transform.SetParent(transform, false);
            ring = r.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(ring);
            ring.sprite = RingSprite;
            ring.sortingOrder = -4;                       // trên decal mặt đất (-5), dưới nhân vật
            ring.color = new Color(warnColor.r, warnColor.g, warnColor.b, 0f);

            var b = new GameObject("Impact");
            b.transform.SetParent(transform, false);
            b.transform.localScale = Vector3.one * bodyScale;
            body = b.AddComponent<SpriteRenderer>();
            body.sortingOrder = sortingOrder;
            body.color = bodyColor;
            body.spriteSortPoint = SpriteSortPoint.Pivot;

            if (shell != null)
            {
                var s = new GameObject("Shell");
                s.transform.position = shellFrom;
                shellSr = s.AddComponent<SpriteRenderer>();
                shellSr.sprite = shell;
                shellSr.sortingOrder = sortingOrder + 20;  // bay trên đầu mọi thứ
                shellSr.color = shellColor;
            }

            if (playerHp == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerHp = pc.GetComponent<Health>();
            }
        }

        private void Update()
        {
            float t = Time.time - t0;
            if (!impacting)
            {
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, warnTime));
                float pulse = 0.55f + 0.25f * Mathf.Sin(t * 18f);
                ring.transform.localScale = Vector3.one * radius * Mathf.Lerp(0.55f, 1f, k);
                ring.color = new Color(warnColor.r, warnColor.g, warnColor.b, pulse * Mathf.Lerp(0.4f, 1f, k));

                if (shellSr != null)
                {
                    Vector2 p = Vector2.Lerp(shellFrom, at, k) + Vector2.up * shellArc * 4f * k * (1f - k);
                    shellSr.transform.position = p;
                    shellSr.transform.localScale = Vector3.one * shellScale * (1f + 0.35f * 4f * k * (1f - k));
                }

                if (t >= warnTime)
                {
                    impacting = true;
                    SoulRequiem.Core.Sfx.Play("ground_strike", transform.position); // 19/11 (§335): âm
                    impactAt = Time.time;
                    if (shellSr != null) Destroy(shellSr.gameObject);
                    if (frames == null || frames.Length == 0) Hit();
                }
                return;
            }

            float ti = Time.time - impactAt;
            ring.color = new Color(warnColor.r, warnColor.g, warnColor.b, Mathf.Max(0f, 0.6f - ti * 3f));
            if (frames == null || frames.Length == 0)
            {
                if (ti > 0.25f) Destroy(gameObject);
                return;
            }
            int f = Mathf.FloorToInt(ti * fps);
            if (f >= frames.Length) { Destroy(gameObject); return; }
            body.sprite = frames[f];
            if (!hitDone && f >= impactFrame) Hit();
        }

        private void Hit()
        {
            hitDone = true;
            if (leaveSprite != null && leaveSeconds > 0f) Leave();
            if (playerHp == null || playerHp.IsDead) return;
            Vector2 d = (Vector2)playerHp.transform.position - at;
            if (d.magnitude > radius + 0.1f) return;
            bool landed = damage > 0f ? playerHp.TakeDamage(damage, d, knockback) : !playerHp.Invulnerable;
            if (!landed) return;
            if (damage <= 0f && knockback > 0f && d.sqrMagnitude > 0.0001f) playerHp.AddImpulse(d.normalized * knockback);
            if (stunSeconds > 0f) playerHp.Stun(stunSeconds);
            onHit?.Invoke(playerHp);
        }

        private void Leave()
        {
            var go = new GameObject("StrikeDebris");
            go.transform.position = at;
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = leaveSprite;
            s.sortingOrder = sortingOrder - 1;
            s.spriteSortPoint = SpriteSortPoint.Pivot;
            go.transform.localScale = Vector3.one * shellScale;
            go.AddComponent<FadeAndDie>().seconds = leaveSeconds;
        }

        private void OnDestroy()
        {
            if (shellSr != null) Destroy(shellSr.gameObject);
        }

        /// Vòng tròn cảnh báo: đường kính 2 đơn vị ở scale 1 (scale = bán kính).
        private static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null) return ringSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                float c = (n - 1) / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        byte a = r > 1f ? (byte)0 : r > 0.86f ? (byte)230 : (byte)(40 + 50 * r);
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
