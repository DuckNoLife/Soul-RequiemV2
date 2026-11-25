using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// RECOLOURS the PURPLE parts of the Hollow Champion (hood, soul smoke, fist gems) to match the crystal it just absorbed.
    /// Swapping sprites in LateUpdate did not change the colour in game, so Map4EnemyBuilder builds one
    /// AnimatorOverrideController per colour instead (same clips as the original but with recoloured frames - made by _tools/m4duo_1119/crystal/recolor.py).
    /// Set() swaps the Animator controller while keeping the current state + time (no animation hitch).
    /// Missing data (Enemies ▸ Map 4 not rerun yet) -> one warning in the Console.
    /// </summary>
    public sealed class ChampionPalette : MonoBehaviour
    {
        [System.Serializable]
        public class Variant { public string name; public AnimatorOverrideController controller; }

        public RuntimeAnimatorController baseController;
        public Variant[] variants;
        private Animator anim;
        private string active = "";

        private void Awake()
        {
            anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (baseController == null && anim != null) baseController = anim.runtimeAnimatorController;
            if (variants == null || variants.Length == 0)
                Debug.LogWarning("[Champion] ChampionPalette has no colour variants - rerun the menu Soul Requiem > Enemies > Map 4.");
        }

        /// <summary>Switch to colour variant `name` (crimson / azure / verdant / gilded); null or "" = the original purple.</summary>
        public void Set(string name)
        {
            name = name ?? "";
            if (anim == null || name == active) return;
            RuntimeAnimatorController want = baseController;
            if (name.Length > 0 && variants != null)
                foreach (var v in variants) if (v != null && v.name == name && v.controller != null) { want = v.controller; break; }
            active = name;
            if (want == null || anim.runtimeAnimatorController == want) return;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            anim.runtimeAnimatorController = want;
            anim.Play(info.fullPathHash, 0, info.loop ? info.normalizedTime % 1f : Mathf.Min(info.normalizedTime, 1f));
            anim.Update(0f);
        }
    }
}
