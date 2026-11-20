using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Hiệu ứng của một con quái (Map 3): ảnh ở Assets/Resources/VFX (làm bằng PixelLab VFX Workbench), phát bằng SpriteVfx.
    /// EnemyAI / LungeSkills / HealerSkills gọi các hàm dưới đây đúng lúc ra đòn. Để trống một trường = không có hiệu ứng đó.
    /// Ảnh vệt đánh có hướng: &lt;swing&gt;_E (lật X khi quay trái), _S (đánh xuống), _N (đánh lên) — neo ở NGỰC.
    /// Các ảnh khác neo ở CHÂN (death, spawn, dash, heal) hoặc ở tâm (windup, cast, hurt).
    /// </summary>
    public class EnemyVfx : MonoBehaviour
    {
        [Tooltip("Tên gốc vệt đánh, thêm _E/_S/_N (vd m3_slash_steel, m3_thrust, m3_claw)")]
        public string swing = "";
        [Tooltip("Loé báo trước đòn (lúc bắt đầu vung)")]
        public string windup = "";
        [Tooltip("Phát windup dưới chân thay vì trước ngực (giun trồi lên)")]
        public bool windupAtFeet = false;
        [Tooltip("Hiệu ứng trên người chơi khi đòn cận chiến trúng")]
        public string hurt = "m3_hurt";
        public string dash = "";
        public string cast = "";
        [Tooltip("Hiệu ứng trên đồng đội được hồi máu (Monk)")]
        public string heal = "";
        public string death = "";
        [Tooltip("Hiệu ứng lúc xuất hiện (xác sống bò khỏi mộ)")]
        public string spawn = "";
        [Tooltip("Tỉ lệ ảnh VFX (0.7 = cùng mật độ điểm ảnh với nhân vật PixelLab)")]
        public float scale = 0.7f;
        [Tooltip("Độ cao ngực so với chân (đơn vị Unity)")]
        public float chestY = 0.34f;

        private SpriteRenderer sr;

        private void Awake() { sr = GetComponentInChildren<SpriteRenderer>(); }

        private int Layer => sr != null ? sr.sortingLayerID : 0;
        private int Order => sr != null ? sr.sortingOrder : 2;
        private Vector2 Feet => transform.position;
        private Vector2 Chest => (Vector2)transform.position + Vector2.up * chestY;

        private static bool Has(string s) => !string.IsNullOrEmpty(s);

        public void Spawned()
        {
            if (Has(spawn)) SpriteVfx.Spawn(spawn, Feet, scale, Layer, Order - 1, false, 12f);
        }

        public void Windup(Vector2 dir)
        {
            if (!Has(windup)) return;
            if (windupAtFeet) { SpriteVfx.Spawn(windup, Feet, scale, Layer, Order - 1, false, 12f); return; }
            Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            SpriteVfx.Spawn(windup, Chest + d * 0.14f + Vector2.up * 0.05f, scale, Layer, Order + 2, false, 14f);
        }

        /// Vệt đánh theo hướng tới người chơi: trục trội (dọc khi |y| > 1.3|x|, khớp cách EnemyAI chọn hướng).
        public void Swing(Vector2 dir)
        {
            SoulRequiem.Core.Sfx.Play("en_swing", transform.position);   // 17/11 (§294): âm
            if (!Has(swing)) return;
            bool vertical = Mathf.Abs(dir.y) > Mathf.Abs(dir.x) * 1.3f;
            string suf = vertical ? (dir.y < 0f ? "_S" : "_N") : "_E";
            int order = vertical && dir.y > 0f ? Order - 1 : Order + 1;      // đánh lên (quay lưng): vệt ở phía xa, sau thân
            SpriteVfx.Spawn(swing + suf, Chest, scale, Layer, order, !vertical && dir.x < 0f, 16f);
        }

        public void HurtPlayer(Vector2 playerFeet)
        {
            if (Has(hurt)) SpriteVfx.Spawn(hurt, playerFeet + Vector2.up * 0.3f, scale, Layer, Order + 3, false, 18f);
        }

        public void Dash(Vector2 dir)
        {
            if (Has(dash)) SpriteVfx.Spawn(dash, Feet, scale, Layer, Order - 1, dir.x < 0f, 14f);
        }

        public void Cast()
        {
            if (Has(cast)) SpriteVfx.Spawn(cast, Chest + Vector2.up * 0.04f, scale, Layer, Order + 2, false, 14f);
        }

        public void HealOn(Transform target)
        {
            if (!Has(heal) || target == null) return;
            var tsr = target.GetComponentInChildren<SpriteRenderer>();
            int o = tsr != null ? tsr.sortingOrder : Order;
            var v = SpriteVfx.Spawn(heal, target.position, scale, Layer, o - 1, false, 14f);
            if (v != null) v.Follow(target);
        }

        public void Death()
        {
            if (Has(death)) SpriteVfx.Spawn(death, Feet, scale, Layer, Order + 1, false, 12f);
        }
    }
}
