using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356) → 20/11 (§360): BẢNG của minigame BURLAP'S PROVING GROUNDS (The Drill Yard, Map 4) — dữ liệu tĩnh để HUD
    /// (RequiemHUD.UpdateDrill) vẽ. DrillYardMechanism ghi, HUD chỉ đọc. Active = false -> HUD ẩn bảng.
    /// §360: bỏ điểm số trừu tượng -> mỗi trạm có MỤC TIÊU rõ ("LANTERNS 1 / 3") + thanh tiến độ, ĐỒNG HỒ (nếu có),
    /// và TINH THẦN (Morale, mặc định 5 ô): mỗi lần dính đòn mất 1 ô, hết ô = trượt trạm.
    /// </summary>
    public static class DrillBoard
    {
        public static bool Active;
        /// <summary>Trạm đang chơi (1..Stations) + tên trạm ("BLADE DRILL").</summary>
        public static int Station = 1, Stations = 3;
        public static string Title = "";
        /// <summary>Dòng mục tiêu to giữa bảng ("SURVIVE", "LANTERNS 1 / 3", "BURLAP 2 / 3") + tiến độ 0..1.</summary>
        public static string Goal = "";
        public static float GoalFrac;
        /// <summary>Đồng hồ: TimeFrac &lt; 0 = trạm không tính giờ. TimeText = "0:42".</summary>
        public static float TimeFrac = -1f;
        public static string TimeText = "";
        /// <summary>Tinh thần còn lại / tối đa; MoraleLostAt = lúc vừa mất một ô (HUD rung ô đó).</summary>
        public static int Morale = 5, MoraleMax = 5;
        public static float MoraleLostAt = -99f;
        /// <summary>20/11 (§362): DÒNG NHIỆM VỤ (việc cần làm ngay, câu đầy đủ) + THANH THEO DÕI: TrackFrac &lt; 0 = dùng GoalFrac;
        /// có TrackLeft / TrackRight / TrackMarker (đường dẫn sprite Resources) -> HUD vẽ biểu tượng hai đầu + biểu tượng chạy trên thanh
        /// (Lantern Run: bệ đèn -> giá đèn, đèn lồng chạy theo quãng đường đã mang).</summary>
        public static string Objective = "";
        public static float TrackFrac = -1f;
        public static string TrackLeft = "", TrackRight = "", TrackMarker = "";
        /// <summary>20/11 (§365): LỆNH đang hô ở trạm Sergeant Says ("SERGEANT SAYS: LEFT FLANK!") — HUD hiện chữ to giữa màn; rỗng = ẩn.</summary>
        public static string Command = "";
        public static float CommandTime = -99f;
        /// <summary>Dòng sự kiện vừa xảy ra ("CLEAN DODGE!") + thời điểm (HUD cho nổi lên rồi mờ).</summary>
        public static string Pop = "";
        public static Color PopColor = Color.white;
        public static float PopTime = -99f;
        /// <summary>Kết quả to giữa màn ("STATION CLEARED", "RANK A - PASSED") trong vài giây.</summary>
        public static string Result = "";
        public static Color ResultColor = Color.white;
        public static float ResultTime = -99f;

        public static void Say(string text, Color c) { Pop = text; PopColor = c; PopTime = Time.time; }
        public static void Finish(string text, Color c) { Result = text; ResultColor = c; ResultTime = Time.time; }

        public static void LoseMorale()
        {
            Morale = Mathf.Max(0, Morale - 1);
            MoraleLostAt = Time.time;
        }

        /// <summary>Bắt đầu một trạm: bảng hiện, tinh thần đầy.</summary>
        public static void Begin(int station, string title, int morale)
        {
            Active = true; Station = station; Title = title; Goal = ""; GoalFrac = 0f; TimeFrac = -1f; TimeText = "";
            Morale = MoraleMax = morale; MoraleLostAt = -99f; Pop = ""; PopTime = -99f;
            Objective = ""; TrackFrac = -1f; TrackLeft = TrackRight = TrackMarker = ""; Command = "";
        }
    }
}
