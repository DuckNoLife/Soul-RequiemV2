using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§349): BÙA RELIC của minigame "đua giành vũ khí" (The Reliquary Lists, Map 4). Ren tới giá vũ khí đang được Champion gọi trước
    /// và bấm T -> cướp được bùa của vũ khí đó:
    ///   halberd -> Relic Might   : hai Warden nhận thêm 40% sát thương 14 s (Health.AddVulnerability).
    ///   hammer  -> Bone Bulwark  : Ren chỉ nhận 50% sát thương 12 s (PlayerCombat nhân TakenMul).
    ///   flail   -> Penitent Haste: Ren nhanh hơn 35% 14 s (PlayerController nhân SpeedMul).
    ///   axes    -> Hurled Hatchets: Ren ném ngay 2 rìu vào Champion (mỗi rìu 7% máu + khựng ngắn).
    /// 20/11 (§350): cột pha lê dùng cùng bùa: crimson = halberd, azure = hammer, verdant = flail, gilded = axes (mảnh pha lê vàng thay rìu).
    /// Trạng thái tĩnh, xoá khi nạp scene / Ren gục giữa trận (Clear).
    /// </summary>
    public static class RelicBoons
    {
        private static float skinUntil, fleetUntil;

        public static float TakenMul => Time.time < skinUntil ? 0.5f : 1f;
        public static float SpeedMul => Time.time < fleetUntil ? 1.35f : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookReset() { Clear(); }

        public static void Clear() { skinUntil = fleetUntil = 0f; }

        /// <summary>Ren cướp được vũ khí `weapon` ở giá đặt tại `from`.</summary>
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
