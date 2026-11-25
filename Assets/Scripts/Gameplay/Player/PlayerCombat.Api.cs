using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PlayerCombat functions called by outside code:
    ///   • Mantra skills (SkillDefinition.Cast): BeginAction / ActionAlive / LockActions, poses + animations, HitEnemies, camera kick.
    ///   • Cutscenes: LookAt (face the speaker), CutscenePose (StoryScenes `ren` command), PlayStagger, PlayCutsceneSlash.
    ///   • Events: Struck (every slash / skill — mechanisms and bosses listen), SkillCast (area skills), MeleeSwung (every slash swung).
    /// </summary>
    public partial class PlayerCombat
    {
        // ================================================================== events
        /// <summary>Every slash / skill: (centre, radius). Map mechanisms (valves, wooden barriers) and bosses (pillars, coffins) listen.</summary>
        public static event Action<Vector2, float> Struck;
        /// <summary>Area skill: (centre, radius). Poison clouds and nearby lights listen.</summary>
        public static event Action<Vector2, float> SkillCast;
        /// <summary>Every slash just swung (misses included): (player, pivot, direction, range, damage, knockback).</summary>
        public static event Action<PlayerCombat, Vector2, Vector2, float, float, float> MeleeSwung;

        internal static void RaiseStruck(Vector2 c, float r) => Struck?.Invoke(c, r);
        internal static void RaiseMeleeSwung(PlayerCombat p, Vector2 pivot, Vector2 dir, float range, float dmg, float knock) => MeleeSwung?.Invoke(p, pivot, dir, range, dmg, knock);
        public void RaiseSkillCast(Vector2 c, float r) => SkillCast?.Invoke(c, r);

        // ================================================================== for Mantra skills
        /// <summary>Start a new action (cancels a pending slash). Returns a token to check it is still valid.</summary>
        public int BeginAction() => actions.Begin();
        /// <summary>Action `id` has not been cancelled by a roll / stun / death.</summary>
        public bool ActionAlive(int id) => actions.Alive(id);
        public void LockActions(float seconds) => actions.Lock(seconds);

        /// <summary>Damage every enemy in the circle; returns the number hit. Also notifies mechanisms (Struck).</summary>
        public int HitEnemies(Vector2 center, float radius, float dmg, float knock)
        {
            RaiseStruck(center, radius);
            return strikes.Damage(center, radius, dmg, knock, null);
        }

        /// <summary>Ground VFX (with VFX off, draws a placeholder ring).</summary>
        public void SpawnGroundVfx(string name, Vector2 c, float scale, Color fallback, float radius) => strikes.GroundVfx(name, c, scale, fallback, radius);
        /// <summary>Camera kick (multiplies cameraKick and the screen shake setting).</summary>
        public void KickCamera(Vector2 dir, float mul, float duration) => strikes.Kick(dir, mul, duration);
        public void HitStopNow() => strikes.HitStop();

        /// <summary>Turn Ren to face d (skills aimed with the mouse).</summary>
        public void SetFacing(Vector2 d) { if (d.sqrMagnitude > 0.0001f) facing = d.normalized; }

        // ---- skill animations (return false if the Animator lacks them)
        /// <summary>Ground stomp (Stomp) toward the facing direction.</summary>
        public bool PlaySkillAnimation() => anim.Stomp(facing);
        /// <summary>Summon orbs (Seeker Embers).</summary>
        public bool PlayEmbersCast() => anim.CastEmbers(facing);
        /// <summary>Hands together to cast the shield (Veil of the Ascended).</summary>
        public bool PlayVeilCast() => anim.CastVeil(facing);
        /// <summary>Dash toward d.</summary>
        public void PlayDash(Vector2 d) => anim.Dash(d);

        /// <summary>Toggle the outstretched beam pose (Judgement Ray), turning the character toward `dir`.</summary>
        public void RayPose(bool on, Vector2 dir)
        {
            if (on) { FacingOverride = dir; anim.RayBegin(dir); }
            else { FacingOverride = Vector2.zero; anim.RayEnd(); if (Sprite != null) Sprite.flipX = false; }
        }
        /// <summary>Turn the beam pose to follow the beam direction.</summary>
        public void RayAim(Vector2 dir) { FacingOverride = dir; anim.RayAim(dir); }

        /// <summary>Slash pose (combo hit 1, weapon shown) toward d — Severing Draw.</summary>
        public void PlaySlashPose(Vector2 d)
        {
            var s = melee.Snap(d);
            anim.Attack(1, s, 1.4f);
            SetFacing(s);
            if (WeaponView == null) return;
            WeaponView.Show(true);
            CancelInvoke(nameof(HideWeapon));
            Invoke(nameof(HideWeapon), 0.32f);
        }
        private void HideWeapon() { if (WeaponView != null) WeaponView.Show(false); }

        // ================================================================== for cutscenes
        private Vector2 lookDir;
        private float lookUntil;

        /// <summary>Face the speaker (NPC, cutscene) for `seconds`; dropped as soon as the player moves.</summary>
        public void LookAt(Vector2 dir, float seconds)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            lookDir = dir.normalized; lookUntil = Time.time + seconds;
            facing = lookDir;
        }
        public void StopLook() => lookUntil = 0f;
        /// <summary>Direction currently faced while talking (zero = none).</summary>
        public Vector2 LookDir => Time.time < lookUntil ? lookDir : Vector2.zero;

        /// <summary>Ren staggers (e.g. landing after being thrown).</summary>
        public void PlayStagger() => anim?.Stagger();
        /// <summary>Scripted cutscene slash (Ren breaking the Vessel's hand) — animation only, no damage.</summary>
        public void PlayCutsceneSlash(Vector2 dir) => anim.Attack(1, dir, 1.2f);

        /// <summary>
        /// Cutscene pose (StoryScenes `ren` command). Only changes the animation, never HP.
        /// draw = draw the sword · sheathe = sheathe · stagger = stagger · fall / down = collapse · getup / up = stand up.
        /// </summary>
        public void CutscenePose(string pose)
        {
            if (anim == null) return;
            switch ((pose ?? "").Trim().ToLowerInvariant())
            {
                case "draw": anim.DrawNow(); SoulRequiem.Core.Sfx.Play("sword_draw", transform.position); break;
                case "sheathe": anim.SheatheNow(); SoulRequiem.Core.Sfx.Play("sword_sheathe", transform.position); break;
                case "stagger": anim.Stagger(); break;
                case "fall": case "down": anim.Die(); SoulRequiem.Core.Sfx.Play("body_fall", transform.position); break;
                case "getup": case "up": anim.Revive(); break;
                default: Debug.LogWarning($"[Player] unknown cutscene pose '{pose}'"); break;
            }
        }
    }
}
