using System;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// STATS of one enemy type (HP, speed, damage, range, special attacks...) - tuned in the Inspector, no code change needed.
    /// Asset: Assets/Enemies/&lt;MapN&gt;/Definitions/&lt;Name&gt;.asset, referenced by the enemy prefab through EnemyAI.definition.
    ///
    /// When the enemy spawns (EnemyAI.Awake) every value here OVERRIDES the prefab value, so editing the asset takes effect at once,
    /// no need to rerun "Enemies ▸ Map N". Sprites / animations / boss skill settings (the *Skills components) still live on the prefab.
    /// Create assets for existing enemies: menu Soul Requiem ▸ Enemies ▸ Create Missing Enemy Definitions.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Soul Requiem/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("HP & body")]
        public float maxHp = 50f;
        [Tooltip("Immune to knockback / stun (large bosses)")]
        public bool immuneToCrowdControl;
        [Tooltip("Rigidbody2D mass: heavier means the player pushes it less")]
        public float mass = 1f;

        [Tooltip("Behaviour stats - names match the EnemyAI fields")]
        public EnemyStats stats = new EnemyStats();

        /// <summary>Write the stats onto an enemy (EnemyAI + Health + Rigidbody2D).</summary>
        public void ApplyTo(EnemyAI ai)
        {
            if (ai == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(stats), ai);
            var h = ai.GetComponent<Health>();
            if (h != null) h.Configure(maxHp, false, immuneToCrowdControl);
            var rb = ai.GetComponent<Rigidbody2D>();
            if (rb != null) rb.mass = mass;
        }

        /// <summary>Copy an enemy's current stats into this asset (used when creating the asset from an existing prefab).</summary>
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
    /// Number / enum stats of EnemyAI. Field names MUST match the EnemyAI field names (copied with JsonUtility).
    /// Not included: attackTime / attack2Time (derived from animation length), sprites, bullet prefabs, flight bounds.
    /// </summary>
    [Serializable]
    public class EnemyStats
    {
        [Header("Chung")]
        public string displayName = "Enemy";
        public EnemyKind kind = EnemyKind.Melee;
        public float moveSpeed = 1.6f;
        public float aggroRange = 3.2f;
        [Tooltip("GAP between the enemy collider and the player collider at which the attack starts")]
        public float attackRange = 0.15f;
        public float damage = 10f;
        public float windup = 0.35f;
        public float cooldown = 1.0f;
        [Tooltip("Give up the chase and return when farther than this from the spawn point (0 = no limit)")]
        public float leashRange = 5f;

        [Header("Effects on the player")]
        public float poisonPercent;
        public float poisonDuration = 5f;
        public float stunSeconds;
        public float knockback;

        [Header("Ranged / spikes")]
        public float projectileSpeed = 2.5f;
        public float projectileLifetime = 2f;
        public float keepDistance = 1.8f;
        public int spikeCount = 10;
        public float spikeTriggerRange = 0.9f;
        public float closeRange;

        [Header("Ground slam")]
        public int strikeImpactFrame;
        public float strikeWarn = 0.6f;
        public float strikeRadius = 0.3f;
        public float strikeFps = 14f;
        public Color strikeColor = new Color(0.45f, 1f, 0.4f, 1f);

        [Header("Special attack")]
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

        [Header("Combo / enrage")]
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

        [Header("Turtle shell")]
        public float shellTime = 2.0f;
        public float shellCooldown = 3.5f;
    }
}
