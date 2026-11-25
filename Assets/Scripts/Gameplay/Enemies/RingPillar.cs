using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Stone pillar in the arena (Map 1 The Stone Ring). A charging boss (BossCombo) that rams it breaks the pillar and is stunned.
    /// Added automatically to objects with the Tiled property `pillar = true` (obstacle collider layer); `pillar_visual` = name of the pillar prop hidden when it breaks.
    /// </summary>
    public class RingPillar : MonoBehaviour
    {
        public static readonly List<RingPillar> All = new List<RingPillar>();

        public GameObject visual;
        public bool Broken { get; private set; }

        private Collider2D col;

        private void Awake() => col = GetComponentInChildren<Collider2D>();
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        public Bounds Bounds => col != null ? col.bounds : new Bounds(transform.position, Vector3.one * 0.3f);

        public void Break()
        {
            if (Broken) return;
            Broken = true;
            SoulRequiem.Core.Sfx.Play("rock_crash", transform.position); // sound: pillar breaks
            SoulRequiem.Core.Sfx.Play("break_stone", transform.position);
            if (visual != null) visual.SetActive(false);
            if (col != null) col.enabled = false;
        }

        /// <summary>An intact pillar touching the body box (expanded by pad).</summary>
        public static RingPillar Touching(Bounds body, float pad)
        {
            body.Expand(pad);
            foreach (var p in All)
                if (p != null && !p.Broken && p.Bounds.Intersects(body)) return p;
            return null;
        }
    }
}
