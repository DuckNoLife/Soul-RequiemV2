using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;

        private Rigidbody2D rb;
        private Animator animator;
        private Health health;
        private PlayerStatus status;           // làm chậm do boss (Map 3)
        private PlayerCombat combat;  // đang chém / dậm thì đứng yên
        private Vector2 input;
        private Vector2 lastFacing = Vector2.down; // Lưu hướng nhìn cuối cùng

        // 07/11 (người dùng: Grok ép kẹt Ren vào góc nước / hàng cây Stone Ring): chống bị quái nặng đè kẹt.
        // Ren muốn đi mà gần như không nhích suốt `PinTime` giây, đồng thời đang chạm thân quái -> tạm tắt va chạm Ren <-> con quái đó,
        // bật lại khi hai bên đã tách nhau. Áp dụng mọi quái / boss, mọi map. Đòn đánh không đổi (quái tính trúng theo khoảng cách).
        private const float PinTime = 0.25f, PinRadius = 0.3f, ReleaseGap = 0.15f, MinIgnore = 0.6f;
        private Vector2 lastPos, lastStep;
        private float pinnedFor;
        private Collider2D[] myCols;
        private readonly Dictionary<Collider2D, float> ghosted = new Dictionary<Collider2D, float>();
        private readonly List<Collider2D> releaseBuf = new List<Collider2D>();

        // 20/11 (người dùng: lướt Q đôi khi xuyên vào trong tường rồi kẹt luôn): Roll / bị đẩy lùi đi 0,2 đv mỗi bước vật lý, vượt qua
        // va chạm dạng VIỀN (tường dựng từ tile = composite outline, không có bề dày) -> lọt vào trong khối tường, không ra được.
        // Bước lớn hơn bước đi bộ thì QUÉT trước bằng rb.Cast: dừng sát tường (chừa Skin) rồi trượt phần còn lại dọc tường.
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
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;   // 20/11: chống xuyên tường khi lướt
            castFilter = new ContactFilter2D { useTriggers = false };
            castFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        }

        private void Update()
        {
            input = ReadInput();

            // Đang choáng / khựng (bơm máu) / đã gục: không nhận điều khiển
            if (health != null && (health.IsDead || health.IsStunned)) input = Vector2.zero;
            if (combat != null && combat.IsAttacking) input *= Mathf.Clamp01(combat.LockedMoveMul);   // 08/11 (§191): 0 = đứng yên
            if (health != null) input *= health.MoveMul;                                            // 08/11 (§197): đạn băng làm chậm

            // Nếu người chơi có bấm nút di chuyển, cập nhật hướng nhìn cuối
            if (input.sqrMagnitude > 0.0001f)
            {
                if (combat != null) combat.StopLook();           // tự đi -> thôi quay mặt nói chuyện
                // Dùng normalized để giá trị x, y luôn nằm trong khoảng -1 đến 1
                lastFacing = input.normalized;
            }

            // Chiêu đang ép hướng nhìn (vd tia Judgement Ray theo chuột) -> nhân vật quay theo hướng đó
            if (combat != null && combat.FacingOverride.sqrMagnitude > 0.01f) lastFacing = combat.FacingOverride.normalized;
            else if (combat != null && combat.LookDir.sqrMagnitude > 0.01f) lastFacing = combat.LookDir;   // 02/11: nhìn người đang nói
            // 16/11 (§270): đang đọc bảng -> quay mặt về phía vật đang đọc (4 hướng như hoạt ảnh đi)
            if (MechanismPanels.Focus.HasValue)
            {
                Vector2 d = MechanismPanels.Focus.Value - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0004f) lastFacing = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2(Mathf.Sign(d.x), 0f) : new Vector2(0f, Mathf.Sign(d.y));
            }

            // Cập nhật Parameters cho Animator Blend Tree
            animator.SetFloat("DirX", lastFacing.x);
            animator.SetFloat("DirY", lastFacing.y);
            animator.SetFloat("Speed", input.magnitude); // Tốc độ = 0 khi đứng im, > 0 khi di chuyển
        }

        private float stepIn;   // 17/11 (§294): âm: tiếng bước chân
        private const float StepEvery = 0.34f;

        private void FixedUpdate()
        {
            Vector2 move = input.sqrMagnitude > 1f ? input.normalized : input;
            Vector2 velocity = move * moveSpeed * (status != null ? status.SpeedMul : 1f) * RelicBoons.SpeedMul;   // 20/11 (§349): Penitent Haste
            if (health != null) velocity += health.ConsumeExtraVelocity(Time.fixedDeltaTime);   // đẩy lùi + Roll
            CheckPinned();
            lastPos = rb.position;
            lastStep = velocity * Time.fixedDeltaTime;
            if (lastStep.magnitude > moveSpeed * RelicBoons.SpeedMul * Time.fixedDeltaTime * 1.1f) lastStep = Sweep(lastStep);   // 20/11: lướt / bị đẩy
            rb.MovePosition(rb.position + lastStep);
            if (move.sqrMagnitude > 0.04f && Time.timeScale > 0f)   // 17/11 (§294): âm
            {
                stepIn -= Time.fixedDeltaTime;
                if (stepIn <= 0f) { stepIn = StepEvery; SoulRequiem.Core.Sfx.Play(SfxHooks.FootstepId(), rb.position); }
            }
            else stepIn = 0.08f;
        }

        /// <summary>20/11: bước lớn -> dừng trước vật cản tĩnh (tường, đá, cổng, bàn...), phần còn lại trượt dọc mặt tường.
        /// Bỏ qua trigger, quái (thân quái do bộ giải vật lý + "lách" CheckPinned lo) và collider đang tạm lách.</summary>
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

        /// <summary>Đi được bao xa theo `dir` (≤ len) trước khi chạm vật cản tĩnh; `normal` = pháp tuyến chỗ chạm.</summary>
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
                if (Vector2.Dot(h.normal, dir) >= -0.01f) continue;                 // đang rời xa / đi song song mặt tường
                if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;   // quái, vật đẩy được
                float d = Mathf.Max(0f, h.distance - Skin);
                if (d < allowed) { allowed = d; normal = h.normal; }
            }
            return allowed;
        }

        /// <summary>Đo bước vừa rồi: muốn đi mà không nhích + đang chạm quái -> "lách" qua con quái đó (tắt va chạm tạm thời).</summary>
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
                Debug.Log("[Unstuck] Ren bị " + c.GetComponentInParent<EnemyAI>().name + " đè kẹt -> tạm lách qua");
            }
        }

        /// <summary>Bật lại va chạm với quái đã tách xa (hoặc đã bị huỷ).</summary>
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

        private static Vector2 ReadInput() => GameInput.Move;     // bàn phím / tay cầm; menu mở -> (0, 0)
    }
}
