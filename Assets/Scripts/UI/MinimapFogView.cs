using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// 05/11: vẽ SƯƠNG MÙ BẢN ĐỒ (dữ liệu: Gameplay/World/MapFog) lên minimap và bản đồ lớn.
    /// Bản 3 (05/11 11h30, người dùng: "mây dày đặc quá, muốn tự nhiên như ảnh mẫu"): mỗi vùng chưa mở = một CỤM vài đám mây lớn.
    ///   - Màn tối mỏng (35% màu mây, alpha ~0,6) phủ mọi ô chưa mở -> khe giữa các cụm thấy map mờ tối.
    ///   - Mây: "búi mây" PixelLab (Resources/UI/Requiem/fogpuff_0..N, phải bật Read/Write). Mỗi vùng: bán kính R = căn(số ô)/3,2
    ///     (3,2–7,5 ô); đặt mây lớn cách nhau ~R, lùi khỏi ranh vùng khác (khe giữa cụm); lấp chỗ trống bằng mây nhỏ hơn, kể cả mép giáp
    ///     vùng đã mở (viền tròn). Vẽ từ trên xuống (dưới đè trên) + bóng đổ; mỗi cụm tông sáng ±8%; ngẫu nhiên theo id vùng (hình cố định).
    ///   - Tất cả dồn vào MỘT ảnh (4 px mỗi ô, lọc Bilinear), chỉ vẽ lại khi MapFog.Version đổi. Mở vùng: ảnh cũ mờ dần 0,8 s + vài búi
    ///     mây bay toả ra rồi tan ở vùng vừa mở.
    ///   - "??" ở tâm vùng chưa mở (vùng bí mật thì không); bản đồ lớn hiện tên vùng đã mở.
    /// Không phải MonoBehaviour — RequiemHUD tạo hai bản (minimap, bản đồ lớn) và gọi Update mỗi khung.
    /// </summary>
    internal sealed class MinimapFogView
    {
        private const int FP = 4;                                            // px ảnh sương mỗi ô (lọc Bilinear khi phóng)
        private const float FadeSeconds = 0.8f, DriftSeconds = 1.0f;
        private const float RadiusMin = 2.4f, RadiusMax = 3.4f;               // búi bay toả khi mở vùng

        // ---- ảnh sương dùng chung
        private static MapFog src;
        private static int srcVersion = -1;
        private static Texture2D cur, prev;
        private static float changedAt;
        private static readonly List<Puff> puffSprites = new List<Puff>();
        private static Color32 veil, veilEdge;
        private static bool spritesTried;

        private sealed class Puff { public Color32[] px; public int w, h; public Texture2D tex; }
        private struct Stamp { public float x, y, r, tint; public int v; }
        private sealed class Drift { public VisualElement e; public Vector2 from, dir; public float r, t0; }

        private readonly VisualElement host, layerCur, layerPrev, driftLayer, labelLayer;
        private readonly List<Label> qPool = new List<Label>(), namePool = new List<Label>();
        private readonly List<Drift> drifts = new List<Drift>();
        private readonly string qClass, nameClass;
        private readonly bool showNames;
        private readonly float qW, qH, nameW, nameH;
        private MapFog seenFog;
        private bool[] seenRevealed;

        public MinimapFogView(VisualElement parent, int insertIndex, string qClass, float qW, float qH,
                              bool showNames = false, string nameClass = "", float nameW = 0f, float nameH = 0f)
        {
            this.qClass = qClass; this.qW = qW; this.qH = qH;
            this.showNames = showNames; this.nameClass = nameClass; this.nameW = nameW; this.nameH = nameH;
            host = new VisualElement { pickingMode = PickingMode.Ignore };
            host.style.position = Position.Absolute;
            parent.Insert(Mathf.Clamp(insertIndex, 0, parent.childCount), host);
            layerCur = Layer(host);
            layerPrev = Layer(host);                                   // ảnh cũ nằm TRÊN, mờ dần
            driftLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            driftLayer.style.position = Position.Absolute; driftLayer.style.left = 0; driftLayer.style.top = 0;
            host.Add(driftLayer);
            labelLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            labelLayer.style.position = Position.Absolute; labelLayer.style.left = 0; labelLayer.style.top = 0;
            host.Add(labelLayer);
        }

        /// <summary>Vẽ sương. <paramref name="origin"/> = vị trí ô (0,0) trong phần tử cha, <paramref name="k"/> = px mỗi ô,
        /// <paramref name="visible"/> = khung nhìn (toạ độ phần tử cha) để bỏ nhãn nằm ngoài. <paramref name="ppt"/> không còn dùng (giữ chữ ký).</summary>
        public void Update(MapFog fog, int ppt, Vector2 origin, float k, Rect visible, bool show = true)
        {
            if (fog == null || !show) { host.style.display = DisplayStyle.None; return; }
            host.style.display = DisplayStyle.Flex;
            Sync(fog);
            DetectReveals(fog);
            host.style.left = origin.x; host.style.top = origin.y;
            host.style.width = fog.Width * k; host.style.height = fog.Height * k;
            if (layerCur.style.backgroundImage.value.texture != cur) layerCur.style.backgroundImage = new StyleBackground(cur);
            float a = prev != null ? Mathf.Clamp01((Time.unscaledTime - changedAt) / FadeSeconds) : 1f;
            if (prev != null && a < 1f)
            {
                if (layerPrev.style.backgroundImage.value.texture != prev) layerPrev.style.backgroundImage = new StyleBackground(prev);
                layerPrev.style.display = DisplayStyle.Flex;
                layerPrev.style.opacity = 1f - a;
            }
            else layerPrev.style.display = DisplayStyle.None;
            UpdateDrifts(k);

            int nq = 0, nn = 0;
            foreach (var r in fog.Regions)
            {
                Vector2 p = origin + r.center * k;
                if (!visible.Contains(p)) continue;
                if (!r.revealed && !r.secret)
                {
                    var l = Pooled(qPool, nq++, "??", qClass);
                    l.style.left = r.center.x * k - qW / 2f; l.style.top = r.center.y * k - qH / 2f;
                }
                else if (showNames && r.revealed && !string.IsNullOrEmpty(r.title))
                {
                    var l = Pooled(namePool, nn++, r.title, nameClass);
                    l.style.left = r.center.x * k - nameW / 2f; l.style.top = r.center.y * k - nameH / 2f;
                }
            }
            for (int i = nq; i < qPool.Count; i++) qPool[i].style.display = DisplayStyle.None;
            for (int i = nn; i < namePool.Count; i++) namePool[i].style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ búi mây bay toả khi mở vùng
        private void DetectReveals(MapFog fog)
        {
            var regs = fog.Regions;
            if (seenFog != fog || seenRevealed == null || seenRevealed.Length != regs.Count)
            {
                seenFog = fog;
                seenRevealed = new bool[regs.Count];
                for (int i = 0; i < regs.Count; i++) seenRevealed[i] = regs[i].revealed;
                return;                                                     // lần đầu: không bay (đang nạp map)
            }
            for (int i = 0; i < regs.Count; i++)
            {
                if (regs[i].revealed && !seenRevealed[i]) SpawnDrifts(regs[i], i);
                seenRevealed[i] = regs[i].revealed;
            }
        }

        private void SpawnDrifts(MapFog.Region r, int seed)
        {
            if (puffSprites.Count == 0) return;
            var rng = new System.Random(seed * 7919 + 17);
            foreach (var a in r.Areas)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(a.width * a.height / 22f), 4, 16);
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector2(a.x + (float)rng.NextDouble() * a.width, a.y + (float)rng.NextDouble() * a.height);
                    var d = p - r.center; if (d.sqrMagnitude < 0.01f) d = Vector2.up;
                    var e = new VisualElement { pickingMode = PickingMode.Ignore };
                    e.style.position = Position.Absolute;
                    e.style.backgroundImage = new StyleBackground(puffSprites[rng.Next(puffSprites.Count)].tex);
                    driftLayer.Add(e);
                    drifts.Add(new Drift { e = e, from = p, dir = d.normalized, r = RadiusMin + (float)rng.NextDouble() * (RadiusMax - RadiusMin), t0 = Time.unscaledTime });
                }
            }
        }

        private void UpdateDrifts(float k)
        {
            for (int i = drifts.Count - 1; i >= 0; i--)
            {
                var d = drifts[i];
                float t = (Time.unscaledTime - d.t0) / DriftSeconds;
                if (t >= 1f) { d.e.RemoveFromHierarchy(); drifts.RemoveAt(i); continue; }
                float ease = 1f - (1f - t) * (1f - t);
                Vector2 p = d.from + d.dir * (2.2f * ease);
                float r = d.r * (1f + 0.35f * ease);
                d.e.style.left = (p.x - r) * k; d.e.style.top = (p.y - r) * k;
                d.e.style.width = 2f * r * k; d.e.style.height = 2f * r * k;
                d.e.style.opacity = 1f - t;
            }
        }

        private Label Pooled(List<Label> pool, int i, string text, string cls)
        {
            Label l;
            if (i < pool.Count) l = pool[i];
            else
            {
                l = RequiemUI.Text(text, cls, labelLayer);
                l.style.position = Position.Absolute;
                bool q = pool == qPool;
                l.style.width = q ? qW : nameW; l.style.height = q ? qH : nameH;
                pool.Add(l);
            }
            string t = RequiemUI.Clean(text);
            if (l.text != t) l.text = t;
            l.style.display = DisplayStyle.Flex;
            return l;
        }

        private static VisualElement Layer(VisualElement parent)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = 0; v.style.top = 0; v.style.right = 0; v.style.bottom = 0;
            parent.Add(v);
            return v;
        }

        // ------------------------------------------------------------------ ảnh sương
        private static void Sync(MapFog fog)
        {
            if (fog == src && fog.Version == srcVersion && cur != null) return;
            bool same = fog == src && cur != null;
            if (prev != null) Object.Destroy(prev);
            prev = null;
            if (same) prev = cur;
            else if (cur != null) Object.Destroy(cur);
            cur = Build(fog);
            src = fog; srcVersion = fog.Version;
            changedAt = Time.unscaledTime;
        }

        private static Texture2D Build(MapFog fog)
        {
            LoadPuffs();
            int mw = fog.Width, mh = fog.Height, W = mw * FP, H = mh * FP;
            var px = new Color32[W * H];                                     // hàng 0 = TRÊN (đảo khi đưa vào Texture2D)
            var regs = fog.Regions;

            var dEdge = Distance(fog, 0);                                    // tới ô không thuộc (vùng này & còn sương)
            var dSeam = Distance(fog, 2);                                    // tới vùng chưa mở KHÁC

            // 1. màn tối mỏng phủ mọi ô chưa mở: khe giữa các cụm mây vẫn thấy map mờ tối (như ảnh mẫu), không lộ rõ
            for (int ty = 0; ty < mh; ty++)
                for (int tx = 0; tx < mw; tx++)
                {
                    if (!Fogged(fog, tx, ty)) continue;
                    var c = dEdge[ty * mw + tx] < 2 ? veilEdge : veil;
                    for (int y = ty * FP; y < ty * FP + FP; y++)
                        for (int x = tx * FP; x < tx * FP + FP; x++) px[y * W + x] = c;
                }

            // 2. mỗi vùng chưa mở = một cụm vài đám mây LỚN (bán kính R theo diện tích vùng), đặt cách nhau, lùi khỏi ranh vùng khác
            //    -> giữa các cụm có khe; rồi lấp chỗ trống trong vùng bằng mây nhỏ hơn (không lấp sát ranh vùng khác).
            var stamps = new List<Stamp>();
            if (puffSprites.Count > 0)
            {
                var tiles = new List<int>[regs.Count];
                for (int i = 0; i < regs.Count; i++) tiles[i] = new List<int>();
                for (int ty = 0; ty < mh; ty++)
                    for (int tx = 0; tx < mw; tx++)
                    {
                        int ri = fog.RegionAt(tx, ty);
                        if (ri >= 0 && Fogged(fog, tx, ty)) tiles[ri].Add(ty * mw + tx);
                    }
                var acc = new List<Stamp>();
                for (int ri = 0; ri < regs.Count; ri++)
                {
                    var list = tiles[ri];
                    if (regs[ri].revealed || list.Count == 0) continue;
                    var rng = new System.Random(Hash(regs[ri].key));
                    float tint = 0.92f + (float)rng.NextDouble() * 0.16f;
                    for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = list[i]; list[i] = list[j]; list[j] = t; }
                    float R = Mathf.Clamp(Mathf.Sqrt(list.Count) / 3.2f, 3.2f, 7.5f);
                    acc.Clear();
                    // mây lớn
                    foreach (int i in list)
                    {
                        if (dEdge[i] < R * 0.45f || dSeam[i] < R * 0.55f) continue;
                        float x = i % mw + 0.5f, y = i / mw + 0.5f;
                        if (Near(acc, x, y, R * 1.05f, 0f)) continue;
                        float r = Mathf.Min(R * (0.85f + (float)rng.NextDouble() * 0.3f), dEdge[i] + 1.6f);
                        acc.Add(new Stamp { x = x, y = y, r = r, v = rng.Next(puffSprites.Count), tint = tint });
                    }
                    // lấp chỗ trống (kể cả mép giáp vùng đã mở -> viền tròn)
                    foreach (int i in list)
                    {
                        if (dEdge[i] < 1 || dSeam[i] < 2) continue;
                        float x = i % mw + 0.5f, y = i / mw + 0.5f;
                        if (Near(acc, x, y, 0f, 0.8f)) continue;
                        float r = Mathf.Max(2.4f, Mathf.Min(R * 0.6f, dEdge[i] + 1.4f));
                        acc.Add(new Stamp { x = x, y = y, r = r, v = rng.Next(puffSprites.Count), tint = tint });
                    }
                    stamps.AddRange(acc);
                }
                stamps.Sort((a, b) => a.y.CompareTo(b.y));                   // trên trước, dưới đè lên
                foreach (var st in stamps) Blit(px, W, H, st, true);         // bóng đổ
                foreach (var st in stamps) Blit(px, W, H, st, false);        // mây
            }

            var flipped = new Color32[W * H];
            for (int y = 0; y < H; y++) System.Array.Copy(px, y * W, flipped, (H - 1 - y) * W, W);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "MapFog" };
            tex.SetPixels32(flipped);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Có đám mây nào đã đặt gần (x, y): cách tâm &lt; minDist, hoặc &lt; bán kính x cover.</summary>
        private static bool Near(List<Stamp> acc, float x, float y, float minDist, float cover)
        {
            foreach (var a in acc)
            {
                float d2 = (a.x - x) * (a.x - x) + (a.y - y) * (a.y - y), lim = Mathf.Max(minDist, a.r * cover);
                if (d2 < lim * lim) return true;
            }
            return false;
        }

        /// <summary>Khoảng cách (bước ô, 4 hướng) trong cùng vùng chưa mở tới: mode 1 = ô đã mở; 2 = ô của vùng chưa mở KHÁC;
        /// 0 = một trong hai. Không tới được = 99.</summary>
        private static int[] Distance(MapFog fog, int mode)
        {
            int mw = fog.Width, mh = fog.Height;
            var d = new int[mw * mh];
            var q = new Queue<int>();
            for (int ty = 0; ty < mh; ty++)
                for (int tx = 0; tx < mw; tx++)
                {
                    int i = ty * mw + tx;
                    d[i] = 99;
                    if (!Fogged(fog, tx, ty)) continue;
                    int me = fog.RegionAt(tx, ty);
                    bool src = false;
                    for (int k = 0; k < 4 && !src; k++)
                    {
                        int nx = tx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = ty + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= mw || ny >= mh) continue;
                        bool nf = Fogged(fog, nx, ny);
                        src = mode == 1 ? !nf : mode == 2 ? nf && fog.RegionAt(nx, ny) != me : !nf || fog.RegionAt(nx, ny) != me;
                    }
                    if (src) { d[i] = 0; q.Enqueue(i); }
                }
            while (q.Count > 0)
            {
                int i = q.Dequeue(), tx = i % mw, ty = i / mw, me = fog.RegionAt(tx, ty);
                for (int k = 0; k < 4; k++)
                {
                    int nx = tx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = ty + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= mw || ny >= mh) continue;
                    int j = ny * mw + nx;
                    if (d[j] <= d[i] + 1 || !Fogged(fog, nx, ny) || fog.RegionAt(nx, ny) != me) continue;
                    d[j] = d[i] + 1;
                    q.Enqueue(j);
                }
            }
            return d;
        }

        private static bool Fogged(MapFog fog, int x, int y)
        {
            if (x < 0 || y < 0 || x >= fog.Width || y >= fog.Height) return true;   // ngoài map coi như sương (mép map không lộ)
            return fog.ClearAt(x, y) < 0.5f;
        }

        /// <summary>Dán một búi mây (lấy mẫu gần nhất, alpha-over). shadow = bóng đổ: lệch xuống dưới, màu tối, nửa trong suốt.</summary>
        private static void Blit(Color32[] px, int W, int H, Stamp s, bool shadow)
        {
            var sp = puffSprites[s.v];
            float r = s.r * FP;
            float cx = s.x * FP + (shadow ? r * 0.10f : 0f), cy = s.y * FP + (shadow ? r * 0.22f : 0f);
            int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r), y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
            float inv = 1f / (2f * r);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(H, y1); y++)
            {
                int sy = sp.h - 1 - Mathf.Clamp((int)((y + 0.5f - (cy - r)) * inv * sp.h), 0, sp.h - 1);   // ảnh Texture2D: hàng 0 ở dưới
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x1); x++)
                {
                    int sx = Mathf.Clamp((int)((x + 0.5f - (cx - r)) * inv * sp.w), 0, sp.w - 1);
                    var c = sp.px[sy * sp.w + sx];
                    if (c.a < 8) continue;
                    int i = y * W + x;
                    var d = px[i];
                    if (shadow)
                    {
                        int a = c.a * 100 / 255;
                        px[i] = Over(new Color32(10, 6, 18, 255), a, d);
                    }
                    else
                    {
                        var t = new Color32((byte)Mathf.Min(255, c.r * s.tint), (byte)Mathf.Min(255, c.g * s.tint), (byte)Mathf.Min(255, c.b * s.tint), c.a);
                        px[i] = Over(t, c.a, d);
                    }
                }
            }
        }

        private static Color32 Over(Color32 s, int a, Color32 d)
        {
            int ia = 255 - a;
            int outA = a + d.a * ia / 255;
            if (outA <= 0) return new Color32(0, 0, 0, 0);
            // trộn màu theo alpha (đích có thể trong suốt)
            int r = (s.r * a + d.r * d.a * ia / 255) / outA, g = (s.g * a + d.g * d.a * ia / 255) / outA, b = (s.b * a + d.b * d.a * ia / 255) / outA;
            return new Color32((byte)Mathf.Min(255, r), (byte)Mathf.Min(255, g), (byte)Mathf.Min(255, b), (byte)outA);
        }

        private static int Hash(string s)
        {
            unchecked { int h = 23; foreach (char ch in s ?? "") h = h * 31 + ch; return h; }
        }

        /// <summary>Búi mây PixelLab (Resources/UI/Requiem/fogpuff_0..N, Read/Write bật). Thiếu -> búi tròn vẽ bằng code.</summary>
        private static void LoadPuffs()
        {
            if (spritesTried) return;
            spritesTried = true;
            for (int i = 0; i < 16; i++)
            {
                var t = Resources.Load<Texture2D>("UI/Requiem/fogpuff_" + i);
                if (t == null) break;
                if (!t.isReadable) { Debug.LogWarning("[MinimapFog] fogpuff_" + i + " chưa bật Read/Write."); continue; }
                puffSprites.Add(new Puff { px = t.GetPixels32(), w = t.width, h = t.height, tex = t });
            }
            if (puffSprites.Count == 0) puffSprites.Add(MakePuff());
            // màn tối = trung bình phần đục của các búi mây, tối đi còn 35%
            long rs = 0, gs = 0, bs = 0, n = 0;
            foreach (var p in puffSprites) foreach (var c in p.px) if (c.a > 200) { rs += c.r; gs += c.g; bs += c.b; n++; }
            var bc = n > 0 ? new Color32((byte)(rs / n * 35 / 100), (byte)(gs / n * 35 / 100), (byte)(bs / n * 35 / 100), 255) : new Color32(24, 20, 36, 255);
            veil = new Color32(bc.r, bc.g, bc.b, 175); veilEdge = new Color32(bc.r, bc.g, bc.b, 150);
        }

        private static Puff MakePuff()
        {
            const int n = 48;
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) continue;
                    float lit = Mathf.Clamp01(0.55f + dy * 0.45f);                 // hàng trên (y lớn) sáng hơn
                    px[y * n + x] = new Color32((byte)(52 + 70 * lit), (byte)(44 + 60 * lit), (byte)(76 + 80 * lit), (byte)(d > 0.9f ? 160 : 255));
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(px); tex.Apply();
            return new Puff { px = px, w = n, h = n, tex = tex };
        }
    }
}
