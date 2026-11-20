using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§271): khung hình thế IAIJUTSU của Ren, lấy từ hoạt ảnh tra kiếm (sheathe) có sẵn — mỗi mảng 8 hướng theo thứ tự
    /// E, NE, N, NW, W, SW, S, SE (giống cây blend DirX / DirY). Ảnh phụ _KatanaUV đi kèm từng khung nên thanh kiếm vẫn đổi theo thiết kế.
    ///   stance : tay đặt lên chuôi, kiếm còn trong bao (khung sheathe 05) — đứng tụ thế.
    ///   drawA  : chuôi vừa rút (khung 04) · drawB : lưỡi nửa ra khỏi bao (khung 03) — chiếu nhanh trước nhát chém rút kiếm.
    /// Dữ liệu: Resources/Weapons/IaiFrames.asset (sinh bằng _tools/lore_1116/iai_frames.py).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Iai Frames")]
    public sealed class IaiFrames : ScriptableObject
    {
        public Sprite[] stance = new Sprite[8];
        public Sprite[] drawA = new Sprite[8];
        public Sprite[] drawB = new Sprite[8];

        private static IaiFrames cached;
        private static bool tried;
        public static IaiFrames Get()
        {
            if (!tried) { tried = true; cached = Resources.Load<IaiFrames>("Weapons/IaiFrames"); }
            return cached;
        }
    }
}
