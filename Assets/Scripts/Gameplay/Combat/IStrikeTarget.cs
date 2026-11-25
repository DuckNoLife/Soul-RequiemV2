using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// A STRIKABLE object that is not an EnemyAI enemy (ink creatures drawn by The Registrar, Ren's drafts...).
    /// Put it on a component on the same object (or a parent) as a TRIGGER Collider2D; PlayerStrikes.Damage also scans triggers and calls TakeStrike.
    /// Shared by every map.
    /// </summary>
    public interface IStrikeTarget
    {
        /// <summary>Receive one sword hit. Return true if it really hit (for hit-stop / camera kick).</summary>
        bool TakeStrike(float damage, Vector2 dir);
    }
}
