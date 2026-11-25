using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// GORGEMAW, THE BROOD TOAD (mini-boss of the Culture Hall) - a fat toad fed on Ichor.
    ///   Hop        : 2-3 hops toward the player; each landing only makes an Ichor ripple (no shock wave)
    ///   Tongue     : (≤ tongueRange) throat swells, aims, the tongue shoots out curving then straightening; on hit it YANKS the player up to its mouth
    ///                (no damage - only pulls him close for the follow-up); then the tongue retracts
    ///   Leap Slam  : jumps off screen, its shadow follows the player then locks; landing: big shock wave + knockback.
    ///                Landing near a GLASS TANK (MapMechanics.Shatter) -> the tank breaks and the toad is stunned `stunTime` seconds
    ///   - Entering phase 2 (≤50% HP): leaps to the MIDDLE of the area and slams down, breaking ALL 4 glass tanks (Ichor spills into stinging pools), then the toadlets
    ///     RISE FROM THOSE Ichor pools (one per pool).
    ///     The brood happens EXACTLY ONCE on entering phase 2 - each pool releases one toadlet (at most `broodCap`), no further waves.
    ///     Phase 2 punishment instead: a Leap Slam landing in an Ichor pool -> the toad sinks and is stuck `sinkTime` seconds (phase 1 has "break a tank -> stunned").
    ///   - ATTACK RHYTHM:
    ///     - Weighted random pick among ready attacks, never repeating the last one (a fixed order made it hop nonstop and was easy to predict).
    ///     - Tongue hit -> BELLY FLOP at once: the toad hops low and crashes onto where Ren was pulled (red ring 0.55 s, can be rolled out of) - the tongue has a consequence.
    ///     - Leap Slam misses (no tank / pool) -> the toad PANTS for 1.2 s (clear counterattack window).
    ///     - Phase 2: while toadlets live, the mother keeps her distance (hops back) and prefers the tongue to pull Ren into the brood; brood all dead -> ENRAGED: faster hops,
    ///       shorter cooldowns. Toadlets are Provoked (rush Ren as soon as they rise from the pool).
    /// Class files: ToadSkills.cs (settings, setup, skill choice, helpers) · ToadSkills.Attacks.cs (each skill as a coroutine).
    /// </summary>
    public partial class ToadSkills : MonoBehaviour, IEnemySkills
    {
        public GameObject globPrefab;                 // (unused)
        public Sprite globShell;                      // (unused - kept so old prefabs do not break)
        public Sprite[] splashFrames;                 // Ichor ripple on landing
        public string broodEnemy = "Skinned_Hopper";
        public int broodCap = 4;
        public float slamEvery = 7f, tongueEvery = 3.2f, broodEvery = 12f;   // broodEvery: unused
        public float hopSpeed = 1.6f, hopTime = 0.8f;
        public float leapHeight = 2.4f, trackTime = 1.2f, lockTime = 0.5f;
        public float slamRadius = 0.9f, shatterRadius = 1.0f, stunTime = 3f;
        [Tooltip("Phase 2 - a Leap Slam landing in an Ichor pool makes the toad sink and get stuck for this many seconds (punish window)")]
        public float sinkTime = 1.8f, sinkRadius = 1.1f;
        public float tongueRange = 2.6f, tongueWidth = 0.16f;
        public Vector2 mouthOffset = new Vector2(0f, 0.5f);
        public Color tongueColor = new Color(0.86f, 0.42f, 0.5f);

        private EnemyAI ai;
        private float nextSlam, nextTongue;
        private bool engaged, phaseLeapDone, sinkHinted;
        private bool tongueHit, enraged, windedHinted;
        private string lastMove = "";
        private readonly List<EnemyAI> brood = new List<EnemyAI>();
        private static Sprite shadowSprite, pixel;
        private GameObject liveShadow, liveTongue, liveWarn;

        /// <summary>Weighted random pick among ready attacks, never repeating the last one (hopping may repeat when nothing else is ready).</summary>
        private string Pick(float d)
        {
            bool broodAlive = ai.InPhase2 && brood.Count > 0;
            var ids = new List<string>(); var ws = new List<float>();
            void Add(string id, bool ok, float w) { if (ok && id != lastMove) { ids.Add(id); ws.Add(w); } }
            Add("slam", Time.time >= nextSlam, broodAlive ? 1.5f : 3f);
            Add("tongue", d <= tongueRange && Time.time >= nextTongue, broodAlive ? 4f : 3f);
            Add("back", broodAlive && d < 1.8f, 2.5f);                    // brood alive: back off and let the brood surround Ren
            Add("hop", !broodAlive || d > 2.6f, 2f);
            if (ids.Count == 0) return broodAlive && d < 1.8f ? "back" : "hop";
            float sum = 0f; foreach (var w in ws) sum += w;
            float r = Random.value * sum;
            for (int i = 0; i < ids.Count; i++) { r -= ws[i]; if (r <= 0f) return ids[i]; }
            return ids[ids.Count - 1];
        }

        private Vector2 Mouth => (Vector2)transform.position + mouthOffset;

        private static Material tongueMat;
        private static Texture2D tongueTex;
        private static Sprite tipSprite;

        private LineRenderer MakeTongueLine(int order)
        {
            if (tongueTex == null)
            {
                // 1x8 tongue cross-section: dark edge - pink body - highlight - body - edge
                var cols = new[] { new Color32(0x5a, 0x1e, 0x2a, 255), new Color32(0xa8, 0x40, 0x52, 255), new Color32(0xd6, 0x6c, 0x7e, 255),
                                   new Color32(0xf2, 0xa4, 0xb0, 255), new Color32(0xd6, 0x6c, 0x7e, 255), new Color32(0xc4, 0x58, 0x6a, 255),
                                   new Color32(0xa8, 0x40, 0x52, 255), new Color32(0x5a, 0x1e, 0x2a, 255) };
                tongueTex = new Texture2D(1, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tongueTex.SetPixels32(cols);
                tongueTex.Apply();
            }
            if (tongueMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                tongueMat = new Material(sh) { mainTexture = tongueTex };
            }
            var go = new GameObject("ToadTongue");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = tongueMat;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.TransformZ;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.positionCount = 14;
            lr.sortingOrder = order;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, tongueWidth * 1.1f), new Keyframe(1f, tongueWidth * 0.65f));
            lr.startColor = lr.endColor = Color.white;
            return lr;
        }

        /// Tongue = a curve: sideways offset shaped like half a sine wave, largest in the middle, both ends fixed to the mouth and the tongue tip.
        private static void DrawTongue(LineRenderer lr, Vector2 from, Vector2 dir, float len, float bend)
        {
            Vector2 n = new Vector2(-dir.y, dir.x);
            int c = lr.positionCount;
            for (int i = 0; i < c; i++)
            {
                float u = i / (float)(c - 1);
                Vector2 p = from + dir * (len * u) + n * (bend * Mathf.Sin(u * Mathf.PI) * len);
                lr.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
        }

        private SpriteRenderer MakeTip(string name, int order)
        {
            if (tipSprite == null)
            {
                const int N = 12;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[N * N];
                float c = (N - 1) / 2f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (N / 2f);
                        Color32 col = new Color32(0, 0, 0, 0);
                        if (r <= 1f) col = r > 0.8f ? new Color32(0x5a, 0x1e, 0x2a, 255)
                                        : (x < c && y > c && r < 0.5f) ? new Color32(0xf2, 0xa4, 0xb0, 255)
                                        : y < c - 1 ? new Color32(0xb0, 0x48, 0x5a, 255) : new Color32(0xd6, 0x6c, 0x7e, 255);
                        px[y * N + x] = col;
                    }
                tex.SetPixels32(px);
                tex.Apply();
                tipSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 48f);
            }
            var go = new GameObject(name);
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = tipSprite;
            s.sortingOrder = order;
            return s;
        }

        private int GroundOrder => ai.Body != null ? ai.Body.sortingOrder - 1 : 1;
        private static readonly Color waveColor = new Color(0.72f, 1f, 0.5f, 0.85f);

        // ------------------------------------------------------------------ helpers
        /// Ichor ripple under the feet (visual only, no damage).
        private void Ripple(Vector2 at, float scale)
        {
            if (splashFrames == null || splashFrames.Length == 0) return;
            var g = GroundStrike.Spawn(at, 0f, 0f, 0f, splashFrames, 999, 14f, ai.Body != null ? ai.Body.sortingOrder - 1 : 1, Color.clear);
            g.impactSfx = "acid_splash"; // sound
            g.bodyScale = scale;
            g.bodyColor = new Color(0.55f, 1f, 0.45f, 0.8f);
        }

        private SpriteRenderer MakeStrip(string name, int order)
        {
            if (pixel == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            }
            var go = new GameObject(name);
            var s = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(s);
            s.sprite = pixel;
            s.sortingOrder = order;
            return s;
        }

        private static void Place(SpriteRenderer s, Vector2 from, Vector2 dir, float len, float width)
        {
            s.transform.position = from;
            s.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            s.transform.localScale = new Vector3(len, width, 1f);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * k)).magnitude;
        }

        private GameObject MakeShadow()
        {
            if (shadowSprite == null)
            {
                const int W = 64, H = 32;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = (x + 0.5f - W / 2f) / (W / 2f), dy = (y + 0.5f - H / 2f) / (H / 2f);
                        float d = dx * dx + dy * dy;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? (d > 0.75f ? 0.6f : 1f) : 0f));
                    }
                tex.Apply();
                shadowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 64f);
            }
            var go = new GameObject("ToadShadow");
            liveShadow = go;
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = shadowSprite;
            s.sortingOrder = ai.Body != null ? ai.Body.sortingOrder - 1 : 1;
            go.transform.position = transform.position;
            return go;
        }

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) h.Died += _ => { CleanupFx(); KillBrood(); };
        }

        private void OnDisable() => CleanupFx();

        private void CleanupFx()
        {
            if (liveShadow != null) Destroy(liveShadow);
            if (liveTongue != null) Destroy(liveTongue);
            if (liveWarn != null) Destroy(liveWarn);
        }

        private void KillBrood()
        {
            foreach (var e in brood)
                if (e != null && e.Health != null && !e.Health.IsDead) e.Health.TakeDamage(99999f);
            brood.Clear();
        }
    }
}
