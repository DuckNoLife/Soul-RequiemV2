using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Hiệu ứng trạng thái boss gây lên Ren (thay cho sát thương ở những đòn rộng, khó né):
    ///   Slow     – đi chậm (PlayerController nhân SpeedMul).                       VFX m3_st_slow (mũi tên chìm dưới chân)
    ///   Silence  – khoá chiêu F (PlayerCombat kiểm tra Silenced).         VFX m3_st_silence (vòng ấn chú quanh người)
    ///   Hemorrhage – không uống được bình máu (HealBlocked).                       VFX m3_st_bleed (máu nhỏ giọt)
    ///   Daze     – choáng một nhịp (Health.Stun).                                  VFX stun_stars
    /// Lăn (bất tử) né được: không nhận trạng thái khi Health.Invulnerable. Chế độ bất tử thử nghiệm (F1) cũng bỏ qua.
    /// Tự gắn vào người chơi khi cần (PlayerStatus.Of). Hồi sinh thì xoá hết.
    /// </summary>
    public class PlayerStatus : MonoBehaviour
    {
        private Health hp;
        private PlayerCombat combat;
        private float slowUntil, slowMul = 1f, silenceUntil, healBlockUntil;
        private SpriteVfx slowFx, silenceFx, bleedFx;
        private static float nextHint;

        public float SpeedMul => Time.time < slowUntil ? slowMul : 1f;
        public bool Silenced => Time.time < silenceUntil;
        /// <summary>17/11 (§306): trói theo MAP (MapRestraint đặt mỗi khung theo cờ): bị tước kiếm -> không chém; Mantra bị phong -> không dùng chiêu.
        /// Chữ báo lấy từ thuộc tính map `disarm_text` / `mantra_seal_text`.</summary>
        public bool Disarmed { get; set; }
        public bool MantrasBound { get; set; }
        public string DisarmText { get; set; } = "Your blade is gone.";
        public string MantraSealText { get; set; } = "Your mantras are sealed.";
        private float nextRestraintHint;
        /// <summary>17/11 (§306): hiện chữ báo bị trói (cách nhau ít nhất 1,5 s).</summary>
        public void SayRestraint(string text)
        {
            if (Time.time < nextRestraintHint || string.IsNullOrEmpty(text)) return;
            nextRestraintHint = Time.time + 1.5f;
            Hints.Show(text, 1.6f);
        }
        public bool HealBlocked => Time.time < healBlockUntil;
        /// Giây còn lại của từng hiệu ứng (HUD hiện biểu tượng + đếm ngược).
        public float SlowLeft => Mathf.Max(0f, slowUntil - Time.time);
        public float SilenceLeft => Mathf.Max(0f, silenceUntil - Time.time);
        public float HealBlockLeft => Mathf.Max(0f, healBlockUntil - Time.time);

        public static PlayerStatus Of(Component player)
        {
            if (player == null) return null;
            var s = player.GetComponent<PlayerStatus>();
            if (s == null) s = player.gameObject.AddComponent<PlayerStatus>();
            return s;
        }

        private void Awake()
        {
            hp = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            if (hp != null) hp.Revived += _ => ClearAll();
        }

        private bool Blocked => hp == null || hp.IsDead || hp.Invulnerable || (combat != null && combat.GodMode);

        public void Slow(float mul, float seconds, string hint = null)
        {
            if (Blocked) return;
            if (Time.time >= slowUntil || mul < slowMul) slowMul = mul;
            slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
            slowFx = Keep(slowFx, "m3_st_slow", 0f, slowUntil);
            Say(hint);
        }

        public void Silence(float seconds, string hint = "Silenced: your mantras are sealed!")
        {
            if (Blocked) return;
            silenceUntil = Mathf.Max(silenceUntil, Time.time + seconds);
            silenceFx = Keep(silenceFx, "m3_st_silence", 0.32f, silenceUntil);
            Say(hint);
        }

        public void BlockHeal(float seconds, string hint = "Hemorrhage: potions will not close your wounds!")
        {
            if (Blocked) return;
            healBlockUntil = Mathf.Max(healBlockUntil, Time.time + seconds);
            bleedFx = Keep(bleedFx, "m3_st_bleed", 0.55f, healBlockUntil);
            Say(hint);
        }

        public void Daze(float seconds, string hint = null)
        {
            if (Blocked) return;
            hp.Stun(seconds);
            var v = SpriteVfx.SpawnAttached("stun_stars", transform, 0.6f, Layer, Order + 2, 14f, seconds);
            if (v != null) v.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            Say(hint);
        }

        public void ClearAll()
        {
            slowUntil = silenceUntil = healBlockUntil = 0f;
            slowMul = 1f;
            foreach (var f in new[] { slowFx, silenceFx, bleedFx }) if (f != null) f.Stop();
            slowFx = silenceFx = bleedFx = null;
        }

        private int Layer { get { var r = GetComponent<SpriteRenderer>(); return r != null ? r.sortingLayerID : 0; } }
        private int Order { get { var r = GetComponent<SpriteRenderer>(); return r != null ? r.sortingOrder : 2; } }

        /// Giữ một VFX lặp gắn theo người chơi tới `until` (tạo mới nếu chưa có / đã hết).
        private SpriteVfx Keep(SpriteVfx fx, string name, float yOffset, float until)
        {
            if (fx != null) { fx.Looping(until - Time.time); return fx; }   // đang hiện: chỉ kéo dài
            var v = SpriteVfx.SpawnAttached(name, transform, 0.7f, Layer, Order + 1, 12f, until - Time.time);
            if (v != null) v.transform.localPosition = new Vector3(0f, yOffset, 0f);
            return v;
        }

        private static void Say(string hint)
        {
            if (string.IsNullOrEmpty(hint) || Time.time < nextHint) return;
            Hints.Show(hint, 1.8f);
            nextHint = Time.time + 4f;
        }
    }
}
