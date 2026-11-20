using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 04/11: sprite_prop — một vật trang trí có hình riêng (Resources), đặt từ Tiled, có chân chặn đường và có thể "thắp sáng".
    /// Dùng cho các trụ phong ấn trong Containment Vault (Specimen Zero núp sau trụ khi nó gào), dùng lại được cho map khác.
    /// Thuộc tính Tiled (object lớp mechanics; chân vật = giữa mép dưới vùng object):
    ///   sprite        ảnh lúc thường (vd VFX/specimen/ward_stone0). Bắt buộc. Pivot của ảnh = chân vật.
    ///   sprite_lit    ảnh lúc sáng (để trống = dùng `sprite`)
    ///   group         tên nhóm để code tìm lại (vd sz_ward) — <see cref="Group"/>
    ///   collider      "rộng,cao" (đơn vị thế giới) của khối chặn ở chân; "0" = không chặn. Mặc định 0.34,0.12
    ///   collider_shape 17/11 (§303): "box" (mặc định, vật đáy vuông) | "round" (vật chân tròn: thùng, chậu, cột tròn, đống đá...)
    ///                 round = CapsuleCollider2D nằm ngang (rộng = cao thì thành hình tròn) -> người chơi trượt quanh mép, không kẹt góc.
    ///   light_color   màu đèn khi sáng (#rrggbb, mặc định vàng nến), light_radius (mặc định 1.6)
    ///   decal         ảnh vẽ trên sàn (vd VFX/specimen/ward_sigil) — vòng ấn; decal_at "x,y" (ô Tiled, tâm vòng);
    ///   decal_size    đường kính (đơn vị, mặc định 0.8), decal_squash (dẹt theo chiều dọc, mặc định 0.6), decal_alpha (lúc thường, 0.14)
    ///   icon          05/11: ảnh biểu tượng của vật (Resources) cho code dùng — vd rune riêng của từng trụ phong ấn hiện dưới chân boss
    ///   flat          06/11: true = vật nằm bẹt trên sàn (xương, vòng neo...) -> luôn vẽ dưới nhân vật, không sắp theo chân
    /// 06/11 (b): CÁCH ĐẶT MẶC ĐỊNH = tile object trong lớp `mechanics_art`, kéo từ tileset SR-mech-art.tsx (thấy hình trong Tiled).
    ///   Tile đã ghi sẵn class sprite_prop + `sprite` / `sprite_lit` / `collider` / `flat`; object chỉ ghi thứ khác mặc định.
    ///   Hình vẽ khít khung tile object (kéo to / lật ngang trong Tiled = to / lật trong game; collider + vòng sáng to theo).
    ///   Cách cũ (hình chữ nhật + `sprite`, chân = giữa mép dưới) vẫn chạy cho map chưa chuyển.
    /// Không lưu trạng thái — đèn do code gọi SetLit (vd boss) bật / tắt.
    /// </summary>
    public sealed class SpritePropMechanism : Mechanism
    {
        private static readonly List<SpritePropMechanism> all = new List<SpritePropMechanism>();

        /// <summary>Các vật cùng nhóm trên map hiện tại (bỏ những vật đã bị huỷ khi đổi map).</summary>
        public static List<SpritePropMechanism> Group(string group)
        {
            all.RemoveAll(p => p == null || p.Body == null);
            var list = new List<SpritePropMechanism>();
            foreach (var p in all) if (p.group == group) list.Add(p);
            return list;
        }

        public SpriteRenderer Body { get; private set; }
        public SpriteRenderer Decal { get; private set; }
        public Light2D Glow { get; private set; }
        /// <summary>Chân vật (thế giới).</summary>
        public Vector2 Foot { get; private set; }
        /// <summary>Tâm vòng ấn trên sàn (thế giới); không có decal = chân vật.</summary>
        public Vector2 DecalAt { get; private set; }
        public float DecalAlpha { get; private set; }
        public bool Lit { get; private set; }
        /// <summary>Ảnh biểu tượng (`icon`), null nếu không có.</summary>
        public Sprite Icon { get; private set; }

        private string group;
        private Sprite normal, lit;

        public override void Init()
        {
            group = Str("group", "");
            string icon = Str("icon", "");
            Icon = icon.Length > 0 ? Resources.Load<Sprite>(icon) : null;
            var art = Art.Count > 0 ? Art[0] : null;
            normal = art != null ? art.Resolve("sprite") : Resources.Load<Sprite>(Str("sprite", ""));
            string litPath = Str("sprite_lit", "");
            lit = litPath.Length > 0 ? Resources.Load<Sprite>(litPath) : null;
            if (normal == null) { Debug.LogWarning("[SpriteProp] " + Name + ": không thấy ảnh '" + Str("sprite", "") + "'"); return; }

            Foot = art != null ? art.PivotFor(normal) : new Vector2(Rect.center.x, Rect.yMin);
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;

            var go = new GameObject("Prop " + Name);
            go.transform.SetParent(Ctx.Host.transform, false);
            go.transform.position = new Vector3(Foot.x, Foot.y, 0f);
            Body = go.AddComponent<SpriteRenderer>();
            Body.sprite = normal;
            if (art != null) art.Apply(Body, normal);                          // khớp chỗ vẽ trong Tiled (vị trí, tỉ lệ, lật)
            Body.spriteSortPoint = SpriteSortPoint.Pivot;                      // cùng lớp nhân vật: Ren đứng phía bắc trụ thì bị trụ che
            if (psr != null) { Body.sortingLayerID = psr.sortingLayerID; Body.sortingOrder = psr.sortingOrder; Body.sharedMaterial = psr.sharedMaterial; }
            if (Props.GetBool("flat", false)) Body.sortingOrder -= 6;           // nằm bẹt trên sàn: dưới chân nhân vật
            // 20/11 (§350): `scale` (cách đặt hình chữ nhật, không có tile art) — phóng to ảnh; `flip` = lật ngang
            float sc = Float("scale", 1f);
            if (art == null && Mathf.Abs(sc - 1f) > 0.001f) go.transform.localScale = new Vector3(sc, sc, 1f);
            if (Props.GetBool("flip", false)) Body.flipX = true;

            string col = Str("collider", "0.34,0.12");
            var wh = col.Split(',');
            if (wh.Length == 2 && float.TryParse(wh[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w) &&
                float.TryParse(wh[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h) && w > 0f && h > 0f)
            {
                if (Str("collider_shape", "box").ToLowerInvariant() == "round")
                {
                    var cap = go.AddComponent<CapsuleCollider2D>();            // §303: chân tròn — không có góc vuông để kẹt
                    cap.direction = w >= h ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                    cap.size = new Vector2(w, h);
                    cap.offset = new Vector2(0f, h * 0.5f);
                }
                else
                {
                    var box = go.AddComponent<BoxCollider2D>();                // không có Rigidbody -> vật cản tĩnh
                    box.size = new Vector2(w, h);
                    box.offset = new Vector2(0f, h * 0.5f);
                }
            }

            DecalAt = Foot;
            string decal = Str("decal", "");
            var ds = decal.Length > 0 ? Resources.Load<Sprite>(decal) : null;
            if (ds != null)
            {
                DecalAt = Ctx.TilePoint(Str("decal_at", ""), Foot);
                DecalAlpha = Float("decal_alpha", 0.14f);
                var dg = new GameObject("Decal " + Name);
                dg.transform.SetParent(go.transform, true);
                dg.transform.position = new Vector3(DecalAt.x, DecalAt.y, 0f);
                float size = Float("decal_size", 0.8f), squash = Float("decal_squash", 0.6f);
                float k = size / Mathf.Max(0.01f, ds.bounds.size.x);
                float parentK = Mathf.Max(0.0001f, go.transform.localScale.x);   // vòng ấn giữ đúng cỡ `decal_size` dù vật được kéo to
                dg.transform.localScale = new Vector3(k / parentK, k * squash / parentK, 1f);
                Decal = dg.AddComponent<SpriteRenderer>();
                Decal.sprite = ds;
                FxLighting.MakeUnlit(Decal);                                   // nét vàng tự phát sáng, không bị bóng tối của vault nuốt
                Decal.sortingLayerID = Body.sortingLayerID;
                Decal.sortingOrder = -4;                                       // trên decal sàn, dưới nhân vật
                Decal.color = new Color(1f, 1f, 1f, DecalAlpha);
            }

            var lg = new GameObject("Glow " + Name);
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            Glow = lg.AddComponent<Light2D>();
            Glow.lightType = Light2D.LightType.Point;
            Glow.color = MechanismContext.ParseColor(Str("light_color", "#ffd38a"), 1f);
            Glow.pointLightOuterRadius = Float("light_radius", 1.6f);
            Glow.pointLightInnerRadius = 0.05f;
            Glow.intensity = 0f;
            Glow.enabled = false;
            all.Add(this);
            // 20/11 (§350): `lit` = sáng sẵn (đồ trang trí có đèn: lò lửa hồn, cụm pha lê) — `light_intensity` (mặc định 0,8)
            if (Props.GetBool("lit", false)) { SetLit(true); Glow.intensity = Float("light_intensity", 0.8f); }
        }

        /// <summary>Bật / tắt trạng thái sáng (đổi ảnh). Đèn và độ sáng vòng ấn do người gọi điều khiển qua Glow / Decal.</summary>
        public void SetLit(bool on)
        {
            if (Body == null) return;
            Lit = on;
            Body.sprite = on && lit != null ? lit : normal;
            Glow.enabled = on;
            if (!on)
            {
                Glow.intensity = 0f;
                if (Decal != null) { Decal.color = new Color(1f, 1f, 1f, DecalAlpha); Decal.transform.localRotation = Quaternion.identity; }
            }
        }
    }
}
