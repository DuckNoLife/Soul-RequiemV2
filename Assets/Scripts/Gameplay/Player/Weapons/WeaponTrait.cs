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
        /// <summary>16/11 (§254) Oathbreaker: không chém trong `window` giây -> nhát kế tiếp là đòn rút kiếm +`bonus`, choáng quái thường `threshold` giây.</summary>
        Iaijutsu,
        /// <summary>16/11 (§254) Ironcrown: máu dưới `threshold` -> nhận ít hơn `bonus` sát thương và không bị đòn nặng làm loạng choạng.</summary>
        LastStand,
        /// <summary>16/11 (§254) Eventide: mỗi đòn chém kết liễu hồi `leech` máu tối đa; bình máu hồi ít hơn `bonus`.</summary>
        Hunger,
        /// <summary>16/11 (§254) Arthur's Accord: nhát cuối combo +`bonus` sát thương; trúng thì giảm `leech` sát thương nhận vào trong `window` giây.</summary>
        Accord,
        /// <summary>16/11 (§257, TẠM) Namesake: hồi chiêu mọi Mantra giảm `bonus` (0,15 = 15%).</summary>
        Namebound,
        /// <summary>16/11 (§257, TẠM) Absolution: mỗi lần trúng đòn, nhát chém kế +`bonus` (cộng dồn tối đa `threshold`×10 lần — 0,3 = 3); nhát đó (trúng hay trượt) tiêu hết.</summary>
        Penance,
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
        [Tooltip("16/11 (§273) Iaijutsu: số giây giữ để ĐẦY thanh nộ (sát thương tối đa). `window` = mốc bắt đầu có tác dụng.")] public float hold = 0f;
        [Tooltip("16/11 (§274) Iaijutsu: sát thương cộng thêm khi ĐẦY thanh (1.0 = +100%). Vừa qua mốc = `bonus`, tăng dần tới đây.")] public float bonusMax = 0f;

        [Header("16/11 (§275) Hiệu ứng lên quái khi chém trúng")]
        [Tooltip("Bleed: chảy máu cộng dồn · Burn: thiêu · Bind: rễ trói (chậm, tím) · Chill: băng giá (chậm, xanh lạnh) · Mark: đánh dấu khát máu")]
        public StatusKind status = StatusKind.None;
        [Tooltip("EveryHit: mọi nhát · Finisher: nhát cuối combo · WhileActive: khi nội tại đang bật")] public StatusTrigger statusOn = StatusTrigger.EveryHit;
        [Tooltip("Bleed: sát thương / giây mỗi tầng · Burn: sát thương / giây · Bind / Chill: phần tốc độ bị giảm (0.4 = chậm 40%) · Mark: không dùng")] public float statusPower = 0f;
        [Tooltip("Thời gian hiệu ứng (giây).")] public float statusSeconds = 0f;
        [Tooltip("Bleed: số tầng tối đa.")] public int statusStacks = 1;

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
        /// <summary>16/11 (§271): thanh đang cầm có nội tại Iaijutsu (đòn giữ).</summary>
        public static bool IsIaijutsu(PlayerCombat c) { var t = Of(c); return t != null && t.kind == WeaponTraitKind.Iaijutsu; }
        /// <summary>16/11 (§271): số giây giữ cho đầy thanh nộ Iaijutsu.</summary>
        public static float IaiWindow(PlayerCombat c) { var t = Of(c); return t != null ? t.window : 0.5f; }
        /// <summary>16/11 (§273): số giây giữ để đầy thanh nộ (sát thương tối đa). Thiếu số liệu -> 3 lần mốc.</summary>
        public static float IaiFull(PlayerCombat c) { var t = Of(c); float w = t != null ? t.window : 0.5f; float h = t != null ? t.hold : 0f; return h > w ? h : w * 3f; }
        /// <summary>16/11 (§273): vị trí mốc có tác dụng trên thanh nộ (0..1).</summary>
        public static float IaiMark(PlayerCombat c) => Mathf.Clamp01(IaiWindow(c) / Mathf.Max(0.01f, IaiFull(c)));

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
                case WeaponTraitKind.Iaijutsu: return c.IaiCharged || (c.IaiHolding && c.IaiCharge >= IaiMark(c));   // 16/11 (§271/§273): đòn giữ qua mốc
                case WeaponTraitKind.LastStand: return c.Health != null && !c.Health.IsDead && c.Health.Percent <= t.threshold;
                case WeaponTraitKind.Hunger: return false;
                case WeaponTraitKind.Accord: return Time.time < c.AccordUntil;
                case WeaponTraitKind.Namebound: return true;
                case WeaponTraitKind.Penance: return c.PenanceStacks > 0;
                default: return false;
            }
        }

        // ------------------------------------------------------------------ 16/11 (§254): 4 nội tại mới
        /// <summary>Gọi lúc BẮT ĐẦU mỗi nhát chém (PlayerMelee.Slash): Iaijutsu xét đã nghỉ đủ lâu chưa.</summary>
        public static void OnSwingStart(PlayerCombat c)
        {
            var t = Of(c);
            if (c == null) return;
            // 16/11 (§271): Iaijutsu = đòn GIỮ (PlayerMelee.IaiSlash tự bật IaiCharged khi thanh nộ đầy) -> ở đây chỉ tắt cho thanh khác
            if (t == null || t.kind != WeaponTraitKind.Iaijutsu) c.IaiCharged = false;
            c.LastSwingTime = Time.time;
        }

        /// <summary>Hệ số sát thương nhát chém; <paramref name="finisher"/> = nhát cuối combo (Accord).</summary>
        public static float MeleeMul(PlayerCombat c, bool finisher)
        {
            var t = Of(c);
            if (t == null) return 1f;
            switch (t.kind)
            {
                case WeaponTraitKind.Iaijutsu: return c.IaiCharged ? 1f + Mathf.Lerp(Mathf.Max(0f, t.bonus), Mathf.Max(t.bonus, t.bonusMax), Mathf.Clamp01(c.IaiPower)) : 1f;   // §274: qua mốc = +bonus, đầy = +bonusMax
                case WeaponTraitKind.LastStand: return 1f;                                  // chỉ phòng thủ
                case WeaponTraitKind.Hunger: return 1f;
                case WeaponTraitKind.Accord: return finisher ? 1f + Mathf.Max(0f, t.bonus) : 1f;
                case WeaponTraitKind.Namebound: return 1f;
                case WeaponTraitKind.Penance: return 1f + Mathf.Max(0f, t.bonus) * c.PenanceStacks;
                default: return MeleeMul(c);
            }
        }

        /// <summary>Gọi sau khi nhát chém gây sát thương (kể cả 0 quái): Iaijutsu choáng quái thường trong vùng chém rồi tiêu; Accord bật giảm sát thương.</summary>
        public static void AfterSwing(PlayerCombat c, Vector2 center, float radius, int enemiesHit, bool finisher)
        {
            var t = Of(c);
            if (t == null) return;
            if (t.kind == WeaponTraitKind.Iaijutsu && c.IaiCharged)
            {
                if (enemiesHit > 0 && t.threshold > 0f)
                    foreach (var ai in SkillKit.EnemiesIn(center, radius))
                        if (!ai.IsBoss && !ai.Health.ImmuneToCrowdControl) ai.Health.Stun(t.threshold);
                c.IaiCharged = false;
                c.IaiPower = 0f;
            }
            if (t.kind == WeaponTraitKind.Accord && finisher && enemiesHit > 0) c.AccordUntil = Time.time + Mathf.Max(0f, t.window);
            if (t.kind == WeaponTraitKind.Penance) c.PenanceStacks = 0;
        }

        /// <summary>Penance: Ren vừa trúng đòn -> thêm một nấc.</summary>
        public static void OnHurt(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null || t.kind != WeaponTraitKind.Penance) return;
            int max = Mathf.Max(1, Mathf.RoundToInt(t.threshold * 10f));
            c.PenanceStacks = Mathf.Min(max, c.PenanceStacks + 1);
        }

        /// <summary>Namebound: hệ số hồi chiêu Mantra.</summary>
        public static float CooldownMul(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && t.kind == WeaponTraitKind.Namebound ? 1f - Mathf.Clamp01(t.bonus) : 1f;
        }

        /// <summary>Hệ số sát thương Ren nhận vào (PlayerCombat đặt vào Health.DamageTakenMul mỗi khung).</summary>
        public static float TakenMul(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null) return 1f;
            if (t.kind == WeaponTraitKind.LastStand && Active(c)) return 1f - Mathf.Clamp01(t.bonus);
            if (t.kind == WeaponTraitKind.Accord && Active(c)) return 1f - Mathf.Clamp01(t.leech);
            return 1f;
        }

        /// <summary>Last Stand: đòn nặng không làm loạng choạng / huỷ đòn chém.</summary>
        public static bool Unflinching(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && t.kind == WeaponTraitKind.LastStand && Active(c);
        }

        /// <summary>Hunger: một nhát chém vừa kết liễu quái. 16/11 (§275): thanh có `status` Mark thì hồi máu qua quái bị đánh dấu chết (EnemyStatus) — không hồi ở đây.</summary>
        public static void OnMeleeKill(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null || t.kind != WeaponTraitKind.Hunger || c.Health == null || c.Health.IsDead) return;
            if (t.status == StatusKind.Mark) return;
            c.Health.Heal(c.Health.Max * Mathf.Max(0f, t.leech));
            if (c.showVfx) { var v = SpriteVfx.Spawn("m3_blood_orb", c.transform.position + Vector3.up * 0.3f, 0.5f, c.VfxLayer, c.PlayerOrder + 1, false, 16f); }
        }

        /// <summary>16/11 (§275): quái mang dấu Khát máu (Eventide) vừa chết (do bất cứ đòn gì) -> Ren hồi `leech` máu tối đa + quả cầu máu.</summary>
        public static void OnMarkedKill(PlayerCombat c, Vector3 at)
        {
            var t = Of(c);
            if (t == null || t.kind != WeaponTraitKind.Hunger || c.Health == null || c.Health.IsDead) return;
            c.Health.Heal(c.Health.Max * Mathf.Max(0f, t.leech));
            if (c.showVfx) SpriteVfx.Spawn("m3_blood_orb", c.transform.position + Vector3.up * 0.3f, 0.5f, c.VfxLayer, c.PlayerOrder + 1, false, 16f);
        }

        /// <summary>16/11 (§275): nhát chém trúng quái (chưa chết) -> hiệu ứng `status` của thanh kiếm (theo `statusOn`).</summary>
        public static void ApplyHitStatus(PlayerCombat c, EnemyAI ai, bool finisher)
        {
            var t = Of(c);
            if (t == null || t.status == StatusKind.None || ai == null) return;
            bool go = t.statusOn == StatusTrigger.EveryHit || (t.statusOn == StatusTrigger.Finisher && finisher) || (t.statusOn == StatusTrigger.WhileActive && Active(c));
            if (!go) return;
            EnemyStatus.Of(ai).Apply(t.status, t.statusPower, t.statusSeconds, t.statusStacks, c);
        }

        /// <summary>16/11 (§275): THƯỚC NỘI TẠI cho HUD. fill 0..1 (đang tích / còn lại), active = nội tại đang bật, mark = vạch mốc (&lt;0 = không có).</summary>
        public static bool Gauge(PlayerCombat c, out float fill, out bool active, out float mark)
        {
            fill = 0f; active = false; mark = -1f;
            var t = Of(c);
            if (t == null) return false;
            float now = Time.time;
            float hp = c.Health != null ? c.Health.Percent : 1f;
            switch (t.kind)
            {
                case WeaponTraitKind.Counterstroke:
                    if (c.CounterReady && t.window > 0f && now - c.LastRollTime <= t.window) { fill = 1f - (now - c.LastRollTime) / t.window; active = true; }
                    break;
                case WeaponTraitKind.Vigil:
                    fill = t.window > 0f ? Mathf.Clamp01((now - c.LastHurtTime) / t.window) : 1f; active = fill >= 1f; break;
                case WeaponTraitKind.BloodOath:
                case WeaponTraitKind.LastStand:
                    fill = t.threshold < 1f ? Mathf.Clamp01(1f - (hp - t.threshold) / (1f - t.threshold)) : 1f; active = Active(c); break;   // máu càng thấp càng đầy
                case WeaponTraitKind.Iaijutsu:
                    fill = c.IaiHolding ? Mathf.Clamp01(c.IaiCharge) : 0f; mark = IaiMark(c); active = Active(c); break;
                case WeaponTraitKind.Hunger:
                    fill = Mathf.Clamp01(EnemyStatus.MarkedAlive / 3f); active = EnemyStatus.MarkedAlive > 0; break;
                case WeaponTraitKind.Accord:
                    fill = t.window > 0f ? Mathf.Clamp01((c.AccordUntil - now) / t.window) : 0f; active = fill > 0f; break;
                case WeaponTraitKind.Namebound:
                    fill = 1f; active = true; break;
                case WeaponTraitKind.Penance:
                    { int max = Mathf.Max(1, Mathf.RoundToInt(t.threshold * 10f)); fill = Mathf.Clamp01(c.PenanceStacks / (float)max); active = c.PenanceStacks > 0; }
                    break;
            }
            return true;
        }

        /// <summary>16/11 (§275): tên nội tại của thanh đang cầm (HUD).</summary>
        public static string Title(PlayerCombat c) { var t = Of(c); return t != null ? t.title : ""; }
        public static WeaponTraitKind KindOf(PlayerCombat c) { var t = Of(c); return t != null ? t.kind : WeaponTraitKind.None; }
        /// <summary>16/11 (§279): ngưỡng máu của nội tại (Blood Oath / Last Stand) để HUD vẽ vạch trên thanh máu; không có = -1.</summary>
        public static float HpThreshold(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && (t.kind == WeaponTraitKind.BloodOath || t.kind == WeaponTraitKind.LastStand) ? t.threshold : -1f;
        }

        /// <summary>Hunger: hệ số máu hồi của bình.</summary>
        public static float FlaskMul(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && t.kind == WeaponTraitKind.Hunger ? 1f - Mathf.Clamp01(t.bonus) : 1f;
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
