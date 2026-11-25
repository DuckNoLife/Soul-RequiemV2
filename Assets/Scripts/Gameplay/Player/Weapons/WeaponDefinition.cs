using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// One hit of the two-handed katana combo — attack1 diagonal cut (kesa), attack2 rising backhand, attack3 horizontal sweep.
    /// Balance: 16 + 16 + 26 over ~1.9 s ≈ 31 DPS. Ren's body (8 frames, 8 directions, hit frame = 3) is shared by every
    /// katana; a katana only changes the numbers + sword colours + slash trail colour.
    /// </summary>
    [Serializable]
    public class ComboStep
    {
        [Tooltip("Display name (debug).")] public string name = "Slash";
        public float damage = 30f;
        [Tooltip("Slash range from the hip (Unity units).")] public float range = 0.75f;
        [Range(30f, 360f)] public float arcDegrees = 140f;
        public float knockback = 2f;
        [Tooltip("Hit frame in the 8-frame clip (0-7). Ren's body: attack1 = 3, attack2 = 3, attack3 = 3.")]
        [Range(0, 7)] public int hitFrame = 3;
        [Tooltip("Frame from which the next hit can chain / moving cancels the recovery.")]
        [Range(1, 8)] public int cancelFrame = 6;
        [Tooltip("Frame rate of this hit (8-frame clip). Normal katanas use 13 fps (0.46 s / hit when chained). Heavy weapons: lower.")]
        public float fps = 13f;
        [Tooltip("Hit-stop / camera kick factor on hit.")] public float impact = 1f;
        [Tooltip("Flip the slash trail (reverse-direction cut).")] public bool flipTrail;
        [Tooltip("Slash trail size factor.")] public float trailScale = 1f;
        [Tooltip("Thrust (straight m3_thrust trail instead of the curved slash trail).")] public bool thrust;

        public float HitTime => hitFrame / Mathf.Max(1f, fps);
        public float CancelTime => cancelFrame / Mathf.Max(1f, fps);
        public float Length => 8f / Mathf.Max(1f, fps);
    }

    /// <summary>
    /// KATANA — ScriptableObject asset (Resources/Weapons/&lt;id&gt;.asset). Ren only uses katanas; every sword
    /// shares the same SHAPE (drawn into Ren's body) and differs in DESIGN: blade pattern (hamon, engravings), tsuba, hilt wrap, scabbard lacquer
    /// (64x32 table looked up through each frame's UV map), glow, showcase image, slash trail colour and combo numbers. Change katana = PlayerCombat.Equip(asset) (or set PlayerCombat.weapon).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        public string id = "katana";
        public string displayName = "Katana";
        [TextArea] public string description = "";
        [Header("Lore — weapon panel in the inventory")]
        [Tooltip("One line under the name: forge / former owner / era.")] public string epithet = "";
        [TextArea(3, 10)] [Tooltip("The sword's story (read in the inventory).")] public string lore = "";
        [Tooltip("Where it was found (shown once owned).")] public string foundAt = "";
        [Header("Passive")]
        public WeaponTrait trait = new WeaponTrait();
        [Tooltip("Small icon (inventory slot).")] public Sprite icon;
        [Tooltip("Large showcase image (sword + scabbard, ~256x96) — equipment panel / on pickup.")] public Sprite showcase;
        [Header("Look on Ren's body (UV map)")]
        [Tooltip("64x32 design table (Catalog/v2/tools/ren2/katana_design.py): blade / tsuba / hilt / scabbard. Point, uncompressed.")]
        public Texture2D design;
        [Tooltip("Colour multiplied into the design's glowing parts (alpha < 1 in the design table).")][ColorUsage(false, true)]
        public Color glowTint = Color.white;
        [Tooltip("Normal glow strength.")] public float glow = 1f;
        [Tooltip("While swinging the glow is multiplied by this.")] public float swingGlowBoost = 2.5f;
        [Tooltip("Slash trail colour (multiplied into the slash_* VFX).")] public Color trailColor = new Color(0.85f, 0.95f, 1f, 1f);
        [Tooltip("The sword's own slash VFX set (PixelLab VFX Creator), Resources/VFX/slash3/<slashStyle>_<hit>_<DIRECTION>. "
                 + "Drawn along the EXACT sword tip path of attack1-3, each animation frame = 2 VFX frames, starting at frame 2, reaching the tip on the hit frame (3). "
                 + "Empty = the old slash_* VFX tinted with trailColor.")] public string slashStyle = "";
        [Tooltip("Time (seconds) after a hit's cancel frame during which the next hit can still chain.")] public float comboWindow = 0.35f;
        [Tooltip("Extra rest after the last combo hit before starting over.")] public float finisherRecovery = 0.30f;
        public ComboStep[] combo =
        {
            new ComboStep { name = "Kesa Cut", damage = 16f, range = 0.75f, arcDegrees = 140f, knockback = 1.4f, hitFrame = 3, cancelFrame = 6, fps = 13f },
            new ComboStep { name = "Rising Cut", damage = 16f, range = 0.75f, arcDegrees = 140f, knockback = 1.4f, hitFrame = 3, cancelFrame = 6, fps = 13f, flipTrail = true },
            new ComboStep { name = "Horizontal Sweep", damage = 26f, range = 0.9f, arcDegrees = 170f, knockback = 3.2f, hitFrame = 3, cancelFrame = 7, fps = 12f, impact = 1.6f, trailScale = 1.2f },
        };

        public const string DefaultId = "Katana";
        public static WeaponDefinition LoadDefault() => Resources.Load<WeaponDefinition>("Weapons/" + DefaultId);
    }
}
