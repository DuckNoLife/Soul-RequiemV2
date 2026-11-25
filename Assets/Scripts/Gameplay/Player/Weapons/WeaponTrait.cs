using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>A katana PASSIVE. Only affects SLASHES (Mantras are unchanged). Formulas live in WeaponTraits (below).</summary>
    public enum WeaponTraitKind
    {
        /// <summary>No passive.</summary>
        None,
        /// <summary>Counter: the FIRST slash within `window` seconds after a roll deals `bonus` extra damage (once per roll).</summary>
        Counterstroke,
        /// <summary>Unextinguished lamp: no hit taken for `window` seconds -> the blade blazes, slashes deal `bonus` more. Taking a hit puts it out.</summary>
        Vigil,
        /// <summary>Blood oath: every hit heals `leech` of the damage dealt; below `threshold` HP slashes deal `bonus` more.</summary>
        BloodOath,
        /// <summary>Oathbreaker: no slash for `window` seconds -> the next one is a draw strike +`bonus`, stunning normal enemies for `threshold` seconds.</summary>
        Iaijutsu,
        /// <summary>Ironcrown: below `threshold` HP -> take `bonus` less damage and heavy hits no longer stagger.</summary>
        LastStand,
        /// <summary>Eventide: every finishing slash heals `leech` of max HP; flasks heal `bonus` less.</summary>
        Hunger,
        /// <summary>Arthur's Accord: the last combo hit +`bonus` damage; on hit, damage taken is reduced by `leech` for `window` seconds.</summary>
        Accord,
        /// <summary>Namesake: every Mantra cooldown reduced by `bonus` (0.15 = 15%).</summary>
        Namebound,
        /// <summary>Absolution: each hit taken gives the next slash +`bonus` (stacks up to `threshold`×10 times — 0.3 = 3); that slash (hit or miss) consumes it all.</summary>
        Penance,
    }

    /// <summary>Passive numbers + display text (the inventory weapon panel reads them directly).</summary>
    [Serializable]
    public class WeaponTrait
    {
        public WeaponTraitKind kind = WeaponTraitKind.None;
        [Tooltip("Passive name shown in the inventory.")] public string title = "";
        [TextArea] [Tooltip("Passive description. Empty -> generated from the numbers.")] public string text = "";
        [Tooltip("Counterstroke: window (seconds) after a roll. Vigil: seconds without being hit before the sword lights up. BloodOath: unused.")]
        public float window = 1f;
        [Tooltip("Extra slash damage while the passive is active (0.4 = +40%).")] public float bonus = 0.4f;
        [Tooltip("BloodOath: share of damage dealt healed back as HP (0.06 = 6%).")] public float leech = 0f;
        [Tooltip("BloodOath: below this HP (0..1) the `bonus` applies.")] [Range(0f, 1f)] public float threshold = 0f;
        [Tooltip("Iaijutsu: seconds held to FILL the charge bar (max damage). `window` = the threshold where it starts to work.")] public float hold = 0f;
        [Tooltip("Iaijutsu: extra damage with a FULL bar (1.0 = +100%). Just past the threshold = `bonus`, rising up to this.")] public float bonusMax = 0f;

        [Header("Effect on enemies when a slash hits")]
        [Tooltip("Bleed: stacking bleed · Burn: burn · Bind: binding roots (slow, violet) · Chill: frost (slow, icy blue) · Mark: blood-hunger mark")]
        public StatusKind status = StatusKind.None;
        [Tooltip("EveryHit: every hit · Finisher: last combo hit · WhileActive: while the passive is active")] public StatusTrigger statusOn = StatusTrigger.EveryHit;
        [Tooltip("Bleed: damage / second per stack · Burn: damage / second · Bind / Chill: speed share removed (0.4 = 40% slower) · Mark: unused")] public float statusPower = 0f;
        [Tooltip("Effect duration (seconds).")] public float statusSeconds = 0f;
        [Tooltip("Bleed: max stacks.")] public int statusStacks = 1;

        public bool Any => kind != WeaponTraitKind.None;

        /// <summary>Description text (generated when `text` is empty).</summary>
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

    /// <summary>Passive calculations for slashes. Every caller goes through here (PlayerMelee, PlayerWeaponView, HUD).</summary>
    public static class WeaponTraits
    {
        /// <summary>The held sword has the Iaijutsu passive (hold strike).</summary>
        public static bool IsIaijutsu(PlayerCombat c) { var t = Of(c); return t != null && t.kind == WeaponTraitKind.Iaijutsu; }
        /// <summary>Seconds held to fill the Iaijutsu charge bar.</summary>
        public static float IaiWindow(PlayerCombat c) { var t = Of(c); return t != null ? t.window : 0.5f; }
        /// <summary>Seconds held to fill the charge bar (max damage). Missing data -> 3 times the threshold.</summary>
        public static float IaiFull(PlayerCombat c) { var t = Of(c); float w = t != null ? t.window : 0.5f; float h = t != null ? t.hold : 0f; return h > w ? h : w * 3f; }
        /// <summary>Position of the effective threshold on the charge bar (0..1).</summary>
        public static float IaiMark(PlayerCombat c) => Mathf.Clamp01(IaiWindow(c) / Mathf.Max(0.01f, IaiFull(c)));

        private static WeaponTrait Of(PlayerCombat c) => c != null && c.Weapon != null && c.Weapon.trait != null && c.Weapon.trait.Any ? c.Weapon.trait : null;

        /// <summary>Is the passive active? (sword glow, HUD).</summary>
        public static bool Active(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null) return false;
            switch (t.kind)
            {
                case WeaponTraitKind.Counterstroke: return c.CounterReady && Time.time - c.LastRollTime <= t.window;
                case WeaponTraitKind.Vigil: return Time.time - c.LastHurtTime >= t.window;
                case WeaponTraitKind.BloodOath: return c.Health != null && c.Health.Percent <= t.threshold;
                case WeaponTraitKind.Iaijutsu: return c.IaiCharged || (c.IaiHolding && c.IaiCharge >= IaiMark(c));   // hold strike past the threshold
                case WeaponTraitKind.LastStand: return c.Health != null && !c.Health.IsDead && c.Health.Percent <= t.threshold;
                case WeaponTraitKind.Hunger: return false;
                case WeaponTraitKind.Accord: return Time.time < c.AccordUntil;
                case WeaponTraitKind.Namebound: return true;
                case WeaponTraitKind.Penance: return c.PenanceStacks > 0;
                default: return false;
            }
        }

        // ------------------------------------------------------------------ the 4 later passives
        /// <summary>Called at the START of each slash (PlayerMelee.Slash): Iaijutsu checks whether Ren rested long enough.</summary>
        public static void OnSwingStart(PlayerCombat c)
        {
            var t = Of(c);
            if (c == null) return;
            // Iaijutsu = HOLD strike (PlayerMelee.IaiSlash sets IaiCharged itself when the bar is full) -> here it is only cleared for other swords
            if (t == null || t.kind != WeaponTraitKind.Iaijutsu) c.IaiCharged = false;
            c.LastSwingTime = Time.time;
        }

        /// <summary>Slash damage multiplier; <paramref name="finisher"/> = last combo hit (Accord).</summary>
        public static float MeleeMul(PlayerCombat c, bool finisher)
        {
            var t = Of(c);
            if (t == null) return 1f;
            switch (t.kind)
            {
                case WeaponTraitKind.Iaijutsu: return c.IaiCharged ? 1f + Mathf.Lerp(Mathf.Max(0f, t.bonus), Mathf.Max(t.bonus, t.bonusMax), Mathf.Clamp01(c.IaiPower)) : 1f;   // past the threshold = +bonus, full = +bonusMax
                case WeaponTraitKind.LastStand: return 1f;                                  // defensive only
                case WeaponTraitKind.Hunger: return 1f;
                case WeaponTraitKind.Accord: return finisher ? 1f + Mathf.Max(0f, t.bonus) : 1f;
                case WeaponTraitKind.Namebound: return 1f;
                case WeaponTraitKind.Penance: return 1f + Mathf.Max(0f, t.bonus) * c.PenanceStacks;
                default: return MeleeMul(c);
            }
        }

        /// <summary>Called after a slash dealt damage (even to 0 enemies): Iaijutsu stuns normal enemies in the arc then is consumed; Accord turns on damage reduction.</summary>
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

        /// <summary>Penance: Ren just got hit -> one more stack.</summary>
        public static void OnHurt(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null || t.kind != WeaponTraitKind.Penance) return;
            int max = Mathf.Max(1, Mathf.RoundToInt(t.threshold * 10f));
            c.PenanceStacks = Mathf.Min(max, c.PenanceStacks + 1);
        }

        /// <summary>Namebound: Mantra cooldown multiplier.</summary>
        public static float CooldownMul(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && t.kind == WeaponTraitKind.Namebound ? 1f - Mathf.Clamp01(t.bonus) : 1f;
        }

        /// <summary>Damage-taken multiplier for Ren (PlayerCombat writes it into Health.DamageTakenMul every frame).</summary>
        public static float TakenMul(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null) return 1f;
            if (t.kind == WeaponTraitKind.LastStand && Active(c)) return 1f - Mathf.Clamp01(t.bonus);
            if (t.kind == WeaponTraitKind.Accord && Active(c)) return 1f - Mathf.Clamp01(t.leech);
            return 1f;
        }

        /// <summary>Last Stand: heavy hits do not stagger / cancel the slash.</summary>
        public static bool Unflinching(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && t.kind == WeaponTraitKind.LastStand && Active(c);
        }

        /// <summary>Hunger: a slash just finished off an enemy. Swords with `status` Mark heal through marked enemies dying (EnemyStatus) — not here.</summary>
        public static void OnMeleeKill(PlayerCombat c)
        {
            var t = Of(c);
            if (t == null || t.kind != WeaponTraitKind.Hunger || c.Health == null || c.Health.IsDead) return;
            if (t.status == StatusKind.Mark) return;
            c.Health.Heal(c.Health.Max * Mathf.Max(0f, t.leech));
            if (c.showVfx) { var v = SpriteVfx.Spawn("m3_blood_orb", c.transform.position + Vector3.up * 0.3f, 0.5f, c.VfxLayer, c.PlayerOrder + 1, false, 16f); }
        }

        /// <summary>An enemy with the Blood-hunger mark (Eventide) just died (from anything) -> Ren heals `leech` of max HP + a blood orb.</summary>
        public static void OnMarkedKill(PlayerCombat c, Vector3 at)
        {
            var t = Of(c);
            if (t == null || t.kind != WeaponTraitKind.Hunger || c.Health == null || c.Health.IsDead) return;
            c.Health.Heal(c.Health.Max * Mathf.Max(0f, t.leech));
            if (c.showVfx) SpriteVfx.Spawn("m3_blood_orb", c.transform.position + Vector3.up * 0.3f, 0.5f, c.VfxLayer, c.PlayerOrder + 1, false, 16f);
        }

        /// <summary>A slash hit an enemy (still alive) -> the sword's `status` effect (per `statusOn`).</summary>
        public static void ApplyHitStatus(PlayerCombat c, EnemyAI ai, bool finisher)
        {
            var t = Of(c);
            if (t == null || t.status == StatusKind.None || ai == null) return;
            bool go = t.statusOn == StatusTrigger.EveryHit || (t.statusOn == StatusTrigger.Finisher && finisher) || (t.statusOn == StatusTrigger.WhileActive && Active(c));
            if (!go) return;
            EnemyStatus.Of(ai).Apply(t.status, t.statusPower, t.statusSeconds, t.statusStacks, c);
        }

        /// <summary>PASSIVE GAUGE for the HUD. fill 0..1 (building / remaining), active = passive on, mark = threshold mark (&lt;0 = none).</summary>
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
                    fill = t.threshold < 1f ? Mathf.Clamp01(1f - (hp - t.threshold) / (1f - t.threshold)) : 1f; active = Active(c); break;   // the lower the HP, the fuller
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

        /// <summary>Passive name of the held sword (HUD).</summary>
        public static string Title(PlayerCombat c) { var t = Of(c); return t != null ? t.title : ""; }
        public static WeaponTraitKind KindOf(PlayerCombat c) { var t = Of(c); return t != null ? t.kind : WeaponTraitKind.None; }
        /// <summary>HP threshold of the passive (Blood Oath / Last Stand) for the HUD mark on the HP bar; none = -1.</summary>
        public static float HpThreshold(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && (t.kind == WeaponTraitKind.BloodOath || t.kind == WeaponTraitKind.LastStand) ? t.threshold : -1f;
        }

        /// <summary>Hunger: flask heal multiplier.</summary>
        public static float FlaskMul(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && t.kind == WeaponTraitKind.Hunger ? 1f - Mathf.Clamp01(t.bonus) : 1f;
        }

        /// <summary>Damage multiplier of the upcoming slash.</summary>
        public static float MeleeMul(PlayerCombat c)
        {
            var t = Of(c);
            return t != null && Active(c) ? 1f + Mathf.Max(0f, t.bonus) : 1f;
        }

        /// <summary>Called RIGHT AFTER a slash dealt damage: consume one-shot passives, life steal.</summary>
        public static void AfterMeleeHit(PlayerCombat c, float dealtTotal, int enemiesHit)
        {
            var t = Of(c);
            if (t == null || enemiesHit <= 0) return;
            if (t.kind == WeaponTraitKind.Counterstroke) c.CounterReady = false;       // only the first slash after a roll
            if (t.kind == WeaponTraitKind.BloodOath && t.leech > 0f && c.Health != null && !c.Health.IsDead)
                c.Health.Heal(dealtTotal * t.leech);
        }
    }
}
