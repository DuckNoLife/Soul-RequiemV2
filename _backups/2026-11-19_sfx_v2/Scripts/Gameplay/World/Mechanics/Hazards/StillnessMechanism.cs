using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§261): stillness — VÙNG CẢM BIẾN CHUYỂN ĐỘNG. Ren ở trong vùng và ĐANG DI CHUYỂN (kể cả lướt) -> phát xung `fires`
    /// (mặc định = tên object) cho các bẫy có `trigger` trùng tên (xem <see cref="TrapSignals"/>). Đứng yên = im lặng.
    /// Dùng cho The Still Walk (Map 1b): máy bắn tên của người ẩn sĩ "chỉ nghe thấy kẻ đang bước".
    ///   speed_min   tốc độ (đơn vị Unity / giây) coi là "đang bước"; mặc định 0.35 (Ren đi bộ ~2)
    ///   rearm       giây nghỉ giữa hai xung khi Ren vẫn bước; mặc định 1.0
    ///   hint        chữ hiện lần đầu bước vào vùng (để trống = không hiện)
    ///   start_when / stop_when   lớp cờ như các bẫy khác (stop_when -> tắt hẳn)
    /// Quái đi trong vùng KHÔNG kích bẫy (chỉ Ren).
    /// </summary>
    public sealed class StillnessMechanism : HazardMechanismBase
    {
        private Vector2 last;
        private bool hasLast, announced;
        private float readyAt;

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = hasLast ? (playerPos - last).magnitude / dt : 0f;
            last = playerPos;
            hasLast = true;
            if (Done || !inside || !Armed()) return;

            if (!announced)
            {
                announced = true;
                string h = Str("hint", "");
                if (h.Length > 0) Hints.Show(h, 3f);
            }

            // dịch chuyển tức thời (nạp cảnh / passage) cho tốc độ rất lớn -> bỏ qua khung đó
            if (speed > 40f) return;
            if (speed < Float("speed_min", 0.35f) || Time.time < readyAt) return;
            readyAt = Time.time + Mathf.Max(0.2f, Float("rearm", 1.0f));
            TrapSignals.Fire(Str("fires", Name));
        }

        internal override void DebugSolve() { }
    }
}
