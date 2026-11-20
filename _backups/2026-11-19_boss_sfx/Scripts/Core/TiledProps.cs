using System.Globalization;
using SuperTiled2Unity;

namespace SoulRequiem.Core
{
    /// <summary>
    /// Đọc Custom Properties của object Tiled (qua SuperTiled2Unity) — MỘT chỗ duy nhất cho cả game.
    /// Số luôn đọc theo InvariantCulture (dấu chấm thập phân) để máy cài tiếng Việt không đọc sai "0.5".
    ///
    ///   string id   = props.GetString("id", "");
    ///   int    hits = props.GetInt("hits", 3);
    ///   float  r    = props.GetFloat("radius", 1f);
    ///   bool   lit  = props.GetBool("lit", false);      // nhận "true" / "True" / "1"
    ///
    /// Không có thuộc tính (hoặc sai kiểu) -> trả về giá trị mặc định, không báo lỗi.
    /// </summary>
    public static class TiledProps
    {
        public static string GetString(this SuperCustomProperties p, string name, string fallback)
        {
            if (p != null && p.m_Properties != null && p.TryGetCustomProperty(name, out var cp) && cp != null)
                return cp.m_Value;
            return fallback;
        }

        public static int GetInt(this SuperCustomProperties p, string name, int fallback) =>
            int.TryParse(p.GetString(name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        public static float GetFloat(this SuperCustomProperties p, string name, float fallback) =>
            float.TryParse(p.GetString(name, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        public static bool GetBool(this SuperCustomProperties p, string name, bool fallback)
        {
            string s = p.GetString(name, "");
            if (string.IsNullOrEmpty(s)) return fallback;
            return s == "true" || s == "True" || s == "1";
        }
    }
}
