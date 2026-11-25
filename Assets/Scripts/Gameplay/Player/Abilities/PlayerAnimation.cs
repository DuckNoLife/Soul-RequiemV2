using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Wraps the player's Animator parameters (built by the menu "Soul Requiem ▸ Player ▸ Ren Animator (v2)", RenAnimatorBuilder).
    /// Parameters missing from the Animator make the matching command do nothing (Has*) — missing animations never throw.
    /// Called by: PlayerCombat and the Abilities / Skills scripts.
    /// </summary>
    internal sealed class PlayerAnimation
    {
        private static readonly int AtkX = Animator.StringToHash("AtkX");
        private static readonly int AtkY = Animator.StringToHash("AtkY");
        private static readonly int DirX = Animator.StringToHash("DirX");
        private static readonly int DirY = Animator.StringToHash("DirY");
        private static readonly int Speed = Animator.StringToHash("Speed");
        // 3-hit combo (Attack1/2/3Trigger), clip speed follows the weapon (AtkSpeed)
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
        // sword sheathed while running / out of combat. Sheathed = true -> Idle sheathes (Sheathe) then IdleSheathed.
        private static readonly int Sheathed = Animator.StringToHash("Sheathed");
        /// <summary>How long (seconds) the sword stays drawn after the last combat action before it is sheathed.</summary>
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
                Debug.LogWarning("[Player] The Animator has no 'DashTrigger' trigger - run Soul Requiem > Player > Ren Animator (v2).");
        }

        public bool Exists => anim != null;

        /// <summary>Combat action: draw the sword (Sheathed = false) and keep it drawn for <see cref="DrawnHold"/> seconds.</summary>
        private void Engage()
        {
            drawnUntil = Time.time + DrawnHold;
            if (HasSheath) anim.SetBool(Sheathed, false);
        }

        /// <summary>Called every frame (PlayerCombat.Update): combat time over -> sheathe.</summary>
        public void Tick()
        {
            if (HasSheath && Time.time > drawnUntil) anim.SetBool(Sheathed, true);
        }

        /// <summary>Combo hit <paramref name="step"/> (1-3) toward d (8 directions), clip speed x <paramref name="speed"/>.</summary>
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

        /// <summary>Slash clip speed (0 = frozen frame — the Iaijutsu hold stance).</summary>
        public void SetAttackSpeed(float v) { if (HasAttack) anim.SetFloat(AtkSpeed, Mathf.Max(0f, v)); }

        /// <summary>Turn the held slash direction (same frame).</summary>
        public void SetAttackDir(Vector2 d) { if (!HasAttack) return; anim.SetFloat(AtkX, d.x); anim.SetFloat(AtkY, d.y); Engage(); }

        public void Dash(Vector2 facing)
        {
            if (!HasDash) return;
            // roll / dash with the sword SHEATHED (both hands free) -> IdleSheathed after the dash; the next slash draws it again via Engage
            drawnUntil = 0f;
            if (HasSheath) anim.SetBool(Sheathed, true);
            anim.SetFloat(DirX, facing.x);
            anim.SetFloat(DirY, facing.y);
            anim.SetTrigger(DashTrigger);
        }

        /// <summary>Stomp animation (skill). false if the Animator lacks it.</summary>
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

        /// <summary>Summon Ether orbs (Seeker Embers). false if the Animator lacks it.</summary>
        public bool CastEmbers(Vector2 facing)
        {
            if (!HasCastEmbers) return false;
            SetDir(facing);
            HoldStill();
            Engage();
            anim.SetTrigger(CastEmbersTrigger);
            return true;
        }

        /// <summary>Hands together to cast the shield (Veil). false if the Animator lacks it.</summary>
        public bool CastVeil(Vector2 facing)
        {
            if (!HasCastVeil) return false;
            SetDir(facing);
            HoldStill();
            Engage();
            anim.SetTrigger(CastVeilTrigger);
            return true;
        }

        /// <summary>Start the beam pose: raise the hand (RayStart) then hold (RayLoop) until <see cref="RayEnd"/>.</summary>
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

        // the drink animation is drawn with the sword SHEATHED (right hand takes the flask from the belt) -> sheathe, back to IdleSheathed after drinking
        public void Drink() { if (!HasDrink) return; HoldStill(); drawnUntil = 0f; if (HasSheath) anim.SetBool(Sheathed, true); anim.SetTrigger(DrinkTrigger); }
        public void Hurt() { if (!HasHurt) return; Engage(); anim.SetTrigger(HurtTrigger); }

        /// <summary>Stagger (heavy hit / stun start) — cancels the light hurt animation.</summary>
        public void Stagger()
        {
            if (!HasStagger) return;
            anim.ResetTrigger(HurtTrigger);
            HoldStill();
            Engage();
            anim.SetTrigger(StaggerTrigger);
        }

        /// <summary>Speed = 0 on the first frame of a foot-locking action (Iaido / Drink / Stagger / Stomp / Cast). These states go to Run on
        /// Speed &gt; 0.1 (no Exit Time); PlayerController may already have written the old Speed this frame if it ran before
        /// PlayerCombat -> the Animator would jump straight to Run. From the next frame PlayerController keeps 0 until the lock ends.</summary>
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

        /// <summary>Cutscene — draw the sword at once (held like after combat for <see cref="DrawnHold"/> seconds).</summary>
        public void DrawNow() => Engage();

        /// <summary>Cutscene — sheathe the sword at once (the Animator plays the Sheathe clip if the sword is drawn).</summary>
        public void SheatheNow()
        {
            drawnUntil = 0f;
            if (HasSheath) anim.SetBool(Sheathed, true);
        }

        public void Revive()
        {
            if (!HasDeath) return;
            anim.SetBool(Dead, false);
            anim.SetTrigger(ReviveTrigger);
        }
    }
}
