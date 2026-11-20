using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Đạn bay thẳng (gai Tắc kè, cầu gió Pháp sư goblin). Không dùng vật lý: tự kiểm tra va chạm
    /// bằng OverlapCircle mỗi khung hình, bỏ qua trigger (zone), quái, và lớp Water (đạn bay qua nước).
    /// Trúng người chơi: gây sát thương. Trúng vật cản cứng: nổ / biến mất.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public float damage = 10f;
        public float speed = 3f;
        public float lifetime = 1.5f;
        public float radius = 0.08f;
        /// <summary>16/11 (§262): true = khung kết thúc (tên cắm) giữ hướng bay (tên bắn dọc cắm dọc), false = về ngang như cũ.</summary>
        public bool keepRotationOnEnd;
        public float knockback = 0f;
        public float poisonPercent = 0f;
        public float poisonDuration = 5f;
        public bool spinToDirection = true;
        /// <summary>08/11 (§197): đạn của BẪY — trúng cả quái; cháy / lạnh cóng khi trúng.</summary>
        public bool hitsEnemies;
        public float burnDps, burnSeconds, chillMul = 1f, chillSeconds;

        [Header("Hoạt ảnh (tùy chọn)")]
        public Sprite[] flyFrames;
        public Sprite[] explodeFrames;
        public float fps = 12f;

        [Header("Mây độc khi vỡ (bong bóng của chuột) — để trống = không có")]
        public Sprite[] cloudPop;
        public Sprite[] cloudLoop;
        public Sprite[] cloudFade;
        public float cloudSeconds = 4f;
        public float cloudRadius = 0.42f;
        public float cloudPoison = 0.03f;
        public float cloudPoisonDuration = 3f;

        private HashSet<Collider2D> ignore;
        private bool cloudDone;

        // 04/11: Turned Blade (sách đỡ đòn) — đạn đang bay; bị chém đúng lúc thì dội ngược, trúng quái thay vì người chơi
        public static readonly List<Projectile> Live = new List<Projectile>();
        public bool Reflected { get; private set; }
        public bool Exploding => exploding;
        public Vector2 Direction => dir;
        private void OnEnable() => Live.Add(this);
        private void OnDisable() => Live.Remove(this);

        /// <summary>Dội đạn ngược theo `newDir`: nhanh hơn, mạnh gấp `damageMul`, sáng vàng, chỉ trúng quái.</summary>
        public void Reflect(Vector2 newDir, float damageMul = 2f)
        {
            if (Reflected || exploding) return;
            Reflected = true;
            dir = newDir.sqrMagnitude > 0.0001f ? newDir.normalized : -dir;
            speed *= 1.6f;
            damage *= damageMul;
            born = Time.time;
            lifetime = Mathf.Max(lifetime, 1.4f);
            cloudDone = true;                                      // đạn dội không nổ thành mây độc
            if (sr != null) sr.color = new Color(1f, 0.92f, 0.55f, 1f);
            if (spinToDirection) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>Bỏ qua các collider này (vd. khối rắn của hố nơi boss lơ lửng — đạn sinh ra bên trong).</summary>
        public void Ignore(IEnumerable<Collider2D> cols)
        {
            if (cols == null) return;
            if (ignore == null) ignore = new HashSet<Collider2D>();
            foreach (var c in cols) if (c != null) ignore.Add(c);
        }

        private Vector2 dir = Vector2.right;
        private float born;
        private bool exploding;
        private float explodeStart;
        private SpriteRenderer sr;
        private int waterLayer;
        private static readonly List<Collider2D> hits = new List<Collider2D>();
        private static ContactFilter2D filter;
        private static bool filterReady;

        public void Launch(Vector2 direction, float spd, float dmg)
        {
            dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            speed = spd;
            damage = dmg;
            if (spinToDirection)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);   // đạn tự sáng, không chìm vào bóng tối
            born = Time.time;
            waterLayer = LayerMask.NameToLayer("Water");
            if (!filterReady)
            {
                filter = new ContactFilter2D();
                filter.useTriggers = false;
                filter.useLayerMask = false;
                filterReady = true;
            }
        }

        private void Update()
        {
            if (exploding)
            {
                int f = Mathf.FloorToInt((Time.time - explodeStart) * fps);
                if (explodeFrames == null || f >= explodeFrames.Length) { Destroy(gameObject); return; }
                sr.sprite = explodeFrames[f];
                return;
            }

            if (sr != null && flyFrames != null && flyFrames.Length > 0)
                sr.sprite = flyFrames[Mathf.FloorToInt((Time.time - born) * fps) % flyFrames.Length];

            if (!Reflected) TurnedBlade.Catch(this);                    // 04/11: cửa sổ đỡ của Turned Blade
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            if (Time.time - born > lifetime) { Explode(); return; }

            hits.Clear();
            Physics2D.OverlapCircle(transform.position, radius, filter, hits);
            foreach (var c in hits)
            {
                if (c == null || c.gameObject.layer == waterLayer) continue;
                if (ignore != null && ignore.Contains(c)) continue;
                if (Reflected)
                {
                    var foe = c.GetComponentInParent<EnemyAI>();
                    if (foe != null)
                    {
                        if (foe.Health != null && !foe.Health.IsDead) foe.Health.TakeDamage(damage, dir, 1.2f);
                        Explode(); return;
                    }
                    var own = c.GetComponentInParent<Health>();
                    if (own != null && own.IsPlayer) continue;
                    if (own == null) { Explode(); return; }
                    continue;
                }
                if (c.GetComponent<TrapLowCover>() != null) continue;      // 08/11 (§197): bàn chưa lật — đạn bay qua
                var enemy = c.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    if (!hitsEnemies || enemy.Health == null || enemy.Health.IsDead) continue;
                    enemy.Health.TakeDamage(damage, dir, knockback);
                    if (burnDps > 0f) enemy.Health.ApplyBurn(burnDps, burnSeconds);
                    Explode(); return;
                }
                var hp = c.GetComponentInParent<Health>();
                if (hp != null && hp.IsPlayer)
                {
                    Health.IncomingRanged = true;                        // 08/11 (§191): Riposte không đỡ đạn
                    bool hurt = hp.TakeDamage(damage, dir, knockback);
                    Health.IncomingRanged = false;
                    if (hurt && poisonPercent > 0f)
                        hp.ApplyPoison(poisonPercent, poisonDuration);
                    if (hurt && burnDps > 0f) hp.ApplyBurn(burnDps, burnSeconds);
                    if (hurt && chillSeconds > 0f) hp.Chill(chillMul, chillSeconds);
                    if (!hp.Invulnerable) { Explode(); return; }
                    continue;                    // đang Roll: đạn xuyên qua
                }
                if (hp == null) { Explode(); return; }   // vật cản cứng (cây, đá, rừng, cổng)
            }
        }

        private void Explode()
        {
            if (!cloudDone && cloudLoop != null && cloudLoop.Length > 0)
            {
                cloudDone = true;
                int order = sr != null ? sr.sortingOrder - 2 : 0;
                PoisonCloud.Spawn(transform.position, cloudPop, cloudLoop, cloudFade, cloudSeconds, cloudRadius,
                                  cloudPoison, cloudPoisonDuration, order);
            }
            if (explodeFrames != null && explodeFrames.Length > 0 && sr != null)
            {
                exploding = true;
                explodeStart = Time.time;
                if (!keepRotationOnEnd) transform.rotation = Quaternion.identity;
            }
            else Destroy(gameObject);
        }

        // ---------- Gai của Tắc kè: tạo sprite hình thoi đơn giản lúc chạy (kho chưa có sprite gai)
        private static Sprite spikeSprite;
        public static Sprite SpikeSprite
        {
            get
            {
                if (spikeSprite != null) return spikeSprite;
                const int w = 12, h = 6;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float half = (h / 2f) * (1f - Mathf.Abs(x - (w - 1) / 2f) / (w / 2f)) * 1.6f;
                        bool inside = Mathf.Abs(y - (h - 1) / 2f) <= half && x > 0;
                        bool edge = inside && (Mathf.Abs(y - (h - 1) / 2f) > half - 1f);
                        px[y * w + x] = inside ? (edge ? new Color32(40, 70, 30, 255) : new Color32(190, 230, 120, 255))
                                               : new Color32(0, 0, 0, 0);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                spikeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 48f);
                return spikeSprite;
            }
        }
    }
}
