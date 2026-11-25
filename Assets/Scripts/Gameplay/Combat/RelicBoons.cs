using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// RELIC BOONS of the "race for the weapons" minigame (The Reliquary Lists, Map 4). Ren reaches the weapon rack the Champion calls first
    /// and presses T -> steals that weapon's boon:
    ///   halberd -> Relic Might    : both Wardens take 40% more damage for 14 s (Health.AddVulnerability).
    ///   hammer  -> Bone Bulwark   : Ren takes only 50% damage for 12 s (PlayerCombat multiplies TakenMul).
    ///   flail   -> Penitent Haste : Ren is 35% faster for 14 s (PlayerController multiplies SpeedMul).
    ///   axes    -> Hurled Hatchets: Ren throws 2 axes at the Champion right away (7% HP each + short flinch).
    /// Crystal pillars use the same boons: crimson = halberd, azure = hammer, verdant = flail, gilded = axes (gold crystal shards instead of axes).
    /// Static state, cleared on scene load / when Ren dies mid-fight (Clear).
    /// </summary>
    public static class RelicBoons
    {
        private static float skinUntil, fleetUntil;

        public static float TakenMul => Time.time < skinUntil ? 0.5f : 1f;
        public static float SpeedMul => Time.time < fleetUntil ? 1.35f : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookReset() { Clear(); }

        public static void Clear() { skinUntil = fleetUntil = 0f; }

        /// <summary>Ren stole weapon `weapon` from the rack at `from`.</summary>
        public static void Grant(string weapon, Vector2 from)
        {
            switch (weapon)
            {
                case "halberd":
                case "crimson":
                    foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                        if (ai != null && ai.Health != null && !ai.Health.IsDead && (ai.GetComponent<HollowChampionSkills>() != null || ai.GetComponent<BrazenReliquarySkills>() != null))
                            ai.Health.AddVulnerability(0.4f, 14f);
                    Hints.Show(weapon == "crimson" ? "CRIMSON ATTUNEMENT - the Wardens take 40% more damage!" : "RELIC MIGHT - the Wardens take 40% more damage!", 3f);
                    break;
                case "hammer":
                case "azure":
                    skinUntil = Time.time + 12f;
                    Hints.Show(weapon == "azure" ? "AZURE ATTUNEMENT - frost armour: you take half damage!" : "BONE BULWARK - you take half damage!", 3f);
                    break;
                case "flail":
                case "verdant":
                    fleetUntil = Time.time + 14f;
                    Hints.Show(weapon == "verdant" ? "VERDANT ATTUNEMENT - you move faster!" : "PENITENT HASTE - you move faster!", 3f);
                    break;
                case "axes":
                case "gilded":
                    var champ = Object.FindFirstObjectByType<HollowChampionSkills>();
                    if (champ != null && champ.Alive) champ.TakeHatchets(from, weapon == "gilded" ? "VFX/props/crystal_shard_gilded" : "VFX/props/relic_axe");
                    Hints.Show(weapon == "gilded" ? "GILDED ATTUNEMENT - radiant shards strike the Champion!" : "HURLED HATCHETS - Ren throws the axes at the Champion!", 3f);
                    break;
            }
        }
    }
}
