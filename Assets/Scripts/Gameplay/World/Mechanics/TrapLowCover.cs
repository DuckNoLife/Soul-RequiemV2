using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Marks a LOW collider (an unflipped table): blocks walking but bullets fly over (Projectile ignores colliders with this component).
    /// </summary>
    public sealed class TrapLowCover : MonoBehaviour { }
}
