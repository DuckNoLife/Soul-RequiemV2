using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§350) -> 20/11 (§353): ĐỔI MÀU các chi tiết TÍM trên người Hollow Champion (mũ trùm, khói hồn, đá ở nắm đấm) theo pha lê vừa hút.
    /// §350 thay ảnh trong LateUpdate — trong game không đổi màu (người dùng). §353 làm lại chắc chắn hơn: Map4EnemyBuilder dựng sẵn
    /// mỗi màu một AnimatorOverrideController (clip y hệt bản gốc nhưng khung đã đổi màu — ảnh _tools/m4duo_1119/crystal/recolor.py);
    /// Set() đổi controller của Animator và giữ nguyên trạng thái + thời điểm đang chạy (không giật hoạt ảnh).
    /// Thiếu dữ liệu (chưa chạy lại menu Build Map 4 Enemies) -> cảnh báo một lần trong Console.
    /// </summary>
    public sealed class ChampionPalette : MonoBehaviour
    {
        [System.Serializable]
        public class Variant { public string name; public AnimatorOverrideController controller; }

        public RuntimeAnimatorController baseController;
        public Variant[] variants;
        private Animator anim;
        private string active = "";

        private void Awake()
        {
            anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (baseController == null && anim != null) baseController = anim.runtimeAnimatorController;
            if (variants == null || variants.Length == 0)
                Debug.LogWarning("[Champion] ChampionPalette chưa có bản màu — chạy lại menu Soul Requiem > Build Map 4 Enemies.");
        }

        /// <summary>Bật bản màu `name` (crimson / azure / verdant / gilded); null hoặc "" = màu tím gốc.</summary>
        public void Set(string name)
        {
            name = name ?? "";
            if (anim == null || name == active) return;
            RuntimeAnimatorController want = baseController;
            if (name.Length > 0 && variants != null)
                foreach (var v in variants) if (v != null && v.name == name && v.controller != null) { want = v.controller; break; }
            active = name;
            if (want == null || anim.runtimeAnimatorController == want) return;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            anim.runtimeAnimatorController = want;
            anim.Play(info.fullPathHash, 0, info.loop ? info.normalizedTime % 1f : Mathf.Min(info.normalizedTime, 1f));
            anim.Update(0f);
        }
    }
}
