using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Một chiêu tháo lắp được (dữ liệu = asset trong Resources/Skills/*.asset).
    /// Thêm chiêu mới: tạo class kế thừa SkillDefinition (viết Cast), rồi Create ▸ Soul Requiem ▸ Skill ▸ ... đặt vào Resources/Skills.
    /// Người chơi gắn tối đa PlayerLoadout.SlotCount chiêu; đổi ở điểm nghỉ (checkpoint).
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        [Tooltip("Mã duy nhất, dùng để lưu bộ chiêu khi qua map (vd. ground_slam).")]
        public string id = "skill";
        public string displayName = "Mantra";
        [TextArea(2, 5)] public string description = "";
        [Tooltip("Icon trên HUD / bảng chiêu. Để trống thì nạp Resources/UI/Requiem/<iconName>.")]
        public Texture2D icon;
        public string iconName = "";
        [Tooltip("Màu nhấn của chiêu (vòng hồi chiêu, viền khi sẵn sàng).")]
        public Color accent = new Color(0.55f, 0.75f, 1f);
        public float cooldown = 5f;
        [Tooltip("Có sẵn từ đầu game (không cần nhặt sách chiêu).")]
        public bool unlockedAtStart = true;
        [Tooltip("Bộ chiêu: các chiêu cùng setId được lắp / tháo cùng nhau, mỗi chiêu vào ô setSlot (0 = ô I, 1 = ô II, 2 = ô III). Trống = chiêu lẻ.")]
        public string setId = "";
        public int setSlot = -1;
        /// <summary>04/11: tên + mô tả của cả bộ (bảng nghỉ hiện bộ thành MỘT thẻ). Chỉ cần điền ở chiêu setSlot 0.</summary>
        public string setName = "";
        [TextArea(2, 5)] public string setDescription = "";

        // 11/11 (§228): bảng Shrine — loại chiêu + thông số (đọc giá trị thật của asset). Khoảng cách đổi ra ô (1 ô = 0,32 đv).
        public virtual string Kind => "Mantra";
        public virtual void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o) { }
        protected static void Stat(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o, string label, string value) => o.Add(new System.Collections.Generic.KeyValuePair<string, string>(label, value));
        protected static string Tiles(float u) => (u / 0.32f).ToString("0.#") + " tiles";
        protected static string Secs(float s) => s.ToString("0.##") + "s";
        protected static string Pct(float f) => Mathf.RoundToInt(f * 100f) + "%";

        public Texture2D Icon
        {
            get
            {
                if (icon == null && !string.IsNullOrEmpty(iconName)) icon = Resources.Load<Texture2D>("UI/Requiem/" + iconName);
                return icon;
            }
        }

        /// 07/11 (§184): bấm lại phím của chiêu đang duy trì (vd Judgement Ray) -> dừng sớm. Trả true nếu đã xử lý (không tính là thi triển).
        public virtual bool TryStop(PlayerCombat p) => false;

        /// Thi triển. `cancel` gọi khi chiêu bị huỷ trước lúc có hiệu lực (Roll / choáng) -> hoàn hồi chiêu.
        public abstract IEnumerator Cast(PlayerCombat p, Action cancel);
    }
}
