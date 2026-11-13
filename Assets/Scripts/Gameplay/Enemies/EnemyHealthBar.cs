using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Đánh dấu quái thường cần thanh máu trên đầu (boss/miniboss treatAsBoss dùng thanh boss lớn của HUD).
    /// RequiemHUD vẽ thanh + chỉ số "máu/tối đa" cho mọi quái trong danh sách All (luôn hiện khi còn sống và trong màn hình).
    /// Độ cao đầu lấy từ lưới Tight của sprite Idle (bỏ phần ô trong suốt).
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        public static readonly List<EnemyHealthBar> All = new List<EnemyHealthBar>();

        public Health Health { get; private set; }
        private float top = 0.5f;

        /// Điểm thế giới ngay trên đỉnh đầu.
        public Vector3 HeadWorld => transform.position + Vector3.up * (top + 0.08f);

        public static void Attach(EnemyAI ai)
        {
            if (ai == null || ai.IsBoss || ai.GetComponent<EnemyHealthBar>() != null) return;
            ai.gameObject.AddComponent<EnemyHealthBar>();
        }

        private SpriteRenderer body;
        private bool init;

        private void Start()
        {
            Health = GetComponent<Health>();
            var ai = GetComponent<EnemyAI>();
            body = ai != null && ai.Body != null ? ai.Body : null;
            if (body == null)       // tìm sprite thân lớn nhất (bỏ bóng đổ / VFX con)
                foreach (var r in GetComponentsInChildren<SpriteRenderer>())
                    if (r.sprite != null && (body == null || r.bounds.size.y > body.bounds.size.y)) body = r;
        }

        /// 28/10: đỉnh đầu theo khung hình hiện tại (bounds thế giới của sprite thân), làm mượt để thanh không nhảy theo hoạt ảnh.
        private void LateUpdate()
        {
            if (body == null || body.sprite == null) return;
            // 08/11 (§190): đỉnh theo lưới Tight của khung hiện tại (phần có điểm ảnh) — bounds cả khung 128 px làm thanh máu Grave Worm
            // lơ lửng cao ~1 đv trên đầu (ảnh người dùng: thanh 90/90 nằm giữa miệng hố, con sâu ở dưới)
            float target = TightTop(body) - transform.position.y;
            if (!init) { top = target; init = true; }
            else top = Mathf.Lerp(top, target, 1f - Mathf.Exp(-Time.deltaTime * 4f));
        }

        private static readonly Dictionary<Sprite, float> tightTop = new Dictionary<Sprite, float>();
        /// Đỉnh thế giới của phần có điểm ảnh (đỉnh cao nhất của lưới sprite; lưới FullRect thì bằng bounds như cũ).
        private static float TightTop(SpriteRenderer r)
        {
            var s = r.sprite;
            if (!tightTop.TryGetValue(s, out float top))
            {
                top = float.MinValue;
                foreach (var v in s.vertices) if (v.y > top) top = v.y;
                if (tightTop.Count > 2048) tightTop.Clear();
                tightTop[s] = top;
            }
            return r.transform.TransformPoint(new Vector3(0f, top, 0f)).y;
        }
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }
    }
}
