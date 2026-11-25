using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// HP + status shared by the player and enemies.
    /// - Poison: % of max HP per second, does NOT stack (a new poison is ignored while poisoned).
    /// - Stun: the character stands still for N seconds (CC-immune enemies / bosses ignore it).
    /// - Knockback / push (Roll): added to an extra velocity that decays; movement scripts read it.
    /// - Invulnerable: on during a Roll (i-frames).
    /// Also: shield (Veil), barrier (armour layer before HP), parry window (Riposte), vulnerability, burn / bleed, slow (Chill), HP floor.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private bool isPlayer = false;
        [SerializeField] private bool immuneToCrowdControl = false;
        [SerializeField] private float knockbackDecay = 20f;
        [Tooltip("Player: after each hit, ignore new hits for N seconds (stops a ring of bullets landing in one frame = instant death)")]
        [SerializeField] private float hitIFrames = 0.25f;
        private float hitImmuneUntil;

        public float Max => maxHp;
        public float Current { get; private set; }
        public float Percent => maxHp > 0f ? Current / maxHp : 0f;
        public bool IsDead => Current <= 0f;
        public bool IsPlayer => isPlayer;
        public bool ImmuneToCrowdControl => immuneToCrowdControl;
        public bool IsStunned => Time.time < stunUntil;
        public float StunTimeLeft => Mathf.Max(0f, stunUntil - Time.time);
        public bool IsPoisoned => poisonRoutine != null;
        /// <summary>Invulnerable: Roll toggles this flag; the shield (<see cref="Shield"/>) has its own timer so rolling never removes it.</summary>
        public bool Invulnerable { get => invulnerable || Time.time < shieldUntil || CutsceneSafe; set => invulnerable = value; }
        /// <summary>The player is always invulnerable during cutscenes (CutsceneState) — independent of the `invulnerable` flag
        /// (PlayerDodge clearing the flag after a roll used to let enemies hit Ren during a boss intro). Poison / burn also pause.</summary>
        private bool CutsceneSafe => isPlayer && CutsceneState.Active;
        private bool invulnerable;
        private float shieldUntil;
        /// <summary>Shield active (Mantra "Veil of the Ascended").</summary>
        public bool Shielded => Time.time < shieldUntil;
        /// <summary>Burning with Ether (Burn).</summary>
        public bool IsBurning => Time.time < burnUntil;
        private float burnUntil, burnDps;
        /// <summary>Chilled (trap ice bullets): multiplies move speed for a while. 1 = normal.</summary>
        public float MoveMul => Time.time < chillUntil ? chillMul : 1f;
        private float chillUntil, chillMul = 1f;

        /// <summary>Slow to <paramref name="mul"/> speed for <paramref name="seconds"/> (keeps the stronger slow).</summary>
        public void Chill(float mul, float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            chillMul = Time.time < chillUntil ? Mathf.Min(chillMul, mul) : mul;
            chillUntil = Mathf.Max(chillUntil, Time.time + seconds);
        }
        private Coroutine burnRoutine;
        /// <summary>BARRIER — a damage layer BEFORE HP (e.g. the Hollow Champion's Soul Armour). Damage (after all multipliers)
        /// goes into the barrier first, the rest into HP; barrier reaching 0 -> <see cref="BarrierBroken"/>. The boss HUD draws it over the HP bar.</summary>
        public float Barrier { get; private set; }
        public float BarrierMax { get; private set; }
        public float BarrierPercent => BarrierMax > 0f ? Barrier / BarrierMax : 0f;
        /// <summary>Barrier colour on the HUD (the Champion changes it with the crystal colour).</summary>
        public Color BarrierColor { get; set; } = new Color(0.62f, 0.38f, 0.95f);
        /// <summary>Last time the barrier absorbed a hit (HUD flash).</summary>
        public float BarrierHitTime { get; private set; } = -99f;
        public event Action<Health> BarrierBroken;
        /// <summary>Set the barrier: max `max`, current `current` (0 = fully off when max = 0).</summary>
        public void SetBarrier(float max, float current)
        {
            BarrierMax = Mathf.Max(0f, max);
            Barrier = Mathf.Clamp(current, 0f, BarrierMax);
        }
        /// <summary>Incoming damage multiplier (1 = normal). The Lantern Knight's vow sets 0.3 on its allies.</summary>
        public float DamageTakenMul { get; set; } = 1f;
        /// <summary>Projectile sets this flag around its TakeDamage call on the player -> the Riposte parry does NOT block bullets
        /// (bullets are for the Turned Blade passive to reflect; the two "timed block" mechanics no longer overlap).</summary>
        public static bool IncomingRanged;
        /// <summary>TrapArt.HurtPlayerCrush sets this flag around its call -> roll invulnerability does NOT save you (rolling over spikes still hurts).</summary>
        public static bool TrapIgnoresRoll;
        /// <summary>Ether veil (Veil of the Ascended) — hits are multiplied by <see cref="Guard"/> mul while the veil lasts.</summary>
        public bool Guarded => Time.time < guardUntil;
        private float guardMul = 1f, guardUntil;
        public void Guard(float mul, float seconds)
        {
            guardMul = Mathf.Clamp01(mul);
            guardUntil = Mathf.Max(guardUntil, Time.time + Mathf.Max(0f, seconds));
        }
        /// <summary>Extra-velocity drag (u/s²) — SkillKit.Pull uses it to compute a pull velocity covering the right distance.</summary>
        public float KnockbackDecay => knockbackDecay;
        /// HP cannot drop below this (e.g. Isolde: cannot die while coffins remain). 0 = normal.
        public float Floor { get; set; }

        public event Action<Health, float> Damaged;
        /// <summary>Every HP loss of ANY Health (health, amount, HP before the hit). Only for test tools (DevCheats Shift+F8).</summary>
        public static event Action<Health, float, float> AnyDamaged;
        public event Action<Health> Died;
        /// <summary>Directional block (e.g. The Last Guard's shield). Receives the hit's push direction (from attacker to self);
        /// returns true = blocked, no HP loss. Only for directional hits (slashes, bullets); poison / burn / bleed still go through.</summary>
        public Func<Vector2, bool> BlockCheck;
        public event Action<Vector2> Blocked;
        /// <summary>Damage of the hit just blocked by the shield (read inside Blocked).</summary>
        public float LastBlockedAmount { get; private set; }
        /// <summary>A real hit (TakeDamage, not poison): (health, amount, push direction, push force). Used for hit animations.</summary>
        public event Action<Health, float, Vector2, float> Hit;
        /// <summary>Called on Revive.</summary>
        public event Action<Health> Revived;

        private float stunUntil;
        // parry stance (Riposte) and exposed openings (Corroded / Spent / Staggered)
        private float parryUntil, vulnUntil, vulnBonus;
        /// <summary>Hit blocked by the parry stance: (damage, knockback direction).</summary>
        public event Action<float, Vector2> Parried;
        public bool Parrying => Time.time < parryUntil;
        /// <summary>Current extra damage taken (0.2 = +20%), 0 when none.</summary>
        public float VulnBonus => Time.time < vulnUntil ? vulnBonus : 0f;
        public float VulnLeft => Mathf.Max(0f, vulnUntil - Time.time);
        /// <summary>Open the parry stance for <paramref name="seconds"/>: the FIRST hit landing in this time is fully blocked (Parried).</summary>
        public void ParryWindow(float seconds) => parryUntil = Time.time + Mathf.Max(0f, seconds);
        public void EndParry() => parryUntil = 0f;
        /// <summary>Exposed: takes <paramref name="bonus"/> extra damage for <paramref name="seconds"/> s. No stacking: keeps the highest, extends the time.</summary>
        public void AddVulnerability(float bonus, float seconds)
        {
            if (IsDead || bonus <= 0f || seconds <= 0f) return;
            vulnBonus = Time.time < vulnUntil ? Mathf.Max(vulnBonus, bonus) : bonus;
            vulnUntil = Mathf.Max(vulnUntil, Time.time + seconds);
        }
        private Vector2 extraVelocity;
        private Coroutine poisonRoutine;
        private Coroutine flashRoutine;
        private SpriteRenderer sr;
        private Color baseColor = Color.white;

        private void Awake()
        {
            Current = maxHp;
            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        public void Configure(float hp, bool player, bool immuneCC)
        {
            maxHp = hp;
            Current = hp;
            isPlayer = player;
            immuneToCrowdControl = immuneCC;
        }

        /// <summary>Returns true if the hit really landed (not invulnerable, not dead).</summary>
        public bool TakeDamage(float amount, Vector2 knockbackDir = default, float knockbackForce = 0f)
        {
            // mechanical traps (spikes, rollers, saw blades, water jets) ignore ROLL invulnerability — only the shield / cutscenes block them
            bool blocked = TrapIgnoresRoll && isPlayer ? (Time.time < shieldUntil || CutsceneSafe) : Invulnerable;
            if (IsDead || blocked || amount <= 0f) return false;
            if (!isPlayer && BlockCheck != null && knockbackDir.sqrMagnitude > 0.0001f && BlockCheck(knockbackDir))   // shield
            {
                LastBlockedAmount = amount;                                   // the shield cracks with the damage it blocks
                Blocked?.Invoke(knockbackDir);
                SfxHooks.Blocked(this);   // sound
                return false;
            }
            if (isPlayer && Time.time < parryUntil && !IncomingRanged)  // Riposte — blocks the first melee hit
            {
                parryUntil = 0f;
                Parried?.Invoke(amount, knockbackDir);
                SfxHooks.Parried(this);   // sound
                return false;
            }
            if (isPlayer && Time.time < hitImmuneUntil) return false;
            amount *= Mathf.Max(0f, DamageTakenMul) * (1f + VulnBonus) * (Guarded ? guardMul : 1f);
            if (isPlayer) hitImmuneUntil = Time.time + hitIFrames;
            if (Barrier > 0f && amount > 0f)                                  // the barrier absorbs first
            {
                float soak = Mathf.Min(Barrier, amount);
                Barrier -= soak; amount -= soak;
                BarrierHitTime = Time.time;
                if (Barrier <= 0.001f) { Barrier = 0f; BarrierBroken?.Invoke(this); }
            }
            ApplyDamage(amount, true);
            if (!immuneToCrowdControl && knockbackForce > 0f && knockbackDir.sqrMagnitude > 0.0001f)
                extraVelocity += knockbackDir.normalized * knockbackForce;
            SfxHooks.Hit(this);   // sound
            if (!IsDead) Hit?.Invoke(this, amount, knockbackDir, knockbackForce);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHp, Current + amount);
        }

        /// <summary>Set current HP (carries HP over from the previous map). Does not revive if down.</summary>
        public void SetCurrent(float value)
        {
            if (IsDead) return;
            Current = Mathf.Clamp(value, 1f, maxHp);
        }

        public void Revive()
        {
            Current = maxHp;
            stunUntil = 0f;
            extraVelocity = Vector2.zero;
            if (poisonRoutine != null) { StopCoroutine(poisonRoutine); poisonRoutine = null; }
            if (burnRoutine != null) { StopCoroutine(burnRoutine); burnRoutine = null; }
            burnUntil = 0f; burnDps = 0f; shieldUntil = 0f;
            parryUntil = 0f; vulnUntil = 0f; vulnBonus = 0f;
            if (sr != null) sr.color = baseColor;
            Revived?.Invoke(this);
        }

        public void Stun(float seconds)
        {
            if (immuneToCrowdControl || IsDead || Invulnerable || seconds <= 0f) return;
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
        }

        /// <summary>Poison: percentPerSecond (e.g. 0.02 = 2%/s) for duration seconds. Does not stack.</summary>
        public void ApplyPoison(float percentPerSecond, float duration)
        {
            if (IsDead || Invulnerable || poisonRoutine != null || percentPerSecond <= 0f) return;
            poisonRoutine = StartCoroutine(PoisonRoutine(percentPerSecond, duration));
            if (isPlayer) FirstTimeHints.Show("tut_poison", "Poisoned! Your health drains for a while (green tint). Keep your distance and drink ({heal}) if it runs low.", 5f);
        }

        /// <summary>Cure poison at once (the healer's green potion).</summary>
        public void CurePoison()
        {
            if (poisonRoutine != null) { StopCoroutine(poisonRoutine); poisonRoutine = null; }
            if (sr != null) sr.color = baseColor;
        }

        /// <summary>Invulnerable for <paramref name="seconds"/> (shield). Not cleared by Roll.</summary>
        public void Shield(float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            shieldUntil = Mathf.Max(shieldUntil, Time.time + seconds);
        }

        /// <summary>Burn (Ether fire): <paramref name="dps"/> damage / second for <paramref name="duration"/> seconds.
        /// Re-applied while burning: extends the time, keeps the higher dps (no stacking).</summary>
        /// <summary>Flash colour while burning (default Ether blue; Accord holy light = gold).</summary>
        private Color burnTint = new Color(0.55f, 0.8f, 1f, 1f);

        /// <summary>Burn with its own flash colour (e.g. the holy fire of Arthur's Accord).</summary>
        public void ApplyBurn(float dps, float duration, Color tint) { burnTint = tint; ApplyBurn(dps, duration); }

        /// <summary>Damage over time (bleed...) — no knockback, no flinch.</summary>
        public void ApplyDot(float amount) { if (!IsDead && !Invulnerable && amount > 0f && !CutsceneSafe) ApplyDamage(amount, false); }

        /// <summary>Body colour flash (status effect).</summary>
        public void TintPulse(Color c, float seconds) { if (sr != null && !IsDead) StartCoroutine(Tint(c, seconds)); }

        public void ApplyBurn(float dps, float duration)
        {
            if (IsDead || Invulnerable || dps <= 0f || duration <= 0f) return;
            burnDps = IsBurning ? Mathf.Max(burnDps, dps) : dps;
            burnUntil = Mathf.Max(burnUntil, Time.time + duration);
            if (burnRoutine == null) burnRoutine = StartCoroutine(BurnRoutine());
        }

        private IEnumerator BurnRoutine()
        {
            Color tint = burnTint;
            while (!IsDead && Time.time < burnUntil)
            {
                yield return new WaitForSeconds(0.5f);
                if (IsDead) break;
                if (CutsceneSafe) continue;                          // cutscene running -> skip this burn tick
                ApplyDamage(burnDps * 0.5f, false);
                if (sr != null) StartCoroutine(Tint(tint, 0.12f));
            }
            burnDps = 0f;
            burnRoutine = null;
        }

        /// <summary>Active push (e.g. Roll) — not blocked by CC immunity.</summary>
        public void AddImpulse(Vector2 velocity) => extraVelocity += velocity;

        /// <summary>Movement scripts call this every FixedUpdate to get the extra velocity (knockback / Roll).</summary>
        public Vector2 ConsumeExtraVelocity(float dt)
        {
            Vector2 v = extraVelocity;
            extraVelocity = Vector2.MoveTowards(extraVelocity, Vector2.zero, knockbackDecay * dt);
            return v;
        }

        private IEnumerator PoisonRoutine(float pct, float duration)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
            Color poisonTint = new Color(0.6f, 1f, 0.5f, 1f);
            for (int i = 0; i < ticks && !IsDead; i++)
            {
                yield return new WaitForSeconds(1f);
                if (IsDead) break;
                if (CutsceneSafe) continue;                          // cutscene running -> skip this poison tick
                ApplyDamage(maxHp * pct, false);
                if (sr != null) StartCoroutine(Tint(poisonTint, 0.15f));
            }
            poisonRoutine = null;
        }

        private void ApplyDamage(float amount, bool flash)
        {
            float before = Current;
            Current = Mathf.Max(Mathf.Min(Floor, Current), Current - amount);
            AnyDamaged?.Invoke(this, amount, before);
            if (Current < 0f) Current = 0f;
            if (flash && sr != null)
            {
                if (flashRoutine != null) StopCoroutine(flashRoutine);
                flashRoutine = StartCoroutine(Tint(new Color(1f, 0.45f, 0.45f, 1f), 0.1f));
            }
            Damaged?.Invoke(this, amount);
            if (Current <= 0f) { SfxHooks.Died(this); Died?.Invoke(this); }   // sound
        }

        private IEnumerator Tint(Color c, float seconds)
        {
            sr.color = c;
            yield return new WaitForSeconds(seconds);
            if (sr != null) sr.color = baseColor;
            flashRoutine = null;
        }
    }
}
