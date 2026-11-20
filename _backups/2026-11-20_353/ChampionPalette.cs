using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§350): ĐỔI MÀU LỬA PHÉP của Hollow Champion theo pha lê vừa hút (tím mặc định -> đỏ / lam / lục / vàng).
    /// Builder (Map4EnemyBuilder) điền sẵn `source` = mọi khung hình gốc và mỗi `variants[i].sprites` = khung cùng thứ tự đã đổi màu
    /// (ảnh _tools/m4duo_1119/crystal/recolor.py). Animator vẫn chạy khung gốc; LateUpdate thay ảnh đang hiện bằng khung cùng chỉ số của bản màu.
    /// </summary>
    public sealed class ChampionPalette : MonoBehaviour
    {
        [System.Serializable]
        public class Variant { public string name; public Sprite[] sprites; }

        public Sprite[] source;
        public Variant[] variants;
        private readonly Dictionary<Sprite, int> index = new Dictionary<Sprite, int>();
        private SpriteRenderer sr;
        private Variant active;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
            if (source != null) for (int i = 0; i < source.Length; i++) if (source[i] != null && !index.ContainsKey(source[i])) index[source[i]] = i;
        }

        /// <summary>Bật bản màu `name` (crimson / azure / verdant / gilded); null hoặc "" = màu gốc.</summary>
        public void Set(string name)
        {
            active = null;
            if (string.IsNullOrEmpty(name) || variants == null) return;
            foreach (var v in variants) if (v != null && v.name == name) { active = v; break; }
        }

        private void LateUpdate()
        {
            if (active == null || sr == null || sr.sprite == null || active.sprites == null) return;
            if (index.TryGetValue(sr.sprite, out int i) && i < active.sprites.Length && active.sprites[i] != null) sr.sprite = active.sprites[i];
        }
    }
}
