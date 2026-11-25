using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Ren's WEAPON + PASSIVES:
    ///   • Equip(w): change the katana (WeaponDefinition), remembered across maps (WeaponRing).
    ///   • Timers / stacks the passives need (read / written by WeaponTraits): Counterstroke, Vigil, Penance, Iaijutsu, Accord.
    ///   • Book The Rampart Forms (flag book_rampart): standing still 0.5 s -> 20 % less damage taken until moving.
    /// Passive formulas: Weapons/WeaponTrait.cs (WeaponTraits).
    /// </summary>
    public partial class PlayerCombat
    {
        /// <summary>Weapon sprite layer (blade attached to the hand per frame).</summary>
        public PlayerWeaponView WeaponView { get; private set; }
        public WeaponDefinition Weapon => weapon;

        /// <summary>Change weapon (null = default Katana). A combo in progress is cancelled.</summary>
        public void Equip(WeaponDefinition w)
        {
            weapon = w != null ? w : WeaponDefinition.LoadDefault();
            if (weapon != null) WeaponRing.SetEquipped(weapon.id);
            traitLit = false;
            if (WeaponView != null) WeaponView.TraitGlow = 1f;                // drop the old sword's passive glow
            melee.ResetCombo();
            if (WeaponView != null) WeaponView.SetWeapon(weapon);
        }

        // ================================================================== numbers for the passives (WeaponTraits)
        /// <summary>Start time of the last roll (Counterstroke).</summary>
        public float LastRollTime { get; internal set; } = -99f;
        /// <summary>The last roll has not used its counter yet (Counterstroke only empowers the first slash).</summary>
        public bool CounterReady { get; internal set; }
        /// <summary>Time of the last hit taken (Vigil).</summary>
        public float LastHurtTime { get; private set; } = -99f;
        /// <summary>Penance stacks built up.</summary>
        public int PenanceStacks { get; internal set; }
        /// <summary>Start time of the last slash (Iaijutsu).</summary>
        public float LastSwingTime { get; internal set; } = -99f;
        /// <summary>The current slash is a draw strike (Iaijutsu).</summary>
        public bool IaiCharged { get; internal set; }
        /// <summary>Holding the Iaijutsu stance (IaiStance draws the charge bar beside Ren).</summary>
        public bool IaiHolding { get; internal set; }
        /// <summary>Draw strike power 0..1 (0 = just past the threshold, 1 = full bar).</summary>
        public float IaiPower { get; internal set; }
        /// <summary>Iaijutsu charge bar 0..1.</summary>
        public float IaiCharge { get; internal set; }
        /// <summary>Accord: reduced damage taken until this time.</summary>
        public float AccordUntil { get; internal set; } = -99f;

        private bool traitLit;

        private void OnDamagedTrait(Health h, float amount) { LastHurtTime = Time.time; WeaponTraits.OnHurt(this); }

        /// <summary>A passive is active -> the blade glows brighter.</summary>
        private void UpdateTraitGlow()
        {
            bool lit = WeaponTraits.Active(this);
            if (lit == traitLit) return;
            traitLit = lit;
            if (WeaponView != null) WeaponView.TraitGlow = lit ? 1.75f : 1f;
        }

        // ================================================================== book The Rampart Forms
        public const string RampartFlag = "book_rampart";
        private const float RampartDelay = 0.5f, RampartMulValue = 0.8f;
        private float stillTime;
        private bool rampartOn;
        public bool RampartActive => rampartOn;

        /// <summary>Rampart damage-taken multiplier (1 = no reduction). On entering the stance: the body flashes steel blue.</summary>
        private float RampartMul()
        {
            var dir = MapEncounterDirector.Instance;
            bool learned = GameSession.Books.Contains(RampartFlag) || (dir != null && dir.Flags != null && dir.Flags.Has(RampartFlag));
            if (!learned || Health == null || Health.IsDead) { stillTime = 0f; rampartOn = false; return 1f; }
            if (GameInput.Move.sqrMagnitude > 0.01f) stillTime = 0f; else stillTime += Time.deltaTime;
            bool on = stillTime >= RampartDelay;
            if (on && !rampartOn) Health.TintPulse(new Color(0.62f, 0.78f, 1f, 1f), 0.15f);
            rampartOn = on;
            return on ? RampartMulValue : 1f;
        }
    }
}
