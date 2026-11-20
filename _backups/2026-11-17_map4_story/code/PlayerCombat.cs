using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Chiến đấu của người chơi — component trên Player, giữ THÔNG SỐ (Inspector) và nhận phím; mỗi tính năng là một class
    /// trong Player/Abilities:
    ///   <see cref="PlayerMelee"/>          chém (Attack: J / chuột trái / tay cầm X)
    ///   <see cref="PlayerDodge"/>          lộn nhào có bất tử (Roll: Q / tay cầm B)
    ///   <see cref="PlayerFlask"/>          bình máu (Heal: E / tay cầm Y)
    ///   <see cref="PlayerStrikes"/>        gây sát thương, dừng hình, giật camera, VFX trúng đòn
    ///   <see cref="PlayerHurtReactions"/>  trúng đòn / choáng / gục / hồi sinh
    ///   <see cref="PlayerAnimation"/>      tham số Animator;   <see cref="PlayerActionState"/>  huỷ / khoá hành động
    /// Kỹ năng tháo lắp (Skill I–III): <see cref="PlayerLoadout"/> + SkillDefinition, dùng API "cho chiêu tháo lắp" bên dưới.
    /// Di chuyển: PlayerController. Phím test F1–F9 (F1 = bảng phím): DevTools/DevCheats.
    /// (Tên cũ: PlayerCombatPrototype — đổi 30/10/2026, file .meta giữ nguyên nên scene không mất liên kết.)
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Weapon (09/11 §201)")]
        [Tooltip("Vũ khí đang cầm (Resources/Weapons/*.asset). Trống = Katana mặc định. Số liệu từng nhát combo lấy từ đây.")]
        public WeaponDefinition weapon;

        [Header("Attack (J / Left Click) — fallback khi không có vũ khí")]
        [Tooltip("Directions that have a slash animation (written by the Player Animator builder). The slash snaps to the closest one.")]
        [FormerlySerializedAs("iaidoDirs")] public Vector2[] directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down,
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f), new Vector2(-0.7071f, -0.7071f), new Vector2(0.7071f, -0.7071f) };
        [FormerlySerializedAs("iaidoDamage")] public float damage = 35f;
        [Tooltip("Seconds from button press to the hit frame.")]
        [FormerlySerializedAs("iaidoHitTime")] public float hitTime = 0.48f;
        [Tooltip("Seconds before the next slash can start (attack rate).")]
        [FormerlySerializedAs("iaidoLockTime")] public float lockTime = 0.6f;
        [Tooltip("Cooldown after a slash finishes (from its hit frame) before the next slash can start. 02/11: 0.5 s - stops button-mashing chains.")]
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

        // ------------------------------------------------------------------ trạng thái đọc từ ngoài
        public Health Health { get; private set; }
        public PlayerLoadout Loadout { get; private set; }
        public int Potions => potions;
        public float RollCooldownLeft => dodge != null ? dodge.CooldownLeft : 0f;
        /// <summary>Hướng nhìn cuối (đã chuẩn hoá).</summary>
        public Vector2 Facing => facing;
        /// <summary>Đang chém / dậm / loạng choạng (khoá di chuyển). PlayerController đọc cờ này.</summary>
        public bool IsAttacking => actions != null && actions.IsLocked;
        /// <summary>Bất tử (chỉ bật từ DevTools/DevCheats — phím F2, không có trong bản phát hành).</summary>
        public bool GodMode { get; set; }
        /// <summary>Khác (0,0) = chiêu đang ép hướng nhìn của nhân vật (PlayerController dùng thay hướng di chuyển).</summary>
        public Vector2 FacingOverride { get; set; }
        /// <summary>08/11 (§191): khi đang bị khoá thao tác (IsAttacking) vẫn được đi với hệ số này (0 = đứng yên, mặc định).
        /// Judgement Ray đặt 0,4 khi Ren có màn Veil (Health.Guarded), trả 0 khi tắt tia.</summary>
        public float LockedMoveMul { get; set; }

        /// <summary>Mỗi nhát chém / kỹ năng: (tâm, bán kính). Cơ quan trên map (van, vật cản gỗ), boss (cột, quan tài) nghe sự kiện này.</summary>
        public static event Action<Vector2, float> Struck;
        /// <summary>Kỹ năng vùng: (tâm, bán kính). Mây độc (sách Purifier), đèn quanh người nghe sự kiện này.</summary>
        public static event Action<Vector2, float> SkillCast;
        /// <summary>16/11 (§252): mỗi nhát chém kiếm vừa ra (kể cả chém hụt): (người chơi, tâm xoay, hướng, tầm, sát thương, đẩy lùi).
        /// Afterecho (bộ Mantras of the Ear) nghe sự kiện này để chém lặp lại.</summary>
        public static event Action<PlayerCombat, Vector2, Vector2, float, float, float> MeleeSwung;
        internal static void RaiseMeleeSwung(PlayerCombat p, Vector2 pivot, Vector2 dir, float range, float dmg, float knock) => MeleeSwung?.Invoke(p, pivot, dir, range, dmg, knock);

        // ------------------------------------------------------------------ tính năng
        private PlayerActionState actions;
        private PlayerAnimation anim;
        private PlayerStrikes strikes;
        private PlayerMelee melee;
        private PlayerDodge dodge;
        private PlayerFlask flask;
        private PlayerHurtReactions reactions;
        private Vector2 facing = Vector2.down;
        // đệm phím: bấm lúc chưa làm được (đang chém / lăn đang hồi) thì nhớ tới thời điểm này
        private float attackQueuedUntil = -1f, rollQueuedUntil = -1f;

        internal SpriteRenderer Sprite { get; private set; }
        internal int VfxLayer => Sprite != null ? Sprite.sortingLayerID : 0;
        internal int PlayerOrder => Sprite != null ? Sprite.sortingOrder : 2;

        private void Awake()
        {
            Health = GetComponent<Health>();
            Loadout = PlayerLoadout.Of(this);
            Sprite = GetComponentInChildren<SpriteRenderer>();
            // Animator nằm trên cùng object với SpriteRenderer (Player)
            anim = new PlayerAnimation(GetComponentInChildren<Animator>());
            actions = new PlayerActionState(Health);
            strikes = new PlayerStrikes(this);
            melee = new PlayerMelee(this, anim, actions, strikes);
            if (weapon == null) weapon = WeaponDefinition.LoadDefault();          // 09/11 (§201)
            WeaponView = PlayerWeaponView.Ensure(this);
            WeaponView.SetWeapon(weapon);
            IaiStance.Ensure(this);                                           // 16/11 (§271): thanh nộ đòn giữ Iaijutsu
            dodge = new PlayerDodge(this, anim, actions);
            flask = new PlayerFlask(this, anim);
            reactions = new PlayerHurtReactions(this, anim, actions, flask);
            Health.Hit += reactions.OnHit;
            Health.Died += reactions.OnDied;
            Health.Revived += reactions.OnRevived;
            Health.Damaged += OnDamagedTrait;                                    // 10/11 (§217): nội tại Vigil
        }

        // Nạp sẵn ảnh VFX hay dùng (lần đầu Resources.LoadAll giữa trận gây giật 1 khung)
        private void Start()
        {
            potions = Mathf.Max(potions, maxPotions);              // 02/11: vào map với đủ bình tối đa
            if (!showVfx) return;
            melee.PreloadVfx();
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

        public void AddPotion(int n) => potions += n;
        /// <summary>02/11: số bình tối đa (nghỉ ở điểm nghỉ / hồi sinh nạp lại tới mức này). Rương của Hob +1.</summary>
        public int maxPotions = 3;
        public void RefillPotions() => potions = Mathf.Max(potions, maxPotions);

        /// <summary>02/11: màn hướng dẫn (dừng game chờ đúng phím) — xếp hàng Đánh / Lăn theo hướng `dir` như người chơi vừa bấm.</summary>
        public void QueueAction(GameAction a, Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.0001f) facing = dir.normalized;
            if (a == GameAction.Attack) attackQueuedUntil = Time.time + inputBuffer;
            else if (a == GameAction.Roll) rollQueuedUntil = Time.time + inputBuffer;
            else if (a == GameAction.Heal) flask.Drink();
            else if (a == GameAction.Skill1) Loadout.TryCast(0);
        }

        // 02/11: quay mặt về phía người đang nói chuyện (NPC, cảnh diễn) trong `seconds` giây; người chơi tự đi thì bỏ ngay
        private Vector2 lookDir;
        private float lookUntil;
        public void LookAt(Vector2 dir, float seconds)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            lookDir = dir.normalized; lookUntil = Time.time + seconds;
            facing = lookDir;
        }
        public void StopLook() => lookUntil = 0f;
        /// <summary>07/11: cảnh diễn cho Ren loạng choạng (vd tiếp đất sau khi bị ném).</summary>
        public void PlayStagger() => anim?.Stagger();
        /// <summary>Hướng đang quay mặt nói chuyện (zero = không).</summary>
        public Vector2 LookDir => Time.time < lookUntil ? lookDir : Vector2.zero;
        public void SetPotions(int n) => potions = Mathf.Max(0, n);

        // ------------------------------------------------------------------ 16/11 (§281): sách The Rampart Forms (cờ book_rampart)
        /// <summary>Đứng yên (không bấm hướng) `RampartDelay` giây -> nhận ít hơn 20% sát thương tới khi bước đi. Lúc thế đứng vào: thân loé xanh thép.</summary>
        public const string RampartFlag = "book_rampart";
        private const float RampartDelay = 0.5f, RampartMulValue = 0.8f;
        private float stillTime;
        private bool rampartOn;
        public bool RampartActive => rampartOn;

        private float RampartMul()
        {
            var dir = MapEncounterDirector.Instance;
            bool learned = GameSession.Books.Contains(RampartFlag) || (dir != null && dir.Flags != null && dir.Flags.Has(RampartFlag));
            if (!learned || Health == null || Health.IsDead) { stillTime = 0f; rampartOn = false; return 1f; }
            if (GameInput.Move.sqrMagnitude > 0.01f) stillTime = 0f; else stillTime += Time.deltaTime;
            bool on = stillTime >= RampartDelay;
            if (on && !rampartOn) Health.TintPulse(new Color(0.62f, 0.78f, 1f, 1f), 0.15f);
            rampartOn = on;
            return on ? RampartMulValue : 1f;
        }

        // ------------------------------------------------------------------ nhận phím
        private void Update()
        {
            anim?.Tick();                                                      // 10/11 (§205): hết giao tranh -> tra kiếm vào bao
            // 10/11 (§217): nội tại đang bật -> lưỡi kiếm sáng hơn (Vigil "ngọn đèn chưa tắt" nhìn thấy được)
            bool lit = WeaponTraits.Active(this);
            if (lit != traitLit) { traitLit = lit; if (WeaponView != null) WeaponView.TraitGlow = lit ? 1.75f : 1f; }
            if (Health != null) Health.DamageTakenMul = WeaponTraits.TakenMul(this) * RampartMul();   // 16/11 (§254): Last Stand / Accord; §281: The Rampart Forms
            if (Health.IsDead) return;
            var move = GameInput.Move;
            if (move.sqrMagnitude > 0.01f) facing = move.normalized;
            if (GameInput.Blocked) { attackQueuedUntil = rollQueuedUntil = -1f; return; }   // menu đang mở: bỏ phím đã nhớ

            reactions.UpdateStun();
            if (Health.IsStunned || MapMechanics.ConsoleActive) return;      // đang chỉnh ống ở bảng điều khiển

            var mouse = Mouse.current;
            bool attack = GameInput.Pressed(GameAction.Attack) ||
                          (mouse != null && mouse.leftButton.wasPressedThisFrame && !MapMechanics.PanelOpen);   // chuột đang bấm bảng
            // đệm phím: nhớ lần bấm inputBuffer giây, làm ngay khi được (Roll ưu tiên: lăn là để huỷ / né)
            // 16/11 (§271): thanh kiếm có Iaijutsu — nhát đầu combo thành đòn GIỮ (giữ phím / chuột: đứng thế, thanh nộ đầy thì thả ra chém mạnh)
            if (attack && WeaponTraits.IsIaijutsu(this) && melee.Ready && melee.ComboStep == 0 && !(Time.time <= rollQueuedUntil))
            {
                attack = false; attackQueuedUntil = -1f;
                StartCoroutine(melee.IaiSlash(AttackHeld));
            }
            if (attack) attackQueuedUntil = Time.time + inputBuffer;
            if (GameInput.Pressed(GameAction.Roll)) rollQueuedUntil = Time.time + inputBuffer;
            if (Time.time <= rollQueuedUntil && dodge.Ready)
            {
                rollQueuedUntil = attackQueuedUntil = -1f;
                melee.ResetCombo();                                               // 09/11 (§201): lăn = huỷ combo, cất vũ khí
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

        // ------------------------------------------------------------------ API cho chiêu tháo lắp (SkillDefinition.Cast)
        /// <summary>Bắt đầu một hành động mới (huỷ đòn chém đang chờ). Trả mã để kiểm tra còn hiệu lực.</summary>
        public int BeginAction() => actions.Begin();
        /// <summary>Hành động `id` chưa bị Roll / choáng / gục huỷ.</summary>
        public bool ActionAlive(int id) => actions.Alive(id);
        /// <summary>Phát hoạt ảnh chiêu (Stomp) theo hướng nhìn. false nếu Animator không có trigger.</summary>
        public bool PlaySkillAnimation() => anim.Stomp(facing);
        public void LockActions(float seconds) => actions.Lock(seconds);
        /// <summary>Hoạt ảnh gọi cầu (Seeker Embers). false nếu Animator chưa có.</summary>
        public bool PlayEmbersCast() => anim.CastEmbers(facing);
        /// <summary>Hoạt ảnh chắp tay niệm khiên (Veil of the Ascended). false nếu Animator chưa có.</summary>
        public bool PlayVeilCast() => anim.CastVeil(facing);
        /// <summary>Bật / tắt tư thế duỗi tay bắn tia (Judgement Ray), quay nhân vật theo <paramref name="dir"/>.</summary>
        public void RayPose(bool on, Vector2 dir)
        {
            if (on) { FacingOverride = dir; anim.RayBegin(dir); }
            else { FacingOverride = Vector2.zero; anim.RayEnd(); if (Sprite != null) Sprite.flipX = false; }   // 09/11 (§200): trả hình lật
        }
        /// <summary>Đổi hướng tư thế bắn tia theo hướng tia.</summary>
        public void RayAim(Vector2 dir) { FacingOverride = dir; anim.RayAim(dir); }
        public void RaiseSkillCast(Vector2 c, float r) => SkillCast?.Invoke(c, r);
        /// <summary>09/11 (§201): tư thế chém (nhát 1 của combo, có hiện vũ khí) theo hướng d — Severing Draw.</summary>
        public void PlaySlashPose(Vector2 d)
        {
            var s = melee.Snap(d);
            anim.Attack(1, s, 1.4f);
            SetFacing(s);
            if (WeaponView != null) { WeaponView.Show(true); CancelInvoke(nameof(HideWeapon)); Invoke(nameof(HideWeapon), 0.32f); }
        }
        private void HideWeapon() { if (WeaponView != null) WeaponView.Show(false); }

        /// <summary>09/11 (§201): lớp vũ khí (hình lưỡi gắn tay theo từng khung).</summary>
        public PlayerWeaponView WeaponView { get; private set; }
        /// <summary>Vũ khí đang cầm.</summary>
        public WeaponDefinition Weapon => weapon;

        // ------------------------------------------------------------------ 10/11 (§217): nội tại của thanh katana
        /// <summary>Lúc bắt đầu cú lăn gần nhất (nội tại Counterstroke).</summary>
        public float LastRollTime { get; internal set; } = -99f;
        /// <summary>Cú lăn gần nhất chưa dùng đòn phản (Counterstroke chỉ ăn nhát chém đầu).</summary>
        public bool CounterReady { get; internal set; }
        /// <summary>Lúc trúng đòn gần nhất (nội tại Vigil).</summary>
        public float LastHurtTime { get; private set; } = -99f;
        private void OnDamagedTrait(Health h, float amount) { LastHurtTime = Time.time; WeaponTraits.OnHurt(this); }   // 16/11 (§257): + Penance
        /// <summary>16/11 (§257): số nấc Penance đang tích.</summary>
        public int PenanceStacks { get; internal set; }
        /// <summary>16/11 (§254): lúc bắt đầu nhát chém gần nhất (Iaijutsu).</summary>
        public float LastSwingTime { get; internal set; } = -99f;
        /// <summary>Nhát chém đang ra là đòn rút kiếm (Iaijutsu).</summary>
        public bool IaiCharged { get; internal set; }
        /// <summary>16/11 (§271): đang đứng thế giữ Iaijutsu / thanh nộ 0..1 (IaiStance vẽ cạnh Ren).</summary>
        public bool IaiHolding { get; internal set; }
        /// <summary>16/11 (§273): độ mạnh đòn rút kiếm 0..1 (0 = vừa qua mốc, 1 = thanh đầy) — nhân với `bonus`.</summary>
        public float IaiPower { get; internal set; }
        public float IaiCharge { get; internal set; }
        /// <summary>Accord: giảm sát thương nhận vào tới lúc này.</summary>
        public float AccordUntil { get; internal set; } = -99f;
        private bool traitLit;
        /// <summary>09/11 (§201): đổi vũ khí (null = Katana mặc định). Combo đang dở bị huỷ.</summary>
        public void Equip(WeaponDefinition w)
        {
            weapon = w != null ? w : WeaponDefinition.LoadDefault();
            if (weapon != null) WeaponRing.SetEquipped(weapon.id);               // 10/11 (§217): nhớ thanh đang đeo (qua map / bản lưu)
            traitLit = false;
            if (WeaponView != null) WeaponView.TraitGlow = 1f;                   // bỏ ánh nội tại của thanh cũ
            melee.ResetCombo();
            if (WeaponView != null) WeaponView.SetWeapon(weapon);
        }
        /// <summary>07/11: hoạt ảnh lướt (Dash) theo hướng d.</summary>
        public void PlayDash(Vector2 d) => anim.Dash(d);
        /// <summary>07/11: quay mặt Ren về hướng d (chiêu ngắm theo chuột).</summary>
        public void SetFacing(Vector2 d) { if (d.sqrMagnitude > 0.0001f) facing = d.normalized; }

        /// <summary>16/11 (§271): phím chém (hoặc chuột trái) còn đang giữ.</summary>
        private bool AttackHeld()
        {
            if (GameInput.Held(GameAction.Attack)) return true;
            var m = Mouse.current;
            return m != null && m.leftButton.isPressed && !MapMechanics.PanelOpen && !GameInput.Blocked && !GameInput.Reading;
        }
        /// <summary>VFX dưới chân (tắt VFX thì vẽ vòng tạm).</summary>
        public void SpawnGroundVfx(string name, Vector2 c, float scale, Color fallback, float radius) => strikes.GroundVfx(name, c, scale, fallback, radius);
        /// <summary>Giật camera (nhân cameraKick và cài đặt rung màn hình).</summary>
        public void KickCamera(Vector2 dir, float mul, float duration) => strikes.Kick(dir, mul, duration);
        public void HitStopNow() => strikes.HitStop();
        /// <summary>Gây sát thương mọi quái trong vòng tròn; trả về số quái trúng. Báo luôn cho cơ quan (Struck).</summary>
        public int HitEnemies(Vector2 center, float radius, float dmg, float knock)
        {
            RaiseStruck(center, radius);
            return strikes.Damage(center, radius, dmg, knock, null);
        }

        internal static void RaiseStruck(Vector2 c, float r) => Struck?.Invoke(c, r);

        // ------------------------------------------------------------------ Scene view
        /// <summary>Chọn Player trong Scene để thấy hình quạt chém các hướng (đỏ = hướng đang nhìn).</summary>
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
