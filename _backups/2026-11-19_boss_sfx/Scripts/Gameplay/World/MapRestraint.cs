using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 17/11 (§306): TRÓI THEO MAP — đọc thuộc tính map (Tiled ▸ Map Properties):
    ///   disarm_until      = cờ ("a,b"): chưa đủ cờ thì Ren bị tước kiếm (không chém được) — vd Map 4: m4_katana (lấy lại ở phòng lính gác)
    ///   mantra_seal_until = cờ: chưa đủ cờ thì Mantra bị phong (không dùng chiêu) — vd Map 4: m4_mantras_free (Pell gỡ phong ấn)
    ///   disarm_text / mantra_seal_text = chữ báo khi Ren cố chém / dùng chiêu lúc đang bị trói.
    /// Gắn tự động bởi MapEncounterDirector khi map có một trong hai thuộc tính. Mỗi 0,2 s đặt PlayerStatus.Disarmed / MantrasBound.
    /// Rời map (scene khác không có thuộc tính) -> PlayerStatus mới không bị trói.
    /// 17/11: khi bị tước kiếm, Ren mặc "áo tế" — đổi Animator sang Resources/Player/RenRobe (đứng thở + đi bộ, dựng bằng
    /// Soul Requiem ▸ Player ▸ Build Ren Robe Animator); có cờ lấy lại kiếm (hoặc rời map) -> trả controller gốc.
    /// 17/11: bình máu cũng bị thu cùng kiếm (PlayerStatus.FlaskTaken = Disarmed).
    /// </summary>
    public sealed class MapRestraint : MonoBehaviour
    {
        private string disarmUntil = "", sealUntil = "", disarmText = "", sealText = "";
        private MapEncounterDirector dir;
        private float next;
        private Animator anim;
        private RuntimeAnimatorController normalCtrl, robeCtrl;
        private bool robeOn;

        public void Setup(MapEncounterDirector director, string disarm, string seal, string dText, string sText)
        {
            dir = director;
            disarmUntil = disarm ?? ""; sealUntil = seal ?? "";
            disarmText = dText ?? ""; sealText = sText ?? "";
        }

        private void Update()
        {
            if (Time.time < next || dir == null || dir.Flags == null || dir.Player == null) return;
            next = Time.time + 0.2f;
            var st = PlayerStatus.Of(dir.Player);
            if (st == null) return;
            st.Disarmed = disarmUntil.Length > 0 && !dir.Flags.HasAll(disarmUntil);
            st.FlaskTaken = st.Disarmed;                     // 17/11: bình máu bị thu cùng kiếm, lấy lại cùng lúc (cờ disarm_until)
            st.MantrasBound = sealUntil.Length > 0 && !dir.Flags.HasAll(sealUntil);
            if (disarmText.Length > 0) st.DisarmText = disarmText;
            if (sealText.Length > 0) st.MantraSealText = sealText;
            SetRobe(st.Disarmed);
        }

        /// <summary>Đổi hình Ren: áo tế khi bị tước kiếm, bộ thường khi đã lấy lại. Chỉ đổi khi trạng thái thay đổi (đổi controller reset Animator).</summary>
        private void SetRobe(bool on)
        {
            if (on == robeOn) return;
            if (anim == null)
            {
                var pc = dir.Player.GetComponentInChildren<PlayerController>();
                anim = pc != null ? pc.GetComponent<Animator>() : dir.Player.GetComponentInChildren<Animator>();
                if (anim == null) return;
            }
            if (on)
            {
                if (robeCtrl == null) robeCtrl = Resources.Load<RuntimeAnimatorController>("Player/RenRobe");
                if (robeCtrl == null) { Debug.LogWarning("MapRestraint: chưa có Resources/Player/RenRobe — chạy Build Ren Robe Animator."); robeOn = true; return; }
                normalCtrl = anim.runtimeAnimatorController;
                anim.runtimeAnimatorController = robeCtrl;
            }
            else if (normalCtrl != null && anim.runtimeAnimatorController == robeCtrl)
                anim.runtimeAnimatorController = normalCtrl;
            robeOn = on;
        }

        private void OnDestroy()
        {
            if (dir == null || dir.Player == null) return;
            var st = dir.Player.GetComponent<PlayerStatus>();
            if (st != null) { st.Disarmed = false; st.MantrasBound = false; st.FlaskTaken = false; }
            if (robeOn) SetRobe(false);
        }
    }
}
