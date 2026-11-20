using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Cột đá trong đấu trường (Map 1 The Stone Ring). Boss lao (BossCombo) đâm vào cột -> cột vỡ, boss choáng.
    /// Gắn tự động cho object có thuộc tính Tiled `pillar = true` (lớp obstacle colliders); `pillar_visual` = tên prop hình cột để ẩn khi vỡ.
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
            if (visual != null) visual.SetActive(false);
            if (col != null) col.enabled = false;
        }

        /// <summary>Cột còn nguyên đang chạm khung thân (nới thêm pad).</summary>
        public static RingPillar Touching(Bounds body, float pad)
        {
            body.Expand(pad);
            foreach (var p in All)
                if (p != null && !p.Broken && p.Bounds.Intersects(body)) return p;
            return null;
        }
    }
}
