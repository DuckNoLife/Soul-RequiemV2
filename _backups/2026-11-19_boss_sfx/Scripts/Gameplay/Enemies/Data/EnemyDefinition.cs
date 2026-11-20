using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// CHỈ SỐ của một loại quái (máu, tốc độ, sát thương, tầm, đòn đặc biệt...) — chỉnh trong Inspector, không cần sửa code.
    /// Asset: Assets/Enemies/&lt;MapN&gt;/Definitions/&lt;Tên&gt;.asset, prefab quái tham chiếu qua EnemyAI.definition.
    ///
    /// Khi quái sinh ra (EnemyAI.Awake) mọi chỉ số ở đây GHI ĐÈ giá trị trong prefab -> chỉnh asset là có hiệu lực ngay,
    /// không cần chạy lại "Dựng quái Map N". Hình ảnh / hoạt ảnh / thông số riêng của bộ chiêu boss (component *Skills) vẫn ở prefab.
    /// Tạo asset cho quái có sẵn: menu Soul Requiem ▸ Quái ▸ Tạo EnemyDefinition từ prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Soul Requiem/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Máu & thân")]
        public float maxHp = 50f;
        [Tooltip("Không bị đẩy lùi / choáng (boss lớn)")]
        public bool immuneToCrowdControl;
        [Tooltip("Khối lượng Rigidbody2D: nặng thì khó bị người chơi đẩy")]
        public float mass = 1f;

        [Tooltip("Chỉ số hành vi — tên trùng với field của EnemyAI")]
        public EnemyStats stats = new EnemyStats();

        /// <summary>Ghi chỉ số lên quái (EnemyAI + Health + Rigidbody2D).</summary>
        public void ApplyTo(EnemyAI ai)
        {
            if (ai == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(stats), ai);
            var h = ai.GetComponent<Health>();
            if (h != null) h.Configure(maxHp, false, immuneToCrowdControl);
            var rb = ai.GetComponent<Rigidbody2D>();
            if (rb != null) rb.mass = mass;
        }

        /// <summary>Chép chỉ số hiện tại của một quái vào asset (dùng khi tạo asset từ prefab có sẵn).</summary>
        public void CaptureFrom(EnemyAI ai)
        {
            if (ai == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(ai), stats);
            var h = ai.GetComponent<Health>();
            if (h != null) { maxHp = h.Max; immuneToCrowdControl = h.ImmuneToCrowdControl; }
            var rb = ai.GetComponent<Rigidbody2D>();
            if (rb != null) mass = rb.mass;
        }
    }

    /// <summary>
    /// Các chỉ số kiểu số / enum của EnemyAI. Tên field PHẢI trùng tên field trong EnemyAI (chép qua JsonUtility).
    /// Không gồm: attackTime / attack2Time (tính từ độ dài hoạt ảnh), sprite, prefab đạn, vùng bay (bounds).
    /// </summary>
    [Serializable]
    public class EnemyStats
    {
        [Header("Chung")]
        public string displayName = "Enemy";
        public EnemyKind kind = EnemyKind.Melee;
        public float moveSpeed = 1.6f;
        public float aggroRange = 3.2f;
        [Tooltip("Khoảng HỞ giữa collider quái và collider người chơi để bắt đầu đánh")]
        public float attackRange = 0.15f;
        public float damage = 10f;
        public float windup = 0.35f;
        public float cooldown = 1.0f;
        [Tooltip("Rời xa điểm sinh quá khoảng này thì bỏ đuổi, quay về (0 = không giới hạn)")]
        public float leashRange = 5f;

        [Header("Hiệu ứng lên người chơi")]
        public float poisonPercent;
        public float poisonDuration = 5f;
        public float stunSeconds;
        public float knockback;

        [Header("Tầm xa / gai")]
        public float projectileSpeed = 2.5f;
        public float projectileLifetime = 2f;
        public float keepDistance = 1.8f;
        public int spikeCount = 10;
        public float spikeTriggerRange = 0.9f;
        public float closeRange;

        [Header("Đòn đánh xuống đất")]
        public int strikeImpactFrame;
        public float strikeWarn = 0.6f;
        public float strikeRadius = 0.3f;
        public float strikeFps = 14f;
        public Color strikeColor = new Color(0.45f, 1f, 0.4f, 1f);

        [Header("Đòn đặc biệt")]
        public EnemySpecial special = EnemySpecial.None;
        public int specialEvery;
        public float specialWindup = 0.8f;
        public float spinTime = 1.2f;
        public float spinEndTime = 0.4f;
        public float spinRadius = 0.55f;
        public float spinTick = 0.35f;
        public float spinMoveFactor = 0.55f;
        public int specialImpactFrame = 1;
        public float specialRadius = 0.38f;

        [Header("Combo / nổi điên")]
        public int comboHits = 1;
        public float comboGap = 0.2f;
        [Range(0f, 1f)] public float enrageAt;
        public float dashSpeed = 4f;
        public float dashTime = 0.35f;
        public float dashCooldown = 3.5f;

        [Header("Boss")]
        public float chargeSpeed = 5f;
        public float chargeTime = 0.45f;
        public float recoveryTime = 1.6f;
        public float slamRadius = 0.35f;
        [Range(0f, 1f)] public float phase2At = 0.5f;
        public bool treatAsBoss;
        public bool floating;

        [Header("Mai rùa")]
        public float shellTime = 2.0f;
        public float shellCooldown = 3.5f;
    }
}
