// Soul Requiem — tạo chiều sâu cho thế giới 2D nhìn từ trên xuống (kiểu Zelda).
//
// Tự chạy khi vào scene (không cần gắn vào đâu). Gồm 3 phần, bổ trợ cho Y-sort đã có
// (Transparency Sort Axis (0,1,0) + pivot ở chân + mọi vật đứng cùng Order in Layer 2):
//
//  1. LÀM MỜ VẬT CHE (occlusion fade) — 12/11 làm lại: chỉ mờ khi vật THẬT SỰ vẽ đè lên nhân vật:
//     (a) vật được vẽ SAU nhân vật theo đúng luật xếp lớp (layer -> order -> trục Y của điểm xếp), và
//     (b) >= 30% ĐIỂM ẢNH ĐẶC của thân nhân vật nằm dưới ĐIỂM ẢNH ĐẶC của vật (đọc kênh alpha, không dùng khung chữ nhật).
//     Trước đây so KHUNG ảnh: đứng cạnh / trước lều, cây (vùng trong suốt của khung) cũng bị mờ.
//     Có trễ vào 30% / ra 12% để không nhấp nháy ở mép vật.
//  2. BÓNG ĐỔ DƯỚI CHÂN (blob shadow): vệt bóng elip mềm dưới nhân vật và mọi quái (kể cả quái sinh ra sau),
//     vẽ trên mặt đất nhưng dưới mọi vật đứng -> nhân vật "đứng trên đất" thay vì trôi trên nền.
//  3. Thông số chỉnh được trong Inspector của object "DepthEffects" khi đang Play.
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
        [Header("Làm mờ vật che nhân vật (12/11: theo ĐIỂM ẢNH thật)")]
        [Tooltip("Order in Layer của vật đứng trên map (bằng nhân vật).")]
        public int propSortingOrder = 2;
        [Tooltip("Chỉ xét vật cao hơn mức này (đơn vị Unity; 1 ô = 0,32).")]
        public float minOccluderHeight = 0.45f;
        [Tooltip("Độ đục của vật khi đang che nhân vật.")]
        [Range(0.1f, 1f)] public float fadedAlpha = 0.35f;
        [Tooltip("Bắt đầu mờ khi >= tỉ lệ này của THÂN NHÂN VẬT (điểm ảnh thật) bị điểm ảnh đặc của các vật vẽ đè lên (gộp mọi vật).")]
        [Range(0.05f, 1f)] public float enterCoverage = 0.3f;
        [Tooltip("Hiện lại khi tỉ lệ bị che tụt dưới mức này (chênh với enter để không nhấp nháy ở mép vật).")]
        [Range(0f, 1f)] public float exitCoverage = 0.12f;
        [Tooltip("Khi Ren đã bị che, vật nào phủ >= tỉ lệ này của thân thì mờ theo (tán cây chồng nhau).")]
        [Range(0.01f, 0.5f)] public float partCoverage = 0.05f;
        public float fadeSpeed = 8f;
        [Tooltip("Lưới lấy mẫu trên khung ảnh nhân vật (cột x hàng).")]
        public int samplesX = 12, samplesY = 18;
        [Tooltip("In ra Console (mỗi 0,5 s) các vật đang chồng khung với nhân vật: có vẽ đè không, tỉ lệ che, đọc được alpha không.")]
        public bool debugLog = false;

        [Header("Đường xếp lớp theo CHÂN THẬT của vật (12/11)")]
        [Tooltip("Dời điểm xếp lớp của vật trên map (ST2U xếp theo ĐÁY KHUNG ẢNH) lên chân thật: đáy điểm ảnh đặc, hoặc chân tường "
               + "của nhà (bỏ bậc cửa / hiên nhô ra). Không làm thì đứng trước tường nhà mà vẫn bị vẽ SAU nhà.")]
        public bool fixSortLines = true;
        public int minSortShiftPx = 2;

        [Header("Bóng đổ dưới chân")]
        public bool shadows = true;
        [Tooltip("Thứ tự vẽ của bóng: trên lớp mặt đất (-5), dưới mọi vật đứng (2).")]
        public int shadowSortingOrder = -4;
        [Range(0f, 1f)] public float shadowOpacity = 0.35f;
        [Tooltip("Bề rộng bóng so với bề rộng phần hình nhân vật/quái.")]
        public float shadowWidthFactor = 0.75f;

        Transform player;
        SpriteRenderer playerSr;
        readonly List<SpriteRenderer> occluders = new List<SpriteRenderer>();
        readonly HashSet<SpriteRenderer> fading = new HashSet<SpriteRenderer>();
        readonly HashSet<SpriteRenderer> covered = new HashSet<SpriteRenderer>();   // đang ở trạng thái "che" (trễ vào/ra)
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
                if (sr.GetComponentInParent<Rigidbody2D>() != null) continue;      // vật di chuyển, đạn...
                // 18/11 (§319): cổng + vòm tường trên cổng (lintel_*) KHÔNG làm mờ: Ren đi qua khung cửa phải khuất sau vòm,
                // làm mờ vòm khiến Ren trông như "trèo lên cổng".
                if (sr.GetComponentInParent<SoulRequiem.Gameplay.GateAnimator>() != null) continue;
                if (IsLintel(sr)) continue;
                FixSortLine(sr);
                if (sr.bounds.size.y < minOccluderHeight) continue;                 // vật thấp không che được người
                occluders.Add(sr); keep.Add(sr);
            }
            // vật đã mờ mà không còn trong danh sách (bị ẩn / xoá) -> trả lại độ đục
            foreach (var sr in new List<SpriteRenderer>(fading))
                if (sr != null && !keep.Contains(sr)) { var c = sr.color; c.a = 1f; sr.color = c; fading.Remove(sr); covered.Remove(sr); }
        }

        /// 18/11: vòm tường trên cổng — tên object Tiled (hoặc tên ảnh) bắt đầu bằng "lintel_".
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
            if (Time.time >= nextRescan) { nextRescan = Time.time + 4f; if (player != null) ScanMap(); }   // vật hiện/ẩn theo cơ quan
            if (player == null || playerSr == null || playerSr.sprite == null) return;

            // Điểm ảnh ĐẶC của thân nhân vật (toạ độ thế giới) — lấy mẫu một lần mỗi khung hình.
            BuildBodyPoints();
            var pb = playerSr.bounds;
            bool log = debugLog && Time.time >= nextLog;
            if (log) nextLog = Time.time + 0.5f;
            float k = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);

            // Lượt 1: vật nào VẼ ĐÈ lên Ren và phủ bao nhiêu điểm thân; gộp HỢP các vật (rừng: nhiều tán, mỗi tán che một ít
            // nhưng cộng lại che kín người -> vẫn phải mờ).
            int n = bodyPts.Count;
            if (unionHit.Length < n) unionHit = new bool[n * 2];
            System.Array.Clear(unionHit, 0, unionHit.Length);
            cov.Clear();
            for (int i = 0; i < occluders.Count && n > 0; i++)
            {
                var sr = occluders[i];
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
                // so chồng khung CHỈ trên mặt phẳng XY (Bounds.Intersects xét cả Z: Ren z=1, vật z=0 -> không bao giờ chạm — lỗi bản đầu)
                var ob = sr.bounds;
                if (!(ob.min.x < pb.max.x && ob.max.x > pb.min.x && ob.min.y < pb.max.y && ob.max.y > pb.min.y)) continue;
                bool over = DrawsOverPlayer(sr);
                int hit = 0;
                if (over)
                    for (int j = 0; j < n; j++)
                        if (Opaque(sr, bodyPts[j])) { hit++; unionHit[j] = true; }
                if (hit > 0) cov[sr] = hit / (float)n;
                if (log)
                    Debug.Log($"[DepthEffects] {sr.name}: vẽ đè={over} che={(hit / (float)n):P0} (điểm thân {n}) " +
                              $"sortY vật={SortY(sr):F2} người={SortY(playerSr):F2}");
            }
            int u = 0;
            for (int j = 0; j < n; j++) if (unionHit[j]) u++;
            float union = n > 0 ? u / (float)n : 0f;
            hidden = union >= (hidden ? exitCoverage : enterCoverage);          // trễ vào/ra
            if (log) Debug.Log($"[DepthEffects] TỔNG bị che {union:P0} -> {(hidden ? "MỜ" : "rõ")}");

            // Lượt 2: khi Ren bị che đủ nhiều, mờ MỌI vật đang góp phần che (>= partCoverage), các vật khác giữ nguyên.
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

        /// Vật có được VẼ SAU (đè lên) nhân vật không — đúng luật xếp lớp của Unity:
        /// sorting layer -> Order in Layer -> trục Custom (0,1,0): điểm xếp thấp hơn (gần camera) vẽ sau.
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
            if (sg != null) return sg.transform.position.y;                        // Sorting Group xếp theo vị trí của chính nó
            return sr.spriteSortPoint == SpriteSortPoint.Pivot ? sr.transform.position.y : sr.bounds.center.y;
        }

        // ------------------------------------------------------------------ đường xếp lớp = chân thật của vật
        /// ST2U: tile object = gốc -> "(Pivot)" -> "(CF)" (lật) -> renderer; renderer xếp theo ĐÁY KHUNG ẢNH.
        /// Đáy khung thường có khoảng trong suốt (cây thông 12 px) hoặc bậc cửa nhô ra (quán trọ: tường 2774, bậc 2806)
        /// -> đứng trước tường mà chân vẫn "cao hơn" đáy khung => bị vẽ SAU nhà, làm mờ bật lên sai.
        /// Sửa không đổi hình: dời "(Pivot)" lên d px, dời "(CF)" xuống d px (hình đứng yên) + gắn SortingGroup vào "(Pivot)"
        /// => cả vật xếp theo vị trí "(Pivot)" = chân thật. Không đổi sprite, không đổi cây object (ẩn/hiện theo tên vẫn đúng).
        void FixSortLine(SpriteRenderer sr)
        {
            if (!fixSortLines || !sortChecked.Add(sr)) return;
            var cf = sr.transform.parent; var pv = cf != null ? cf.parent : null;
            if (pv == null || !cf.name.EndsWith("(CF)") || !pv.name.EndsWith("(Pivot)")) return;
            if (pv.GetComponent<SortingGroup>() != null || pv.GetComponentInParent<SortingGroup>() != null) return;
            if (cf.localScale.y < 0f || Mathf.Abs(pv.parent != null ? pv.parent.eulerAngles.z : 0f) > 0.01f) return;   // lật dọc / xoay: bỏ qua
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
            if (debugLog) Debug.Log($"[DepthEffects] Đường xếp {sp.name}: +{d}px");
        }

        /// Số điểm ảnh từ đáy khung ảnh lên CHÂN THẬT: đáy đặc thấp nhất; với vật rộng (nhà...) nếu >= 50% số cột đặc có đáy
        /// nằm trên cùng một đường (+-3 px) cao hơn đáy thấp nhất >= 4 px (bậc cửa, hiên nhô ra) thì lấy đường đó (chân tường).
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
            int padBottom = Mathf.RoundToInt(sp.textureRectOffset.y);            // phần trong suốt bị cắt khi đóng gói
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
                    if (!SpritePixelOpaque(sp, px, py, true)) continue;          // chỉ điểm thuộc HÌNH nhân vật
                    float lx = (px - sp.pivot.x) / ppu, ly = (py - sp.pivot.y) / ppu;
                    if (playerSr.flipX) lx = -lx;
                    if (playerSr.flipY) ly = -ly;
                    var w = playerSr.transform.TransformPoint(new Vector3(lx, ly, 0f));
                    bodyPts.Add(new Vector3(w.x, w.y, 0f));
                }
        }

        /// Điểm thế giới có rơi vào điểm ảnh ĐẶC của sprite không.
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

        // ------------------------------------------------------------------ kênh alpha (đọc 1 lần / ảnh, kể cả ảnh không bật Read/Write)
        class AlphaMap { public int w, h; public byte[] a; }
        static readonly Dictionary<Texture2D, AlphaMap> alphaCache = new Dictionary<Texture2D, AlphaMap>();

        /// (px,py) = điểm ảnh trong rect của sprite (gốc dưới-trái). Không đọc được ảnh -> trả fallback.
        static bool SpritePixelOpaque(Sprite sp, float px, float py, bool fallback)
        {
            var map = GetAlpha(sp.texture);
            if (map == null) return fallback;
            Rect tr;
            try { tr = sp.textureRect; } catch { return fallback; }          // sprite đóng gói kiểu Tight
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
            catch (System.Exception e) { Debug.LogWarning("[DepthEffects] Không đọc được alpha của " + tex.name + ": " + e.Message); }
            alphaCache[tex] = m;
            return m;
        }

        // ------------------------------------------------------------------ bóng đổ
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

            // bề rộng theo phần hình đục của sprite (không theo khung ảnh trống)
            float w = body.sprite.bounds.size.x;
            var tex = body.sprite.texture;
            if (tex != null && body.sprite.rect.width > 0) w = body.sprite.rect.width / body.sprite.pixelsPerUnit;
            w = Mathf.Clamp(w * shadowWidthFactor, 0.12f, 1.6f);          // đơn vị local của cha
            var ws = GetShadowSprite().bounds.size;                         // kích thước gốc của sprite bóng
            go.transform.localScale = new Vector3(w / ws.x, w * 0.4f / ws.y, 1f);
            go.transform.localPosition = new Vector3(0f, 0.01f, 0f);       // pivot cha = bàn chân
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
                    float a = d >= 1f ? 0f : (d < 0.45f ? 1f : 0.55f);         // 2 nấc kiểu pixel-art
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            shadowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            shadowSprite.name = "BlobShadow";
            return shadowSprite;
        }
    }

    /// Ẩn bóng khi chủ thể đã ẩn/chết (renderer tắt hoặc trong suốt).
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
