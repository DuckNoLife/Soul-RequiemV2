using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    ///   J / chuột trái : chém kiếm (animation Iaido, khoá hướng theo các hướng có animation)
    ///   Q              : lộn nhào (Roll) có i-frame
    ///   E              : bơm máu (bình máu có hạn, khựng 0,5 giây)
    ///   F              : kỹ năng diện rộng (AOE), hồi 5 giây — VFX đập đất (ground_impact)
    ///   Phím test F1–F8: xem DevTools/DevCheats.cs (chỉ có trong Editor / Development Build)
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerCombatPrototype : MonoBehaviour
    {
        [Header("Attack (J / Left Click)")]
        [Tooltip("Directions that have a slash animation (written by the Player Animator builder). The slash snaps to the closest one.")]
        [FormerlySerializedAs("iaidoDirs")] public Vector2[] directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
        [FormerlySerializedAs("iaidoDamage")] public float damage = 40f;
        [Tooltip("Seconds from button press to the hit frame.")]
        [FormerlySerializedAs("iaidoHitTime")] public float hitTime = 0.48f;
        [Tooltip("Seconds the player cannot move while slashing. Moving afterwards cancels the sheathing frames.")]
        [FormerlySerializedAs("iaidoLockTime")] public float lockTime = 0.64f;
        [Tooltip("How far the slash reaches from its pivot (Unity units). The blade itself reaches about 0.46; the rest is a fair margin.")]
        public float range = 0.75f;
        [Tooltip("Width of the slash, in degrees, centred on the facing direction.")]
        [Range(30f, 360f)] public float arcDegrees = 140f;
        [Tooltip("Enemies this close to the pivot are hit whatever the angle (point blank).")]
        public float innerRadius = 0.3f;
        [Tooltip("Height of the slash pivot above the feet (Unity units), about the hips.")]
        public float pivotHeight = 0.2f;
        [FormerlySerializedAs("iaidoKnockback")] public float knockback = 2.5f;

        [Header("Getting Hit")]
        [Tooltip("Hits with at least this much knockback make the player stagger (interrupts attacks). Lighter hits only flinch.")]
        public float heavyHitKnockback = 3f;
        [Tooltip("Seconds the player cannot act while staggering from a heavy hit.")]
        public float staggerLock = 0.45f;

        [Header("Roll (Q)")]
        public float rollSpeed = 7f;
        [Tooltip("Seconds of invulnerability at the start of the roll.")]
        public float rollIFrames = 0.3f;
        public float rollCooldown = 0.7f;

        [Header("Healing Potion (E)")]
        public int potions = 2;
        [Tooltip("Share of max HP restored per potion.")]
        [Range(0f, 1f)] public float healPercent = 0.35f;
        [Tooltip("Seconds the player is locked in place while drinking.")]
        public float healLock = 0.5f;

        [Header("Area Skill (F)")]
        public float skillDamage = 40f;
        public float skillRadius = 1.1f;
        public float skillCooldown = 5f;
        public float skillKnockback = 3f;
        [Tooltip("Seconds from key press to the stomp landing (damage + ground impact). Matches the Stomp animation (frame 4).")]
        public float skillImpactTime = 0.31f;
        [Tooltip("Seconds the player cannot move while stomping.")]
        public float skillLockTime = 0.5f;

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
        [Tooltip("Scale of the ground impact of the Area Skill (F). 1.4 makes its ring about the skill radius.")]
        public float groundImpactScale = 1.4f;

        public Health Health { get; private set; }
        public int Potions => potions;
        public float RollCooldownLeft => Mathf.Max(0f, nextRoll - Time.time);
        /// Hồi chiêu ô chiêu 1 (giữ cho code cũ; HUD mới đọc PlayerLoadout).
        public float SkillCooldownLeft => Loadout != null ? Loadout.CooldownLeft(0) : Mathf.Max(0f, nextSkill - Time.time);
        public PlayerLoadout Loadout { get; private set; }
        public Vector2 Facing => facing;
        /// Đang trong pha chém (khoá di chuyển). PlayerController đọc cờ này.
        public bool IsAttacking => Time.time < attackLockUntil;

        private float nextAttack, nextRoll, nextSkill;
        /// <summary>Bất tử (chỉ bật từ DevTools/DevCheats — phím F1, không có trong bản phát hành).</summary>
        public bool GodMode { get; set; }
        private Vector2 facing = Vector2.down;
        private SpriteRenderer sr;
        private Animator anim;
        private bool animHasDash, animHasIaido;
        private float attackLockUntil;
        private int actionId;                 // tăng khi Roll -> huỷ đòn chém đang chờ trúng
        private static readonly int AtkXId = Animator.StringToHash("AtkX");
        private static readonly int AtkYId = Animator.StringToHash("AtkY");
        private static readonly int IaidoTriggerId = Animator.StringToHash("IaidoTrigger");
        private static readonly int DirXId = Animator.StringToHash("DirX");
        private static readonly int DirYId = Animator.StringToHash("DirY");
        private static readonly int DashTriggerId = Animator.StringToHash("DashTrigger");
        private static readonly int HurtTriggerId = Animator.StringToHash("HurtTrigger");
        private static readonly int SkillTriggerId = Animator.StringToHash("SkillTrigger");
        private static readonly int ReviveTriggerId = Animator.StringToHash("ReviveTrigger");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int StaggerTriggerId = Animator.StringToHash("StaggerTrigger");
        private static readonly int StunnedId = Animator.StringToHash("Stunned");
        private static readonly int DrinkTriggerId = Animator.StringToHash("DrinkTrigger");
        private bool animHasHurt, animHasSkill, animHasDeath, animHasStagger, animHasStun, animHasDrink;
        private bool wasStunned;
        private float drinkUntil;
        private SpriteVfx stunStars;
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private ContactFilter2D filter;

        private void Awake()
        {
            Health = GetComponent<Health>();
            Loadout = PlayerLoadout.Of(this);
            sr = GetComponentInChildren<SpriteRenderer>();
            // Animator nằm trên cùng object với SpriteRenderer (Player)
            anim = GetComponentInChildren<Animator>();
            if (anim != null)
                foreach (var p in anim.parameters)
                {
                    if (p.nameHash == DashTriggerId && p.type == AnimatorControllerParameterType.Trigger) animHasDash = true;
                    if (p.nameHash == IaidoTriggerId && p.type == AnimatorControllerParameterType.Trigger) animHasIaido = true;
                    if (p.nameHash == HurtTriggerId && p.type == AnimatorControllerParameterType.Trigger) animHasHurt = true;
                    if (p.nameHash == SkillTriggerId && p.type == AnimatorControllerParameterType.Trigger) animHasSkill = true;
                    if (p.nameHash == DeadId && p.type == AnimatorControllerParameterType.Bool) animHasDeath = true;
                    if (p.nameHash == StaggerTriggerId && p.type == AnimatorControllerParameterType.Trigger) animHasStagger = true;
                    if (p.nameHash == StunnedId && p.type == AnimatorControllerParameterType.Bool) animHasStun = true;
                    if (p.nameHash == DrinkTriggerId && p.type == AnimatorControllerParameterType.Trigger) animHasDrink = true;
                }
            Health.Hit += OnHit;
            Health.Died += OnDied;
            Health.Revived += OnRevived;
            if (anim != null && !animHasDash)
                Debug.LogWarning("[Player] Animator chưa có trigger 'DashTrigger' — chạy menu Soul Requiem ▸ Gắn animation Dash vào Animator.");
            filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
        }

        public void AddPotion(int n) => potions += n;
        public void SetPotions(int n) => potions = Mathf.Max(0, n);

        private void Update()
        {
            if (Health.IsDead) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            Vector2 move = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            if (move.sqrMagnitude > 0.01f) facing = move.normalized;

            if (GameInput.Blocked) return;                                   // menu đang mở (Pause / Settings / tháo lắp chiêu)

            UpdateStun();
            if (Health.IsStunned || MapMechanics.ConsoleActive) return;      // đang chỉnh ống ở bảng điều khiển

            // phím đọc qua GameInput (đổi phím được, HUD hiện đúng ký hiệu); chiêu theo bộ đang gắn (PlayerLoadout)
            bool attack = GameInput.Pressed(GameAction.Attack) ||
                          (mouse != null && mouse.leftButton.wasPressedThisFrame && !MapMechanics.PanelOpen);   // chuột đang bấm bảng khoá
            if (attack && Time.time >= nextAttack && !IsAttacking) StartCoroutine(Slash());
            if (GameInput.Pressed(GameAction.Roll) && Time.time >= nextRoll) StartCoroutine(Roll());
            if (GameInput.Pressed(GameAction.Heal)) DrinkPotion();
            for (int i = 0; i < PlayerLoadout.SlotCount; i++)
                if (GameInput.Pressed(GameInput.SkillAction(i))) Loadout.TryCast(i);
        }

        /// Đòn rút kiếm chém: khoá hướng theo 4 phía, phát animation Iaido, gây sát thương đúng khung trúng đòn.
        /// Hướng gần nhất (tích vô hướng lớn nhất) trong directions; danh sách rỗng thì dùng 4 hướng chính.
        private Vector2 SnapIaido(Vector2 f)
        {
            if (directions == null || directions.Length == 0)
                return Mathf.Abs(f.x) >= Mathf.Abs(f.y) ? new Vector2(Mathf.Sign(f.x), 0f) : new Vector2(0f, Mathf.Sign(f.y));
            Vector2 best = directions[0].normalized; float bd = -2f;
            foreach (var v in directions)
            {
                if (v.sqrMagnitude < 0.01f) continue;
                float dd = Vector2.Dot(f, v.normalized);
                if (dd > bd + 1e-4f) { bd = dd; best = v.normalized; }
            }
            return best;
        }

        private IEnumerator Slash()
        {
            int id = ++actionId;
            Vector2 d = SnapIaido(facing);
            if (animHasIaido)
            {
                anim.SetFloat(AtkXId, d.x);
                anim.SetFloat(AtkYId, d.y);
                anim.SetTrigger(IaidoTriggerId);
            }
            attackLockUntil = Time.time + lockTime;
            nextAttack = attackLockUntil;

            // Vệt chém bắt đầu sớm 1 khung VFX để khung sáng nhất (khung 2) rơi đúng lúc trúng đòn
            float lead = showVfx ? Mathf.Min(1f / SlashVfxFps, hitTime) : 0f;
            yield return new WaitForSeconds(hitTime - lead);
            if (id != actionId || Health.IsDead || Health.IsStunned) yield break;   // bị Roll / choáng huỷ
            if (showVfx)
            {
                SpawnSlashVfx(d);
                if (lead > 0f) yield return new WaitForSeconds(lead);
                if (id != actionId || Health.IsDead || Health.IsStunned) yield break;
            }
            Vector2 pivot = SlashPivot;
            // cơ quan trên map (van, khoá số...) vẫn nghe theo vòng tròn: vòng nằm trong hình quạt, lệch về phía chém
            Struck?.Invoke(pivot + d * range * 0.5f, range * 0.6f);
            int n = HitEnemiesInArc(pivot, d, damage, knockback);
            if (n > 0) Impact(d);
            if (debugHitbox || (!animHasIaido && !showVfx)) StartCoroutine(DebugArc(pivot, d));   // vùng trúng thật
        }

        private Vector2 SlashPivot => (Vector2)transform.position + Vector2.up * pivotHeight;

        /// Quái trúng nếu collider của nó (điểm gần nhất hoặc tâm) nằm trong hình quạt: cách tâm quạt ≤ range và lệch
        /// hướng chém ≤ arcDegrees/2; sát người hơn innerRadius thì trúng mọi góc.
        private bool InArc(Collider2D c, Vector2 pivot, Vector2 d)
        {
            Vector2 near = c.ClosestPoint(pivot) - pivot;
            if (near.magnitude <= innerRadius) return true;
            float half = arcDegrees * 0.5f;
            Vector2 mid = (Vector2)c.bounds.center - pivot;
            return Vector2.Angle(d, near) <= half || (mid.magnitude <= range && Vector2.Angle(d, mid) <= half);
        }

        private int VfxLayer => sr != null ? sr.sortingLayerID : 0;
        private int PlayerOrder => sr != null ? sr.sortingOrder : 2;

        // ---- Vệt chém (Resources/VFX/slash_<E|NE|N|NW|W|SW|S|SE>, PixelLab VFX Workbench "Iaido Blade Arc", 20 fps)
        // Tâm vòng = SlashPivot (hông). Lưỡi chính bán kính 89 px, sóng mỏng 103 px, quét 140° quanh hướng chém từ tay trái
        // sang tay phải nhân vật. Ở tỉ lệ 0.7 mép sóng ~0.75 đơn vị = đúng range mặc định; đổi range thì vệt giãn theo.
        const float SlashVfxFps = 20f;
        const float SlashArtRange = 0.75f;
        static readonly string[] SlashKeys = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

        private float SlashScale => vfxScale * Mathf.Max(0.3f, range / SlashArtRange);

        private void SpawnSlashVfx(Vector2 d)
        {
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int i = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            // chém lên trên: vệt vòng qua sau đầu -> vẽ sau nhân vật cho có chiều sâu
            SpriteVfx.Spawn("slash_" + SlashKeys[i], SlashPivot, SlashScale, VfxLayer, PlayerOrder + (d.y > 0.3f ? -1 : 1), false, SlashVfxFps);
        }

        // ---- Cảm giác lực khi chém trúng: dừng hình rất ngắn + giật camera theo hướng chém
        private static bool hitStopping;
        private CameraFollow camFollow;

        private void Impact(Vector2 d)
        {
            if (hitStop > 0f) StartCoroutine(HitStop(hitStop));
            if (cameraKick > 0f)
            {
                if (camFollow == null && Camera.main != null) camFollow = Camera.main.GetComponent<CameraFollow>();
                if (camFollow != null) camFollow.Kick(d, cameraKick, 0.16f);
            }
        }

        private IEnumerator HitStop(float seconds)
        {
            if (hitStopping || Time.timeScale < 0.99f) yield break;      // đang pause / đang dừng hình khác
            hitStopping = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
            hitStopping = false;
        }

        private void OnDisable()
        {
            if (hitStopping) { hitStopping = false; if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f; }
        }

        // Chọn Player trong Scene để thấy hình quạt chém của 8 hướng (đỏ = hướng đang nhìn)
        private void OnDrawGizmosSelected()
        {
            Vector2 f = Application.isPlaying ? SnapIaido(facing) : Vector2.down;
            var dirs = directions != null && directions.Length > 0 ? directions : new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            foreach (var v in dirs)
            {
                if (v.sqrMagnitude < 0.01f) continue;
                Vector2 n = v.normalized;
                Gizmos.color = Vector2.Dot(n, f) > 0.99f ? new Color(1f, 0.25f, 0.25f, 0.9f) : new Color(1f, 0.6f, 0.2f, 0.2f);
                var pts = ArcOutline(SlashPivot, n);
                for (int k = 0; k < pts.Length; k++) Gizmos.DrawLine(pts[k], pts[(k + 1) % pts.Length]);
            }
        }

        /// Viền hình quạt chém (và vòng sát người) để vẽ gizmo / debug.
        private Vector3[] ArcOutline(Vector2 pivot, Vector2 d)
        {
            const int seg = 24;
            var list = new List<Vector3>();
            float a0 = Mathf.Atan2(d.y, d.x) - arcDegrees * 0.5f * Mathf.Deg2Rad;
            float span = arcDegrees * Mathf.Deg2Rad;
            list.Add(pivot + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius);
            for (int k = 0; k <= seg; k++)
            {
                float a = a0 + span * k / seg;
                list.Add(pivot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * range);
            }
            for (int k = seg; k >= 0; k--)
            {
                float a = a0 + span * k / seg;
                list.Add(pivot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * innerRadius);
            }
            return list.ToArray();
        }

        private IEnumerator DebugArc(Vector2 pivot, Vector2 d)
        {
            var go = new GameObject("fx_hitbox");
            var lr = go.AddComponent<LineRenderer>();
            var pts = ArcOutline(pivot, d);
            lr.positionCount = pts.Length; lr.SetPositions(pts); lr.loop = true;
            lr.widthMultiplier = 0.015f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(1f, 0.25f, 0.25f, 0.9f);
            lr.sortingLayerID = VfxLayer; lr.sortingOrder = PlayerOrder + 5;
            yield return new WaitForSeconds(0.25f);
            Destroy(lr.material);
            Destroy(go);
        }

        /// Chiêu F "dậm đất": hoạt ảnh Stomp (lấy đà -> nhấc gối -> dậm) rồi mới gây sát thương + nứt đất đúng khung chạm đất.
        /// Lộn (Q) trước khi chạm đất thì huỷ chiêu và hoàn lại hồi chiêu.
        private IEnumerator Skill()
        {
            int id = ++actionId;
            nextSkill = Time.time + skillCooldown;
            if (animHasSkill)
            {
                anim.SetFloat(DirXId, facing.x);
                anim.SetFloat(DirYId, facing.y);
                anim.SetTrigger(SkillTriggerId);
                attackLockUntil = Time.time + skillLockTime;
                yield return new WaitForSeconds(skillImpactTime);
                if (id != actionId || Health.IsDead || Health.IsStunned) { nextSkill = Time.time; yield break; }
            }
            Vector2 c = transform.position;
            int n = HitEnemies(c, skillRadius, skillDamage, skillKnockback);
            SkillCast?.Invoke(c, skillRadius);
            if (showVfx) SpriteVfx.Spawn("ground_impact", c, groundImpactScale, VfxLayer, PlayerOrder - 1);   // nằm dưới nhân vật
            else StartCoroutine(Swing(c, skillRadius, new Color(0.6f, 0.85f, 1f, 0.5f)));
            // đất rung: giật camera xuống, dừng hình nếu trúng quái
            if (cameraKick > 0f)
            {
                if (camFollow == null && Camera.main != null) camFollow = Camera.main.GetComponent<CameraFollow>();
                if (camFollow != null) camFollow.Kick(Vector2.down, cameraKick * 1.4f, 0.22f);
            }
            if (n > 0 && hitStop > 0f) StartCoroutine(HitStop(hitStop));
        }

        /// Hồi máu: vòng sáng xanh lan dưới chân (sau nhân vật) + dấu cộng và đốm sáng bay lên (trước nhân vật),
        /// bắt đầu lúc ngụm thuốc (0,22 s sau khi bấm E). VFX "Ronin Heal" (PixelLab), Resources/VFX/heal_back|heal_front.
        private IEnumerator HealVfx()
        {
            yield return new WaitForSeconds(0.22f);          // đúng khung ngửa cổ uống (khung 4 của hoạt ảnh Drink)
            if (Health.IsDead) yield break;
            var b = SpriteVfx.Spawn("heal_back", transform.position, vfxScale, VfxLayer, PlayerOrder - 1, false, 14f);
            var f = SpriteVfx.Spawn("heal_front", transform.position, vfxScale, VfxLayer, PlayerOrder + 1, false, 14f);
            if (b != null) b.Follow(transform);
            if (f != null) f.Follow(transform);
        }

        // ---- Trúng đòn / gục / hồi sinh (hoạt ảnh Hurt, Death, Revive trong Animator)
        private void OnHit(Health h, float amount, Vector2 dir, float force)
        {
            if (anim == null || h.IsDead) return;
            if (force >= heavyHitKnockback && animHasStagger)
            {
                // đòn nặng: loạng choạng, huỷ chém / dậm đang làm
                actionId++;
                attackLockUntil = Time.time + staggerLock;
                anim.ResetTrigger(HurtTriggerId);
                anim.SetTrigger(StaggerTriggerId);
                return;
            }
            if (!animHasHurt) return;
            if (IsAttacking || Health.Invulnerable) return;      // đang chém / dậm / lộn: giữ nguyên động tác (không cướp điều khiển)
            anim.SetTrigger(HurtTriggerId);
        }

        /// Choáng (không phải khựng do uống thuốc): Stagger -> vòng lặp Stunned + sao quay trên đầu.
        private void UpdateStun()
        {
            bool stunned = Health.IsStunned && Time.time >= drinkUntil && !Health.IsDead;
            if (anim != null && animHasStun) anim.SetBool(StunnedId, stunned);
            if (stunned && !wasStunned)
            {
                if (anim != null && animHasStagger) { anim.ResetTrigger(HurtTriggerId); anim.SetTrigger(StaggerTriggerId); }
                if (showVfx && sr != null)
                    stunStars = SpriteVfx.SpawnAttached("stun_stars", transform, vfxScale, VfxLayer, PlayerOrder + 1, 12f, Health.StunTimeLeft);
            }
            if (!stunned && wasStunned && stunStars != null) stunStars.Stop();
            wasStunned = stunned;
        }

        private void OnDied(Health h)
        {
            actionId++;                                          // huỷ chém / dậm đang chờ
            attackLockUntil = 0f;
            if (anim == null || !animHasDeath) return;
            foreach (var t in new[] { HurtTriggerId, SkillTriggerId, IaidoTriggerId, DashTriggerId, ReviveTriggerId }) anim.ResetTrigger(t);
            anim.SetBool(DeadId, true);
        }

        private void OnRevived(Health h)
        {
            if (anim == null || !animHasDeath) return;
            anim.SetBool(DeadId, false);
            anim.SetTrigger(ReviveTriggerId);
            attackLockUntil = Time.time + 0.75f;                // đứng dậy xong mới đi được (khớp hoạt ảnh Revive)
        }

        private void OnDestroy()
        {
            if (Health == null) return;
            Health.Hit -= OnHit; Health.Died -= OnDied; Health.Revived -= OnRevived;
        }

        /// <summary>Mỗi nhát chém / kỹ năng: (tâm, bán kính). Cơ quan trên map (van, ống, vật cản gỗ) nghe sự kiện này.</summary>
        public static event System.Action<Vector2, float> Struck;
        /// <summary>Kỹ năng vùng (F): (tâm, bán kính). Mây độc nghe sự kiện này (sách Purifier).</summary>
        public static event System.Action<Vector2, float> SkillCast;

        // ------------------------------------------------------------------ API cho chiêu tháo lắp (SkillDefinition.Cast)
        /// Bắt đầu một hành động mới (huỷ đòn chém đang chờ). Trả mã để kiểm tra còn hiệu lực.
        public int BeginAction() => ++actionId;
        /// Hành động `id` chưa bị Roll / choáng / gục huỷ.
        public bool ActionAlive(int id) => id == actionId && !Health.IsDead && !Health.IsStunned;
        /// Phát hoạt ảnh chiêu (Stomp) theo hướng nhìn. false nếu Animator không có trigger.
        public bool PlaySkillAnimation()
        {
            if (!animHasSkill) return false;
            anim.SetFloat(DirXId, facing.x);
            anim.SetFloat(DirYId, facing.y);
            anim.SetTrigger(SkillTriggerId);
            return true;
        }
        public void LockActions(float seconds) => attackLockUntil = Mathf.Max(attackLockUntil, Time.time + seconds);
        public void RaiseSkillCast(Vector2 c, float r) => SkillCast?.Invoke(c, r);
        /// VFX dưới chân (tắt VFX thì vẽ vòng tạm).
        public void SpawnGroundVfx(string name, Vector2 c, float scale, Color fallback, float radius)
        {
            if (showVfx) SpriteVfx.Spawn(name, c, scale, VfxLayer, PlayerOrder - 1);
            else StartCoroutine(Swing(c, radius, fallback));
        }
        /// Giật camera (nhân cameraKick và cài đặt rung màn hình).
        public void KickCamera(Vector2 dir, float mul, float duration)
        {
            if (cameraKick <= 0f) return;
            if (camFollow == null && Camera.main != null) camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow != null) camFollow.Kick(dir, cameraKick * mul, duration);
        }
        public void HitStopNow() { if (hitStop > 0f) StartCoroutine(HitStop(hitStop)); }

        /// Gây sát thương mọi quái trong vòng tròn (kỹ năng F); trả về số quái trúng.
        public int HitEnemies(Vector2 center, float radius, float dmg, float knock)
        {
            Struck?.Invoke(center, radius);
            return Damage(center, radius, dmg, knock, null);
        }

        /// Đòn chém: quái trong hình quạt quanh hướng d.
        private int HitEnemiesInArc(Vector2 pivot, Vector2 d, float dmg, float knock) =>
            Damage(pivot, range, dmg, knock, c => InArc(c, pivot, d));

        private int Damage(Vector2 center, float radius, float dmg, float knock, System.Func<Collider2D, bool> accept)
        {
            hits.Clear();
            Physics2D.OverlapCircle(center, radius, filter, hits);
            var done = new HashSet<Health>();
            foreach (var c in hits)
            {
                if (accept != null && !accept(c)) continue;
                var ai = c.GetComponentInParent<EnemyAI>();
                if (ai == null || ai.Health == null || done.Contains(ai.Health)) continue;
                done.Add(ai.Health);
                Vector2 dir = (Vector2)ai.transform.position - (Vector2)transform.position;
                ai.Health.TakeDamage(dmg, dir, knock);
                // VFX trúng đòn ở thân quái (collider quái chỉ là vệt chân -> kéo lên phía giữa hình quái);
                // máu bắn theo hướng đòn (ảnh gốc bắn sang phải)
                if (showVfx)
                {
                    Vector2 p = c.bounds.center;
                    var body = ai.GetComponentInChildren<SpriteRenderer>();
                    if (body != null) p.y = Mathf.Lerp(p.y, body.bounds.center.y, 0.6f);
                    SpriteVfx.Spawn("hit", p, vfxScale, VfxLayer, PlayerOrder + 2, dir.x < 0f);
                }
            }
            return done.Count;
        }

        private IEnumerator Roll()
        {
            nextRoll = Time.time + rollCooldown;
            actionId++;                 // Roll huỷ đòn chém đang dở
            attackLockUntil = 0f;

            // Animation Dash: truyền hướng lướt vào Blend Tree rồi kích hoạt
            if (anim != null && animHasDash)
            {
                anim.SetFloat(DirXId, facing.x);
                anim.SetFloat(DirYId, facing.y);
                anim.SetTrigger(DashTriggerId);
            }

            Health.AddImpulse(facing * rollSpeed);
            Health.Invulnerable = true;
            Color c0 = sr != null ? sr.color : Color.white;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, 0.5f);
            yield return new WaitForSeconds(rollIFrames);
            Health.Invulnerable = false;
            if (sr != null) sr.color = c0;
        }

        private void DrinkPotion()
        {
            if (potions <= 0)
            {
                Hints.Show("Out of potions!", 1.5f);
                return;
            }
            if (Health.Percent >= 0.999f) return;
            var st = GetComponent<PlayerStatus>();
            if (st != null && st.HealBlocked)
            {
                Hints.Show("Hemorrhage: the potion will not take!", 1.4f);
                return;
            }
            potions--;
            Health.Heal(Health.Max * healPercent);
            drinkUntil = Time.time + healLock + 0.05f;   // khựng này là uống thuốc, không phải choáng
            if (anim != null && animHasDrink) anim.SetTrigger(DrinkTriggerId);
            Health.Stun(healLock);          // khựng 0,5 giây theo logbook
            if (showVfx) StartCoroutine(HealVfx());
        }

        // Vòng tròn mờ báo hiệu tầm đánh (chỉ để test, không cần sprite)
        private IEnumerator Swing(Vector2 center, float radius, Color color)
        {
            var go = new GameObject("fx_swing");
            go.transform.position = center;
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sprite = CircleSprite;
            r.color = color;
            r.sortingOrder = 50;
            float t = 0f;
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                float s = radius * 2f * Mathf.Lerp(0.7f, 1f, t / 0.12f);
                go.transform.localScale = new Vector3(s, s, 1f);
                r.color = new Color(color.r, color.g, color.b, color.a * (1f - t / 0.12f));
                yield return null;
            }
            Destroy(go);
        }

        private static Sprite circle;
        private static Sprite CircleSprite
        {
            get
            {
                if (circle != null) return circle;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        byte a = (byte)(d > 1f ? 0 : d > 0.85f ? 255 : 90);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 đơn vị đường kính
                return circle;
            }
        }
    }
}
