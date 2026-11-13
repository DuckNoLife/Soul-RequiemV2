using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>10/11 (§217): NỘI TẠI của một thanh katana. Chỉ tác động lên đòn CHÉM (Mantra không đổi).</summary>
    public enum WeaponTraitKind
    {
        /// <summary>Không có nội tại.</summary>
        None,
        /// <summary>Đòn phản: nhát chém ĐẦU TIÊN trong `window` giây sau khi lăn gây thêm `bonus` sát thương (dùng một lần mỗi lần lăn).</summary>
        Counterstroke,
        /// <summary>Ngọn đèn chưa tắt: không trúng đòn trong `window` giây -> lưỡi kiếm bừng sáng, chém thêm `bonus`. Trúng đòn là tắt.</summary>
        Vigil,
        /// <summary>Lời thề máu: mỗi nhát trúng hồi `leech` phần sát thương gây ra; máu dưới `threshold` thì chém thêm `bonus`.</summary>
        BloodOath,
    }

    /// <summary>Số liệu + chữ hiển thị của nội tại (bảng vũ khí trong túi đồ đọc thẳng từ đây).</summary>
    [Serializable]
    public class WeaponTrait
    {
        public WeaponTraitKind kind = WeaponTraitKind.None;
        [Tooltip("Tên nội tại hiện trong túi đồ.")] public string title = "";
        [TextArea] [Tooltip("Mô tả nội tại. Để trống -> tự sinh từ số liệu.")] public string text = "";
        [Tooltip("Counterstroke: cửa sổ (giây) sau khi lăn. Vigil: số giây không trúng đòn thì kiếm sáng. BloodOath: không dùng.")]
        public float window = 1f;
        [Tooltip("Sát thương chém cộng thêm khi nội tại bật (0.4 = +40%).")] public float bonus = 0.4f;
        [Tooltip("BloodOath: phần sát thương gây ra được hồi thành máu (0.06 = 6%).")] public float leech = 0f;
        [Tooltip("BloodOath: máu dưới mức này (0..1) thì được cộng `bonus`.")] [Range(0f, 1f)] public float threshold = 0f;

        public bool Any => kind != WeaponTraitKind.None;

        /// <summary>Chữ mô tả (tự sinh nếu `text` trống).</summary>
        public string Describe()
        {
            if (!string.IsNullOrWhiteSpace(text)) return text;
            int pct = Mathf.RoundToInt(bonus * 100f);
            switch (kind)
            {
                case WeaponTraitKind.Counterstroke:
                    return $"The first cut within {window:0.#}s of a roll strikes {pct}% harder.";
                case WeaponTraitKind.Vigil:
                    return $"Go {window:0.#}s without being hit and the edge takes light: +{pct}% cut damage. One hit puts it out.";
                case WeaponTraitKind.BloodOath:
                    return $"Every cut returns {Mathf.RoundToInt(leech * 100f)}% of its damage as health. Below {Mathf.RoundToInt(threshold * 100f)}% health, cuts strike {pct}% harder.";
                default: return "";
            }
        }
    }

    /// <summary>10/11 (§217): tính nội tại cho đòn chém. Mọi chỗ gọi đều đi qua đây (PlayerMelee, PlayerWeaponView, HUD).</summary>
    public static class WeaponTraits
    {
        private static WeaponTrait Of(PlayerCombat c) => c != null && c.Weapon != null && c.Weapon.trait != null && c.Weapon.trait.Any ? c.Weapon.trait : null;

        /// <summary>Nội tại đang bật? (ánh kiếm, HUD).</summary>
        public static bool Active(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null) return false;
            switch (t.kind)
            {
                case WeaponTraitKind.Counterstroke: return c.CounterReady && Time.time - c.LastRollTime <= t.window;
                case WeaponTraitKind.Vigil: return Time.time - c.LastHurtTime >= t.window;
                case WeaponTraitKind.BloodOath: return c.Health != null && c.Health.Percent <= t.threshold;
                default: return false;
            }
        }

        /// <summary>Hệ số sát thương của nhát chém sắp ra.</summary>
        public static float MeleeMul(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && Active(c) ? 1f + Mathf.Max(0f, t.bonus) : 1f;
        }

        /// <summary>Gọi NGAY SAU khi nhát chém gây sát thương: tiêu nội tại dùng một lần, hút máu.</summary>
        public static void AfterMeleeHit(PlayerCombat c, float dealtTotal, int enemiesHit)
        {
            var t = Of(c);
            if (t == null || enemiesHit <= 0) return;
            if (t.kind == WeaponTraitKind.Counterstroke) c.CounterReady = false;       // chỉ nhát đầu sau khi lăn
            if (t.kind == WeaponTraitKind.BloodOath && t.leech > 0f && c.Health != null && !c.Health.IsDead)
                c.Health.Heal(dealtTotal * t.leech);
        }
    }
}
