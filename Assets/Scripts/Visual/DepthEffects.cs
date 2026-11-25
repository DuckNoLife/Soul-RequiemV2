// Soul Requiem — adds depth to the top-down 2D world (Zelda style).
//
// Runs by itself on scene load (no need to attach it). 3 parts, on top of the existing Y-sort
// (Transparency Sort Axis (0,1,0) + pivot at the feet + every standing object on Order in Layer 2):
//
//  1. OCCLUSION FADE: only fades an object that REALLY draws over the player:
//     (a) the object is drawn AFTER the player by the sorting rules (layer -> order -> Y axis of the sort point), and
//     (b) >= 30% of the player's SOLID PIXELS lie under the object's SOLID PIXELS (reads the alpha channel, not the bounding box).
//     Comparing bounding boxes made tents / trees fade even when standing beside / in front of them (transparent parts of the frame).
//     Enter 30% / exit 12% hysteresis so it does not flicker at object edges.
//  2. BLOB SHADOW: soft elliptical shadow under the player and every enemy (including enemies spawned later),
//     drawn above the ground but below every standing object -> characters "stand on the ground" instead of floating.
//  3. Settings can be tuned in the Inspector of the "DepthEffects" object while in Play Mode.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.Visual
{
    public static class DepthEffectsBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<DepthEffects>() == null)
                new GameObject("DepthEffects").AddComponent<DepthEffects>();
        }
    }

    public class DepthEffects : MonoBehaviour
    {
        [Header("Occlusion fade (by real pixels)")]
        [Tooltip("Order in Layer of standing map objects (same as the player).")]
        public int propSortingOrder = 2;
        [Tooltip("Only objects taller than this (Unity units; 1 tile = 0.32).")]
        public float minOccluderHeight = 0.45f;
        [Tooltip("Object opacity while it covers the player.")]
        [Range(0.1f, 1f)] public float fadedAlpha = 0.35f;
        [Tooltip("Start fading when >= this share of the PLAYER BODY (real pixels) is covered by solid pixels of objects drawn over it (all objects combined).")]
        [Range(0.05f, 1f)] public float enterCoverage = 0.3f;
        [Tooltip("Restore when the covered share drops below this (gap from enter avoids flicker at object edges).")]
        [Range(0f, 1f)] public float exitCoverage = 0.12f;
        [Tooltip("Once Ren is covered, every object covering >= this share of the body fades too (overlapping tree crowns).")]
        [Range(0.01f, 0.5f)] public float partCoverage = 0.05f;
        public float fadeSpeed = 8f;
        [Tooltip("Sampling grid over the player's sprite frame (columns x rows).")]
        public int samplesX = 12, samplesY = 18;
        [Tooltip("Print to the Console (every 0.5 s) the objects overlapping the player: drawn over or not, covered share, alpha readable or not.")]
        public bool debugLog = false;

        [Header("Sort line at the object's REAL feet")]
        [Tooltip("Move the sort point of map objects (ST2U sorts by the FRAME BOTTOM) up to the real feet: bottom of the solid pixels, or the wall base "
               + "of a house (ignoring protruding door steps / porches). Without it, standing in front of a house wall still draws you BEHIND the house.")]
        public bool fixSortLines = true;
        public int minSortShiftPx = 2;

        [Header("Blob shadow")]
        public bool shadows = true;
        [Tooltip("Shadow draw order: above the ground layer (-5), below every standing object (2).")]
        public int shadowSortingOrder = -4;
        [Range(0f, 1f)] public float shadowOpacity = 0.35f;
        [Tooltip("Shadow width relative to the visible width of the character / enemy.")]
        public float shadowWidthFactor = 0.75f;

        Transform player;
        SpriteRenderer playerSr;
        readonly List<SpriteRenderer> occluders = new List<SpriteRenderer>();
        readonly HashSet<SpriteRenderer> fading = new HashSet<SpriteRenderer>();
        readonly HashSet<SpriteRenderer> covered = new HashSet<SpriteRenderer>();   // currently "covered" (enter / exit hysteresis)
        readonly List<Vector3> bodyPts = new List<Vector3>();
        readonly HashSet<SpriteRenderer> sortChecked = new HashSet<SpriteRenderer>();
        int sortFixedCount;
        readonly Dictionary<SpriteRenderer, float> cov = new Dictionary<SpriteRenderer, float>();
        bool[] unionHit = new bool[0];
        bool hidden;
        float nextScan, nextRescan, nextLog;
        static Sprite shadowSprite;

        void Start() => ScanMap();

        void ScanMap()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                player = pc.transform;
                playerSr = pc.GetComponent<SpriteRenderer>();
                if (playerSr == null) playerSr = pc.GetComponentInChildren<SpriteRenderer>();
            }
            var keep = new HashSet<SpriteRenderer>();
            occluders.Clear();
            foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.sortingOrder != propSortingOrder || sr.sprite == null) continue;
                if (sr.GetComponentInParent<PlayerController>() != null) continue;
                if (sr.GetComponentInParent<EnemyAI>() != null) continue;
                if (sr.GetComponentInParent<Rigidbody2D>() != null) continue;      // moving objects, bullets...
                // gates + wall arches above gates (lintel_*) are NOT faded: Ren walking through a doorway must disappear behind the arch,
                // fading the arch makes Ren look like he is "climbing onto the gate".
                if (sr.GetComponentInParent<SoulRequiem.Gameplay.GateAnimator>() != null) continue;
                if (IsLintel(sr)) continue;
                FixSortLine(sr);
                if (sr.bounds.size.y < minOccluderHeight) continue;                 // low objects cannot cover a person
                occluders.Add(sr); keep.Add(sr);
            }
            // faded object no longer in the list (hidden / destroyed) -> restore its opacity
            foreach (var sr in new List<SpriteRenderer>(fading))
                if (sr != null && !keep.Contains(sr)) { var c = sr.color; c.a = 1f; sr.color = c; fading.Remove(sr); covered.Remove(sr); }
        }

        /// wall arch above a gate — Tiled object name (or sprite name) starts with "lintel_".
        static bool IsLintel(SpriteRenderer sr)
        {
            if (sr.sprite != null && sr.sprite.name.StartsWith("lintel_")) return true;
            for (var t = sr.transform; t != null; t = t.parent)
                if (t.name.StartsWith("lintel_")) return true;
            return false;
        }

        void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.5f;
                if (player == null) ScanMap();
                if (shadows) AddMissingShadows();
            }
            if (Time.time >= nextRescan) { nextRescan = Time.time + 4f; if (player != null) ScanMap(); }   // objects shown / hidden by mechanisms
            if (player == null || playerSr == null || playerSr.sprite == null) return;

            // SOLID pixels of the player's body (world coordinates) — sampled once per frame.
            BuildBodyPoints();
            var pb = playerSr.bounds;
            bool log = debugLog && Time.time >= nextLog;
            if (log) nextLog = Time.time + 0.5f;
            float k = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);

            // Pass 1: which objects DRAW OVER Ren and how many body points each covers; take the UNION of all objects (forest: many crowns, each
            // covering a little but together hiding the whole body -> must still fade).
            int n = bodyPts.Count;
            if (unionHit.Length < n) unionHit = new bool[n * 2];
            System.Array.Clear(unionHit, 0, unionHit.Length);
            cov.Clear();
            for (int i = 0; i < occluders.Count && n > 0; i++)
            {
                var sr = occluders[i];
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
                // overlap test ONLY on the XY plane (Bounds.Intersects also checks Z: Ren z=1, objects z=0 -> they would never touch)
                var ob = sr.bounds;
                if (!(ob.min.x < pb.max.x && ob.max.x > pb.min.x && ob.min.y < pb.max.y && ob.max.y > pb.min.y)) continue;
                bool over = DrawsOverPlayer(sr);
                int hit = 0;
                if (over)
                    for (int j = 0; j < n; j++)
                        if (Opaque(sr, bodyPts[j])) { hit++; unionHit[j] = true; }
                if (hit > 0) cov[sr] = hit / (float)n;
                if (log)
                    Debug.Log($"[DepthEffects] {sr.name}: drawnOver={over} covered={(hit / (float)n):P0} (body points {n}) " +
                              $"sortY object={SortY(sr):F2} player={SortY(playerSr):F2}");
            }
            int u = 0;
            for (int j = 0; j < n; j++) if (unionHit[j]) u++;
            float union = n > 0 ? u / (float)n : 0f;
            hidden = union >= (hidden ? exitCoverage : enterCoverage);          // enter / exit hysteresis
            if (log) Debug.Log($"[DepthEffects] TOTAL covered {union:P0} -> {(hidden ? "FADED" : "clear")}");

            // Pass 2: once Ren is covered enough, fade EVERY object contributing to the cover (>= partCoverage), other objects stay as they are.
            for (int i = 0; i < occluders.Count; i++)
            {
                var sr = occluders[i];
                if (sr == null) continue;
                bool was = covered.Contains(sr);
                float target = 1f;
                if (hidden && cov.TryGetValue(sr, out var c0) && c0 >= (was ? partCoverage * 0.5f : partCoverage)) target = fadedAlpha;
                if (target < 1f) covered.Add(sr); else covered.Remove(sr);

                var c = sr.color;
                if (Mathf.Abs(c.a - target) < 0.005f)
                {
                    if (target >= 1f) fading.Remove(sr);
                    continue;
                }
                c.a = Mathf.Lerp(c.a, target, k);
                if (target >= 1f && c.a > 0.995f) c.a = 1f;
                sr.color = c;
                fading.Add(sr);
            }
        }

        /// Is the object drawn AFTER (over) the player — following Unity's sorting rules:
        /// sorting layer -> Order in Layer -> Custom axis (0,1,0): the lower sort point (closer to the camera) draws later.
        bool DrawsOverPlayer(SpriteRenderer sr)
        {
            var sg = sr.GetComponentInParent<SortingGroup>();
            int layer = sg != null ? sg.sortingLayerID : sr.sortingLayerID, order = sg != null ? sg.sortingOrder : sr.sortingOrder;
            if (layer != playerSr.sortingLayerID)
                return SortingLayer.GetLayerValueFromID(layer) > SortingLayer.GetLayerValueFromID(playerSr.sortingLayerID);
            if (order != playerSr.sortingOrder) return order > playerSr.sortingOrder;
            return SortY(sr) < SortY(playerSr) - 0.001f;
        }

        static float SortY(SpriteRenderer sr)
        {
            var sg = sr.GetComponentInParent<SortingGroup>();
            if (sg != null) return sg.transform.position.y;                        // a Sorting Group sorts by its own position
            return sr.spriteSortPoint == SpriteSortPoint.Pivot ? sr.transform.position.y : sr.bounds.center.y;
        }

        // ------------------------------------------------------------------ sort line = the object's real feet
        /// ST2U: tile object = root -> "(Pivot)" -> "(CF)" (flip) -> renderer; the renderer sorts by the FRAME BOTTOM.
        /// The frame bottom often has transparent padding (pine tree 12 px) or a protruding door step (inn: wall 2774, step 2806):
        /// standing in front of the wall the feet are still "higher" than the frame bottom => drawn BEHIND the house, the fade triggers wrongly.
        /// Fix without changing the image: move "(Pivot)" up d px, move "(CF)" down d px (image stays put) + add a SortingGroup on "(Pivot)"
        /// => the whole object sorts by the "(Pivot)" position = real feet. Sprite and object tree unchanged (show / hide by name still works).
        void FixSortLine(SpriteRenderer sr)
        {
            if (!fixSortLines || !sortChecked.Add(sr)) return;
            var cf = sr.transform.parent; var pv = cf != null ? cf.parent : null;
            if (pv == null || !cf.name.EndsWith("(CF)") || !pv.name.EndsWith("(Pivot)")) return;
            if (pv.GetComponent<SortingGroup>() != null || pv.GetComponentInParent<SortingGroup>() != null) return;
            if (cf.localScale.y < 0f || Mathf.Abs(pv.parent != null ? pv.parent.eulerAngles.z : 0f) > 0.01f) return;   // flipped vertically / rotated: skip
            var sp = sr.sprite;
            if (sp == null || sr.drawMode != SpriteDrawMode.Simple) return;
            string nm = sp.name.ToLowerInvariant();
            bool organic = nm.Contains("tree") || nm.Contains("pine") || nm.Contains("bush");
            int d = BaseLinePx(sp, !organic);
            if (d < minSortShiftPx) return;
            float du = d / sp.pixelsPerUnit;
            pv.localPosition += new Vector3(0f, du, 0f);
            cf.localPosition -= new Vector3(0f, du, 0f);
            var g = pv.gameObject.AddComponent<SortingGroup>();
            g.sortingLayerID = sr.sortingLayerID;
            g.sortingOrder = sr.sortingOrder;
            sortFixedCount++;
            if (debugLog) Debug.Log($"[DepthEffects] Sort line {sp.name}: +{d}px");
        }

        /// Pixels from the frame bottom up to the REAL feet: lowest solid bottom; for wide objects (houses...) if >= 50% of the solid columns have their bottom
        /// on the same line (+-3 px) at least 4 px above the lowest bottom (door step, porch) use that line (wall base).
        static readonly Dictionary<Sprite, int> baseCache = new Dictionary<Sprite, int>();
        static int BaseLinePx(Sprite sp, bool allowDominant)
        {
            if (baseCache.TryGetValue(sp, out var cached)) return cached;
            int v = BaseLinePxRaw(sp, allowDominant);
            baseCache[sp] = v;
            return v;
        }

        static int BaseLinePxRaw(Sprite sp, bool allowDominant)
        {
            var map = GetAlpha(sp.texture);
            if (map == null) return 0;
            Rect tr;
            try { tr = sp.textureRect; } catch { return 0; }
            int x0 = Mathf.RoundToInt(tr.x), y0 = Mathf.RoundToInt(tr.y), w = Mathf.RoundToInt(tr.width), h = Mathf.RoundToInt(tr.height);
            int padBottom = Mathf.RoundToInt(sp.textureRectOffset.y);            // transparent area trimmed when packed
            var bottoms = new List<int>(w);
            for (int x = 0; x < w; x++)
            {
                int cnt = 0, low = -1;
                for (int y = 0; y < h; y++)
                {
                    int tx = x0 + x, ty = y0 + y;
                    if (tx < 0 || ty < 0 || tx >= map.w || ty >= map.h) continue;
                    if (map.a[ty * map.w + tx] > 100) { cnt++; if (low < 0) low = y; }
                }
                if (cnt >= 3) bottoms.Add(low + padBottom);
            }
            if (bottoms.Count == 0) return 0;
            bottoms.Sort();
            int lowest = bottoms[0];
            if (allowDominant && bottoms.Count >= 64)
            {
                int best = 0, bestL = lowest;
                for (int i = 0, j = 0; i < bottoms.Count; i++)
                {
                    while (j < bottoms.Count && bottoms[j] <= bottoms[i] + 3) j++;
                    if (j - i > best) { best = j - i; bestL = bottoms[i]; }
                }
                if (best >= bottoms.Count * 0.5f && bestL - lowest >= 4) return bestL;
            }
            return lowest;
        }

        void BuildBodyPoints()
        {
            bodyPts.Clear();
            var sp = playerSr.sprite;
            var r = sp.rect; float ppu = sp.pixelsPerUnit;
            int nx = Mathf.Max(2, samplesX), ny = Mathf.Max(2, samplesY);
            for (int yi = 0; yi < ny; yi++)
                for (int xi = 0; xi < nx; xi++)
                {
                    float px = (xi + 0.5f) / nx * r.width, py = (yi + 0.5f) / ny * r.height;
                    if (!SpritePixelOpaque(sp, px, py, true)) continue;          // only points that belong to the player's IMAGE
                    float lx = (px - sp.pivot.x) / ppu, ly = (py - sp.pivot.y) / ppu;
                    if (playerSr.flipX) lx = -lx;
                    if (playerSr.flipY) ly = -ly;
                    var w = playerSr.transform.TransformPoint(new Vector3(lx, ly, 0f));
                    bodyPts.Add(new Vector3(w.x, w.y, 0f));
                }
        }

        /// Does a world point fall on a SOLID pixel of the sprite.
        static bool Opaque(SpriteRenderer sr, Vector3 world)
        {
            var sp = sr.sprite;
            if (sp == null) return false;
            if (sr.drawMode != SpriteDrawMode.Simple) { var b = sr.bounds; return world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y; }
            Vector3 l = sr.transform.InverseTransformPoint(new Vector3(world.x, world.y, sr.transform.position.z));
            float ppu = sp.pixelsPerUnit;
            float px = l.x * ppu, py = l.y * ppu;
            if (sr.flipX) px = -px;
            if (sr.flipY) py = -py;
            px += sp.pivot.x; py += sp.pivot.y;
            if (px < 0f || py < 0f || px >= sp.rect.width || py >= sp.rect.height) return false;
            return SpritePixelOpaque(sp, px, py, true);
        }

        // ------------------------------------------------------------------ alpha channel (read once per image, even without Read/Write)
        class AlphaMap { public int w, h; public byte[] a; }
        static readonly Dictionary<Texture2D, AlphaMap> alphaCache = new Dictionary<Texture2D, AlphaMap>();

        /// (px,py) = pixel inside the sprite rect (origin bottom-left). Unreadable image -> returns fallback.
        static bool SpritePixelOpaque(Sprite sp, float px, float py, bool fallback)
        {
            var map = GetAlpha(sp.texture);
            if (map == null) return fallback;
            Rect tr;
            try { tr = sp.textureRect; } catch { return fallback; }          // sprite packed with Tight mode
            var off = sp.textureRectOffset;
            int tx = Mathf.FloorToInt(tr.x + px - off.x), ty = Mathf.FloorToInt(tr.y + py - off.y);
            if (tx < tr.xMin || ty < tr.yMin || tx >= tr.xMax || ty >= tr.yMax) return false;
            if (tx < 0 || ty < 0 || tx >= map.w || ty >= map.h) return false;
            return map.a[ty * map.w + tx] > 100;
        }

        static AlphaMap GetAlpha(Texture2D tex)
        {
            if (tex == null) return null;
            if (alphaCache.TryGetValue(tex, out var m)) return m;
            m = null;
            try
            {
                int w = tex.width, h = tex.height;
                Color32[] px;
                if (tex.isReadable) px = tex.GetPixels32();
                else
                {
                    var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    var prev = RenderTexture.active;
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                    tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    px = tmp.GetPixels32();
                    Destroy(tmp);
                }
                var a = new byte[w * h];
                for (int i = 0; i < a.Length; i++) a[i] = px[i].a;
                m = new AlphaMap { w = w, h = h, a = a };
            }
            catch (System.Exception e) { Debug.LogWarning("[DepthEffects] Cannot read alpha of " + tex.name + ": " + e.Message); }
            alphaCache[tex] = m;
            return m;
        }

        // ------------------------------------------------------------------ blob shadow
        void AddMissingShadows()
        {
            if (player != null) EnsureShadow(player);
            foreach (var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)) EnsureShadow(ai.transform);
        }

        void EnsureShadow(Transform t)
        {
            if (t.Find("_BlobShadow") != null) return;
            var body = t.GetComponentInChildren<SpriteRenderer>();
            if (body == null || body.sprite == null) return;

            var go = new GameObject("_BlobShadow");
            go.transform.SetParent(t, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetShadowSprite();
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = shadowSortingOrder;
            sr.color = new Color(0f, 0f, 0f, shadowOpacity);
            if (body.sharedMaterial != null) sr.sharedMaterial = body.sharedMaterial;

            // width from the sprite's opaque part (not the empty frame)
            float w = body.sprite.bounds.size.x;
            var tex = body.sprite.texture;
            if (tex != null && body.sprite.rect.width > 0) w = body.sprite.rect.width / body.sprite.pixelsPerUnit;
            w = Mathf.Clamp(w * shadowWidthFactor, 0.12f, 1.6f);          // parent local units
            var ws = GetShadowSprite().bounds.size;                         // original size of the shadow sprite
            go.transform.localScale = new Vector3(w / ws.x, w * 0.4f / ws.y, 1f);
            go.transform.localPosition = new Vector3(0f, 0.01f, 0f);       // parent pivot = feet
            go.AddComponent<BlobShadowFollow>().Init(body, sr);
        }

        static Sprite GetShadowSprite()
        {
            if (shadowSprite != null) return shadowSprite;
            const int W = 32, H = 16;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - W / 2f) / (W / 2f), dy = (y + 0.5f - H / 2f) / (H / 2f);
                    float d = dx * dx + dy * dy;
                    float a = d >= 1f ? 0f : (d < 0.45f ? 1f : 0.55f);         // 2 pixel-art steps
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            shadowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            shadowSprite.name = "BlobShadow";
            return shadowSprite;
        }
    }

    /// Hide the shadow when its owner is hidden / dead (renderer off or transparent).
    public class BlobShadowFollow : MonoBehaviour
    {
        SpriteRenderer body, shadow;
        float baseAlpha;

        public void Init(SpriteRenderer b, SpriteRenderer s)
        {
            body = b; shadow = s; baseAlpha = s.color.a;
        }

        void LateUpdate()
        {
            if (body == null || shadow == null) return;
            var c = shadow.color;
            c.a = body.enabled && body.gameObject.activeInHierarchy ? baseAlpha * body.color.a : 0f;
            shadow.color = c;
        }
    }
}
