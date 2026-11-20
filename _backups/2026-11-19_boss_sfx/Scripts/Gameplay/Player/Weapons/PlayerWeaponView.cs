using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 09/11 (§202): NGOẠI HÌNH KATANA + BAO KIẾM trên người Ren. Ren chỉ dùng katana; kiếm và bao được VẼ CÙNG THÂN mọi khung
    /// (PixelLab: hai tay nắm chuôi thật, bao rỗng ở hông trái). Mỗi khung có ảnh phụ _KatanaUV (bake lúc nhập) ghi điểm ảnh nào là
    /// lưỡi / tsuba / chuôi / bao và nằm ở đâu dọc / ngang phần đó. Shader "SoulRequiem/Sprite-Lit-Katana" tra bảng thiết kế 64x32 của
    /// thanh đang cầm (<see cref="WeaponDefinition.design"/>) -> đổi katana = đổi 1 ảnh, giữ nguyên animation.
    /// <see cref="Show"/>: lúc vung kiếm phần phát sáng của thiết kế sáng hơn (<see cref="WeaponDefinition.swingGlowBoost"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponView : MonoBehaviour
    {
        private const string ShaderName = "SoulRequiem/Sprite-Lit-Katana";
        private static readonly int DesignId = Shader.PropertyToID("_KatanaDesign"), BoostId = Shader.PropertyToID("_GlowBoost"),
            TintId = Shader.PropertyToID("_GlowTint");

        private SpriteRenderer body;
        private Material mat;
        private WeaponDefinition weapon;
        private float swing, swingTarget;

        public WeaponDefinition Weapon => weapon;

        public static PlayerWeaponView Ensure(PlayerCombat owner)
        {
            var v = owner.GetComponent<PlayerWeaponView>();
            if (v == null) v = owner.gameObject.AddComponent<PlayerWeaponView>();
            return v;
        }

        private void Awake()
        {
            body = GetComponentInChildren<SpriteRenderer>();
            var src = Resources.Load<Material>("Weapons/KatanaBody");
            if (src == null)
            {
                var sh = Shader.Find(ShaderName);
                if (sh == null) { Debug.LogWarning("[Weapon] Thiếu shader " + ShaderName + " / Resources/Weapons/KatanaBody.mat"); return; }
                src = new Material(sh);
            }
            mat = new Material(src) { name = "KatanaBody (Ren)" };
            if (body != null) body.sharedMaterial = mat;
        }

        private void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }

        public void SetWeapon(WeaponDefinition w)
        {
            weapon = w;
            if (mat == null || w == null) return;
            if (w.design != null) mat.SetTexture(DesignId, w.design);
            mat.SetColor(TintId, w.glowTint);
            SetGlow();
        }

        /// <summary>Đang vung kiếm (true) / thôi (false): phần phát sáng của thiết kế loé lên.</summary>
        public void Show(bool on) => swingTarget = on ? 1f : 0f;

        /// <summary>10/11 (§217): hệ số sáng thêm khi nội tại của thanh kiếm đang bật (1 = thường).</summary>
        public float TraitGlow { get => traitGlow; set { traitGlow = Mathf.Max(0f, value); SetGlow(); } }
        private float traitGlow = 1f;

        private void SetGlow()
        {
            if (mat == null || weapon == null) return;
            mat.SetFloat(BoostId, weapon.glow * traitGlow * (1f + swing * Mathf.Max(0f, weapon.swingGlowBoost - 1f)));
        }

        private void LateUpdate()
        {
            // cutscene / hiệu ứng khác có thể đã thay material của thân -> gắn lại (thiếu material = kiếm hiện màu khoá)
            if (body != null && mat != null && body.sharedMaterial != mat) body.sharedMaterial = mat;
            if (Mathf.Approximately(swing, swingTarget)) return;
            swing = Mathf.MoveTowards(swing, swingTarget, Time.deltaTime * (swingTarget > swing ? 14f : 4f));
            SetGlow();
        }
    }
}
