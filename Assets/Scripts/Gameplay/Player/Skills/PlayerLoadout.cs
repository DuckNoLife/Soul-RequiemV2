using System;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// The player's equipped skills: SlotCount slots (keys Skill1/2/3), the list of owned skills, per-slot cooldowns.
    ///   - Skill library = every SkillDefinition in Resources/Skills (no assets -> creates a default Ground Slam).
    ///   - Changing skills: Equip(slot, skill) — only allowed while resting at a checkpoint (checked by RequiemMenus).
    ///   - Across maps: GameSession keeps the owned skill ids + the equipped set.
    /// Attached to the player automatically (PlayerCombat.Awake).
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        public const int SlotCount = 3;

        public SkillDefinition[] slots = new SkillDefinition[SlotCount];
        public List<SkillDefinition> owned = new List<SkillDefinition>();

        private readonly float[] readyAt = new float[SlotCount];
        private PlayerCombat combat;
        private static SkillDefinition[] library;

        public event Action Changed;
        /// Slot just used (the HUD plays a press effect).
        public event Action<int> Cast;

        public static SkillDefinition[] Library
        {
            get
            {
                if (library != null && library.Length > 0) return library;
                library = Resources.LoadAll<SkillDefinition>("Skills");
                if (library == null || library.Length == 0)
                {
                    var g = ScriptableObject.CreateInstance<GroundSlamSkill>();
                    g.id = "ground_slam"; g.displayName = "Earthshatter"; g.iconName = "ic_skill_slam";
                    g.description = "Stamp the earth. A ring of force strikes and throws back every foe around you.";
                    library = new SkillDefinition[] { g };
                }
                return library;
            }
        }

        public static SkillDefinition Find(string id)
        {
            foreach (var s in Library) if (s != null && s.id == id) return s;
            return null;
        }

        /// <summary>The skill belongs to a set (setId not empty).</summary>
        public static bool InSet(SkillDefinition s) => s != null && !string.IsNullOrEmpty(s.setId);

        /// <summary>Every library skill in the same set as <paramref name="s"/>.</summary>
        public static List<SkillDefinition> SetOf(SkillDefinition s)
        {
            var list = new List<SkillDefinition>();
            if (!InSet(s)) return list;
            foreach (var x in Library) if (x != null && x.setId == s.setId) list.Add(x);
            return list;
        }

        /// <summary>The set's lead skill (lowest setSlot) — holds the name / description of the whole set.</summary>
        public static SkillDefinition SetLeader(SkillDefinition s)
        {
            SkillDefinition best = null;
            foreach (var m in SetOf(s)) if (best == null || m.setSlot < best.setSlot) best = m;
            return best;
        }

        /// <summary>Display name of the set (setName of the lead skill; empty -> "Mantra set").</summary>
        public static string SetName(SkillDefinition s)
        {
            var l = SetLeader(s);
            return l != null && !string.IsNullOrEmpty(l.setName) ? l.setName : "Mantra set";
        }

        public static PlayerLoadout Of(Component c)
        {
            var l = c.GetComponent<PlayerLoadout>();
            return l != null ? l : c.gameObject.AddComponent<PlayerLoadout>();
        }

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            if (owned.Count == 0)
                foreach (var s in Library) if (s != null && s.unlockedAtStart) owned.Add(s);
            bool any = false;
            foreach (var s in slots) if (s != null) any = true;
            if (!any && owned.Count > 0) slots[0] = owned[0];
        }

        private void Start() => GameSession.ApplyLoadout(this);

        public SkillDefinition SkillAt(int i) => i >= 0 && i < SlotCount ? slots[i] : null;
        public float CooldownLeft(int i) => SkillAt(i) != null ? Mathf.Max(0f, readyAt[i] - Time.time) : 0f;
        public float CooldownTotal(int i) => SkillAt(i) != null ? Mathf.Max(0.01f, SkillAt(i).cooldown * CdMul) : 1f;
        private float CdMul => WeaponTraits.CooldownMul(combat != null ? combat : GetComponent<PlayerCombat>());
        public bool Owns(SkillDefinition s) => s != null && owned.Contains(s);

        /// Slot key `i` pressed: check cooldown / locks, then cast.
        public bool TryCast(int i)
        {
            var s = SkillAt(i);
            if (s != null && combat != null && s.TryStop(combat)) return true;   // pressed again = stop a sustained skill
            if (s == null || combat == null || Time.time < readyAt[i] || combat.IsAttacking) return false;
            var st = GetComponent<PlayerStatus>();
            if (st != null && st.Silenced)
            {
                Hints.Show("Your mantras are sealed!", 1.2f);
                return false;
            }
            if (st != null && st.MantrasBound) { st.SayRestraint(st.MantraSealText); return false; }   // sealed by the map (MapRestraint)
            readyAt[i] = Time.time + s.cooldown * CdMul;                        // Namebound passive
            int slot = i;
            combat.StartCoroutine(s.Cast(combat, () => readyAt[slot] = Time.time));
            Cast?.Invoke(i);
            SfxHooks.Mantra(s, combat.transform.position); // sound
            return true;
        }

        /// Equip a skill in a slot (if it is already in another slot, swap). null = unequip.
        public void Equip(int slot, SkillDefinition s)
        {
            if (slot < 0 || slot >= SlotCount) return;
            if (s != null && !Owns(s)) return;
            // Skill sets: equipping one skill equips the whole set, each skill in its own setSlot (takes all 3 slots)
            if (InSet(s))
            {
                for (int i = 0; i < SlotCount; i++) slots[i] = null;
                foreach (var m in SetOf(s))
                    if (Owns(m) && m.setSlot >= 0 && m.setSlot < SlotCount) slots[m.setSlot] = m;
                Changed?.Invoke();
                return;
            }
            // Slot belonging to a set: remove the whole set before equipping a single skill / unequipping
            if (InSet(slots[slot]))
            {
                string set = slots[slot].setId;
                for (int i = 0; i < SlotCount; i++) if (slots[i] != null && slots[i].setId == set) slots[i] = null;
            }
            int other = Array.IndexOf(slots, s);
            if (s != null && other >= 0 && other != slot)
            {
                slots[other] = slots[slot];
                (readyAt[other], readyAt[slot]) = (readyAt[slot], readyAt[other]);
            }
            slots[slot] = s;
            Changed?.Invoke();
        }

        /// <summary>Refund <paramref name="fraction"/> of the remaining cooldown of skill <paramref name="s"/> (e.g. a successful Riposte parry).</summary>
        public void Refund(SkillDefinition s, float fraction)
        {
            int i = Array.IndexOf(slots, s);
            if (i < 0 || fraction <= 0f) return;
            float left = Mathf.Max(0f, readyAt[i] - Time.time);
            readyAt[i] = Time.time + left * (1f - Mathf.Clamp01(fraction));
        }

        public void Unlock(SkillDefinition s, bool autoEquip = true)
        {
            if (s == null) return;
            if (InSet(s))                                  // unlocking one skill of a set = unlocks the whole set (and equips it)
            {
                bool added = false;
                foreach (var m in SetOf(s)) if (!owned.Contains(m)) { owned.Add(m); added = true; }
                if (!added) return;
                if (autoEquip) Equip(0, s); else Changed?.Invoke();
                return;
            }
            if (owned.Contains(s)) return;
            owned.Add(s);
            if (autoEquip)
                for (int i = 0; i < SlotCount; i++)
                    if (slots[i] == null) { slots[i] = s; break; }
            Changed?.Invoke();
        }

        public void Unlock(string id) => Unlock(Find(id));

        public void NotifyChanged() => Changed?.Invoke();
    }
}
