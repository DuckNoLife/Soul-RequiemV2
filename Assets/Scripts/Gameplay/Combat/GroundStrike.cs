using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Attack landing on one point on the ground:
    ///   1. a warning ring appears at the landing point (at least ~0.6 s, the general trap rule);
    ///   2. (optional) a shell flies in an arc from the muzzle to the landing point during the warning;
    ///   3. plays the impact animation, deals damage ONCE if the player stands within the radius.
    /// Used for: the Greystone Seer's mud bolts + rock spikes, the Camp Gunner's shells, Sunfall-style boss pillars.
    /// A dash (i-frames) dodges it because Health.TakeDamage ignores an invulnerable player.
    /// </summary>
    public class GroundStrike : MonoBehaviour
    {
        public float warnTime = 0.6f;
        public float radius = 0.3f;
        public float damage = 10f;
        public float knockback = 0f;
        public float stunSeconds = 0f;
        [Tooltip("Called when the strike hits the player (damage 0 = status effect only).")]
        public System.Action<Health> onHit;
        public Color warnColor = new Color(1f, 0.45f, 0.25f, 1f);
        [Tooltip("Impact sound (Sfx id). Empty = automatic: with a falling shell -> rock_crash, with an animation -> ground_strike, warning ring only -> silent (the boss plays its own). 'none' = silent.")]
        public string impactSfx = "";
        /// <summary>Light2D flash brightness on impact (0 = off). Light radius = 2.5 x ring radius.</summary>
        public float impactLight = 0.9f;
        /// <summary>The falling shell rotates to its flight direction — for shell images drawn facing right (+x), e.g. m3x_blood_bolt. Adds a small light on the shell.</summary>
        public bool shellFaceMotion;
        public float shellLight = 0f;
        private Vector2 shellPrev;
        /// <summary>Screen shake on impact (0 = none; bosses use 1-3, normal enemies keep 0 to avoid constant shaking).</summary>
        public int impactShake = 0;

        [Header("Impact animation")]
        public Sprite[] frames;
        public int impactFrame = 0;          // frame that deals the damage
        public float fps = 14f;
        public int sortingOrder = 2;
        public float bodyScale = 1f;         // scales the impact animation (does not affect the warning ring)
        public Color bodyColor = Color.white; // tints the impact animation
        [Tooltip("Image left on the floor after landing (e.g. a boulder), fading over leaveSeconds")]
        public Sprite leaveSprite;
        public float leaveSeconds = 0f;
        public float shellScale = 1f;
        public Color shellColor = Color.white;

        [Header("Arcing shell (optional)")]
        public Sprite shell;
        public Vector2 shellFrom;
        public float shellArc = 0.9f;

        private SpriteRenderer ring, fill, body, shellSr;
        private float t0, impactAt;
        private bool impacting, hitDone;
        private Vector2 at;

        private static Sprite ringSprite, discSprite;
        private static Health playerHp;

        public static GroundStrike Spawn(Vector2 at, float warn, float radius, float damage, Sprite[] frames, int impactFrame,
                                         float fps, int order, Color warnColor)
        {
            FirstTimeHints.Show("tut_circle", "A red circle marks where something will land - get out of it before it strikes.", 4.5f);
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
            ring.sortingOrder = -4;                       // above ground decals (-5), below characters
            ring.color = new Color(warnColor.r, warnColor.g, warnColor.b, 0f);
            // FILLING CORE — a faint disc grows from the centre to the rim: reaching the rim = impact (shows how long is left)
            var fgo = new GameObject("WarningFill");
            fgo.transform.SetParent(transform, false);
            fill = fgo.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(fill);
            fill.sprite = DiscSprite;
            fill.sortingOrder = -4;
            fill.color = new Color(warnColor.r, warnColor.g, warnColor.b, 0f);
            fgo.transform.localScale = Vector3.zero;

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
                shellSr.sortingOrder = sortingOrder + 20;  // flies above everything
                shellSr.color = shellColor;
                shellPrev = shellFrom;
                if (shellLight > 0f) BossJuice.Light(shellFrom, warnColor, 0.8f, shellLight, 0f, s.transform);   // self-lit shell
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
                // FIXED RIM at exactly the hit radius (no growing ring that was up to 45% smaller than the real area),
                // blinking faster in the last 30%; the CORE fills from the centre to the rim = countdown.
                float blink = k < 0.7f ? 0.8f + 0.2f * Mathf.Sin(t * 10f) : 0.65f + 0.35f * Mathf.Sin(t * 38f);
                float pop = Mathf.Min(1f, t / 0.08f);                                            // rim pops out in the first 0.08 s
                ring.transform.localScale = Vector3.one * radius * Mathf.Lerp(1.25f, 1f, pop);
                ring.color = new Color(warnColor.r, warnColor.g, warnColor.b, blink * Mathf.Lerp(0.55f, 1f, k) * pop);
                fill.transform.localScale = Vector3.one * radius * k;
                fill.color = new Color(warnColor.r, warnColor.g, warnColor.b, Mathf.Lerp(0.18f, 0.42f, k));

                if (shellSr != null)
                {
                    Vector2 p = Vector2.Lerp(shellFrom, at, k) + Vector2.up * shellArc * 4f * k * (1f - k);
                    if (shellFaceMotion && (p - shellPrev).sqrMagnitude > 1e-6f)
                    {
                        Vector2 v = p - shellPrev;
                        shellSr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                    }
                    shellPrev = p;
                    shellSr.transform.position = p;
                    shellSr.transform.localScale = Vector3.one * shellScale * (1f + 0.35f * 4f * k * (1f - k));
                }

                if (t >= warnTime)
                {
                    impacting = true;
                    string sfxId = !string.IsNullOrEmpty(impactSfx) ? impactSfx : shell != null ? "rock_crash" : frames != null && frames.Length > 0 && frames.Length > impactFrame ? "ground_strike" : ""; // sound
                    if (sfxId != "none") SoulRequiem.Core.Sfx.Play(sfxId, transform.position);
                    impactAt = Time.time;
                    if (shellSr != null) Destroy(shellSr.gameObject);
                    if (impactLight > 0f) BossJuice.Flash(at, warnColor, radius * 2.5f + 0.3f, impactLight, 0.3f);   // real light flash
                    if (impactShake > 0) BossJuice.Shake(impactShake);
                    fill.color = new Color(1f, 1f, 1f, 0.75f);                                    // core flashes white for one beat on impact
                    fill.transform.localScale = Vector3.one * radius;
                    if (frames == null || frames.Length == 0) Hit();
                }
                return;
            }

            float ti = Time.time - impactAt;
            ring.color = new Color(warnColor.r, warnColor.g, warnColor.b, Mathf.Max(0f, 0.6f - ti * 3f));
            fill.color = new Color(1f, 1f, 1f, Mathf.Max(0f, 0.75f - ti * 6f));                // white flash fades fast
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

        /// soft solid disc (filling core) — 2 units across at scale 1, 12% soft edge.
        private static Sprite DiscSprite
        {
            get
            {
                if (discSprite != null) return discSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                float c = (n - 1) / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        byte a = r > 1f ? (byte)0 : r > 0.88f ? (byte)(255 * (1f - (r - 0.88f) / 0.12f)) : (byte)(150 + 105 * r);   // brighter toward the edge: crisp front edge
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                discSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
                return discSprite;
            }
        }

        /// Warning ring: 2 units across at scale 1 (scale = radius).
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
