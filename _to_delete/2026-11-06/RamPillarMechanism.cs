using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 06/11: ram_pillar — cột chống đá trong đấu trường của boss lao húc (Ether Ram). Người chơi dụ boss đâm vào cột:
    /// boss choáng lâu hơn đâm tường (RamSkills), cột nứt; đủ `hits` lần (mặc định 2) thì SỤP thành đống đá thấp (đi qua được),
    /// đá trần rơi quanh đó. Đấu trường đổi dần theo trận đánh — người chơi phải tính cột nào dùng trước.
    /// Thuộc tính Tiled (object vùng chân cột, lớp mechanics):
    ///   `sprite` (còn nguyên), `sprite_cracked` (đã nứt), `sprite_broken` (đống đổ nát) — ảnh Resources, chân ảnh ở mép dưới vùng;
    ///   `sprite_width` = bề rộng ảnh (đv, mặc định 0,7); `collider` = "rộng,cao" chân chặn (đv, mặc định 0,46,0,2); `hits`; `sets`.
    /// Trạng thái lưu bằng cờ &lt;tên&gt;_cracked / &lt;tên&gt;_collapsed (khu làm lại khi chết KHÔNG dựng lại cột: đổ là đổ).
    /// </summary>
    public sealed class RamPillarMechanism : Mechanism
    {
        private SpriteRenderer body;
        private BoxCollider2D col;
        private Sprite whole, cracked, broken;
        private int hits, left;
        private float scale = 1f;

        public Vector2 Foot => new Vector2(Rect.center.x, Rect.yMin);
        public bool Collapsed => left <= 0;

        public override void Init()
        {
            hits = Mathf.Max(1, Int("hits", 2));
            whole = Load(Str("sprite", ""));
            cracked = Load(Str("sprite_cracked", ""));
            broken = Load(Str("sprite_broken", ""));
            left = hits;
            if (Ctx.Flags.Has(Name + "_collapsed")) left = 0;
            else if (Ctx.Flags.Has(Name + "_cracked")) left = Mathf.Max(1, hits - 1);

            var psr = Ctx.Director != null && Ctx.Director.Player != null ? Ctx.Director.Player.GetComponentInChildren<SpriteRenderer>() : null;
            var go = new GameObject("RamPillar " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = new Vector3(Foot.x, Foot.y, 0f);
            body = go.AddComponent<SpriteRenderer>();
            body.spriteSortPoint = SpriteSortPoint.Pivot;
            if (psr != null) { body.sortingLayerID = psr.sortingLayerID; body.sortingOrder = psr.sortingOrder; body.sharedMaterial = psr.sharedMaterial; }
            var wh = Str("collider", "0.46,0.2").Split(',');
            float cw = 0.46f, ch = 0.2f;
            if (wh.Length == 2)
            {
                float.TryParse(wh[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cw);
                float.TryParse(wh[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ch);
            }
            col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cw, ch);
            col.offset = new Vector2(0f, ch * 0.5f);
            Refresh();
        }

        public override void OnFlagsChanged()
        {
            if (left > 0 && Ctx.Flags.Has(Name + "_collapsed")) { left = 0; Refresh(); }
        }

        /// <summary>Boss đâm vào khoảng `r` quanh chân cột: 0 = không trúng, 1 = trúng (nứt), 2 = cột sụp.</summary>
        public int TryRamHit(Vector2 at, float r)
        {
            if (Collapsed || body == null) return 0;
            if ((at - Foot).magnitude > r + 0.35f) return 0;
            left--;
            Ctx.Run(Shake());
            var psr = body;
            CutsceneFx.Burst(Ctx.Host, new Vector3(Foot.x, Foot.y + 0.1f, 0f), 0.35f, 0.18f, 4, 3, psr.sortingLayerID, psr.sortingOrder);
            if (left > 0) { Ctx.Flags.Set(Name + "_cracked"); Refresh(); return 1; }
            Ctx.Flags.Set(Name + "_collapsed");
            string sets = Str("sets", "");
            if (sets.Length > 0) Ctx.Flags.Set(sets);
            CutsceneFx.Burst(Ctx.Host, new Vector3(Foot.x, Foot.y + 0.2f, 0f), 0.6f, 0.3f, 8, 6, psr.sortingLayerID, psr.sortingOrder);
            Refresh();
            return 2;
        }

        private void Refresh()
        {
            var sp = Collapsed ? broken : left < hits && cracked != null ? cracked : whole;
            if (body == null) return;
            body.enabled = sp != null;
            if (sp != null)
            {
                if (whole != null) scale = Float("sprite_width", 0.7f) / Mathf.Max(0.01f, whole.bounds.size.x);
                body.sprite = sp;
                body.transform.localScale = new Vector3(scale, scale, 1f);
                body.transform.position = new Vector3(Foot.x, Foot.y - sp.bounds.min.y * scale, 0f);
                if (Collapsed) body.sortingOrder = Mathf.Min(body.sortingOrder, -3);   // đống đổ nát nằm dưới nhân vật
            }
            if (col != null) col.enabled = !Collapsed;                                   // đổ rồi: đi qua được
        }

        private System.Collections.IEnumerator Shake()
        {
            if (body == null) yield break;
            Vector3 p0 = body.transform.position;
            for (float t = 0f; t < 0.35f && body != null; t += Time.deltaTime)
            {
                body.transform.position = p0 + new Vector3(Mathf.Sin(t * 80f) * 0.03f * (1f - t / 0.35f), 0f, 0f);
                yield return null;
            }
            if (body != null) body.transform.position = p0;
        }

        private static Sprite Load(string p) => string.IsNullOrEmpty(p) ? null : Resources.Load<Sprite>(p);
    }
}
