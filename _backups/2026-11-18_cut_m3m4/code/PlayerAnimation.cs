using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Gói các tham số Animator của người chơi (do menu "Soul Requiem ▸ Player Animator" dựng).
    /// Tham số nào Animator không có thì lệnh tương ứng không làm gì (Has*) — để thiếu hoạt ảnh không văng lỗi.
    /// </summary>
    internal sealed class PlayerAnimation
    {
        private static readonly int AtkX = Animator.StringToHash("AtkX");
        private static readonly int AtkY = Animator.StringToHash("AtkY");
        private static readonly int DirX = Animator.StringToHash("DirX");
        private static readonly int DirY = Animator.StringToHash("DirY");
        private static readonly int Speed = Animator.StringToHash("Speed");
        // 09/11 (§201): bỏ Iaido -> combo 3 nhát (Attack1/2/3Trigger), tốc độ clip theo vũ khí (AtkSpeed)
        private static readonly int[] AttackTriggers = { Animator.StringToHash("Attack1Trigger"), Animator.StringToHash("Attack2Trigger"), Animator.StringToHash("Attack3Trigger") };
        private static readonly int AtkSpeed = Animator.StringToHash("AtkSpeed");
        private static readonly int DashTrigger = Animator.StringToHash("DashTrigger");
        private static readonly int HurtTrigger = Animator.StringToHash("HurtTrigger");
        private static readonly int SkillTrigger = Animator.StringToHash("SkillTrigger");
        private static readonly int ReviveTrigger = Animator.StringToHash("ReviveTrigger");
        private static readonly int StaggerTrigger = Animator.StringToHash("StaggerTrigger");
        private static readonly int DrinkTrigger = Animator.StringToHash("DrinkTrigger");
        private static readonly int CastEmbersTrigger = Animator.StringToHash("CastEmbersTrigger");
        private static readonly int CastVeilTrigger = Animator.StringToHash("CastVeilTrigger");
        private static readonly int RayTrigger = Animator.StringToHash("RayTrigger");
        private static readonly int Ray = Animator.StringToHash("Ray");
        private static readonly int Dead = Animator.StringToHash("Dead");
        private static readonly int Stunned = Animator.StringToHash("Stunned");
        // 10/11 (§205): kiếm trong bao khi chạy / ngoài giao tranh. Sheathed = true -> Idle tra kiếm (Sheathe) rồi IdleSheathed.
        private static readonly int Sheathed = Animator.StringToHash("Sheathed");
        /// <summary>Giữ kiếm trên tay bao lâu (giây) sau hành động giao tranh cuối cùng rồi mới tra vào bao.</summary>
        public const float DrawnHold = 3f;
        private float drawnUntil;

        private readonly Animator anim;
        public readonly bool HasAttack, HasDash, HasHurt, HasSkill, HasDeath, HasStagger, HasStun, HasDrink;
        public readonly bool HasCastEmbers, HasCastVeil, HasRay, HasSheath;

        public PlayerAnimation(Animator animator)
        {
            anim = animator;
            if (anim == null) return;
            foreach (var p in anim.parameters)
            {
                bool trigger = p.type == AnimatorControllerParameterType.Trigger, flag = p.type == AnimatorControllerParameterType.Bool;
                if (trigger && p.nameHash == AttackTriggers[0]) HasAttack = true;
                if (trigger && p.nameHash == DashTrigger) HasDash = true;
                if (trigger && p.nameHash == HurtTrigger) HasHurt = true;
                if (trigger && p.nameHash == SkillTrigger) HasSkill = true;
                if (trigger && p.nameHash == StaggerTrigger) HasStagger = true;
                if (trigger && p.nameHash == DrinkTrigger) HasDrink = true;
                if (trigger && p.nameHash == CastEmbersTrigger) HasCastEmbers = true;
                if (trigger && p.nameHash == CastVeilTrigger) HasCastVeil = true;
                if (flag && p.nameHash == Ray) HasRay = true;
                if (flag && p.nameHash == Dead) HasDeath = true;
                if (flag && p.nameHash == Stunned) HasStun = true;
                if (flag && p.nameHash == Sheathed) HasSheath = true;
            }
            if (!HasDash)
                Debug.LogWarning("[Player] Animator chưa có trigger 'DashTrigger' — chạy menu Soul Requiem ▸ Gắn animation Dash vào Animator.");
        }

        public bool Exists => anim != null;

        /// <summary>Hành động giao tranh: rút kiếm (Sheathed = false) và giữ trên tay <see cref="DrawnHold"/> giây.</summary>
        private void Engage()
        {
            drawnUntil = Time.time + DrawnHold;
            if (HasSheath) anim.SetBool(Sheathed, false);
        }

        /// <summary>Gọi mỗi khung (PlayerCombat.Update): hết thời gian giao tranh -> tra kiếm.</summary>
        public void Tick()
        {
            if (HasSheath && Time.time > drawnUntil) anim.SetBool(Sheathed, true);
        }

        /// <summary>Nhát combo <paramref name="step"/> (1-3) theo hướng d (8 hướng), tốc độ clip x <paramref name="speed"/>.</summary>
        public void Attack(int step, Vector2 d, float speed)
        {
            if (!HasAttack) return;
            anim.SetFloat(AtkX, d.x);
            anim.SetFloat(AtkY, d.y);
            anim.SetFloat(AtkSpeed, Mathf.Max(0.1f, speed));
            HoldStill();
            Engage();
            foreach (var t in AttackTriggers) anim.ResetTrigger(t);
            anim.SetTrigger(AttackTriggers[Mathf.Clamp(step, 1, 3) - 1]);
        }

        /// <summary>16/11 (§271): tốc độ clip chém (0 = dừng khung — thế giữ Iaijutsu).</summary>
        public void SetAttackSpeed(float v) { if (HasAttack) anim.SetFloat(AtkSpeed, Mathf.Max(0f, v)); }

        /// <summary>16/11 (§271): xoay hướng đòn chém đang giữ (không đổi khung).</summary>
        public void SetAttackDir(Vector2 d) { if (!HasAttack) return; anim.SetFloat(AtkX, d.x); anim.SetFloat(AtkY, d.y); Engage(); }

        public void Dash(Vector2 facing)
        {
            if (!HasDash) return;
            // 10/11 (§210): lăn / lướt với kiếm TRONG BAO (hai tay rảnh) -> sau cú lướt về IdleSheathed; chém tiếp thì Engage rút lại
            drawnUntil = 0f;
            if (HasSheath) anim.SetBool(Sheathed, true);
            anim.SetFloat(DirX, facing.x);
            anim.SetFloat(DirY, facing.y);
            anim.SetTrigger(DashTrigger);
        }

        /// <summary>Hoạt ảnh dậm (kỹ năng). false nếu Animator không có.</summary>
        public bool Stomp(Vector2 facing)
        {
            if (!HasSkill) return false;
            anim.SetFloat(DirX, facing.x);
            anim.SetFloat(DirY, facing.y);
            HoldStill();
            Engage();
            anim.SetTrigger(SkillTrigger);
            return true;
        }

        private void SetDir(Vector2 d)
        {
            anim.SetFloat(DirX, d.x);
            anim.SetFloat(DirY, d.y);
        }

        /// <summary>Gọi cầu Ether (Seeker Embers). false nếu Animator chưa có.</summary>
        public bool CastEmbers(Vector2 facing)
        {
            if (!HasCastEmbers) return false;
            SetDir(facing);
            HoldStill();
            Engage();
            anim.SetTrigger(CastEmbersTrigger);
            return true;
        }

        /// <summary>Chắp tay niệm khiên (Veil). false nếu Animator chưa có.</summary>
        public bool CastVeil(Vector2 facing)
        {
            if (!HasCastVeil) return false;
            SetDir(facing);
            HoldStill();
            Engage();
            anim.SetTrigger(CastVeilTrigger);
            return true;
        }

        /// <summary>Bắt đầu tư thế bắn tia: giơ tay (RayStart) rồi giữ (RayLoop) tới khi <see cref="RayEnd"/>.</summary>
        public void RayBegin(Vector2 d)
        {
            if (!HasRay) return;
            SetDir(d);
            anim.SetBool(Ray, true);
            Engage();
            anim.SetTrigger(RayTrigger);
        }

        public void RayAim(Vector2 d) { if (!HasRay) return; SetDir(d); drawnUntil = Time.time + DrawnHold; }

        public void RayEnd()
        {
            if (!HasRay) return;
            anim.ResetTrigger(RayTrigger);
            anim.SetBool(Ray, false);
        }

        // 10/11 (§212): hoạt ảnh uống vẽ với kiếm TRONG BAO (tay phải lấy chai ở thắt lưng) -> tra kiếm, uống xong về IdleSheathed
        public void Drink() { if (!HasDrink) return; HoldStill(); drawnUntil = 0f; if (HasSheath) anim.SetBool(Sheathed, true); anim.SetTrigger(DrinkTrigger); }
        public void Hurt() { if (!HasHurt) return; Engage(); anim.SetTrigger(HurtTrigger); }

        /// <summary>Loạng choạng (đòn nặng / bắt đầu choáng) — huỷ hoạt ảnh trúng đòn nhẹ.</summary>
        public void Stagger()
        {
            if (!HasStagger) return;
            anim.ResetTrigger(HurtTrigger);
            HoldStill();
            Engage();
            anim.SetTrigger(StaggerTrigger);
        }

        /// <summary>Speed = 0 ngay khung bắt đầu động tác khoá chân (Iaido / Drink / Stagger / Stomp / Cast). Các state này sang Run bằng
        /// điều kiện Speed &gt; 0.1 (không Exit Time); PlayerController có thể đã ghi Speed cũ trong khung này nếu chạy trước
        /// PlayerCombat -> Animator nhảy thẳng sang Run. Từ khung sau PlayerController tự giữ 0 tới hết khoá.</summary>
        private void HoldStill() => anim.SetFloat(Speed, 0f);

        public void SetStunned(bool on) { if (HasStun) anim.SetBool(Stunned, on); }

        public void Die()
        {
            if (!HasDeath) return;
            foreach (var t in new[] { HurtTrigger, SkillTrigger, DashTrigger, ReviveTrigger }) anim.ResetTrigger(t);
            if (HasAttack) foreach (var t in AttackTriggers) anim.ResetTrigger(t);
            if (HasCastEmbers) anim.ResetTrigger(CastEmbersTrigger);
            if (HasCastVeil) anim.ResetTrigger(CastVeilTrigger);
            RayEnd();
            anim.SetBool(Dead, true);
        }

        public void Revive()
        {
            if (!HasDeath) return;
            anim.SetBool(Dead, false);
            anim.SetTrigger(ReviveTrigger);
        }
    }
}
