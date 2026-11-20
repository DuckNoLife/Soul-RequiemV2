using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 04/11: TURNED BLADE — sách "Book of the Turned Blade" (cờ book_turned_blade, Forgotten Store Map 2). Trước đây nhặt sách chỉ bật cờ,
    /// không có tác dụng gì. Giờ: nhát chém thường (PlayerMelee) trúng một viên đạn đang bay trong tầm chém -> đạn dội NGƯỢC về phía
    /// nó bay tới, nhanh hơn, mạnh gấp đôi, chỉ trúng quái. Loé sáng + giật camera nhẹ + khựng hình rất ngắn.
    /// Cờ sách mang theo qua các map (GameSession.Books) và nằm trong bản lưu.
    /// </summary>
    public static class TurnedBlade
    {
        public const string Flag = "book_turned_blade";

        public static bool Known
        {
            get { var d = MapEncounterDirector.Instance; return d != null && d.Flags != null && d.Flags.Has(Flag); }
        }

        // 04/11 (chạy thử): nhát chém trúng sau 0,48 s -> đạn lao tới trong khoảng đó; giữ "cửa sổ đỡ" thêm `Window` giây sau khung chém,
        // đạn bay vào hình quạt trong cửa sổ cũng bị dội (Projectile.Update gọi Catch) — đỡ vừa tay, không đòi khung hình chính xác
        public const float Window = 0.2f;
        private static float openUntil = -1f;
        private static Vector2 wPivot, wFacing;
        private static float wRange;

        /// <summary>Đỡ mọi viên đạn trong hình quạt chém (tâm `pivot`, hướng `facing`, tầm `range`) và mở cửa sổ đỡ. Trả về số đạn dội ngay.</summary>
        public static int TryParry(Vector2 pivot, Vector2 facing, float range)
        {
            if (!Known) return 0;
            wPivot = pivot; wFacing = facing; wRange = range; openUntil = Time.time + Window;
            int n = 0;
            for (int i = Projectile.Live.Count - 1; i >= 0; i--)
                if (Catch(Projectile.Live[i], false)) n++;
            if (n > 0) Feedback(facing);
            return n;
        }

        /// <summary>Đạn `p` nằm trong hình quạt của nhát chém vừa rồi (còn trong cửa sổ đỡ) -> dội ngược.</summary>
        public static bool Catch(Projectile p, bool feedback = true)
        {
            if (p == null || p.Reflected || p.Exploding || Time.time > openUntil) return false;
            Vector2 v = (Vector2)p.transform.position - wPivot;
            if (v.magnitude > wRange + 0.3f) return false;
            if (v.sqrMagnitude > 0.0025f && Vector2.Angle(wFacing, v) > 90f) return false;
            p.Reflect(-p.Direction);
            SpriteVfx.Spawn("hit", p.transform.position, 0.6f, 0, 50, false, 20f);
            if (feedback) Feedback(wFacing);
            return true;
        }

        private static void Feedback(Vector2 facing)
        {
            CameraFollow.KickMain(facing, 0.05f, 0.15f);
            var d = MapEncounterDirector.Instance;
            if (d != null && d.Flags != null && !d.Flags.Has("tut_parry_done"))
            {
                d.Flags.Set("tut_parry_done");
                Hints.Show("Turned! A bolt struck back flies true - and hits twice as hard.", 3f);
            }
        }
    }
}
