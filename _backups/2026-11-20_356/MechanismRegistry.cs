using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Bảng "Type trong Tiled" -> class cơ quan. Thêm loại mới: một dòng ở đây.
    /// Type không có trong bảng -> cảnh báo trong Console (thường là gõ sai trong Tiled) và coi như PassiveMechanism.
    /// </summary>
    internal static class MechanismRegistry
    {
        private static readonly Dictionary<string, Func<Mechanism>> Factories = new Dictionary<string, Func<Mechanism>>
        {
            // vùng nguy hiểm
            { "spout", () => new SpoutMechanism() },
            { "vent", () => new VentMechanism() },
            { "hazard", () => new HazardMechanism() },
            { "plate", () => new PlateMechanism() },             // 08/11 (§196): vùng kích bẫy (tấm sàn) -> xung tín hiệu
            { "spikes", () => new SpikeMechanism() },            // 08/11 (§196): ô gai sàn hoạt ảnh (nhịp / giẫm / theo tín hiệu)
            { "launcher", () => new LauncherMechanism() },       // 08/11 (§197): máy bắn tên / lửa / băng gắn tường
            { "stillness", () => new StillnessMechanism() },     // 16/11 (§261): cảm biến chuyển động -> xung tín hiệu (The Still Walk)
            { "cover", () => new CoverMechanism() },             // 08/11 (§197): bàn lật làm khiên chắn đạn
            { "roller", () => new RollerMechanism() },           // 08/11 (§197): khúc gỗ gai lăn theo làn
            { "ranks", () => new RankMechanism() },              // 18/11 (§331): hàng lính hồn hành quân (Marching Hall, Map 3b)
            { "pit", () => new PitTrapMechanism() },             // 17/11 (§305): sàn sập (Map 4, trước cửa kho Reliquary)
            { "blade", () => new BladeMechanism() },
            { "warden_statue", () => new WardenStatueMechanism() },   // 19/11 (§345): tượng che chắn Warden Gallery (Champion lao trúng -> vỡ, choáng)
            { "powder_keg", () => new PowderKegMechanism() },         // 19/11 (§345): thùng thuốc nổ (chém / đạn / cối / Champion ném)             // 08/11 (§197): lưỡi cưa / lưỡi xoay chạy ray
            { "relic_rack", () => new RelicRackMechanism() },         // 20/11 (§349): giá vũ khí — minigame đua giành vũ khí (Reliquary Lists)
            { "crystal_pillar", () => new CrystalPillarMechanism() }, // 20/11 (§350): cột pha lê 4 màu (thay giá vũ khí) — Champion hút đổi nguyên tố
            { "flood", () => new FloodMechanism() },
            // hiệu ứng theo cờ
            { "beam", () => new BeamMechanism() },
            { "shield", () => new ShieldMechanism() },
            { "light", () => new LightMechanism() },
            { "toggle", () => new ToggleMechanism() },
            { "tank", () => new TankMechanism() },
            { "sprite_prop", () => new SpritePropMechanism() },   // 04/11: vật trang trí có ảnh riêng + chân chặn + thắp sáng (trụ phong ấn)
            // câu đố / vật tương tác
            { "breakable", () => new BreakableMechanism() },
            { "sluice", () => new SluiceMechanism() },           // 06/11: van xả cống — chém van, nước xả dọc một làn (Ether Ram bị dập)
            { "lever", () => new LeverMechanism() },
            { "dial", () => new DialMechanism() },
            { "valve", () => new ValveMechanism() },
            { "bell", () => new BellMechanism() },
            { "summon", () => new SummonMechanism() },          // 07/11: cảnh bị hút / kéo đi (lời thoại + dịch chuyển) khi đủ cờ
            { "console", () => new ConsoleMechanism() },
            { "pipegrid", () => new PipeGridMechanism() },
            { "note", () => new NoteMechanism() },
            { "chest", () => new ChestMechanism() },             // 04/11: rương có hoạt ảnh mở, khoá bằng chìa, phần thưởng
            { "passage", () => new PassageMechanism() },         // 05/11: lối tắt / thang / nắp hầm (dịch chuyển trong map, có then một chiều)
            { "cipher", () => new CipherMechanism() },
            { "rosewindow", () => new RoseWindowMechanism() },
            // chỉ là mốc (boss đọc theo tên)
            { "ritual_pillar", () => new PassiveMechanism() },
        };

        private static readonly HashSet<string> warned = new HashSet<string>();

        public static IEnumerable<string> KnownTypes => Factories.Keys;

        public static Mechanism Create(string type, string objectName)
        {
            if (type != null && Factories.TryGetValue(type, out var make)) return make();
            if (warned.Add(type ?? ""))
                Debug.LogWarning($"[MapMechanics] Type '{type}' (object '{objectName}') không có trong MechanismRegistry — gõ sai trong Tiled? Bỏ qua hành vi.");
            return new PassiveMechanism();
        }
    }
}
