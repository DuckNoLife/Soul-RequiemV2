using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>Runs the NoteMarker glow ring pulse by itself when no mechanism calls Animate (e.g. the glow ring under a pickup).
    /// `hideWhen` -> turns itself off once the condition is true (e.g. the rest point is lit).</summary>
    public class MarkerPulse : MonoBehaviour
    {
        public System.Func<bool> hideWhen;

        private void Update()
        {
            if (hideWhen != null && hideWhen()) { gameObject.SetActive(false); return; }
            NoteMarker.Animate(gameObject);
        }
    }
}
