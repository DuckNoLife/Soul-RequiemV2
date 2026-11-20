using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// breakable — vật cản gỗ: chém `hits` lần (mặc định 3) -> cờ `sets` (mặc định &lt;tên&gt;_broken) + chữ `text`.
    /// Cổng cùng tên trong layer gates rung mỗi nhát và mở theo cờ. `hint` hiện khi lại gần lần đầu.
    /// 06/11 (người dùng: "rào chắn bí mật đang dùng tile cây cầu, nhìn không ra là cửa / vật chặn"): hình riêng, dùng chung mọi map:
    ///   `sprite`        = ảnh Resources của vật còn nguyên (vd VFX/props/barricade_crates_0); nhiều ảnh cách nhau dấu phẩy thì
    ///                     các bản lần lượt lấy xoay vòng (đỡ lặp hình). Chân ảnh ở mép dưới, sắp theo y cùng lớp nhân vật.
    ///   `sprite_broken` = ảnh mảnh vụn sau khi vỡ (cũng nhận danh sách), nằm lại trên sàn dưới nhân vật. Trống = biến mất.
    ///   `sprite_count`  = số bản ảnh rải theo cạnh dài của vùng (vùng dọc: xếp từ dưới lên; vùng ngang: rải trái -> phải); mặc định 1.
    ///   `sprite_width`  = bề rộng mỗi bản (đơn vị Unity); mặc định vùng dọc = bề rộng vùng ×1.1, vùng ngang = bề rộng / số bản.
    ///   `broken_scale`  = mảnh vụn rộng bằng bao nhiêu phần bản nguyên (mặc định 0.85).
    ///   06/11 (b): `sprite_damaged` = các ảnh hư hại dần (nhẹ -> nặng, cách nhau dấu phẩy): mỗi nhát chém đổi sang ảnh nặng hơn,
    ///                     nhát cuối mới vỡ. Vd rào gỗ Old Prison: fence_h_1,fence_h_2,fence_h_3 với hits = 4.
    ///              `debris` = ảnh ván vụn (danh sách) rải trên sàn khi vỡ thay cho `sprite_broken`; `debris_count` (mặc định 10),
    ///                     `debris_scale` (mặc định 1 = đúng cỡ pixel gốc). Vị trí rải cố định theo tên vật (lần nạp sau y hệt).
    /// 06/11 (c) CÁCH ĐẶT MẶC ĐỊNH: vùng chém = hình chữ nhật trong `mechanics` (như cũ); MỖI MẢNH hình = một tile object trong lớp
    ///   `mechanics_art` (tileset SR-mech-art) có `art_for` = tên vật này -> thấy và kéo thả từng mảnh trong Tiled. Khi có mảnh thì
    ///   bỏ qua `sprite` / `sprite_count` / `sprite_width`; `sprite_damaged`, `debris`, `sprite_broken` vẫn ghi trên vật như cũ.
    /// Mỗi nhát: vật rung + dăm gỗ văng; vỡ: bụi + dăm gỗ bung ra, đổi sang ảnh mảnh vụn. Bản lưu đã vỡ -> hiện mảnh vụn ngay.
    /// </summary>
    public sealed class BreakableMechanism : Mechanism
    {
        private int hits;
        private bool announced;
        private readonly List<SpriteRenderer> pieces = new List<SpriteRenderer>();
        private readonly List<float> widths = new List<float>();
        private Sprite[] whole, broken, damaged, debris;
        private int totalHits;
        private static Sprite chip;

        public override bool ReactsToStrike => true;

        public override void Init()
        {
            hits = totalHits = Mathf.Max(1, Int("hits", 3));
            whole = LoadAll(Str("sprite", ""));
            broken = LoadAll(Str("sprite_broken", ""));
            damaged = LoadAll(Str("sprite_damaged", ""));
            debris = LoadAll(Str("debris", ""));
            if (Art.Count > 0) BuildFromArt();
            else if (whole.Length > 0) Build();
            if (Ctx.Flags.Has(Str("sets", Name + "_broken"))) { Done = true; ShowBroken(); }
        }

        public override void OnFlagsChanged()
        {
            if (Done || !Ctx.Flags.Has(Str("sets", Name + "_broken"))) return;   // F5 / cơ quan khác bật cờ
            Done = true;
            ShowBroken();
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (announced || Done || Vector2.Distance(playerPos, Rect.center) >= 1.0f) return;
            announced = true;
            Hints.Show(Str("hint", "Rotten wood. A few strikes would break it."), 2.5f);
        }

        public override void OnStruck()
        {
            if (Done) return;
            // 17/11 (§304): `requires` = cờ cần có thì mới chém được (vd xích tời Map 4 cần katana); chưa đủ -> `locked_text`
            if (!Ctx.Flags.HasAll(Str("requires", "")))
            {
                foreach (var p in pieces) if (p != null) Ctx.Run(Jiggle(p.transform, 0.1f, 0.015f));
                Hints.Show(Str("locked_text", "It will not break. Not like this."), 2.2f);
                return;
            }
            // 16/11 (§255): `min_radius` > 0 = chỉ đòn nặng phá được (Earthshatter ~1,1; nhát kiếm ~0,45). Đòn nhẹ: rung nhẹ + `weak_hint`.
            float minR = Float("min_radius", 0f);
            if (minR > 0f && MapMechanics.StrikeRadius < minR)
            {
                foreach (var p in pieces) if (p != null) Ctx.Run(Jiggle(p.transform, 0.1f, 0.015f));
                Hints.Show(Str("weak_hint", "Your blade only scratches it. It needs a heavier blow."), 2.2f);
                return;
            }
            hits--;
            Ctx.Run(Ctx.ShakeGate(Name));
            foreach (var p in pieces) if (p != null) Ctx.Run(Jiggle(p.transform, 0.18f, 0.035f));
            Ctx.Run(Chips(6));
            string what = (Name + " " + Str("sprite", "")).ToLowerInvariant();
            bool stoneLike = minR > 0f || what.Contains("rubble") || what.Contains("stone") || what.Contains("rock") || what.Contains("slab"); // 19/11 (§335): âm
            if (hits > 0) { ShowDamage(); if (!stoneLike) SfxHooks.MechanismEvent(this, "hit", "hit_wood"); return; }
            Done = true;
            SfxHooks.MechanismEvent(this, "break", stoneLike ? "break_stone" : "break_wood");
            Hints.Show(Str("text", "It breaks apart."), 2.5f);
            Ctx.Flags.Set(Str("sets", Name + "_broken"));
            var psr = PlayerSprite();
            if (psr != null) CutsceneFx.Burst(Ctx.Host, new Vector3(Rect.center.x, Rect.center.y, 0f), Rect.width * 0.4f, Rect.height * 0.3f, 5, 0, psr.sortingLayerID, psr.sortingOrder);
            Ctx.Run(Chips(16));
            CameraFollow.KickMain(Vector2.up, 0.05f, 0.18f);
            ShowBroken();
        }

        // ------------------------------------------------------------------ hình
        private static Sprite[] LoadAll(string paths)
        {
            var list = new List<Sprite>();
            foreach (var raw in paths.Split(','))
            {
                string path = raw.Trim();
                if (path.Length == 0) continue;
                var sp = Resources.Load<Sprite>(path);
                if (sp != null) list.Add(sp);
                else Debug.LogWarning("[Breakable] Không thấy sprite: " + path);
            }
            return list.ToArray();
        }

        private SpriteRenderer PlayerSprite() =>
            Ctx.Director != null && Ctx.Director.Player != null ? Ctx.Director.Player.GetComponentInChildren<SpriteRenderer>() : null;

        private void Build()
        {
            int n = Mathf.Max(1, Int("sprite_count", 1));
            bool vertical = Rect.height > Rect.width * 1.2f;
            float w = Float("sprite_width", vertical ? Rect.width * 1.1f : Rect.width / n);
            var psr = PlayerSprite();
            for (int i = 0; i < n; i++)
            {
                var sr = new GameObject("Breakable " + Name + " " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(Ctx.Host.transform, true);
                var spr = whole[i % whole.Length];
                sr.sprite = spr;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder; sr.sharedMaterial = psr.sharedMaterial; }
                float s = w / Mathf.Max(0.01f, spr.bounds.size.x);
                sr.transform.localScale = new Vector3(s, s, 1f);
                // chân ảnh: vùng ngang -> rải đều trên mép dưới; vùng dọc -> chia đều theo chiều cao (bản dưới cùng ở mép dưới)
                float x = vertical ? Rect.center.x : Rect.xMin + Rect.width * (i + 0.5f) / n;
                float y = (vertical ? Rect.yMin + Rect.height * i / n : Rect.yMin) - spr.bounds.min.y * s;
                sr.transform.position = new Vector3(x, y, 0f);
                pieces.Add(sr);
                widths.Add(w);
            }
        }

        /// <summary>06/11: mỗi mảnh hình = một tile object `art_for` (vẽ khít khung như trong Tiled).</summary>
        private void BuildFromArt()
        {
            var psr = PlayerSprite();
            for (int i = 0; i < Art.Count; i++)
            {
                var a = Art[i];
                var spr = a.Resolve("sprite");
                if (spr == null) continue;
                var sr = new GameObject("Breakable " + Name + " " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(Ctx.Host.transform, true);
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder; sr.sharedMaterial = psr.sharedMaterial; }
                a.Apply(sr, spr);
                pieces.Add(sr);
                widths.Add(a.Box.width);
            }
        }

        /// <summary>Đổi sang ảnh hư hại ứng với số nhát đã nhận (nhát cuối dành cho lúc vỡ).</summary>
        private void ShowDamage()
        {
            if (damaged.Length == 0 || pieces.Count == 0) return;
            int taken = totalHits - hits;
            int idx = taken * damaged.Length / Mathf.Max(1, totalHits - 1) - 1;
            if (idx < 0) return;
            var spr = damaged[Mathf.Min(idx, damaged.Length - 1)];
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p == null || p.sprite == spr) continue;
                float s = widths[i] / Mathf.Max(0.01f, spr.bounds.size.x);
                float footY = p.transform.position.y + p.sprite.bounds.min.y * p.transform.localScale.y;
                p.sprite = spr;
                p.transform.localScale = new Vector3(s, s, 1f);
                p.transform.position = new Vector3(p.transform.position.x, footY - spr.bounds.min.y * s, 0f);
            }
        }

        /// <summary>Ván vụn rải trên sàn (dưới nhân vật); vị trí cố định theo tên để lần nạp sau giống hệt.</summary>
        private void ScatterDebris()
        {
            var psr = PlayerSprite();
            var rng = new System.Random(Name.GetHashCode());
            int n = Mathf.Max(1, Int("debris_count", 10));
            float k = Float("debris_scale", 1f);
            for (int i = 0; i < n; i++)
            {
                var sr = new GameObject("Debris " + Name + " " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(Ctx.Host.transform, true);
                sr.sprite = debris[rng.Next(debris.Length)];
                sr.flipX = rng.Next(2) == 0;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder - 6; sr.sharedMaterial = psr.sharedMaterial; }
                float x = Rect.xMin + (float)rng.NextDouble() * Rect.width;
                float y = Rect.yMin - 0.06f + (float)rng.NextDouble() * (Rect.height > Rect.width * 1.2f ? Rect.height : Mathf.Min(Rect.height, 0.5f));   // vùng dọc: rải hết chiều cao
                sr.transform.position = new Vector3(x, y, 0f);
                sr.transform.localScale = new Vector3(k, k, 1f);
            }
        }

        private void ShowBroken()
        {
            if (debris.Length > 0)
            {
                foreach (var p in pieces) if (p != null) p.gameObject.SetActive(false);
                ScatterDebris();
                return;
            }
            float k = Float("broken_scale", 0.85f);
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p == null) continue;
                if (broken.Length == 0) { p.gameObject.SetActive(false); continue; }
                var b = broken[i % broken.Length];
                float s = widths[i] * k / Mathf.Max(0.01f, b.bounds.size.x);
                p.sprite = b;
                p.transform.localScale = new Vector3(s, s, 1f);
                p.sortingOrder -= 6;                                             // mảnh vụn nằm dưới nhân vật
            }
        }

        private static IEnumerator Jiggle(Transform t, float seconds, float amp)
        {
            if (t == null) yield break;
            Vector3 p0 = t.position;
            for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
            {
                t.position = p0 + new Vector3(Mathf.Sin(e * 90f) * amp * (1f - e / seconds), 0f, 0f);
                yield return null;
            }
            if (t != null) t.position = p0;
        }

        /// <summary>Dăm gỗ: mẩu nâu nhỏ văng theo vòng cung rồi nằm lại mờ dần.</summary>
        private IEnumerator Chips(int n)
        {
            if (chip == null)
            {
                var tex = new Texture2D(3, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var c = new Color32(122, 86, 52, 255); var d = new Color32(84, 56, 34, 255);
                tex.SetPixels32(new[] { c, c, d, d, c, c }); tex.Apply();
                chip = Sprite.Create(tex, new Rect(0, 0, 3, 2), new Vector2(0.5f, 0.5f), 100f);
            }
            var psr = PlayerSprite();
            var list = new List<(SpriteRenderer sr, Vector2 from, Vector2 to, float arc)>();
            for (int i = 0; i < n; i++)
            {
                var sr = new GameObject("Wood Chip").AddComponent<SpriteRenderer>();
                sr.sprite = chip;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder + 1; }
                Vector2 from = new Vector2(Random.Range(Rect.xMin, Rect.xMax), Random.Range(Rect.yMin, Rect.yMax));
                Vector2 to = from + Random.insideUnitCircle * 0.5f + Vector2.down * 0.15f;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                list.Add((sr, from, to, Random.Range(0.15f, 0.35f)));
            }
            const float dur = 0.35f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                foreach (var c in list)
                    if (c.sr != null) c.sr.transform.position = Vector2.Lerp(c.from, c.to, u) + Vector2.up * (c.arc * 4f * u * (1f - u));
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                foreach (var c in list) if (c.sr != null) c.sr.color = new Color(1f, 1f, 1f, 1f - t / 0.5f);
                yield return null;
            }
            foreach (var c in list) if (c.sr != null) Object.Destroy(c.sr.gameObject);
        }
    }
}
