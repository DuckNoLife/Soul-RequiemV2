using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// KATANA + SCABBARD LOOK on Ren's body. Ren only uses katanas; the sword and scabbard are DRAWN INTO THE BODY in every frame
    /// (PixelLab: both hands on a real hilt, empty scabbard at the left hip). Each frame has a secondary _KatanaUV image (baked on import) telling which pixels are
    /// blade / tsuba / hilt / scabbard and where along / across that part they are. The shader "SoulRequiem/Sprite-Lit-Katana" looks up the 64x32 design table of
    /// the held sword (<see cref="WeaponDefinition.design"/>) -> changing katana = changing 1 image, the animations stay the same.
    /// <see cref="Show"/>: while swinging, the glowing parts of the design shine brighter (<see cref="WeaponDefinition.swingGlowBoost"/>).
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
                if (sh == null) { Debug.LogWarning("[Weapon] Missing shader " + ShaderName + " / Resources/Weapons/KatanaBody.mat"); return; }
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

        /// <summary>Swinging (true) / not (false): the design's glowing parts flare.</summary>
        public void Show(bool on) => swingTarget = on ? 1f : 0f;

        /// <summary>Extra glow factor while the sword's passive is active (1 = normal).</summary>
        public float TraitGlow { get => traitGlow; set { traitGlow = Mathf.Max(0f, value); SetGlow(); } }
        private float traitGlow = 1f;

        private void SetGlow()
        {
            if (mat == null || weapon == null) return;
            mat.SetFloat(BoostId, weapon.glow * traitGlow * (1f + swing * Mathf.Max(0f, weapon.swingGlowBoost - 1f)));
        }

        private void LateUpdate()
        {
            // cutscenes / other effects may have replaced the body material -> reapply (missing material = the sword shows its key colours)
            if (body != null && mat != null && body.sharedMaterial != mat) body.sharedMaterial = mat;
            if (Mathf.Approximately(swing, swingTarget)) return;
            swing = Mathf.MoveTowards(swing, swingTarget, Time.deltaTime * (swingTarget > swing ? 14f : 4f));
            SetGlow();
        }
    }
}
