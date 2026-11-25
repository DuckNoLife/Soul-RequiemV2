using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// TONE OF THE WHOLE DOMAIN by The Crystal Vessel's phase (set every frame by VesselDomainMechanism.TickTone).
    /// While `Active`, void decorations (rock islands, watch towers, tower eyes, nebulae) use the GREY "_g" version (_tools/s418/make_tone_assets.py) times `Tint`,
    /// so they can take any tone (volcanic red / ice blue / lightning gold / purple); `Glow` = light, light cone and eye colour. Before the fight (Active = false): original purple images.
    /// </summary>
    public static class DomainTone
    {
        public static bool Active;
        public static Color Tint = Color.white;
        public static Color Glow = new Color(0.78f, 0.45f, 1f);

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static void Reset()
        {
            SetDesat(0f, 1f);
            Active = false;
            Tint = Color.white;
            Glow = new Color(0.78f, 0.45f, 1f);
        }

        /// <summary>Grey version of `resPath` (Resources, no extension) - null if not generated.</summary>
        public static Sprite Gray(string resPath)
        {
            if (string.IsNullOrEmpty(resPath)) return null;
            if (cache.TryGetValue(resPath, out var s) && s != null) return s;
            s = Resources.Load<Sprite>(resPath + "_g");
            cache[resPath] = s;
            return s;
        }

        // --------------------------------------------------------------- desaturating floor / tile objects with a shader
        private static readonly int DesatId = Shader.PropertyToID("_DomainDesat"), GainId = Shader.PropertyToID("_DomainGain");
        private static Material toneMat;
        private static bool toneMatTried;

        /// <summary>Set the global desaturation (0 = original image, 1 = grey x `gain`) for every renderer using "SoulRequiem/Sprite-Lit-Tone".</summary>
        public static void SetDesat(float desat, float gain)
        {
            Shader.SetGlobalFloat(DesatId, Mathf.Clamp01(desat));
            Shader.SetGlobalFloat(GainId, gain);
        }

        /// <summary>Switch the material to Sprite-Lit-Tone if the renderer uses Sprite-Lit-Default (other materials are left alone).</summary>
        public static void UseToneMaterial(Renderer r)
        {
            if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader == null) return;
            if (!r.sharedMaterial.shader.name.EndsWith("Sprite-Lit-Default")) return;
            if (!toneMatTried)
            {
                toneMatTried = true;
                toneMat = Resources.Load<Material>("Materials/DomainTone");
                if (toneMat == null)
                {
                    var sh = Shader.Find("SoulRequiem/Sprite-Lit-Tone");
                    if (sh != null) toneMat = new Material(sh) { name = "DomainTone (runtime)" };
                }
                if (toneMat == null) Debug.LogWarning("[DomainTone] Missing Resources/Materials/DomainTone.mat (shader SoulRequiem/Sprite-Lit-Tone)");
            }
            if (toneMat != null) r.sharedMaterial = toneMat;
        }

        /// <summary>Multiply colours (keeps the alpha of `c`).</summary>
        public static Color Mul(Color c, Color t) => new Color(c.r * t.r, c.g * t.g, c.b * t.b, c.a);
    }
}
