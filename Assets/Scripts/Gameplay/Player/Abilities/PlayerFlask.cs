using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Healing flask (E): heals healPercent of max HP, locks for healLock seconds (Drink animation). No flask / full HP / Hemorrhage -> no drink.
    /// The flask count lives in PlayerCombat.potions (carried to the next map by GameSession).
    /// </summary>
    internal sealed class PlayerFlask
    {
        private readonly PlayerCombat owner;
        private readonly PlayerAnimation anim;

        /// <summary>Drinking until this time: this lock comes from drinking, not a stun (no stun stars).</summary>
        public float DrinkingUntil { get; private set; }

        public PlayerFlask(PlayerCombat owner, PlayerAnimation anim) { this.owner = owner; this.anim = anim; }

        /// <summary>When set -> the sip is STOLEN: the flask is still used but Ren does not heal, this is called instead
        /// (The Margin: The Registrar heals / adds pages). The fight that sets it clears it when it ends.</summary>
        public static System.Action<PlayerCombat> Stolen;

        public void Drink()
        {
            var health = owner.Health;
            var st = owner.GetComponent<PlayerStatus>();
            if (st != null && st.FlaskTaken) { st.SayRestraint(st.FlaskText); return; }   // flasks confiscated (Map 4)
            if (owner.potions <= 0)
            {
                Hints.Show("Out of potions!", 1.5f);
                SoulRequiem.Core.Sfx.Play("flask_empty", owner.transform.position); // sound
                return;
            }
            if (health.Percent >= 0.999f) return;
            if (st != null && st.HealBlocked)
            {
                Hints.Show("Hemorrhage: the potion will not take!", 1.4f);
                return;
            }
            owner.potions--;
            DrinkingUntil = Time.time + owner.healLock + 0.05f;
            anim.Drink();
            SoulRequiem.Core.Sfx.Play("flask_drink", owner.transform.position);   // sound
            health.Stun(owner.healLock);
            owner.StartCoroutine(Gulp());
        }

        /// <summary>Moment of the SIP in the drink animation (frame 3 = head back, flask at the lips):
        /// sum of DrinkMs frames 0-2 in RenAnimatorBuilder (50 + 60 + 50 ms). HP + VFX are applied exactly then, not on the key press.</summary>
        public const float GulpTime = 0.16f;

        private IEnumerator Gulp()
        {
            yield return new WaitForSeconds(GulpTime);
            var health = owner.Health;
            if (health.IsDead) yield break;
            if (Stolen != null) { Stolen(owner); yield break; }   // The Margin — the flask heals the boss, not Ren
            health.Heal(health.Max * owner.healPercent * WeaponTraits.FlaskMul(owner));   // Hunger
            if (owner.showVfx) HealVfx();
        }

        /// <summary>Green light ring under the feet + plus signs rising, on the head-back drink frame. VFX "Ronin Heal".</summary>
        private void HealVfx()
        {
            var pos = owner.transform.position;
            var b = SpriteVfx.Spawn("heal_back", pos, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder - 1, false, 14f);
            var f = SpriteVfx.Spawn("heal_front", pos, owner.vfxScale, owner.VfxLayer, owner.PlayerOrder + 1, false, 14f);
            if (b != null) b.Follow(owner.transform);
            if (f != null) f.Follow(owner.transform);
        }
    }
}
