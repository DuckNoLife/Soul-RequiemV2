using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// REN'S COMBAT — component on the Player: holds the SETTINGS (Inspector) and reads keys; the real work lives in the classes in Player/Abilities:
    ///   PlayerMelee (slash, 3-hit combo) · PlayerDodge (roll Q) · PlayerFlask (flask E) · PlayerStrikes (damage, hit-stop, camera kick)
    ///   PlayerHurtReactions (hit / stun / death) · PlayerAnimation (Animator) · PlayerActionState (lock / cancel actions).
    /// Other parts of this class:
    ///   PlayerCombat.Flasks.cs  flasks (count, max slots MaxFlaskSlots)
    ///   PlayerCombat.Api.cs     functions for Mantra skills (SkillDefinition.Cast), cutscenes, events Struck / SkillCast / MeleeSwung
    ///   PlayerCombat.Traits.cs  katana passives, Rampart book, weapon change
    /// Movement: PlayerController. Equippable skills: PlayerLoadout. Test keys: DevTools/DevCheats.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public partial class PlayerCombat : MonoBehaviour
    {
        [Header("Weapon")]
        [Tooltip("Held weapon (Resources/Weapons/*.asset). Empty = default Katana. Per-hit combo numbers come from here.")]
        public WeaponDefinition weapon;

        [Header("Attack (J / Left Click) — fallback when there is no weapon")]
        [Tooltip("Directions that have a slash animation (written by the Player Animator builder). The slash snaps to the closest one.")]
        [FormerlySerializedAs("iaidoDirs")] public Vector2[] directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down,
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f), new Vector2(-0.7071f, -0.7071f), new Vector2(0.7071f, -0.7071f) };
        [FormerlySerializedAs("iaidoDamage")] public float damage = 35f;
        [Tooltip("Seconds from button press to the hit frame.")]
        [FormerlySerializedAs("iaidoHitTime")] public float hitTime = 0.48f;
        [Tooltip("Seconds before the next slash can start (attack rate).")]
        [FormerlySerializedAs("iaidoLockTime")] public float lockTime = 0.6f;
        [Tooltip("Cooldown after a slash finishes (from its hit frame) before the next slash can start. Stops button-mashing chains.")]
        [Range(0f, 1.5f)] public float attackCooldown = 0.5f;
        [Tooltip("Seconds after the hit frame before moving cancels the rest of the slash (sheathing). Never longer than Lock Time.")]
        [Range(0f, 0.5f)] public float moveCancelAfterHit = 0.08f;
        [Tooltip("How far the slash reaches from its pivot (Unity units). The blade itself reaches about 0.46; the rest is a fair margin.")]
        public float range = 0.75f;
        [Tooltip("Width of the slash, in degrees, centred on the facing direction.")]
        [Range(30f, 360f)] public float arcDegrees = 140f;
        [Tooltip("Enemies this close to the pivot are hit whatever the angle (point blank).")]
        public float innerRadius = 0.3f;
        [Tooltip("Height of the slash pivot above the feet (Unity units), about the hips.")]
        public float pivotHeight = 0.2f;
        [FormerlySerializedAs("iaidoKnockback")] public float knockback = 2f;

        [Header("Input")]
        [Tooltip("Seconds an Attack / Roll press is remembered when it cannot happen yet (still slashing, roll on cooldown). 0 = off.")]
        [Range(0f, 0.4f)] public float inputBuffer = 0.2f;

        [Header("Getting Hit")]
        [Tooltip("Hits with at least this much knockback make the player stagger (interrupts attacks). Lighter hits only flinch.")]
        public float heavyHitKnockback = 3f;
        [Tooltip("Seconds the player cannot act while staggering from a heavy hit.")]
        public float staggerLock = 0.45f;

        [Header("Roll (Q)")]
        public float rollSpeed = 6f;
        [Tooltip("Seconds of invulnerability at the start of the roll.")]
        public float rollIFrames = 0.5f;
        public float rollCooldown = 1.5f;

        [Header("Healing Potion (E)")]
        public int potions = 3;
        [Tooltip("Max flask count (resting at a campfire / respawning refills to this). Capped at MaxFlaskSlots.")]
        public int maxPotions = 3;
        [Tooltip("Share of max HP restored per potion.")]
        [Range(0f, 1f)] public float healPercent = 0.5f;
        [Tooltip("Seconds the player is locked in place while drinking.")]
        public float healLock = 0.5f;

        [Header("Effects (PixelLab VFX)")]
        [Tooltip("Show the slash, hit and ground impact effects (Resources/VFX).")]
        public bool showVfx = true;
        [Tooltip("Scale of the slash and hit effects (the character sprite uses 0.7). The slash is also stretched to the slash range.")]
        public float vfxScale = 0.7f;
        [Tooltip("Real-time seconds the game freezes when the slash connects (hit-stop). 0 = off.")]
        public float hitStop = 0.06f;
        [Tooltip("How far (Unity units) the camera is kicked along the slash when it connects. 0 = off.")]
        public float cameraKick = 0.05f;
        [Tooltip("Draw the real hit area for a moment on every slash (for tuning range / arc).")]
        public bool debugHitbox = false;

        // ================================================================== state read from outside
        public Health Health { get; private set; }
        public PlayerLoadout Loadout { get; private set; }
        public float RollCooldownLeft => dodge != null ? dodge.CooldownLeft : 0f;
        /// <summary>Last facing direction (normalized).</summary>
        public Vector2 Facing => facing;
        /// <summary>Slashing / stomping / staggering (movement locked). PlayerController reads this flag.</summary>
        public bool IsAttacking => actions != null && actions.IsLocked;
        /// <summary>Invulnerable (only set by DevCheats).</summary>
        public bool GodMode { get; set; }
        /// <summary>Non-(0,0) = a skill forces the facing direction (PlayerController uses it instead of the movement direction).</summary>
        public Vector2 FacingOverride { get; set; }
        /// <summary>While actions are locked (IsAttacking) the player may still move at this factor (0 = stand still). Judgement Ray + Veil set 0.4.</summary>
        public float LockedMoveMul { get; set; }
        /// <summary>Status effects on Ren (slow, silence, roll lock, disarmed...) — added when needed.</summary>
        public PlayerStatus Status => status != null ? status : (status = PlayerStatus.Of(this));

        internal SpriteRenderer Sprite { get; private set; }
        internal int VfxLayer => Sprite != null ? Sprite.sortingLayerID : 0;
        internal int PlayerOrder => Sprite != null ? Sprite.sortingOrder : 2;

        // ================================================================== parts
        private PlayerActionState actions;
        private PlayerAnimation anim;
        private PlayerStrikes strikes;
        private PlayerMelee melee;
        private PlayerDodge dodge;
        private PlayerFlask flask;
        private PlayerHurtReactions reactions;
        private PlayerStatus status;
        private Vector2 facing = Vector2.down;
        private float attackQueuedUntil = -1f, rollQueuedUntil = -1f;   // input buffer: a press that cannot happen yet is remembered until this time

        private void Awake()
        {
            Health = GetComponent<Health>();
            Loadout = PlayerLoadout.Of(this);
            Sprite = GetComponentInChildren<SpriteRenderer>();
            anim = new PlayerAnimation(GetComponentInChildren<Animator>());   // Animator on the same object as the SpriteRenderer
            actions = new PlayerActionState(Health);
            strikes = new PlayerStrikes(this);
            melee = new PlayerMelee(this, anim, actions, strikes);
            if (weapon == null) weapon = WeaponDefinition.LoadDefault();
            WeaponView = PlayerWeaponView.Ensure(this);
            WeaponView.SetWeapon(weapon);
            IaiStance.Ensure(this);                                            // Iaijutsu hold charge bar
            dodge = new PlayerDodge(this, anim, actions);
            flask = new PlayerFlask(this, anim);
            reactions = new PlayerHurtReactions(this, anim, actions, flask);
            Health.Hit += reactions.OnHit;
            Health.Died += reactions.OnDied;
            Health.Revived += reactions.OnRevived;
            Health.Damaged += OnDamagedTrait;
        }

        private void Start()
        {
            potions = Mathf.Max(potions, maxPotions);                         // enter the map with full flasks
            if (!showVfx) return;
            melee.PreloadVfx();                                                // preload common VFX (loading them mid-fight the first time causes a 1-frame hitch)
            foreach (var n in new[] { "hit", "stun_stars", "heal_back", "heal_front", "ground_impact" }) SpriteVfx.Load(n);
        }

        private void OnDestroy()
        {
            if (Health == null || reactions == null) return;
            Health.Hit -= reactions.OnHit;
            Health.Died -= reactions.OnDied;
            Health.Revived -= reactions.OnRevived;
            Health.Damaged -= OnDamagedTrait;
        }

        private void OnDisable() => PlayerStrikes.ReleaseHitStop();

        // ================================================================== every frame: read keys
        private void Update()
        {
            anim?.Tick();                                                      // combat over -> sheathe the sword
            UpdateTraitGlow();
            if (Health != null) Health.DamageTakenMul = WeaponTraits.TakenMul(this) * RampartMul() * RelicBoons.TakenMul;
            if (Health.IsDead) return;
            var move = GameInput.Move;
            if (move.sqrMagnitude > 0.01f) facing = move.normalized;
            if (GameInput.Blocked) { attackQueuedUntil = rollQueuedUntil = -1f; return; }   // menu open: drop buffered presses

            reactions.UpdateStun();
            if (Health.IsStunned || MapMechanics.ConsoleActive) return;

            ReadAttack();
            ReadRoll();
            // Roll has priority over Attack (rolling is for cancelling / dodging)
            if (Time.time <= rollQueuedUntil && dodge.Ready)
            {
                rollQueuedUntil = attackQueuedUntil = -1f;
                melee.ResetCombo();                                            // roll = cancel the combo, put the weapon away
                StartCoroutine(dodge.Roll(facing));
            }
            else if (Time.time <= attackQueuedUntil && melee.Ready)
            {
                attackQueuedUntil = -1f;
                StartCoroutine(melee.Slash(facing));
            }
            if (GameInput.Pressed(GameAction.Heal)) flask.Drink();
            for (int i = 0; i < PlayerLoadout.SlotCount; i++)
                if (GameInput.Pressed(GameInput.SkillAction(i))) Loadout.TryCast(i);
        }

        /// <summary>Attack key / left click -> buffered. Ignored while disarmed; with an Iaijutsu sword the first hit becomes the HOLD strike.</summary>
        private void ReadAttack()
        {
            var mouse = Mouse.current;
            bool attack = GameInput.Pressed(GameAction.Attack) ||
                          (mouse != null && mouse.leftButton.wasPressedThisFrame && !MapMechanics.PanelOpen && !GameInput.MouseOverUI);
            if (attack && Status != null && Status.Disarmed) { attack = false; attackQueuedUntil = -1f; }
            if (attack && WeaponTraits.IsIaijutsu(this) && melee.Ready && melee.ComboStep == 0 && !(Time.time <= rollQueuedUntil))
            {
                attack = false; attackQueuedUntil = -1f;
                StartCoroutine(melee.IaiSlash(AttackHeld));
            }
            if (attack) attackQueuedUntil = Time.time + inputBuffer;
        }

        /// <summary>Roll key -> buffered. No rolling in prison clothes (disarmed); a locked roll (ghost skull) shows a message.</summary>
        private void ReadRoll()
        {
            if (GameInput.Pressed(GameAction.Roll)) rollQueuedUntil = Time.time + inputBuffer;
            if (Status != null && Status.Disarmed) rollQueuedUntil = -1f;
            if (Status != null && Status.RollSealed && Time.time <= rollQueuedUntil)
            {
                rollQueuedUntil = -1f;
                Status.SayRestraint("Your dash is sealed!");
            }
        }

        /// <summary>The attack key (or left mouse button) is still held — Iaijutsu hold.</summary>
        private bool AttackHeld()
        {
            if (GameInput.Held(GameAction.Attack)) return true;
            var m = Mouse.current;
            return m != null && m.leftButton.isPressed && !MapMechanics.PanelOpen && !GameInput.Blocked && !GameInput.Reading && !GameInput.MouseOverUI;
        }

        /// <summary>Tutorial screen (game paused waiting for an exact key): perform action `a` toward `dir` as if the player had just pressed it.</summary>
        public void QueueAction(GameAction a, Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.0001f) facing = dir.normalized;
            if (a == GameAction.Attack) attackQueuedUntil = Time.time + inputBuffer;
            else if (a == GameAction.Roll) rollQueuedUntil = Time.time + inputBuffer;
            else if (a == GameAction.Heal) flask.Drink();
            else if (a == GameAction.Skill1) Loadout.TryCast(0);
        }

        // ================================================================== Scene view
        /// <summary>Select the Player in the Scene view to see the slash arcs for each direction (red = facing direction).</summary>
        private void OnDrawGizmosSelected()
        {
            var m = melee ?? new PlayerMelee(this, new PlayerAnimation(null), new PlayerActionState(GetComponent<Health>()), null);
            Vector2 f = Application.isPlaying ? m.Snap(facing) : Vector2.down;
            var dirs = directions != null && directions.Length > 0 ? directions : new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            foreach (var v in dirs)
            {
                if (v.sqrMagnitude < 0.01f) continue;
                Vector2 n = v.normalized;
                Gizmos.color = Vector2.Dot(n, f) > 0.99f ? new Color(1f, 0.25f, 0.25f, 0.9f) : new Color(1f, 0.6f, 0.2f, 0.2f);
                var pts = m.ArcOutline(m.Pivot, n);
                for (int k = 0; k < pts.Length; k++) Gizmos.DrawLine(pts[k], pts[(k + 1) % pts.Length]);
            }
        }
    }
}
