#if UNITY_EDITOR || DEVELOPMENT_BUILD
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Lệnh test (DevCheats: F5 — kèm nhận mọi chìa của map). Chỉ có trong Editor / Development Build.</summary>
    public partial class MapMechanics
    {
        /// <summary>Bật mọi cờ mà câu đố / vật cản đặt ra (kể cả tay quay Map 2).</summary>
        public void DebugSolveAll()
        {
            if (ctx == null) return;
            ctx.Flags.Set("lever_handle");
            foreach (var m in mechs) m.DebugSolve();
            Hints.Show("[TEST] All puzzles and barricades solved", 2f);
        }
    }
}
#endif
