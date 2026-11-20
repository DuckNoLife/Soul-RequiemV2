using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    public enum EnemyKind
    {
        Melee,       // Greystone Warrior, Mad Bear, Blightdrinker, Nhện, Rắn
        Ranged,      // Greystone Seer: giữ khoảng cách, đánh xuống điểm người chơi đứng
        SpikeBurst,  // Thornback Lizard: áp sát rồi tung gai tỏa tròn
        Boss,        // Grok: gồng -> lao tới đập -> khựng (điểm phản công)
        Turret,      // Camp Gunner: đứng yên, xoay nòng, bắn đạn pháo vòng cung có bóng báo điểm rơi
        Beholder,    // Plague Eye: đứng yên trên hố, chiêu do PlagueEyeSkills điều khiển
        Custom       // Map 2 v2: mọi hành vi do component IEnemySkills (ToadSkills, RamSkills, LungeSkills...) điều khiển
    }

    /// 13/11: GHI SỐ CỐ ĐỊNH. Unity lưu enum trong prefab bằng SỐ; trước đây Shell được chèn vào giữa (sau None) khi làm Map 2
    /// -> prefab Map 1 dựng từ trước (Spin = 1, GroundSpikes = 2) bị đọc lệch: Greystone Spearman thành RÙA (trúng đòn là
    /// bất tử 2 s — "đánh hiện trúng mà không mất máu"), Greystone Seer thành xoay giáo thay vì gai đá.
    /// Giá trị mới luôn thêm Ở CUỐI với số mới, KHÔNG chèn / đổi thứ tự.
    public enum EnemySpecial
    {
        None = 0,
        Spin = 1,          // Greystone Warrior / Spearman: gồng rồi xoay vòng — phải lướt ra
        GroundSpikes = 2,  // Greystone Seer: gai đá mọc dưới chân người chơi
        Shell = 3          // Ichor Snapper (rùa): trúng đòn thì thụt vào mai, bất tử vài giây — đánh lúc nó thò ra
    }

    /// <summary>
    /// AI quái Map 1. Mọi khoảng cách tính bằng đơn vị Unity (1 ô map = 0,32).
    /// Animator cần các state (có state nào thì dùng state đó): Idle, Move, Attack, Attack2, Windup, Spin, SpinEnd,
    /// Recovery, Death. Thiếu state nào thì tự rơi về state gần nhất (xem Play).
    /// Sprite gốc quay mặt sang phải; lật theo hướng người chơi.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        [Tooltip("Chỉ số của loại quái này (Assets/Enemies/<MapN>/Definitions). Có thì ghi đè các chỉ số bên dưới khi quái sinh ra.")]
        public EnemyDefinition definition;

        [Header("Chung")]
        public string displayName = "Enemy";
        public EnemyKind kind = EnemyKind.Melee;
        public float moveSpeed = 1.6f;
        public float aggroRange = 3.2f;
        [Tooltip("Khoảng HỞ giữa collider quái và collider người chơi để bắt đầu đánh (đơn vị Unity)")]
        public float attackRange = 0.15f;
        public float damage = 10f;
        public float windup = 0.35f;
        public float attackTime = 0.6f;
        public float attack2Time = 0.6f;
        public float cooldown = 1.0f;
        [Tooltip("Rời xa điểm sinh quá khoảng này thì bỏ đuổi, quay về và hồi máu (0 = không giới hạn)")]
        public float leashRange = 5f;

        [Header("Hiệu ứng lên người chơi")]
        public float poisonPercent = 0f;      // 0.02 = 2% máu/giây
        public float poisonDuration = 5f;
        public float stunSeconds = 0f;
        public float knockback = 0f;

        [Header("Tầm xa / gai")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 2.5f;
        public float projectileLifetime = 2f;
        public float keepDistance = 1.8f;
        public int spikeCount = 10;
        public float spikeTriggerRange = 0.9f;
        [Tooltip("Seer: người chơi áp sát trong khoảng hở này thì quật gậy (state Attack2). 0 = tắt")]
        public float closeRange = 0f;

        [Header("Đòn đánh xuống đất (Seer, Camp Gunner)")]
        public Sprite[] strikeFrames;
        public int strikeImpactFrame = 0;
        public float strikeWarn = 0.6f;
        public float strikeRadius = 0.3f;
        public float strikeFps = 14f;
        public Color strikeColor = new Color(0.45f, 1f, 0.4f, 1f);
        public Sprite shellSprite;              // Camp Gunner: viên đạn bay vòng cung

        [Header("Đòn đặc biệt (mỗi N đòn một lần)")]
        public EnemySpecial special = EnemySpecial.None;
        public int specialEvery = 0;
        public float specialWindup = 0.8f;
        public float spinTime = 1.2f;
        public float spinEndTime = 0.4f;
        public float spinRadius = 0.55f;
        public float spinTick = 0.35f;
        public float spinMoveFactor = 0.55f;
        public Sprite[] specialFrames;          // Seer: gai đá
        public int specialImpactFrame = 1;
        public float specialRadius = 0.38f;

        [Header("Combo / nổi điên (Mad Bear)")]
        public int comboHits = 1;
        public float comboGap = 0.2f;
        [Range(0f, 1f)] public float enrageAt = 0f;
        public float dashSpeed = 4f;
        public float dashTime = 0.35f;
        public float dashCooldown = 3.5f;

        [Header("Ụ súng (Camp Gunner)")]
        [Tooltip("Thứ tự: Right, UpRight, Up, DownRight, Down — hướng trái lật ngang")]
        public Sprite[] aimSprites;

        [Header("Boss")]
        public float chargeSpeed = 5f;
        public float chargeTime = 0.45f;
        [Tooltip("02/11: boss lao đâm vào cột đá (RingPillar) -> cột vỡ, boss choáng chừng này giây")]
        public float pillarStun = 3f;
        public float recoveryTime = 1.6f;
        [Tooltip("Khoảng hở tối đa để cú đập trúng")]
        public float slamRadius = 0.35f;
        [Range(0f, 1f)] public float phase2At = 0.5f;

        public event Action<EnemyAI> Phase2Reached;
        public event Action<EnemyAI> Killed;

        [Tooltip("Boss không phải kiểu Boss (vd. Plague Eye bắn xa): vẫn tính là boss — không dây xích, có pha 2")]
        public bool treatAsBoss = false;
        [Tooltip("Bay lơ lửng (Plague Eye): bỏ qua tường/nước, chỉ bị giữ trong khung `bounds` (Director đặt = khu của nó)")]
        public bool floating = false;
        [HideInInspector] public Rect bounds;

        [Header("Mai rùa (EnemySpecial.Shell)")]
        public float shellTime = 2.0f;
        public float shellCooldown = 3.5f;

        public Health Health { get; private set; }
        public bool IsBoss => kind == EnemyKind.Boss || treatAsBoss;
        /// <summary>04/11: đã phát hiện người chơi và đang đuổi / đánh (MapEncounterDirector.InCombat dùng).</summary>
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
        // ---- hướng nhìn lên/xuống (quái có state "<State>_S"/"<State>_N"); ngang = state gốc, lật trái/phải bằng flipX
        private bool hasDirs;
        private string dirSuffix = "";          // "" ngang, "_S" nhìn xuống, "_N" nhìn lên
        private string currentBase;
        private Vector2 lastLook = Vector2.right;
        private Vector2 home;
        private Collider2D bodyCol, playerCol;
        private bool hasDeathState;
        private bool returning;
        private int attackCount;
        private float nextDash, nextShot, nextShell;
        private bool inShell;

        // 31/10: né vật cản, tầm nhìn, chống bị "trap" (người chơi đứng sau đá đánh mà quái không tới được)
        private EnemySteering steering;
        private float lastDamagedAt = -99f;
        private float reachCheckAt, reachGap = float.MaxValue, noProgressT;
        private float reaggroGuardUntil;          // vừa rút lui vì bị trap: tạm không gây hấn lại trừ khi người chơi áp sát
        private float returningSince;
        private float sightCheckAt;
        private bool seesPlayer;
        private const float TrapNoProgressSeconds = 2f;   // không tiến lại gần được chừng này giây...
        private const float TrapRecentHitSeconds = 2.5f;  // ...trong khi vừa bị đánh -> coi là bị trap
        private IEnemySkills skills;

        // ---- cho bộ chiêu riêng của boss (PlagueEyeSkills)
        public Transform Player => player;
        public Health PlayerHealth => playerHealth;
        public bool InPhase2 => phase2Fired;
        public SpriteRenderer Body => sr;
        /// <summary>Lớp vẽ / thứ tự vẽ của thân quái — để VFX, đạn vẽ cùng lớp.</summary>
        public int SortingLayer => sr != null ? sr.sortingLayerID : 0;
        public int SortingOrder => sr != null ? sr.sortingOrder : 2;
        /// <summary>Cho renderer (đạn, VFX) vẽ cùng lớp với thân quái, thứ tự + <paramref name="orderOffset"/>.</summary>
        public void MatchSorting(Renderer r, int orderOffset)
        {
            if (r == null || sr == null) return;
            r.sortingLayerID = sr.sortingLayerID;
            r.sortingOrder = sr.sortingOrder + orderOffset;
        }
        public void PlayState(string state, bool restart = true) => Play(state, restart);

        /// <summary>07/11: ảnh cho bảng giới thiệu boss — luôn là dáng đứng NHÌN XUỐNG (Idle_S) nếu có, không thì Idle ngang
        /// (không lật), bất kể boss đang quay hướng nào. Lấy bằng cách cho Animator nhảy tạm sang state đó rồi trả lại.</summary>
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
        /// Bắn một viên đạn của quái (prefab projectilePrefab) theo hướng/tốc độ/sát thương tuỳ chọn (bộ chiêu boss).
        public Projectile ShootProjectile(Vector2 dir, float speed, float dmg, float lifetime = -1f)
        {
            float s0 = projectileSpeed, d0 = damage, l0 = projectileLifetime;
            projectileSpeed = speed; damage = dmg; if (lifetime > 0f) projectileLifetime = lifetime;
            SpawnProjectile(dir, false);
            projectileSpeed = s0; damage = d0; projectileLifetime = l0;
            return null;
        }

        /// Dịch chuyển tức thời (boss: Vespers Step, Mist Step).
        public void TeleportTo(Vector2 p)
        {
            desired = Vector2.zero;
            if (rb != null) rb.position = p;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        /// Phát động tác nhưng bắt đầu ở giây thứ `seconds` của clip (vd. nhát chém thứ 2 bỏ qua đoạn gồng).
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

        // ---- cho IEnemySkills (quái Map 2 v2)
        public Vector2 Desired { get => desired; set => desired = value; }
        /// <summary>Bộ chiêu bật khi muốn quái nhìn người chơi dù đang bước hướng khác (vd đi vòng quanh). Mặc định: nhìn theo hướng bước.</summary>
        public bool LookAtPlayer { get; set; }
        /// <summary>02/11: giữ đứng yên (cảnh giới thiệu boss) — bộ não không đi, không đánh, không đổi hoạt ảnh.</summary>
        public bool Held { get; set; }
        /// <summary>06/11: quái do boss gọi ra (Provoke) — không bị kéo về chỗ sinh, không rút lui vì "trap".</summary>
        public bool Summoned { get; private set; }

        /// <summary>06/11: gây hấn ngay với người chơi, bỏ qua tầm nhìn / tầm phát hiện (lỗi: cóc con của Gorgemaw chui ra ở góc phòng,
        /// xa hơn aggroRange -> đứng im chờ Ren lại gần). summoned = quái triệu hồi: bỏ dây xích về chỗ sinh.</summary>
        // ------------------------------------------------------------------ 16/11 (§252): Discord (Mantras of the Ear)
        private Coroutine retargetCo;
        /// <summary>Đang bị Discord lừa: coi một con quái khác là "người chơi".</summary>
        public bool Retargeted { get; private set; }
        /// <summary>Tạm coi <paramref name="other"/> là mục tiêu (mọi đòn / chiêu sẵn có của quái tự đánh vào nó) trong <paramref name="seconds"/> s;
        /// hết giờ hoặc mục tiêu chết -> trả về Ren. Boss không bị lừa.</summary>
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

        public void Provoke(bool summoned = true)
        {
            aggro = true; reachGap = float.MaxValue; noProgressT = 0f;
            if (summoned) { Summoned = true; leashRange = 0f; }
        }
        /// <summary>04/11: mọi quái đứng im khi đang diễn cảnh (CutsceneState) — trừ con đang "đóng vai" trong cảnh (vd tên cướp
        /// bước tới quát Ren ở cảnh mở đầu Map 1): cảnh bật cờ này cho nó.</summary>
        public bool CutsceneActor { get; set; }
        /// <summary>02/11: quái bắt đầu gồng đòn cận chiến (màn hướng dẫn né dùng).</summary>
        public static event System.Action<EnemyAI> MeleeWindup;
        /// <summary>02/11: quái bắt đầu MỌI đòn có gồng (cận chiến, nổ gai, bắn) — màn hướng dẫn né ở khu thằn lằn dùng.</summary>
        public static event System.Action<EnemyAI> AttackStarted;
        /// <summary>02/11: đạn / gai vừa bay ra (thấy được trên màn hình) — màn DODGE ở khu thằn lằn dùng.</summary>
        public static event System.Action<EnemyAI> ProjectilesFired;
        /// <summary>Nhịp hoạt ảnh Move (1 = bình thường). Đi chậm thì hạ xuống cho chân khớp quãng đường, khỏi trượt.</summary>
        public float MoveAnimSpeed { get; set; } = 1f;
        private EnemyVfx vfx;
        public EnemyVfx Vfx => vfx;
        public Vector2 DirToPlayer => player != null ? (Vector2)(player.position - transform.position) : Vector2.right;
        public float GapToPlayer() => Gap();
        public void HitPlayerNow(float amount) => HitPlayer(amount);
        public void FaceX(float dx) => Face(dx);
        public Rigidbody2D Rb => rb;
        public Collider2D BodyCollider => bodyCol;
        [HideInInspector] public bool Airborne;          // đang nhảy khỏi mặt đất (ToadSkills): FixedUpdate không đẩy thân

        private void Awake()
        {
            if (definition != null)
            {
                definition.ApplyTo(this);
                attackTime = Mathf.Max(attackTime, windup + 0.1f);   // windup đổi trong asset: đòn không kết thúc trước khung trúng
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
                rb.bodyType = RigidbodyType2D.Kinematic;       // bay qua bể Ichor / bờ kênh
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
            EnemyHealthBar.Attach(this);                     // thanh máu trên đầu quái thường (boss dùng thanh HUD)
            StartCoroutine(Brain());
        }

        private void Update()
        {
            if (anim != null && currentBase == "Move" && !Health.IsDead) anim.speed = MoveAnimSpeed;   // bộ chiêu đổi nhịp giữa lúc đang đi
            if (!hasDirs || Health.IsDead) return;
            Vector2 look = LookVector();
            // trong lúc ra đòn/gồng/lao: giữ nguyên hướng đã chọn khi bắt đầu động tác
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

        /// Trục trội quyết định hướng; có trễ (1.3 lần) để không nháy qua lại quanh góc 45°.
        private string PickSuffix(Vector2 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y);
            bool vertical = dirSuffix == "" ? ay > ax * 1.3f : !(ax > ay * 1.3f);
            return vertical ? (v.y < 0f ? "_S" : "_N") : "";
        }

        private void ApplyFacing()
        {
            if (sr == null) return;
            if (dirSuffix != "") sr.flipX = false;                   // ảnh trước/sau: không lật (giữ đúng tay cầm vũ khí)
            else if (Mathf.Abs(lastLook.x) > 0.01f) sr.flipX = lastLook.x < 0f;
        }

        private void FixedUpdate()
        {
            if (Health.IsDead || kind == EnemyKind.Turret || Airborne) return;
            steering?.TrackProgress(desired, Time.fixedDeltaTime);
            Vector2 extra = Health.ConsumeExtraVelocity(Time.fixedDeltaTime);
            Vector2 next = rb.position + (desired * Health.MoveMul + extra) * Time.fixedDeltaTime;   // 16/11 (§275): bị làm chậm (Bind / Chill)
            if (floating && bounds.width > 0f)
                next = new Vector2(Mathf.Clamp(next.x, bounds.xMin + 0.3f, bounds.xMax - 0.3f), Mathf.Clamp(next.y, bounds.yMin + 0.3f, bounds.yMax - 0.3f));
            rb.MovePosition(next);
        }

        // ------------------------------------------------------------------ bộ não
        private IEnumerator Brain()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.5f));   // lệch nhịp giữa các con
            while (!Health.IsDead)
            {
                if (inShell || Held) { desired = Vector2.zero; yield return null; continue; }
                if (CutsceneState.Active && !CutsceneActor) { desired = Vector2.zero; Play("Idle"); yield return null; continue; }
                if (player == null || Health.IsStunned || (playerHealth != null && playerHealth.IsDead))
                {
                    desired = Vector2.zero;
                    Play("Idle");
                    yield return null;
                    continue;
                }

                Vector2 toPlayer = (Vector2)(player.position - transform.position);
                float d = toPlayer.magnitude;
                float gap = Gap();
                if (kind != EnemyKind.Turret) Face(toPlayer.x);

                // Dây xích: đuổi quá xa điểm sinh thì quay về; bị trap cũng quay về (boss thì không, vì đấu trường đã khóa)
                if (!IsBoss && kind != EnemyKind.Turret)
                {
                    Vector2 fromHome = (Vector2)transform.position - home;
                    if (leashRange > 0f && aggro && fromHome.magnitude > leashRange) StartReturning();
                    if (returning)
                    {
                        // tới nơi — hoặc đi mãi không tới (chỗ cũ bị chắn): dừng, lấy chỗ này làm "nhà"
                        if (fromHome.magnitude < 0.15f || Time.time - returningSince > 6f)
                        {
                            returning = false; desired = Vector2.zero; Play("Idle");
                            if (fromHome.magnitude >= 0.15f) home = transform.position;
                        }
                        else
                        {
                            desired = steering != null ? steering.Steer(home, moveSpeed * 1.3f) : -fromHome.normalized * moveSpeed * 1.3f;
                            Face(-fromHome.x); Play("Move");
                        }
                        Health.Heal(Health.Max * 0.5f * Time.deltaTime);
                        yield return null;
                        continue;
                    }
                }

                if (!aggro)
                {
                    bool playerNearHome = leashRange <= 0f || IsBoss ||
                                          ((Vector2)player.position - home).magnitude <= leashRange;
                    // phải THẤY người chơi (đá / tường chắn thì không) — trừ khi đứng rất sát
                    bool notices = d <= aggroRange && playerNearHome && (SeesPlayer() || d <= aggroRange * 0.35f);
                    // vừa rút lui vì bị trap: chỉ gây hấn lại khi người chơi thật sự áp sát
                    if (notices && Time.time < reaggroGuardUntil && gap > attackRange + 0.3f) notices = false;
                    if (notices) { aggro = true; reachGap = float.MaxValue; noProgressT = 0f; SfxHooks.EnemyAlert(this); } // 19/11 (§335): âm
                    else { desired = Vector2.zero; Play("Idle"); yield return null; continue; }
                }

                if (CheckTrapped(gap)) { StartReturning(); reaggroGuardUntil = Time.time + 4f; yield return null; continue; }

                switch (kind)
                {
                    case EnemyKind.Melee:
                        if (CanDash(d, gap)) yield return Dash(toPlayer);
                        else if (gap > attackRange) Chase(toPlayer);
                        else
                        {
                            attackCount++;
                            if (special == EnemySpecial.Spin && specialEvery > 0 && attackCount % specialEvery == 0)
                                yield return SpinAttack();
                            else
                                yield return MeleeAttack("Attack", damage, attackTime, windup);
                        }
                        break;

                    case EnemyKind.SpikeBurst:
                        if (d > spikeTriggerRange) Chase(toPlayer);
                        else yield return SpikeAttack();
                        break;

                    case EnemyKind.Ranged:
                        if (closeRange > 0f && gap <= closeRange && HasState("Attack2"))
                            yield return MeleeAttack("Attack2", damage * 0.7f, attack2Time, windup * 0.8f);
                        else if (d > aggroRange) Chase(toPlayer);
                        else if (d < keepDistance * 0.7f) { desired = -toPlayer.normalized * moveSpeed; Play("Move"); }
                        else yield return RangedAttack();
                        break;

                    case EnemyKind.Boss:
                        if (gap > attackRange) Chase(toPlayer);
                        else yield return BossCombo();
                        break;

                    case EnemyKind.Turret:
                        yield return TurretTick(toPlayer, d);
                        break;

                    case EnemyKind.Custom:
                        if (skills == null) skills = GetComponent<IEnemySkills>();
                        if (skills != null) yield return skills.Act(this);
                        else Chase(toPlayer);
                        break;

                    case EnemyKind.Beholder:
                        desired = Vector2.zero;
                        if (skills == null) skills = GetComponent<IEnemySkills>();
                        if (skills != null) yield return skills.Act(this);
                        else yield return RangedAttack();
                        break;
                }
                yield return null;
            }
        }

        private void Chase(Vector2 toPlayer)
        {
            // 31/10: né vật cản thay vì lao thẳng vào đá (EnemySteering)
            desired = steering != null && player != null ? steering.Steer(player.position, moveSpeed) : toPlayer.normalized * moveSpeed;
            Play(desired.sqrMagnitude > 0.0001f ? "Move" : "Idle");
        }

        private void StartReturning()
        {
            aggro = false;
            returning = true;
            returningSince = Time.time;
            reachGap = float.MaxValue;
            noProgressT = 0f;
        }

        /// <summary>Người chơi nằm trong tầm nhìn (không bị vật cản che). Kiểm tra 5 lần/giây.</summary>
        private bool SeesPlayer()
        {
            if (Time.time < sightCheckAt) return seesPlayer;
            sightCheckAt = Time.time + 0.2f;
            Vector2 eye = bodyCol != null ? (Vector2)bodyCol.bounds.center : (Vector2)transform.position;
            seesPlayer = LineOfSight.CanSee(eye, playerCol);
            return seesPlayer;
        }

        /// <summary>
        /// Bị "trap": quái cận chiến vừa bị đánh nhưng suốt TrapNoProgressSeconds giây không rút ngắn được khoảng cách
        /// (kẹt sau đá, cách nước...). Khi đó nó rút về chỗ cũ và hồi máu — người chơi phải ra đánh đàng hoàng.
        /// </summary>
        private bool CheckTrapped(float gap)
        {
            bool melee = kind == EnemyKind.Melee || kind == EnemyKind.SpikeBurst;
            if (!melee || IsBoss || floating || Summoned || gap <= attackRange) { noProgressT = 0f; reachGap = gap; return false; }
            if (Time.time >= reachCheckAt)
            {
                reachCheckAt = Time.time + 0.5f;
                noProgressT = gap > reachGap - 0.05f ? noProgressT + 0.5f : 0f;   // nửa giây vừa rồi không lại gần hơn
                reachGap = gap;
            }
            return noProgressT >= TrapNoProgressSeconds && Time.time - lastDamagedAt < TrapRecentHitSeconds;
        }

        // ------------------------------------------------------------------ các đòn
        private IEnumerator MeleeAttack(string state, float dmg, float totalTime, float hitAt)
        {
            desired = Vector2.zero;
            Play(state, true);
            if (vfx != null) vfx.Windup(DirToPlayer);
            MeleeWindup?.Invoke(this);
            AttackStarted?.Invoke(this);
            yield return Wait(hitAt);
            if (Health.IsDead) yield break;
            if (vfx != null) vfx.Swing(DirToPlayer);
            int hits = Mathf.Max(1, comboHits);
            for (int i = 0; i < hits; i++)
            {
                if (i > 0) { yield return Wait(comboGap); if (Health.IsDead) yield break; }
                if (Gap() <= attackRange + 0.2f) HitPlayer(dmg);
            }
            yield return Wait(Mathf.Max(0f, totalTime - hitAt - comboGap * (hits - 1)));
            Play("Idle");
            yield return Wait(cooldown);
        }

        /// Greystone Warrior — đòn 2: gồng (nhấp nháy cam) rồi xoay vòng, trượt chậm về phía người chơi.
        private IEnumerator SpinAttack()
        {
            desired = Vector2.zero;
            Play("Windup", true);
            yield return Telegraph(specialWindup, new Color(1f, 0.6f, 0.35f));
            if (Health.IsDead) yield break;
            Play("Spin", true);
            float t = 0f, nextTick = 0f;
            while (t < spinTime && !Health.IsDead && player != null)
            {
                Vector2 to = (Vector2)(player.position - transform.position);
                desired = to.normalized * moveSpeed * spinMoveFactor;
                if (t >= nextTick)
                {
                    nextTick += spinTick;
                    if (to.magnitude <= spinRadius) HitPlayer(damage * 0.6f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            desired = Vector2.zero;
            Play("SpinEnd", true);
            yield return Wait(spinEndTime);
            Play("Idle");
            yield return Wait(cooldown * 1.3f);   // khựng sau khi xoay: cửa sổ phản công
        }

        private bool CanDash(float d, float gap) =>
            enrageAt > 0f && Health.Percent <= enrageAt && Time.time >= nextDash && gap > attackRange + 0.3f && d < 3f;

        /// Mad Bear dưới 50% máu: gồng ngắn rồi lao thẳng.
        private IEnumerator Dash(Vector2 toPlayer)
        {
            nextDash = Time.time + dashCooldown;
            desired = Vector2.zero;
            Play("Windup", true);
            yield return Telegraph(0.5f, new Color(1f, 0.4f, 0.4f));
            if (Health.IsDead || player == null) yield break;
            Vector2 dir = ((Vector2)(player.position - transform.position)).normalized;
            Face(dir.x);
            Play("Move", true);
            bool hit = false;
            float t = 0f;
            while (t < dashTime && !Health.IsDead)
            {
                desired = dir * dashSpeed;
                if (!hit && Gap() <= 0.1f) { HitPlayer(damage * 1.2f); hit = true; }
                t += Time.deltaTime;
                yield return null;
            }
            desired = Vector2.zero;
            Play("Idle");
            yield return Wait(0.5f);
        }

        private IEnumerator SpikeAttack()
        {
            desired = Vector2.zero;
            Play("Attack", true);
            AttackStarted?.Invoke(this);
            yield return Telegraph(windup, new Color(1f, 0.95f, 0.5f));
            if (Health.IsDead) yield break;
            float offset = BulletPatterns.RandomRingOffset(spikeCount);
            foreach (var dir in BulletPatterns.Ring(spikeCount, offset)) SpawnProjectile(dir, true);
            ProjectilesFired?.Invoke(this);
            yield return Wait(Mathf.Max(0f, attackTime - windup));
            Play("Idle");
            yield return Wait(cooldown);
        }

        private IEnumerator RangedAttack()
        {
            desired = Vector2.zero;
            attackCount++;
            bool spec = special == EnemySpecial.GroundSpikes && specialEvery > 0 && attackCount % specialEvery == 0
                        && specialFrames != null && specialFrames.Length > 0;
            Play(spec && HasState("Cast") ? "Cast" : "Attack", true);
            if (vfx != null) vfx.Cast();
            AttackStarted?.Invoke(this);
            yield return Wait(windup);
            if (Health.IsDead || player == null) yield break;

            int order = sr != null ? sr.sortingOrder : 2;
            if (spec)
            {
                var g = GroundStrike.Spawn(player.position, 0.65f, specialRadius, damage * 1.2f, specialFrames,
                                           specialImpactFrame, strikeFps, order, strikeColor);
                g.knockback = 2.5f;
            }
            else if (strikeFrames != null && strikeFrames.Length > 0)
            {
                GroundStrike.Spawn(player.position, strikeWarn, strikeRadius, damage, strikeFrames,
                                   strikeImpactFrame, strikeFps, order, strikeColor);
            }
            else { SpawnProjectile((Vector2)(player.position - transform.position), false); ProjectilesFired?.Invoke(this); }

            yield return Wait(Mathf.Max(0f, attackTime - windup));
            Play("Idle");
            // lượn ngang một chút trong lúc hồi chiêu để khó bị áp sát
            Vector2 side = Vector2.Perpendicular((Vector2)(player.position - transform.position)).normalized;
            if (UnityEngine.Random.value < 0.5f) side = -side;
            desired = side * moveSpeed * 0.6f;
            Play("Move");
            yield return Wait(cooldown);
            desired = Vector2.zero;
        }

        /// Camp Gunner: xoay nòng theo người chơi; mỗi `cooldown` giây bắn một viên vòng cung tới chỗ người chơi đang đứng.
        private IEnumerator TurretTick(Vector2 toPlayer, float d)
        {
            desired = Vector2.zero;
            Aim(toPlayer);
            if (d > aggroRange * 1.25f) { aggro = false; yield break; }
            if (Time.time < nextShot) yield break;
            nextShot = Time.time + cooldown;
            yield return Telegraph(0.25f, new Color(1f, 0.7f, 0.4f));
            if (Health.IsDead || player == null) yield break;
            int order = sr != null ? sr.sortingOrder : 2;
            var g = GroundStrike.Spawn(player.position, strikeWarn, strikeRadius, damage, strikeFrames,
                                       strikeImpactFrame, strikeFps, order, strikeColor);
            g.shell = shellSprite;
            g.shellFrom = (Vector2)transform.position + Vector2.up * 0.2f;
            g.knockback = knockback;
        }

        private void Aim(Vector2 v)
        {
            if (aimSprites == null || aimSprites.Length < 5 || sr == null) return;
            float a = Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg;
            int i = a > 67.5f ? 2 : a > 22.5f ? 1 : a > -22.5f ? 0 : a > -67.5f ? 3 : 4;
            if (aimSprites[i] != null) sr.sprite = aimSprites[i];
            sr.flipX = v.x < 0f && i != 2 && i != 4;
        }

        private IEnumerator BossCombo()
        {
            // 1. Gồng đòn — báo hiệu rõ ràng
            desired = Vector2.zero;
            Play("Windup");
            yield return Telegraph(windup, new Color(1f, 0.6f, 0.4f));
            if (Health.IsDead || player == null) yield break;

            // 2. Lao tới vị trí người chơi lúc gồng xong, đập khi tới nơi
            Vector2 dir = ((Vector2)(player.position - transform.position)).normalized;
            Face(dir.x);
            Play("Attack");
            bool hitDuringCharge = false, hitPillar = false;
            float t = 0f;
            while (t < chargeTime && !Health.IsDead)
            {
                desired = dir * chargeSpeed;
                // 02/11: đâm vào cột đá -> cột vỡ, khựng lâu (người chơi dụ boss vào cột)
                var pillar = bodyCol != null ? RingPillar.Touching(bodyCol.bounds, 0.06f) : null;
                if (pillar != null) { pillar.Break(); hitPillar = true; break; }
                if (!hitDuringCharge && Gap() <= 0.1f)
                {
                    HitPlayer(damage * 0.8f);
                    hitDuringCharge = true;
                }
                t += Time.deltaTime;
                yield return null;
            }
            desired = Vector2.zero;
            if (hitPillar)
            {
                Play("Recovery");
                Hints.Show($"{displayName} smashed into the standing stone - strike now!", 2.5f);
                yield return Wait(pillarStun);
                Play("Idle");
                yield return Wait(cooldown);
                yield break;
            }
            if (!hitDuringCharge && Gap() <= slamRadius) HitPlayer(damage);
            yield return Wait(Mathf.Max(0f, attackTime - chargeTime));

            // 3. Khựng lại — cửa sổ phản công theo logbook
            Play("Recovery");
            yield return Wait(recoveryTime);
            Play("Idle");
            yield return Wait(cooldown);
        }

        // ------------------------------------------------------------------ tiện ích
        private void HitPlayer(float amount)
        {
            if (playerHealth == null) return;
            // 31/10: đòn cận chiến không xuyên đá / tường
            Vector2 from = bodyCol != null ? (Vector2)bodyCol.bounds.center : (Vector2)transform.position;
            if (!LineOfSight.CanSee(from, playerCol)) return;
            Vector2 dir = (Vector2)(player.position - transform.position);
            if (!playerHealth.TakeDamage(amount, dir, knockback)) return;
            if (vfx != null) vfx.HurtPlayer(player.position);
            if (poisonPercent > 0f) playerHealth.ApplyPoison(poisonPercent, poisonDuration);
            if (stunSeconds > 0f) playerHealth.Stun(stunSeconds);
        }

        private void SpawnProjectile(Vector2 dir, bool spike)
        {
            GameObject go;
            if (projectilePrefab != null) go = Instantiate(projectilePrefab, transform.position + Vector3.up * 0.15f, Quaternion.identity);
            else
            {
                go = new GameObject(spike ? "Spike" : "Projectile");
                go.transform.position = transform.position + Vector3.up * 0.15f;
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = Projectile.SpikeSprite;
                s.sortingOrder = 1;
                go.AddComponent<Projectile>();
            }
            var p = go.GetComponent<Projectile>();
            p.lifetime = projectileLifetime;
            p.knockback = knockback;
            p.poisonPercent = poisonPercent;
            p.poisonDuration = poisonDuration;
            p.Launch(dir, projectileSpeed, damage);
        }

        /// <summary>Khoảng hở giữa thân quái và thân người chơi (0 = đang chạm). Tránh lỗi "chạy tại chỗ"
        /// khi collider chặn không cho quái tới đủ gần tâm người chơi.</summary>
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
            if (hasDirs && dirSuffix != "") return;                  // đang nhìn lên/xuống: không lật
            if (sr != null && Mathf.Abs(dx) > 0.01f) sr.flipX = dx < 0f;
        }

        private IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !Health.IsDead) yield return null;
        }

        private IEnumerator Telegraph(float seconds, Color c)
        {
            // 02/11: bỏ dòng gợi ý tut_glow (dạy lăn) — đã có màn dừng DODGE [Q] ở khu thằn lằn
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
                // động tác mới (đòn, gồng, lao, hồi máu): chọn hướng theo mục tiêu lúc bắt đầu; chết luôn ngã ngang
                if (state == "Death") { dirSuffix = ""; ApplyFacing(); }
                else if (state != "Idle" && state != "Move" && (restart || state != currentBase)) { dirSuffix = PickSuffix(LookVector()); ApplyFacing(); }
                if (dirSuffix != "" && HasState(state + dirSuffix))
                {
                    string resolved = state + dirSuffix;
                    if (!restart && resolved == currentState) return;
                    anim.Play(Animator.StringToHash(resolved), 0, 0f);
                    currentState = resolved; currentBase = state;
                    SfxHooks.EnemyState(this, state); // 19/11 (§335): âm
                    anim.speed = state == "Move" ? MoveAnimSpeed : 1f;   // đi chậm: chân bước chậm theo
                    return;
                }
                // thiếu bản lên/xuống của động tác này: dùng bản ngang, quay mặt về phía mục tiêu
                if (dirSuffix != "" && sr != null && Mathf.Abs(lastLook.x) > 0.01f) sr.flipX = lastLook.x < 0f;
            }
            if (!restart && state == currentState) return;
            if (!HasState(state))
            {
                // state thiếu thì rơi về state gần nghĩa nhất
                if (state == "Windup" || state == "Recovery" || state == "SpinEnd" || state == "GuardIn" || state == "GuardOut") Play("Idle", restart);
                else if (state == "Spin" || state == "Attack2") Play("Attack", restart);
                return;
            }
            anim.Play(Animator.StringToHash(state), 0, 0f);
            currentState = state; currentBase = state;
            SfxHooks.EnemyState(this, state); // 19/11 (§335): âm
            anim.speed = state == "Move" ? MoveAnimSpeed : 1f;   // đi chậm: chân bước chậm theo
        }

        private void OnDamaged(Health h, float amount)
        {
            lastDamagedAt = Time.time;
            if (!returning) { if (!aggro) SfxHooks.EnemyAlert(this); aggro = true; } // 19/11 (§335): âm
            if (special == EnemySpecial.Shell && !inShell && !h.IsDead && Time.time >= nextShell) StartCoroutine(ShellRoutine());
            if (IsBoss && !phase2Fired && h.Percent <= phase2At)
            {
                phase2Fired = true;
                Phase2Reached?.Invoke(this);
            }
        }

        /// Rùa: thụt vào mai (bất tử) shellTime giây rồi thò ra, hồi chiêu shellCooldown.
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
            else if (anim != null) anim.speed = 0f;          // không có hoạt ảnh chết: đứng hình rồi mờ dần
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
