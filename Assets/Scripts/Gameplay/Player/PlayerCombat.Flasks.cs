using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Ren's HEALING FLASKS. Default 3 slots, at most MaxFlaskSlots (5) for the whole game: Hob's chest (Map 1) +1, Odo's chest (Map 2) +1.
    /// Drinking: PlayerFlask. Heal amount: healPercent (Inspector).
    /// Picking up a flask when full: MapTriggerHandler refuses (FlasksFull).
    /// </summary>
    public partial class PlayerCombat
    {
        public const int MaxFlaskSlots = 5;

        public int Potions => potions;
        public bool FlasksFull => potions >= maxPotions;

        /// <summary>Add `n` flasks, never above the slot count.</summary>
        public void AddPotion(int n) => potions = Mathf.Min(potions + n, Mathf.Max(potions, maxPotions));

        /// <summary>Set the flask count (loading a save), never above the slot count.</summary>
        public void SetPotions(int n) => potions = Mathf.Clamp(n, 0, Mathf.Max(1, maxPotions));

        /// <summary>Refill to exactly the slot count (resting at a campfire, respawn, boss phase cleared).</summary>
        public void RefillPotions() { maxPotions = Mathf.Clamp(maxPotions, 1, MaxFlaskSlots); potions = maxPotions; }

        /// <summary>+`n` flask slots (never above MaxFlaskSlots) and add `n` flasks. Sources: skill scrolls (potions), chests (potions_max).</summary>
        public void AddFlaskSlots(int n)
        {
            if (n <= 0) return;
            maxPotions = Mathf.Max(maxPotions, Mathf.Min(MaxFlaskSlots, maxPotions + n));
            potions = Mathf.Min(potions + n, Mathf.Max(potions, maxPotions));
        }
    }
}
