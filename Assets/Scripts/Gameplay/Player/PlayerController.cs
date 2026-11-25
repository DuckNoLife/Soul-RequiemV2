using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// REN'S MOVEMENT: reads GameInput.Move, applies the multipliers (stun, attacking, slows, Relic), updates the Animator (DirX / DirY / Speed).
    ///   • Speed: moveSpeed (Inspector). Ice floor (PlayerStatus.OnIce) slides; wind (WindVel) pushes; Health.ConsumeExtraVelocity = knockback + roll.
    ///   • Big steps (roll / pushed) are swept first with rb.Cast (Sweep) -> never pass through tile outline walls.
    ///   • Pinned by an enemy (trying to move without moving for PinTime seconds) -> temporarily ignore collisions with that enemy (CheckPinned).
    ///   • Footsteps every StepEvery seconds (SfxHooks.FootstepId by floor type).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;

        private Rigidbody2D rb;
        private Animator animator;
        private Health health;
        private PlayerStatus status;           // boss slow (Map 3)
        private PlayerCombat combat;  // stand still while slashing / stomping
        private Vector2 input;
        private Vector2 lastFacing = Vector2.down; // remember the last facing direction

        // anti-pinning by heavy enemies.
        // Ren tries to move but barely moves for `PinTime` seconds while touching an enemy body -> temporarily disable Ren <-> that enemy collisions,
        // re-enabled once they are apart. Applies to every enemy / boss on every map. Attacks are unchanged (enemies hit by distance).
        private const float PinTime = 0.25f, PinRadius = 0.3f, ReleaseGap = 0.15f, MinIgnore = 0.6f;
        private Vector2 lastPos, lastStep;
        private float pinnedFor;
        private Collider2D[] myCols;
        private readonly Dictionary<Collider2D, float> ghosted = new Dictionary<Collider2D, float>();
        private readonly List<Collider2D> releaseBuf = new List<Collider2D>();

        // Roll / knockback moves 0.2 u per physics step, which can pass
        // OUTLINE colliders (walls built from tiles = composite outline, no thickness) -> stuck inside the wall block.
        // Steps larger than a walking step are SWEPT first with rb.Cast: stop next to the wall (keeping Skin) then slide the rest along the wall.
        private const float Skin = 0.015f;
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
        private ContactFilter2D castFilter;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            animator = GetComponent<Animator>();
            health = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            status = PlayerStatus.Of(this);
            myCols = GetComponentsInChildren<Collider2D>();
            lastPos = rb.position;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;   // no passing through walls while dashing
            castFilter = new ContactFilter2D { useTriggers = false };
            castFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        }

        private void Update()
        {
            input = ReadInput();

            // stunned / locked (drinking) / down: no control
            if (health != null && (health.IsDead || health.IsStunned)) input = Vector2.zero;
            if (combat != null && combat.IsAttacking) input *= Mathf.Clamp01(combat.LockedMoveMul);   // 0 = stand still
            if (health != null) input *= health.MoveMul;                                            // ice bullet slow

            // if the player pressed a movement key, update the last facing direction
            if (input.sqrMagnitude > 0.0001f)
            {
                if (combat != null) combat.StopLook();           // moving -> stop facing the speaker
                // normalized so x, y always stay between -1 and 1
                lastFacing = input.normalized;
            }

            // a skill forces the facing direction (e.g. Judgement Ray follows the mouse) -> the character turns that way
            if (combat != null && combat.FacingOverride.sqrMagnitude > 0.01f) lastFacing = combat.FacingOverride.normalized;
            else if (combat != null && combat.LookDir.sqrMagnitude > 0.01f) lastFacing = combat.LookDir;   // face the speaker
            // reading a panel -> face the object being read (4 directions like the walk animation)
            if (MechanismPanels.Focus.HasValue)
            {
                Vector2 d = MechanismPanels.Focus.Value - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0004f) lastFacing = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2(Mathf.Sign(d.x), 0f) : new Vector2(0f, Mathf.Sign(d.y));
            }

            // update the Animator Blend Tree parameters
            animator.SetFloat("DirX", lastFacing.x);
            animator.SetFloat("DirY", lastFacing.y);
            animator.SetFloat("Speed", input.magnitude); // Speed = 0 when standing still, > 0 when moving
        }

        private float stepIn;   // sound: footsteps
        private Vector2 iceVel;   // velocity carried on the ice floor
        private const float IceGrip = 2.2f;   // grip on ice (x run speed / second)
        private const float StepEvery = 0.34f;

        private void FixedUpdate()
        {
            Vector2 move = input.sqrMagnitude > 1f ? input.normalized : input;
            Vector2 velocity = move * moveSpeed * (status != null ? status.SpeedMul : 1f) * RelicBoons.SpeedMul;   // Penitent Haste
            if (status != null && status.OnIce)                                      // ice floor — slow acceleration / deceleration, slides when keys are released
            {
                iceVel = Vector2.MoveTowards(iceVel, velocity, moveSpeed * IceGrip * Time.fixedDeltaTime);
                velocity = iceVel;
            }
            else iceVel = velocity;
            if (status != null) velocity += status.WindVel;                       // blizzard wind
            if (health != null) velocity += health.ConsumeExtraVelocity(Time.fixedDeltaTime);   // knockback + Roll
            CheckPinned();
            lastPos = rb.position;
            lastStep = velocity * Time.fixedDeltaTime;
            if (lastStep.magnitude > moveSpeed * RelicBoons.SpeedMul * Time.fixedDeltaTime * 1.1f) lastStep = Sweep(lastStep);   // dash / pushed
            rb.MovePosition(rb.position + lastStep);
            if (move.sqrMagnitude > 0.04f && Time.timeScale > 0f)   // sound
            {
                stepIn -= Time.fixedDeltaTime;
                if (stepIn <= 0f) { stepIn = StepEvery; SoulRequiem.Core.Sfx.Play(SfxHooks.FootstepId(), rb.position); }
            }
            else stepIn = 0.08f;
        }

        /// <summary>Big step -> stop before static obstacles (walls, rocks, gates, tables...), slide the rest along the wall surface.
        /// Ignores triggers, enemies (their bodies are handled by the physics solver + the CheckPinned pass-through) and colliders currently passed through.</summary>
        private Vector2 Sweep(Vector2 step)
        {
            float len = step.magnitude;
            float allowed = CastFree(step / len, len, out Vector2 normal);
            if (allowed >= len) return step;
            Vector2 moved = step / len * allowed;
            Vector2 rest = step - moved;
            Vector2 tangent = rest - Vector2.Dot(rest, normal) * normal;
            float tl = tangent.magnitude;
            if (tl > 0.0005f) moved += tangent / tl * CastFree(tangent / tl, tl, out _);
            return moved;
        }

        /// <summary>How far along `dir` (≤ len) before touching a static obstacle; `normal` = surface normal at the contact.</summary>
        private float CastFree(Vector2 dir, float len, out Vector2 normal)
        {
            normal = Vector2.zero;
            float allowed = len;
            int n = rb.Cast(dir, castFilter, castHits, len + Skin);
            for (int i = 0; i < n; i++)
            {
                var h = castHits[i];
                var c = h.collider;
                if (c == null || c.isTrigger || ghosted.ContainsKey(c)) continue;
                if (Vector2.Dot(h.normal, dir) >= -0.01f) continue;                 // moving away / parallel to the wall
                if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;   // enemies, pushable objects
                float d = Mathf.Max(0f, h.distance - Skin);
                if (d < allowed) { allowed = d; normal = h.normal; }
            }
            return allowed;
        }

        /// <summary>Measure the last step: trying to move without moving + touching an enemy -> "pass through" that enemy (temporarily disable the collision).</summary>
        private void CheckPinned()
        {
            ReleaseGhosts();
            float want = lastStep.magnitude;
            if (want < 0.002f) { pinnedFor = 0f; return; }
            float moved = (rb.position - lastPos).magnitude;
            pinnedFor = moved < want * 0.25f ? pinnedFor + Time.fixedDeltaTime : 0f;
            if (pinnedFor < PinTime) return;
            pinnedFor = 0f;
            foreach (var c in Physics2D.OverlapCircleAll(rb.position, PinRadius))
            {
                if (c == null || c.isTrigger || ghosted.ContainsKey(c) || c.GetComponentInParent<EnemyAI>() == null) continue;
                foreach (var mine in myCols) if (mine != null && !mine.isTrigger) Physics2D.IgnoreCollision(mine, c, true);
                ghosted[c] = Time.time;
                Debug.Log("[Unstuck] Ren pinned by " + c.GetComponentInParent<EnemyAI>().name + " -> passing through temporarily");
            }
        }

        /// <summary>Re-enable collisions with enemies that moved apart (or were destroyed).</summary>
        private void ReleaseGhosts()
        {
            if (ghosted.Count == 0) return;
            releaseBuf.Clear();
            foreach (var kv in ghosted)
            {
                var c = kv.Key;
                if (c == null) { releaseBuf.Add(c); continue; }
                if (Time.time - kv.Value < MinIgnore) continue;
                bool apart = true;
                foreach (var mine in myCols)
                    if (mine != null && !mine.isTrigger && mine.Distance(c).distance < ReleaseGap) { apart = false; break; }
                if (!apart) continue;
                foreach (var mine in myCols) if (mine != null && !mine.isTrigger) Physics2D.IgnoreCollision(mine, c, false);
                releaseBuf.Add(c);
            }
            foreach (var c in releaseBuf) ghosted.Remove(c);
        }

        private static Vector2 ReadInput() => GameInput.Move;     // keyboard / gamepad; menu open -> (0, 0)
    }
}
