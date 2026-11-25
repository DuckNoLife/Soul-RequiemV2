using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// FUNCTIONS FOR CUSTOM SKILL SETS (IEnemySkills, BossSkillsBase) and outside code: play animations, fire bullets, teleport, warning flashes,
    /// damage Ren, Desired / LookAtPlayer / Held, Retarget (fight another enemy for a while), Provoke (aggro at once).
    /// </summary>
    public partial class EnemyAI
    {
        // ---- for boss skill sets (PlagueEyeSkills)
        public Transform Player => player;
        public Health PlayerHealth => playerHealth;
        public bool InPhase2 => phase2Fired;
        public SpriteRenderer Body => sr;
        /// <summary>Sorting layer / order of the enemy body — so VFX and bullets draw on the same layer.</summary>
        public int SortingLayer => sr != null ? sr.sortingLayerID : 0;
        public int SortingOrder => sr != null ? sr.sortingOrder : 2;
        /// <summary>Make a renderer (bullet, VFX) draw on the enemy body's layer, order + <paramref name="orderOffset"/>.</summary>
        public void MatchSorting(Renderer r, int orderOffset)
        {
            if (r == null || sr == null) return;
            r.sortingLayerID = sr.sortingLayerID;
            r.sortingOrder = sr.sortingOrder + orderOffset;
        }
        public void PlayState(string state, bool restart = true) => Play(state, restart);

        /// <summary>Image for the boss name card — always the standing pose FACING DOWN (Idle_S) if present, otherwise sideways Idle
        /// (not flipped), whichever way the boss faces. Taken by jumping the Animator to that state for a moment, then back.</summary>
        public Sprite PortraitSprite()
        {
            if (sr == null) return null;
            if (anim == null || anim.runtimeAnimatorController == null || !anim.isActiveAndEnabled) return sr.sprite;
            string pick = HasState("Idle_S") ? "Idle_S" : HasState("Idle") ? "Idle" : null;
            if (pick == null) return sr.sprite;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            float speed = anim.speed;
            anim.speed = 1f;
            anim.Play(Animator.StringToHash(pick), 0, 0f);
            anim.Update(0f);
            var shot = sr.sprite;
            anim.Play(info.fullPathHash, 0, info.normalizedTime);
            anim.Update(0f);
            anim.speed = speed;
            return shot;
        }
        public bool HasAnimState(string state) => HasState(state);
        /// Fire one enemy bullet (projectilePrefab) with optional direction / speed / damage (boss skill sets).
        public Projectile ShootProjectile(Vector2 dir, float speed, float dmg, float lifetime = -1f)
        {
            float s0 = projectileSpeed, d0 = damage, l0 = projectileLifetime;
            projectileSpeed = speed; damage = dmg; if (lifetime > 0f) projectileLifetime = lifetime;
            SpawnProjectile(dir, false);
            projectileSpeed = s0; damage = d0; projectileLifetime = l0;
            return null;
        }

        /// Instant teleport (bosses: Vespers Step, Mist Step).
        public void TeleportTo(Vector2 p)
        {
            desired = Vector2.zero;
            if (rb != null) rb.position = p;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        /// Play a motion starting at second `seconds` of the clip (e.g. a second slash skipping the wind-up).
        public void PlayStateFrom(string state, float seconds)
        {
            Play(state, true);
            if (anim == null || anim.runtimeAnimatorController == null || string.IsNullOrEmpty(currentState) || seconds <= 0f) return;
            foreach (var c in anim.runtimeAnimatorController.animationClips)
                if (c != null && c.length > 0f && c.name.EndsWith("_" + currentState))
                {
                    anim.Play(Animator.StringToHash(currentState), 0, Mathf.Clamp01(seconds / c.length));
                    return;
                }
        }
        public IEnumerator WaitFor(float seconds) => Wait(seconds);
        public IEnumerator Flash(float seconds, Color c) => Telegraph(seconds, c);
        public void Hurt(float amount, Vector2 from, float push)
        {
            if (playerHealth == null || player == null) return;
            Vector2 dir = (Vector2)player.position - from;
            if (!playerHealth.TakeDamage(amount, dir, push)) return;
            if (vfx != null) vfx.HurtPlayer(player.position);
            if (poisonPercent > 0f) playerHealth.ApplyPoison(poisonPercent, poisonDuration);
        }

        // ---- for IEnemySkills (Map 2 v2 enemies)
        public Vector2 Desired { get => desired; set => desired = value; }
        /// <summary>Set by a skill set to keep facing the player while stepping another way (e.g. circling). Default: face the step direction.</summary>
        public bool LookAtPlayer { get; set; }
        /// <summary>Hold still (boss intro scene) — the brain does not move, attack or change animation.</summary>
        public bool Held { get; set; }
        /// <summary>Enemy summoned by a boss (Provoke) — never pulled back to its spawn, never retreats for being "trapped".</summary>
        public bool Summoned { get; private set; }

        // ------------------------------------------------------------------ Retarget
        private Coroutine retargetCo;
        /// <summary>Retargeted: treats another enemy as "the player".</summary>
        public bool Retargeted { get; private set; }
        /// <summary>Temporarily treat <paramref name="other"/> as the target (every existing attack / skill of the enemy hits it) for <paramref name="seconds"/> s;
        /// time up or target dead -> back to Ren. Bosses are not fooled.</summary>
        public void Retarget(EnemyAI other, float seconds)
        {
            if (other == null || other == this || other.Health == null || Health == null || Health.IsDead || IsBoss) return;
            if (retargetCo != null) StopCoroutine(retargetCo);
            retargetCo = StartCoroutine(RetargetRoutine(other, seconds));
        }

        private IEnumerator RetargetRoutine(EnemyAI other, float seconds)
        {
            Retargeted = true;
            player = other.transform; playerHealth = other.Health; playerCol = other.bodyCol;
            aggro = true; returning = false; reachGap = float.MaxValue; noProgressT = 0f;
            float end = Time.time + seconds;
            while (Time.time < end && other != null && other.Health != null && !other.Health.IsDead && !Health.IsDead) yield return null;
            var pc = FindFirstObjectByType<PlayerController>();
            player = null; playerHealth = null; playerCol = null;
            if (pc != null)
            {
                player = pc.transform;
                playerHealth = pc.GetComponent<Health>();
                foreach (var c in pc.GetComponents<Collider2D>()) if (!c.isTrigger) { playerCol = c; break; }
            }
            Retargeted = false; retargetCo = null;
        }

        /// <summary>Aggro on the player at once, ignoring line of sight / detection range (e.g. Gorgemaw's toadlings spawning in a room corner
        /// beyond aggroRange would stand still waiting for Ren). summoned = summoned enemy: no leash to its spawn.</summary>
        public void Provoke(bool summoned = true)
        {
            aggro = true; reachGap = float.MaxValue; noProgressT = 0f;
            if (summoned) { Summoned = true; leashRange = 0f; }
        }
        /// <summary>Every enemy stands still during cutscenes (CutsceneState) — except one "acting" in the scene (e.g. the bandit
        /// stepping up to shout at Ren in the Map 1 opening): the scene sets this flag on it.</summary>
        public bool CutsceneActor { get; set; }
        /// <summary>The enemy starts winding up a melee attack (used by the dodge tutorial screen).</summary>
        public static event System.Action<EnemyAI> MeleeWindup;
        /// <summary>The enemy starts ANY attack with a wind-up (melee, spike burst, ranged) — used by the dodge tutorial in the lizard zone.</summary>
        public static event System.Action<EnemyAI> AttackStarted;
        /// <summary>A bullet / spike just flew out (visible on screen) — used by the DODGE screen in the lizard zone.</summary>
        public static event System.Action<EnemyAI> ProjectilesFired;
        /// <summary>Move animation speed (1 = normal). Lowered when walking slowly so the feet match the distance, no sliding.</summary>
        public float MoveAnimSpeed { get; set; } = 1f;
        private EnemyVfx vfx;
        public EnemyVfx Vfx => vfx;
        public Vector2 DirToPlayer => player != null ? (Vector2)(player.position - transform.position) : Vector2.right;
        public float GapToPlayer() => Gap();
        public void HitPlayerNow(float amount) => HitPlayer(amount);
        public void FaceX(float dx) => Face(dx);
        public Rigidbody2D Rb => rb;
        public Collider2D BodyCollider => bodyCol;
        [HideInInspector] public bool Airborne;          // off the ground (ToadSkills jump): FixedUpdate does not push the body
    }
}
