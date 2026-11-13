using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>03/11: tự chạy hiệu ứng nhịp của vòng sáng NoteMarker khi không có cơ quan nào gọi Animate (vd vòng sáng dưới đồ nhặt).
    /// 04/11: `hideWhen` -> tự tắt khi điều kiện đúng (vd điểm nghỉ đã thắp).</summary>
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
