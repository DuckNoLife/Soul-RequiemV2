using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Máu + trạng thái dùng chung cho người chơi và quái.
    /// - Độc: % máu tối đa mỗi giây, KHÔNG cộng dồn (đang trúng độc thì bỏ qua lần mới).
    /// - Choáng: nhân vật đứng yên trong N giây (quái/boss miễn khống chế thì bỏ qua).
    /// - Đẩy lùi / lực đẩy (Roll): cộng dồn vào một vận tốc phụ, giảm dần, script di chuyển tự đọc.
    /// - Invulnerable: bật trong lúc Roll (i-frame).
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private bool isPlayer = false;
        [SerializeField] private bool immuneToCrowdControl = false;
        [SerializeField] private float knockbackDecay = 20f;
        [Tooltip("Người chơi: sau mỗi lần trúng đòn, bỏ qua đòn mới trong N giây (chặn cả vòng đạn trúng cùng 1 khung = chết tức thì)")]
        [SerializeField] private float hitIFrames = 0.25f;
        private float hitImmuneUntil;

        public float Max => maxHp;
        public float Current { get; private set; }
        public float Percent => maxHp > 0f ? Current / maxHp : 0f;
        public bool IsDead => Current <= 0f;
        public bool IsPlayer => isPlayer;
        public bool ImmuneToCrowdControl => immuneToCrowdControl;
        public bool IsStunned => Time.time < stunUntil;
        public float StunTimeLeft => Mathf.Max(0f, stunUntil - Time.time);
        public bool IsPoisoned => poisonRoutine != null;
        /// <summary>Bất tử: Roll bật/tắt cờ này; khiên (<see cref="Shield"/>) tính theo thời gian riêng nên lăn không làm mất khiên.</summary>
        public bool Invulnerable { get => invulnerable || Time.time < shieldUntil || CutsceneSafe; set => invulnerable = value; }
        /// <summary>04/11: người chơi luôn bất tử khi đang diễn cảnh (CutsceneState) — không phụ thuộc cờ `invulnerable`
        /// (trước đây lăn xong là PlayerDodge tắt cờ -> quái đánh được Ren giữa cảnh giới thiệu boss). Độc / cháy cũng tạm ngưng.</summary>
        private bool CutsceneSafe => isPlayer && CutsceneState.Active;
        private bool invulnerable;
        private float shieldUntil;
        /// <summary>Đang có khiên (Mantra "Veil of the Ascended").</summary>
        public bool Shielded => Time.time < shieldUntil;
        /// <summary>Đang cháy Ether (Burn).</summary>
        public bool IsBurning => Time.time < burnUntil;
        private float burnUntil, burnDps;
        /// <summary>08/11 (§197): lạnh cóng (đạn băng của bẫy): nhân tốc độ đi trong một lúc. 1 = bình thường.</summary>
        public float MoveMul => Time.time < chillUntil ? chillMul : 1f;
        private float chillUntil, chillMul = 1f;

        /// <summary>08/11 (§197): làm chậm còn <paramref name="mul"/> tốc độ trong <paramref name="seconds"/> giây (lấy mức chậm hơn).</summary>
        public void Chill(float mul, float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            chillMul = Time.time < chillUntil ? Mathf.Min(chillMul, mul) : mul;
            chillUntil = Mathf.Max(chillUntil, Time.time + seconds);
        }
        private Coroutine burnRoutine;
        /// <summary>Hệ số sát thương nhận vào (1 = bình thường). Lời thề Lantern Knight đặt 0,3 cho đồng đội.</summary>
        public float DamageTakenMul { get; set; } = 1f;
        /// <summary>08/11 (§191): Projectile bật cờ này quanh lời gọi TakeDamage lên người chơi -> thế đỡ (Riposte) KHÔNG chặn đạn
        /// (đạn để nội tại Turned Blade dội lại; hai cơ chế "đỡ đúng nhịp" không còn chồng nhau).</summary>
        public static bool IncomingRanged;
        /// <summary>16/11 (§284): TrapArt.HurtPlayerCrush bật cờ này quanh lời gọi -> bất tử khi lăn KHÔNG cứu (lăn qua bãi gai vẫn dính).</summary>
        public static bool TrapIgnoresRoll;
        /// <summary>08/11 (§191): màn Ether (Veil of the Ascended) — đòn trúng nhân <see cref="Guard"/> mul trong thời gian còn màn.</summary>
        public bool Guarded => Time.time < guardUntil;
        private float guardMul = 1f, guardUntil;
        public void Guard(float mul, float seconds)
        {
            guardMul = Mathf.Clamp01(mul);
            guardUntil = Mathf.Max(guardUntil, Time.time + Mathf.Max(0f, seconds));
        }
        /// <summary>08/11 (§191): lực hãm vận tốc phụ (đv/s²) — SkillKit.Pull tính vận tốc kéo cho đúng quãng.</summary>
        public float KnockbackDecay => knockbackDecay;
        /// Máu không thể xuống dưới mức này (vd Isolde: không chết khi còn quan tài). 0 = bình thường.
        public float Floor { get; set; }

        public event Action<Health, float> Damaged;
        /// <summary>Mọi lần mất máu của BẤT KỲ Health nào (máu, lượng, máu trước khi trúng). Chỉ dùng cho công cụ test (DevCheats Shift+F8).</summary>
        public static event Action<Health, float, float> AnyDamaged;
        public event Action<Health> Died;
        /// <summary>16/11 (§281): chặn đòn theo hướng (vd khiên The Last Guard). Nhận hướng đẩy của đòn (từ kẻ đánh tới mình);
        /// trả true = đòn bị chặn, không mất máu. Chỉ áp cho đòn có hướng (chém, đạn); độc / cháy / chảy máu vẫn qua.</summary>
        public Func<Vector2, bool> BlockCheck;
        public event Action<Vector2> Blocked;
        /// <summary>Trúng đòn thật (TakeDamage, không tính độc): (máu, lượng, hướng đẩy, lực đẩy). Dùng cho hoạt ảnh trúng đòn.</summary>
        public event Action<Health, float, Vector2, float> Hit;
        /// <summary>Gọi khi hồi sinh (Revive).</summary>
        public event Action<Health> Revived;

        private float stunUntil;
        // 07/11 (§183): thế đỡ (Riposte) và lộ sơ hở (Corroded / Spent / Staggered)
        private float parryUntil, vulnUntil, vulnBonus;
        /// <summary>Đòn bị chặn trong thế đỡ: (sát thương, hướng đẩy lùi).</summary>
        public event Action<float, Vector2> Parried;
        public bool Parrying => Time.time < parryUntil;
        /// <summary>Sát thương nhận thêm hiện tại (0.2 = +20%), 0 nếu hết.</summary>
        public float VulnBonus => Time.time < vulnUntil ? vulnBonus : 0f;
        public float VulnLeft => Mathf.Max(0f, vulnUntil - Time.time);
        /// <summary>Mở thế đỡ <paramref name="seconds"/> giây: đòn ĐẦU TIÊN trúng trong lúc này bị chặn hoàn toàn (Parried).</summary>
        public void ParryWindow(float seconds) => parryUntil = Time.time + Mathf.Max(0f, seconds);
        public void EndParry() => parryUntil = 0f;
        /// <summary>Lộ sơ hở: nhận thêm <paramref name="bonus"/> sát thương trong <paramref name="seconds"/> s. Không cộng dồn: lấy mức cao nhất, gia hạn.</summary>
        public void AddVulnerability(float bonus, float seconds)
        {
            if (IsDead || bonus <= 0f || seconds <= 0f) return;
            vulnBonus = Time.time < vulnUntil ? Mathf.Max(vulnBonus, bonus) : bonus;
            vulnUntil = Mathf.Max(vulnUntil, Time.time + seconds);
        }
        private Vector2 extraVelocity;
        private Coroutine poisonRoutine;
        private Coroutine flashRoutine;
        private SpriteRenderer sr;
        private Color baseColor = Color.white;

        private void Awake()
        {
            Current = maxHp;
            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        public void Configure(float hp, bool player, bool immuneCC)
        {
            maxHp = hp;
            Current = hp;
            isPlayer = player;
            immuneToCrowdControl = immuneCC;
        }

        /// <summary>Trả về true nếu thật sự trúng đòn (không bất tử, chưa chết).</summary>
        public bool TakeDamage(float amount, Vector2 knockbackDir = default, float knockbackForce = 0f)
        {
            // 16/11 (§284): bẫy cơ khí (gai, trụ lăn, lưỡi cưa, phụt nước) bỏ qua bất tử khi LĂN — chỉ khiên / cảnh diễn mới chặn
            bool blocked = TrapIgnoresRoll && isPlayer ? (Time.time < shieldUntil || CutsceneSafe) : Invulnerable;
            if (IsDead || blocked || amount <= 0f) return false;
            if (!isPlayer && BlockCheck != null && knockbackDir.sqrMagnitude > 0.0001f && BlockCheck(knockbackDir))   // 16/11 (§281): khiên
            {
                Blocked?.Invoke(knockbackDir);
                return false;
            }
            if (isPlayer && Time.time < parryUntil && !IncomingRanged)  // 07/11: Riposte — chặn đòn cận chiến đầu tiên (§191: không chặn đạn)
            {
                parryUntil = 0f;
                Parried?.Invoke(amount, knockbackDir);
                return false;
            }
            if (isPlayer && Time.time < hitImmuneUntil) return false;
            amount *= Mathf.Max(0f, DamageTakenMul) * (1f + VulnBonus) * (Guarded ? guardMul : 1f);
            if (isPlayer) hitImmuneUntil = Time.time + hitIFrames;
            ApplyDamage(amount, true);
            if (!immuneToCrowdControl && knockbackForce > 0f && knockbackDir.sqrMagnitude > 0.0001f)
                extraVelocity += knockbackDir.normalized * knockbackForce;
            if (!IsDead) Hit?.Invoke(this, amount, knockbackDir, knockbackForce);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHp, Current + amount);
        }

        /// <summary>Đặt máu hiện tại (mang máu từ map trước sang). Không hồi sinh nếu đang gục.</summary>
        public void SetCurrent(float value)
        {
            if (IsDead) return;
            Current = Mathf.Clamp(value, 1f, maxHp);
        }

        public void Revive()
        {
            Current = maxHp;
            stunUntil = 0f;
            extraVelocity = Vector2.zero;
            if (poisonRoutine != null) { StopCoroutine(poisonRoutine); poisonRoutine = null; }
            if (burnRoutine != null) { StopCoroutine(burnRoutine); burnRoutine = null; }
            burnUntil = 0f; burnDps = 0f; shieldUntil = 0f;
            parryUntil = 0f; vulnUntil = 0f; vulnBonus = 0f;
            if (sr != null) sr.color = baseColor;
            Revived?.Invoke(this);
        }

        public void Stun(float seconds)
        {
            if (immuneToCrowdControl || IsDead || Invulnerable || seconds <= 0f) return;
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
        }

        /// <summary>Độc: percentPerSecond (vd 0.02 = 2%/giây) trong duration giây. Không cộng dồn.</summary>
        public void ApplyPoison(float percentPerSecond, float duration)
        {
            if (IsDead || Invulnerable || poisonRoutine != null || percentPerSecond <= 0f) return;
            poisonRoutine = StartCoroutine(PoisonRoutine(percentPerSecond, duration));
            if (isPlayer) FirstTimeHints.Show("tut_poison", "Poisoned! Your health drains for a while (green tint). Keep your distance and drink ({heal}) if it runs low.", 5f);
        }

        /// <summary>02/11: giải độc ngay (thuốc xanh của người chữa bệnh).</summary>
        public void CurePoison()
        {
            if (poisonRoutine != null) { StopCoroutine(poisonRoutine); poisonRoutine = null; }
            if (sr != null) sr.color = baseColor;
        }

        /// <summary>Bất tử trong <paramref name="seconds"/> giây (khiên). Không bị Roll tắt.</summary>
        public void Shield(float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            shieldUntil = Mathf.Max(shieldUntil, Time.time + seconds);
        }

        /// <summary>Burn (lửa Ether): <paramref name="dps"/> sát thương / giây trong <paramref name="duration"/> giây.
        /// Dính lại khi đang cháy: gia hạn thời gian, giữ mức dps lớn hơn (không cộng dồn).</summary>
        /// <summary>16/11 (§275): màu nháy khi cháy (mặc định xanh Ether; thánh quang Accord = vàng).</summary>
        private Color burnTint = new Color(0.55f, 0.8f, 1f, 1f);

        /// <summary>16/11 (§275): cháy với màu nháy riêng (vd lửa thánh quang của Arthur's Accord).</summary>
        public void ApplyBurn(float dps, float duration, Color tint) { burnTint = tint; ApplyBurn(dps, duration); }

        /// <summary>16/11 (§275): sát thương theo thời gian (chảy máu...) — không đẩy lùi, không làm khựng.</summary>
        public void ApplyDot(float amount) { if (!IsDead && !Invulnerable && amount > 0f && !CutsceneSafe) ApplyDamage(amount, false); }

        /// <summary>16/11 (§275): nháy màu thân (hiệu ứng trạng thái).</summary>
        public void TintPulse(Color c, float seconds) { if (sr != null && !IsDead) StartCoroutine(Tint(c, seconds)); }

        public void ApplyBurn(float dps, float duration)
        {
            if (IsDead || Invulnerable || dps <= 0f || duration <= 0f) return;
            burnDps = IsBurning ? Mathf.Max(burnDps, dps) : dps;
            burnUntil = Mathf.Max(burnUntil, Time.time + duration);
            if (burnRoutine == null) burnRoutine = StartCoroutine(BurnRoutine());
        }

        private IEnumerator BurnRoutine()
        {
            Color tint = burnTint;
            while (!IsDead && Time.time < burnUntil)
            {
                yield return new WaitForSeconds(0.5f);
                if (IsDead) break;
                if (CutsceneSafe) continue;                          // 04/11: đang diễn cảnh -> bỏ nhịp cháy này
                ApplyDamage(burnDps * 0.5f, false);
                if (sr != null) StartCoroutine(Tint(tint, 0.12f));
            }
            burnDps = 0f;
            burnRoutine = null;
        }

        /// <summary>Lực đẩy chủ động (vd Roll) — không bị miễn khống chế chặn.</summary>
        public void AddImpulse(Vector2 velocity) => extraVelocity += velocity;

        /// <summary>Script di chuyển gọi mỗi FixedUpdate để lấy vận tốc phụ (đẩy lùi / Roll).</summary>
        public Vector2 ConsumeExtraVelocity(float dt)
        {
            Vector2 v = extraVelocity;
            extraVelocity = Vector2.MoveTowards(extraVelocity, Vector2.zero, knockbackDecay * dt);
            return v;
        }

        private IEnumerator PoisonRoutine(float pct, float duration)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
            Color poisonTint = new Color(0.6f, 1f, 0.5f, 1f);
            for (int i = 0; i < ticks && !IsDead; i++)
            {
                yield return new WaitForSeconds(1f);
                if (IsDead) break;
                if (CutsceneSafe) continue;                          // 04/11: đang diễn cảnh -> bỏ nhịp độc này
                ApplyDamage(maxHp * pct, false);
                if (sr != null) StartCoroutine(Tint(poisonTint, 0.15f));
            }
            poisonRoutine = null;
        }

        private void ApplyDamage(float amount, bool flash)
        {
            float before = Current;
            Current = Mathf.Max(Mathf.Min(Floor, Current), Current - amount);
            AnyDamaged?.Invoke(this, amount, before);
            if (Current < 0f) Current = 0f;
            if (flash && sr != null)
            {
                if (flashRoutine != null) StopCoroutine(flashRoutine);
                flashRoutine = StartCoroutine(Tint(new Color(1f, 0.45f, 0.45f, 1f), 0.1f));
            }
            Damaged?.Invoke(this, amount);
            if (Current <= 0f) Died?.Invoke(this);
        }

        private IEnumerator Tint(Color c, float seconds)
        {
            sr.color = c;
            yield return new WaitForSeconds(seconds);
            if (sr != null) sr.color = baseColor;
            flashRoutine = null;
        }
    }
}
