using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.Visual
{
    /// <summary>
    /// Soul Lantern – vòng sáng lam nhạt quanh nhân vật để người chơi luôn thấy mình và sàn quanh mình ở chỗ tối.
    /// Không phải đèn pin: cường độ thấp, tự giảm khi ánh nền sáng (Map 1 ban ngày gần như tắt).
    /// Phản ứng: thở nhẹ; máu dưới 30% ngả đỏ và đập theo nhịp tim; trúng đòn chớp đỏ; lướt/bất tử sáng lên; tung chiêu lóe sáng.
    /// Tự gắn vào người chơi khi nạp scene (không cần sửa scene).
    /// </summary>
    public class SoulLantern : MonoBehaviour
    {
        public Color soulColor = new Color(0.74f, 0.86f, 1f);
        public Color lowHpColor = new Color(1f, 0.42f, 0.38f);
        // 29/10: nới bán kính (cũ: lõi 1.1 / quầng 2.4) để thấy rõ vật xung quanh khi lại gần. 1 ô = 0,32 đơn vị.
        // Chỉnh thử khi đang Play: chọn Player > SoulLantern trong Inspector (đổi ngay, nhưng dừng Play là mất -> ghi số vào đây).
        [Header("Lõi sáng (bán kính ≈6 ô)")] public float coreRadius = 2.0f; public float coreIntensity = 0.8f;
        [Range(0f, 0.9f)] public float coreInner = 0.3f;
        [Header("Quầng mờ (bán kính ≈13 ô)")] public float haloRadius = 4.2f; public float haloIntensity = 0.3f;
        [Range(0f, 0.9f)] public float haloInner = 0.25f;
        [Tooltip("Ánh nền sáng hơn mức này thì đèn tắt hẳn")] public float fullOffAmbient = 0.75f;
        [Tooltip("Ánh nền tối hơn mức này thì đèn sáng tối đa")] public float fullOnAmbient = 0.30f;

        private Light2D core, halo;
        private Health health;
        private float flash;          // lóe sáng (tung chiêu / lướt)
        private float hurt;           // chớp đỏ khi trúng đòn
        private float scale = 1f;
        private Light2D global;
        private float nextSearch;

        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { if (hooked) SceneManager.sceneLoaded -= OnSceneLoaded; hooked = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!hooked) { SceneManager.sceneLoaded += OnSceneLoaded; hooked = true; }
            Attach();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Attach();

        private static void Attach()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null && pc.GetComponent<SoulLantern>() == null) pc.gameObject.AddComponent<SoulLantern>();
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            core = MakeLight("SoulLantern Core", coreRadius, coreInner, 0.8f);
            halo = MakeLight("SoulLantern Halo", haloRadius, haloInner, 0.35f);
        }

        private Light2D MakeLight(string name, float radius, float inner, float falloff)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);   // tâm ở ngực, không ở chân
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * inner;
            l.falloffIntensity = falloff;
            l.color = soulColor;
            l.intensity = 0f;
            return l;
        }

        private void OnEnable()
        {
            PlayerCombat.SkillCast += OnSkill;
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            PlayerCombat.SkillCast -= OnSkill;
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnSkill(Vector2 at, float r) => flash = Mathf.Max(flash, 0.7f);
        private void OnDamaged(Health h, float amount) => hurt = 1f;

        private float AmbientLum()
        {
            if (global == null && Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1f;
                foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                    if (l.lightType == Light2D.LightType.Global) { global = l; break; }
            }
            if (global == null) return 0f;
            var c = global.color;
            return global.intensity * (0.3f * c.r + 0.59f * c.g + 0.11f * c.b);
        }

        private void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            bool dead = health != null && health.IsDead;
            float hp = health != null ? health.Percent : 1f;

            // tự giảm theo ánh nền (chỗ sáng không cần đèn, tránh cháy sáng)
            float amb = AmbientLum();
            float want = dead ? 0f : Mathf.Clamp01((fullOffAmbient - amb) / Mathf.Max(0.01f, fullOffAmbient - fullOnAmbient));
            scale = Mathf.MoveTowards(scale, want, dt * (dead ? 0.6f : 1.5f));

            // lướt / bất tử: sáng lên một chút, rung nhẹ
            if (health != null && health.Invulnerable && !dead) flash = Mathf.Max(flash, 0.3f + 0.1f * Mathf.Sin(t * 40f));
            flash = Mathf.MoveTowards(flash, 0f, dt * 3.5f);
            hurt = Mathf.MoveTowards(hurt, 0f, dt * 5f);

            // thở nhẹ; máu thấp: nhịp tim (2 nhịp dồn rồi nghỉ)
            float breathe = 1f + 0.05f * Mathf.Sin(t * 2f * Mathf.PI * 0.55f);
            float low = Mathf.Clamp01((0.3f - hp) / 0.2f);              // 0 khi máu ≥30%, 1 khi ≤10%
            float beat = 0f;
            if (low > 0f)
            {
                float ph = (t * 1.25f) % 1f;
                beat = Mathf.Exp(-ph * 18f) + 0.6f * Mathf.Exp(-Mathf.Max(0f, ph - 0.18f) * 18f) * (ph > 0.18f ? 1f : 0f);
            }
            Color c = Color.Lerp(soulColor, lowHpColor, Mathf.Max(low * 0.85f, hurt));

            float k = scale * (breathe + flash + 0.35f * beat * low + 0.4f * hurt);
            core.intensity = coreIntensity * k;
            halo.intensity = haloIntensity * k;
            core.color = halo.color = c;
            core.pointLightOuterRadius = coreRadius * (1f + 0.25f * flash);
            core.pointLightInnerRadius = core.pointLightOuterRadius * coreInner;
            halo.pointLightOuterRadius = haloRadius;
            halo.pointLightInnerRadius = haloRadius * haloInner;
        }
    }
}
