using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    public enum EnemyKind
    {
        Melee,       // Greystone Warrior, Mad Bear, Blightdrinker, Spider, Snake
        Ranged,      // Greystone Seer: keeps distance, strikes down where the player stands
        SpikeBurst,  // Thornback Lizard: closes in then fires spikes in a ring
        Boss,        // Grok: wind-up -> charge and slam -> stagger (counter window)
        Turret,      // Camp Gunner: stands still, turns its barrel, fires arcing shells with a shadow marking the landing spot
        Beholder,    // Plague Eye: stands still over its pit, skills driven by PlagueEyeSkills
        Custom       // Map 2 v2: all behaviour driven by an IEnemySkills component (ToadSkills, RamSkills, LungeSkills...)
    }

    /// FIXED NUMBERS. Unity stores enums in prefabs as NUMBERS; inserting a value in the middle shifts every prefab built earlier
    /// (e.g. Greystone Spearman read as the turtle Shell: invulnerable for 2 s after every hit; Greystone Seer spinning its spear instead of rock spikes).
    /// New values are always added AT THE END with a new number; NEVER insert / reorder.
    public enum EnemySpecial
    {
        None = 0,
        Spin = 1,          // Greystone Warrior / Spearman: wind-up then spin — dash away
        GroundSpikes = 2,  // Greystone Seer: rock spikes rise under the player's feet
        Shell = 3          // Ichor Snapper (turtle): retreats into its shell when hit, invulnerable for a few seconds — hit it when it comes out
    }

    /// <summary>
    /// Enemy AI (every map). All distances in Unity units (1 map tile = 0.32).
    /// Animator states used when present: Idle, Move, Attack, Attack2, Windup, Spin, SpinEnd,
    /// Recovery, Death. A missing state falls back to the closest one (see Play).
    /// Source sprites face right; flipped toward the player.
    /// Files of this class: EnemyAI.cs (Inspector settings, setup, facing, animation, hit / death) · .Brain.cs (brain: chase, return home, trapped)
    ///   .Attacks.cs (attacks per EnemyKind / EnemySpecial: melee, spin, dash, spikes, ranged, boss combo) · .Api.cs (functions for IEnemySkills / bosses, Retarget, Provoke)
    /// Enemies with their own skill set (Kind = Custom / Beholder): an IEnemySkills component (Enemies/Bosses/*Skills.cs) replaces the brain.
    /// Stats can come from an EnemyDefinition asset (definition). Sounds: SfxHooks. Spawned by: MapEncounterDirector zones.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public partial class EnemyAI : MonoBehaviour
    {
        [Tooltip("Stats of this enemy type (Assets/Enemies/<MapN>/Definitions). When set, overrides the stats below when the enemy spawns.")]
        public EnemyDefinition definition;

        [Header("Chung")]
        public string displayName = "Enemy";
        /// <summary>Hide the boss HP bar at the bottom of the HUD (e.g. The Registrar — the fight uses the PHASE / PAGE panel at the top).</summary>
        [System.NonSerialized] public bool HideBossBar;
        public EnemyKind kind = EnemyKind.Melee;
        public float moveSpeed = 1.6f;
        public float aggroRange = 3.2f;
        [Tooltip("GAP between the enemy collider and the player collider to start attacking (Unity units)")]
        public float attackRange = 0.15f;
        public float damage = 10f;
        public float windup = 0.35f;
        public float attackTime = 0.6f;
        public float attack2Time = 0.6f;
        public float cooldown = 1.0f;
        [Tooltip("Beyond this distance from the spawn point the enemy gives up, returns and heals (0 = no limit)")]
        public float leashRange = 5f;

        [Header("Effects on the player")]
        public float poisonPercent = 0f;      // 0.02 = 2% HP/second
        public float poisonDuration = 5f;
        public float stunSeconds = 0f;
        public float knockback = 0f;

        [Header("Ranged / spikes")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 2.5f;
        public float projectileLifetime = 2f;
        public float keepDistance = 1.8f;
        public int spikeCount = 10;
        public float spikeTriggerRange = 0.9f;
        [Tooltip("Seer: the player within this gap -> staff swipe (state Attack2). 0 = off")]
        public float closeRange = 0f;

        [Header("Ground strike (Seer, Camp Gunner)")]
        public Sprite[] strikeFrames;
        public int strikeImpactFrame = 0;
        public float strikeWarn = 0.6f;
        public float strikeRadius = 0.3f;
        public float strikeFps = 14f;
        public Color strikeColor = new Color(0.45f, 1f, 0.4f, 1f);
        public Sprite shellSprite;              // Camp Gunner: arcing shell

        [Header("Special attack (once every N attacks)")]
        public EnemySpecial special = EnemySpecial.None;
        public int specialEvery = 0;
        public float specialWindup = 0.8f;
        public float spinTime = 1.2f;
        public float spinEndTime = 0.4f;
        public float spinRadius = 0.55f;
        public float spinTick = 0.35f;
        public float spinMoveFactor = 0.55f;
        public Sprite[] specialFrames;          // Seer: rock spikes
        public int specialImpactFrame = 1;
        public float specialRadius = 0.38f;

        [Header("Combo / enrage (Mad Bear)")]
        public int comboHits = 1;
        public float comboGap = 0.2f;
        [Range(0f, 1f)] public float enrageAt = 0f;
        public float dashSpeed = 4f;
        public float dashTime = 0.35f;
        public float dashCooldown = 3.5f;

        [Header("Turret (Camp Gunner)")]
        [Tooltip("Order: Right, UpRight, Up, DownRight, Down — left directions are flipped horizontally")]
        public Sprite[] aimSprites;

        [Header("Boss")]
        public float chargeSpeed = 5f;
        public float chargeTime = 0.45f;
        [Tooltip("Boss charging into a stone pillar (RingPillar) -> the pillar breaks, the boss is stunned this many seconds")]
        public float pillarStun = 3f;
        public float recoveryTime = 1.6f;
        [Tooltip("Max gap for the slam to hit")]
        public float slamRadius = 0.35f;
        [Range(0f, 1f)] public float phase2At = 0.5f;

        public event Action<EnemyAI> Phase2Reached;
        public event Action<EnemyAI> Killed;

        [Tooltip("Boss that is not of kind Boss (e.g. the ranged Plague Eye): still counts as a boss — no leash, has phase 2")]
        public bool treatAsBoss = false;
        [Tooltip("Floating (Plague Eye): ignores walls/water, only kept inside the `bounds` rect (set by the Director = its zone)")]
        public bool floating = false;
        [HideInInspector] public Rect bounds;

        [Header("Turtle shell (EnemySpecial.Shell)")]
        public float shellTime = 2.0f;
        public float shellCooldown = 3.5f;

        public Health Health { get; private set; }
        public bool IsBoss => kind == EnemyKind.Boss || treatAsBoss;
        /// <summary>Has spotted the player and is chasing / attacking (used by MapEncounterDirector.InCombat).</summary>
        public bool Aggro => aggro;

        private Rigidbody2D rb;
        private Animator anim;
        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private Transform player;
        private Health playerHealth;
        private Vector2 desired;
        private bool aggro;
        private bool phase2Fired;
        private string currentState;
        // ---- facing up/down (enemies with "<State>_S"/"<State>_N" states); sideways = base state, flipped left/right with flipX
        private bool hasDirs;
        private string dirSuffix = "";          // "" sideways, "_S" facing down, "_N" facing up
        private string currentBase;
        private Vector2 lastLook = Vector2.right;
        private Vector2 home;
        private Collider2D bodyCol, playerCol;
        private bool hasDeathState;
        private bool returning;
        private int attackCount;
        private float nextDash, nextShot, nextShell;
        private bool inShell;

        // obstacle avoidance, line of sight, anti-"trap" (the player hitting from behind a rock where the enemy cannot reach)
        private EnemySteering steering;
        private float lastDamagedAt = -99f;
        private float reachCheckAt, reachGap = float.MaxValue, noProgressT;
        private float reaggroGuardUntil;          // just retreated after being trapped: no re-aggro for a while unless the player closes in
        private float returningSince;
        private float sightCheckAt;
        private bool seesPlayer;
        private const float TrapNoProgressSeconds = 2f;   // cannot get closer for this many seconds...
        private const float TrapRecentHitSeconds = 2.5f;  // ...while recently hit -> counts as trapped
        private IEnemySkills skills;

        private void Awake()
        {
            if (definition != null)
            {
                definition.ApplyTo(this);
                attackTime = Mathf.Max(attackTime, windup + 0.1f);   // windup changed in the asset: the attack must not end before its hit frame
            }
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            anim = GetComponentInChildren<Animator>();
            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            Health = GetComponent<Health>();
            bodyCol = GetComponent<Collider2D>();
            vfx = GetComponent<EnemyVfx>();
            Health.Died += OnDied;
            Health.Damaged += OnDamaged;
            if (floating)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;       // flies over Ichor pools / channel banks
                rb.useFullKinematicContacts = false;
            }
        }

        private void Start()
        {
            home = transform.position;
            if (vfx != null) vfx.Spawned();
            nextShot = Time.time + UnityEngine.Random.Range(0.6f, 1.4f);
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                player = pc.transform;
                playerHealth = pc.GetComponent<Health>();
                foreach (var c in pc.GetComponents<Collider2D>()) if (!c.isTrigger) { playerCol = c; break; }
            }
            hasDirs = HasState("Idle_S") || HasState("Move_S") || HasState("Idle_N") || HasState("Move_N");
            if (!floating && kind != EnemyKind.Turret) steering = new EnemySteering(rb, bodyCol);
            Play("Idle");
            EnemyHealthBar.Attach(this);                     // HP bar over normal enemies (bosses use the HUD bar)
            StartCoroutine(Brain());
        }

        private void Update()
        {
            if (anim != null && currentBase == "Move" && !Health.IsDead) anim.speed = MoveAnimSpeed;   // the skill set changes the pace mid-walk
            if (!hasDirs || Health.IsDead) return;
            Vector2 look = LookVector();
            // while attacking / winding up / charging: keep the direction chosen at the start of the motion
            if (currentBase != "Idle" && currentBase != "Move") return;
            string s2 = PickSuffix(look);
            if (s2 != dirSuffix) { dirSuffix = s2; ApplyFacing(); Play(currentBase); }
        }

        private Vector2 LookVector()
        {
            Vector2 look = LookAtPlayer && player != null ? (Vector2)(player.position - transform.position)
                         : desired.sqrMagnitude > 0.0025f ? desired
                         : player != null ? (Vector2)(player.position - transform.position) : lastLook;
            if (look.sqrMagnitude > 0.0001f) lastLook = look;
            return lastLook;
        }

        /// The dominant axis decides the direction; hysteresis (1.3x) so it does not flicker around 45°.
        private string PickSuffix(Vector2 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y);
            bool vertical = dirSuffix == "" ? ay > ax * 1.3f : !(ax > ay * 1.3f);
            return vertical ? (v.y < 0f ? "_S" : "_N") : "";
        }

        private void ApplyFacing()
        {
            if (sr == null) return;
            if (dirSuffix != "") sr.flipX = false;                   // front/back sprites: no flip (keeps the weapon in the correct hand)
            else if (Mathf.Abs(lastLook.x) > 0.01f) sr.flipX = lastLook.x < 0f;
        }

        private void FixedUpdate()
        {
            if (Health.IsDead || kind == EnemyKind.Turret || Airborne) return;
            steering?.TrackProgress(desired, Time.fixedDeltaTime);
            Vector2 extra = Health.ConsumeExtraVelocity(Time.fixedDeltaTime);
            Vector2 next = rb.position + (desired * Health.MoveMul + extra) * Time.fixedDeltaTime;   // slowed (Bind / Chill)
            if (floating && bounds.width > 0f)
                next = new Vector2(Mathf.Clamp(next.x, bounds.xMin + 0.3f, bounds.xMax - 0.3f), Mathf.Clamp(next.y, bounds.yMin + 0.3f, bounds.yMax - 0.3f));
            rb.MovePosition(next);
        }

        /// <summary>Gap between the enemy body and the player body (0 = touching). Avoids "running in place"
        /// when colliders stop the enemy from getting close enough to the player's centre.</summary>
        private float Gap()
        {
            if (player == null) return float.MaxValue;
            if (bodyCol != null && playerCol != null && bodyCol.enabled && playerCol.enabled)
            {
                var cd = bodyCol.Distance(playerCol);
                if (cd.isValid) return Mathf.Max(0f, cd.distance);
            }
            return ((Vector2)(player.position - transform.position)).magnitude;
        }

        private void Face(float dx)
        {
            if (hasDirs && dirSuffix != "") return;                  // facing up/down: no flip
            if (sr != null && Mathf.Abs(dx) > 0.01f) sr.flipX = dx < 0f;
        }

        private IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !Health.IsDead) yield return null;
        }

        private IEnumerator Telegraph(float seconds, Color c)
        {
            // no tut_glow hint (teaching the roll) — the DODGE [Q] pause screen in the lizard zone covers it
            float end = Time.time + seconds;
            Color tint = c * baseColor;
            while (Time.time < end && !Health.IsDead)
            {
                if (sr != null) sr.color = Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? tint : baseColor;
                yield return null;
            }
            if (sr != null) sr.color = baseColor;
        }

        private bool HasState(string state) =>
            anim != null && anim.runtimeAnimatorController != null && anim.HasState(0, Animator.StringToHash(state));

        private void Play(string state, bool restart = false)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            if (hasDirs)
            {
                // new motion (attack, wind-up, charge, heal): pick the direction toward the target at the start; death always falls sideways
                if (state == "Death") { dirSuffix = ""; ApplyFacing(); }
                else if (state != "Idle" && state != "Move" && (restart || state != currentBase)) { dirSuffix = PickSuffix(LookVector()); ApplyFacing(); }
                if (dirSuffix != "" && HasState(state + dirSuffix))
                {
                    string resolved = state + dirSuffix;
                    if (!restart && resolved == currentState) return;
                    anim.Play(Animator.StringToHash(resolved), 0, 0f);
                    currentState = resolved; currentBase = state;
                    SfxHooks.EnemyState(this, state); // sound
                    anim.speed = state == "Move" ? MoveAnimSpeed : 1f;   // walking slowly: steps slow down to match
                    return;
                }
                // no up/down version of this motion: use the sideways one, facing the target
                if (dirSuffix != "" && sr != null && Mathf.Abs(lastLook.x) > 0.01f) sr.flipX = lastLook.x < 0f;
            }
            if (!restart && state == currentState) return;
            if (!HasState(state))
            {
                // a missing state falls back to the closest meaning
                if (state == "Windup" || state == "Recovery" || state == "SpinEnd" || state == "GuardIn" || state == "GuardOut") Play("Idle", restart);
                else if (state == "Spin" || state == "Attack2") Play("Attack", restart);
                return;
            }
            anim.Play(Animator.StringToHash(state), 0, 0f);
            currentState = state; currentBase = state;
            SfxHooks.EnemyState(this, state); // sound
            anim.speed = state == "Move" ? MoveAnimSpeed : 1f;   // walking slowly: steps slow down to match
        }

        private void OnDamaged(Health h, float amount)
        {
            lastDamagedAt = Time.time;
            if (!returning) { if (!aggro) SfxHooks.EnemyAlert(this); aggro = true; } // sound
            if (special == EnemySpecial.Shell && !inShell && !h.IsDead && Time.time >= nextShell) StartCoroutine(ShellRoutine());
            if (IsBoss && !phase2Fired && h.Percent <= phase2At)
            {
                phase2Fired = true;
                Phase2Reached?.Invoke(this);
            }
        }

        /// Turtle: retreats into its shell (invulnerable) for shellTime seconds then comes out, cooldown shellCooldown.
        private IEnumerator ShellRoutine()
        {
            inShell = true;
            desired = Vector2.zero;
            Play("GuardIn", true);
            Health.Invulnerable = true;
            yield return new WaitForSeconds(shellTime);
            if (Health.IsDead) yield break;
            Play("GuardOut", true);
            yield return new WaitForSeconds(0.25f);
            Health.Invulnerable = false;
            inShell = false;
            nextShell = Time.time + shellCooldown;
            Play("Idle", true);
        }

        private void OnDied(Health h)
        {
            StopAllCoroutines();
            if (vfx != null) vfx.Death();
            desired = Vector2.zero;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            rb.simulated = false;
            currentState = null;
            hasDeathState = HasState("Death");
            if (hasDeathState) Play("Death");
            else if (anim != null) anim.speed = 0f;          // no death animation: freeze then fade out
            Killed?.Invoke(this);
            StartCoroutine(FadeAndDestroy());
        }

        private IEnumerator FadeAndDestroy()
        {
            float hold = IsBoss ? 1.5f : hasDeathState ? 1.0f : 0.4f;
            yield return new WaitForSeconds(hold);
            float t = 0f;
            Color c0 = hasDeathState ? baseColor : baseColor * new Color(0.75f, 0.75f, 0.75f, 1f);
            Vector3 s0 = transform.localScale;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, 1f - t / 0.5f);
                if (!hasDeathState) transform.localScale = new Vector3(s0.x * (1f + 0.2f * t / 0.5f), s0.y * (1f - 0.4f * t / 0.5f), s0.z);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
