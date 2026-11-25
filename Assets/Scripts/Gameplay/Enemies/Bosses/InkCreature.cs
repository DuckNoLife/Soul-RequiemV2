using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Something The Registrar DRAWS that Ren can cut (paper cranes, older ink beasts). One slash destroys it.
    /// Holds state only; movement / visuals are driven by RegistrarSkills (it checks Alive / WasStruck every frame).
    /// Needs a TRIGGER Collider2D on the same object (PlayerStrikes scans triggers for IStrikeTarget).
    /// </summary>
    public sealed class InkCreature : MonoBehaviour, IStrikeTarget
    {
        /// <summary>Alive (not cut / not dissolved yet).</summary>
        public bool Alive = true;
        /// <summary>Still a SKETCH (not inked) or flying high -> slashes miss.</summary>
        public bool Strikable = true;
        /// <summary>Cut by Ren (as opposed to dissolving when its time runs out).</summary>
        public bool WasStruck;

        public bool TakeStrike(float damage, Vector2 dir)
        {
            if (!Alive || !Strikable) return false;
            Alive = false;
            WasStruck = true;
            Sfx.Play("ink_splash", transform.position, 0.85f);
            return true;
        }
    }
}
