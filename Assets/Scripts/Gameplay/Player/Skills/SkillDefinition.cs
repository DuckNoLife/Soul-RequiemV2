using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// One equippable skill (data = an asset in Resources/Skills/*.asset).
    /// New skill: create a class inheriting SkillDefinition (write Cast), then Create ▸ Soul Requiem ▸ Skill ▸ ... and put it in Resources/Skills.
    /// The player equips up to PlayerLoadout.SlotCount skills; changed at rest points (checkpoints).
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        [Tooltip("Unique id, used to save the loadout across maps (e.g. ground_slam).")]
        public string id = "skill";
        public string displayName = "Mantra";
        [TextArea(2, 5)] public string description = "";
        [Tooltip("Icon on the HUD / skill panel. Empty = load Resources/UI/Requiem/<iconName>.")]
        public Texture2D icon;
        public string iconName = "";
        [Tooltip("Accent colour of the skill (cooldown ring, ready outline).")]
        public Color accent = new Color(0.55f, 0.75f, 1f);
        public float cooldown = 5f;
        [Tooltip("Owned from the start of the game (no skill book needed).")]
        public bool unlockedAtStart = true;
        [Tooltip("Skill set: skills with the same setId are equipped / removed together, each in slot setSlot (0 = slot I, 1 = slot II, 2 = slot III). Empty = single skill.")]
        public string setId = "";
        public int setSlot = -1;
        /// <summary>Name + description of the whole set (the rest panel shows the set as ONE card). Only needed on the setSlot 0 skill.</summary>
        public string setName = "";
        [TextArea(2, 5)] public string setDescription = "";

        // Shrine panel — skill kind + numbers (reads the asset's real values). Distances converted to tiles (1 tile = 0.32 u).
        public virtual string Kind => "Mantra";
        public virtual void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o) { }
        protected static void Stat(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o, string label, string value) => o.Add(new System.Collections.Generic.KeyValuePair<string, string>(label, value));
        protected static string Tiles(float u) => (u / 0.32f).ToString("0.#") + " tiles";
        protected static string Secs(float s) => s.ToString("0.##") + "s";
        protected static string Pct(float f) => Mathf.RoundToInt(f * 100f) + "%";

        public Texture2D Icon
        {
            get
            {
                if (icon == null && !string.IsNullOrEmpty(iconName)) icon = Resources.Load<Texture2D>("UI/Requiem/" + iconName);
                return icon;
            }
        }

        /// the key of a sustained skill pressed again (e.g. Judgement Ray) -> stop early. Return true if handled (does not count as a cast).
        public virtual bool TryStop(PlayerCombat p) => false;

        /// Cast. `cancel` is called when the skill is cancelled before taking effect (Roll / stun) -> cooldown refunded.
        public abstract IEnumerator Cast(PlayerCombat p, Action cancel);
    }
}
