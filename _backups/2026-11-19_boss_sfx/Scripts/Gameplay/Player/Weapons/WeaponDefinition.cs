using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 09/11 (§201, §202): một nhát trong combo katana 2 tay — attack1 chém chéo (kesa), attack2 hất ngược, attack3 quét ngang.
    /// Cân bằng (Ronin's Katana, 10/11 §205e — chậm lại): 16 + 16 + 26 trong ~1,9 s ≈ 31 DPS (cũ 1,1 s ≈ 52 DPS: đánh quá nhanh, 4 nhát/giây; người dùng chọn GIỮ sát thương). Thân Ren (8 khung, 8 hướng, khung trúng = 3) dùng chung cho mọi
    /// thanh katana; katana chỉ đổi số liệu + màu kiếm + màu vệt chém.
    /// </summary>
    [Serializable]
    public class ComboStep
    {
        [Tooltip("Tên hiển thị (debug).")] public string name = "Slash";
        public float damage = 30f;
        [Tooltip("Tầm chém tính từ hông (đơn vị Unity).")] public float range = 0.75f;
        [Range(30f, 360f)] public float arcDegrees = 140f;
        public float knockback = 2f;
        [Tooltip("Khung trúng đòn trong clip 8 khung (0-7). Thân Ren: attack1 = 3, attack2 = 3, attack3 = 3.")]
        [Range(0, 7)] public int hitFrame = 3;
        [Tooltip("Khung từ đó được nối nhát kế / bấm đi để huỷ phần thu đòn.")]
        [Range(1, 8)] public int cancelFrame = 6;
        [Tooltip("Tốc độ khung hình của nhát này (clip 8 khung). 10/11: katana thường 13 fps (0,46 s / nhát khi nối). Vũ khí nặng: thấp hơn.")]
        public float fps = 13f;
        [Tooltip("Hệ số dừng hình / giật camera khi trúng.")] public float impact = 1f;
        [Tooltip("Lật vệt chém (nhát chém ngược chiều).")] public bool flipTrail;
        [Tooltip("Hệ số cỡ vệt chém.")] public float trailScale = 1f;
        [Tooltip("Nhát đâm (vệt đâm thẳng m3_thrust thay vệt chém cong).")] public bool thrust;

        public float HitTime => hitFrame / Mathf.Max(1f, fps);
        public float CancelTime => cancelFrame / Mathf.Max(1f, fps);
        public float Length => 8f / Mathf.Max(1f, fps);
    }

    /// <summary>
    /// 09/11 (§201, §202): THANH KATANA — tài sản ScriptableObject (Resources/Weapons/&lt;id&gt;.asset). Ren chỉ dùng katana; mọi thanh
    /// dùng chung DÁNG (vẽ sẵn trong thân Ren) và khác nhau ở THIẾT KẾ: hoa văn lưỡi (hamon, chữ khắc), tsuba, quấn chuôi, sơn bao kiếm
    /// (bảng 64x32 tra theo UV-map từng khung), ánh phát sáng, ảnh trưng bày, màu vệt chém và số liệu combo. Đổi katana = PlayerCombat.Equip(asset) (hoặc gán PlayerCombat.weapon).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        public string id = "katana";
        public string displayName = "Katana";
        [TextArea] public string description = "";
        [Header("Cốt truyện (§217) — bảng vũ khí trong túi đồ")]
        [Tooltip("Một dòng dưới tên: lò rèn / chủ cũ / thời đại.")] public string epithet = "";
        [TextArea(3, 10)] [Tooltip("Tích của thanh kiếm (đọc trong túi đồ).")] public string lore = "";
        [Tooltip("Tìm thấy ở đâu (hiện khi đã có).")] public string foundAt = "";
        [Header("Nội tại (§217)")]
        public WeaponTrait trait = new WeaponTrait();
        [Tooltip("Biểu tượng nhỏ (ô túi đồ).")] public Sprite icon;
        [Tooltip("Ảnh trưng bày lớn (kiếm + bao, ~256x96) — bảng trang bị / lúc nhặt được.")] public Sprite showcase;
        [Header("Ngoại hình trên người Ren (UV-map, §202)")]
        [Tooltip("Bảng thiết kế 64x32 (Catalog/v2/tools/ren2/katana_design.py): lưỡi / tsuba / chuôi / bao. Point, không nén.")]
        public Texture2D design;
        [Tooltip("Màu nhân vào phần phát sáng của thiết kế (alpha < 1 trong bảng thiết kế).")][ColorUsage(false, true)]
        public Color glowTint = Color.white;
        [Tooltip("Độ phát sáng lúc thường.")] public float glow = 1f;
        [Tooltip("Lúc vung kiếm độ phát sáng nhân lên bấy nhiêu lần.")] public float swingGlowBoost = 2.5f;
        [Tooltip("Màu vệt chém (nhân vào VFX slash_*).")] public Color trailColor = new Color(0.85f, 0.95f, 1f, 1f);
        [Tooltip("10/11 (§209): bộ VFX chém riêng của thanh kiếm (PixelLab VFX Creator), Resources/VFX/slash3/<slashStyle>_<nhát>_<HƯỚNG>. "
                 + "Vẽ theo ĐÚNG đường mũi kiếm trong hoạt ảnh attack1-3, mỗi khung hoạt ảnh = 2 khung VFX, bắt đầu ở khung 2, tới mũi kiếm đúng khung trúng (3). "
                 + "Trống = VFX cũ slash_* nhuộm trailColor.")] public string slashStyle = "";
        [Tooltip("Thời gian (giây) sau khung huỷ của một nhát vẫn còn nối được nhát kế tiếp.")] public float comboWindow = 0.35f;
        [Tooltip("Nghỉ thêm sau nhát cuối của combo trước khi đánh lại từ đầu.")] public float finisherRecovery = 0.30f;
        public ComboStep[] combo =
        {
            new ComboStep { name = "Kesa Cut", damage = 16f, range = 0.75f, arcDegrees = 140f, knockback = 1.4f, hitFrame = 3, cancelFrame = 6, fps = 13f },
            new ComboStep { name = "Rising Cut", damage = 16f, range = 0.75f, arcDegrees = 140f, knockback = 1.4f, hitFrame = 3, cancelFrame = 6, fps = 13f, flipTrail = true },
            new ComboStep { name = "Horizontal Sweep", damage = 26f, range = 0.9f, arcDegrees = 170f, knockback = 3.2f, hitFrame = 3, cancelFrame = 7, fps = 12f, impact = 1.6f, trailScale = 1.2f },
        };

        public const string DefaultId = "Katana";
        public static WeaponDefinition LoadDefault() => Resources.Load<WeaponDefinition>("Weapons/" + DefaultId);
    }
}
