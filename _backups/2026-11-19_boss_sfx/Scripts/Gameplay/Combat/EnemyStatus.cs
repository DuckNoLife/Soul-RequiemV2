using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§275): TRẠNG THÁI trên một con quái do nội tại katana gây ra (WeaponTraits.ApplyHitStatus). Tự gắn khi trúng lần đầu.
    ///   Bleed : tầng chảy máu (mỗi tầng hết hạn riêng), mỗi 0,5 s mất (số tầng × statusPower × 0,5) máu; nháy đỏ.
    ///   Burn  : chuyển cho Health.ApplyBurn (màu vàng thánh quang).
    ///   Bind / Chill : Health.Chill (quái miễn khống chế: không chậm; boss: chậm một nửa); nháy tím / xanh lạnh.
    ///   Mark  : dấu khát máu — quái chết khi còn dấu -> WeaponTraits.OnMarkedKill (Hunger hồi máu). Đếm <see cref="MarkedAlive"/> cho HUD.
    /// HÌNH: hiện chỉ nháy màu thân. Biểu tượng / hạt trạng thái (giọt máu, lửa, băng, rễ, dấu) sẽ vẽ bằng PixelLab: đặt dải khung vào
    ///   Resources/VFX/status_&lt;bleed|burn|bind|chill|mark&gt; (cắt khung trong .meta như VFX khác) -> tự hiện trên đầu quái, không phải sửa code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyStatus : MonoBehaviour
    {
        public static int MarkedAlive { get; private set; }

        private static readonly Color BleedTint = new Color(1f, 0.35f, 0.35f, 1f);
        private static readonly Color BindTint = new Color(0.78f, 0.5f, 1f, 1f);
        private static readonly Color ChillTint = new Color(0.65f, 0.85f, 1f, 1f);
        private static readonly Color MarkTint = new Color(0.7f, 0.12f, 0.2f, 1f);
        private static readonly Color HolyTint = new Color(1f, 0.85f, 0.45f, 1f);

        private EnemyAI ai;
        private Health hp;
        private readonly List<float> bleed = new List<float>();
        private float bleedDps, markUntil, bindUntil, chillUntil, nextPulse;
        private bool marked;
        private PlayerCombat marker;
        private Coroutine bleedRoutine;
        private SpriteRenderer icon, body;
        private Sprite[] iconFrames;
        private string iconKey = "";

        public int BleedStacks { get { Prune(); return bleed.Count; } }

        public static EnemyStatus Of(EnemyAI ai)
        {
            var s = ai.GetComponent<EnemyStatus>();
            if (s == null) s = ai.gameObject.AddComponent<EnemyStatus>();
            return s;
        }

        private void Awake()
        {
            ai = GetComponent<EnemyAI>();
            hp = ai != null ? ai.Health : GetComponent<Health>();
            body = GetComponentInChildren<SpriteRenderer>();
            if (hp != null) hp.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (hp != null) hp.Died -= OnDied;
            if (marked) { marked = false; MarkedAlive = Mathf.Max(0, MarkedAlive - 1); }
        }

        public void Apply(StatusKind kind, float power, float seconds, int maxStacks, PlayerCombat from)
        {
            if (hp == null || hp.IsDead || seconds <= 0f) return;
            float now = Time.time;
            bool boss = ai != null && ai.IsBoss;
            switch (kind)
            {
                case StatusKind.Bleed:
                    Prune();
                    bleed.Add(now + seconds);
                    while (bleed.Count > Mathf.Max(1, maxStacks)) bleed.RemoveAt(0);
                    bleedDps = Mathf.Max(0f, power);
                    if (bleedRoutine == null) bleedRoutine = StartCoroutine(BleedRoutine());
                    hp.TintPulse(BleedTint, 0.1f);
                    break;
                case StatusKind.Burn:
                    hp.ApplyBurn(power, seconds, HolyTint);
                    break;
                case StatusKind.Bind:
                case StatusKind.Chill:
                    if (hp.ImmuneToCrowdControl) break;
                    float slow = Mathf.Clamp01(power) * (boss ? 0.5f : 1f);
                    hp.Chill(1f - slow, seconds);
                    if (kind == StatusKind.Bind) bindUntil = now + seconds; else chillUntil = now + seconds;
                    hp.TintPulse(kind == StatusKind.Bind ? BindTint : ChillTint, 0.15f);
                    break;
                case StatusKind.Mark:
                    markUntil = now + seconds;
                    marker = from;
                    if (!marked) { marked = true; MarkedAlive++; }
                    hp.TintPulse(MarkTint, 0.15f);
                    break;
            }
        }

        private void Prune()
        {
            float now = Time.time;
            bleed.RemoveAll(t => t <= now);
        }

        private IEnumerator BleedRoutine()
        {
            while (hp != null && !hp.IsDead)
            {
                yield return new WaitForSeconds(0.5f);
                Prune();
                if (bleed.Count == 0) break;
                hp.ApplyDot(bleed.Count * bleedDps * 0.5f);
                hp.TintPulse(BleedTint, 0.08f);
            }
            bleedRoutine = null;
        }

        private void Update()
        {
            float now = Time.time;
            if (marked && now >= markUntil) { marked = false; MarkedAlive = Mathf.Max(0, MarkedAlive - 1); }
            // nháy nhẹ đều tay khi đang có trạng thái kéo dài (để người chơi nhận ra)
            if (hp != null && !hp.IsDead && now >= nextPulse)
            {
                nextPulse = now + 0.6f;
                if (now < bindUntil) hp.TintPulse(BindTint, 0.12f);
                else if (now < chillUntil) hp.TintPulse(ChillTint, 0.12f);
                else if (marked) hp.TintPulse(MarkTint, 0.12f);
            }
            UpdateIcon(now);
        }

        // ---- biểu tượng trên đầu (chỉ hiện khi đã có ảnh PixelLab trong Resources/VFX/status_*)
        private void UpdateIcon(float now)
        {
            string key = BleedStacks > 0 ? "status_bleed" : now < bindUntil ? "status_bind" : now < chillUntil ? "status_chill"
                       : marked ? "status_mark" : hp != null && hp.IsBurning ? "status_burn" : "";
            if (key != iconKey)
            {
                iconKey = key;
                iconFrames = key.Length > 0 ? Frames(key) : null;
            }
            bool show = iconFrames != null && iconFrames.Length > 0 && body != null && hp != null && !hp.IsDead;
            if (!show) { if (icon != null) icon.enabled = false; return; }
            if (icon == null)
            {
                icon = new GameObject("Status icon").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(transform, true);
                SoulRequiem.Core.FxLighting.MakeUnlit(icon);
            }
            icon.enabled = true;
            icon.sprite = iconFrames[(int)(now * 10f) % iconFrames.Length];
            icon.sortingLayerID = body.sortingLayerID;
            icon.sortingOrder = body.sortingOrder + 2;
            var b = body.bounds;
            icon.transform.position = new Vector3(b.center.x, b.max.y + 0.06f, 0f);
        }

        private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();
        /// <summary>Dải khung Resources/VFX/&lt;key&gt; (chưa có ảnh = mảng rỗng, không báo lỗi — ảnh PixelLab thêm sau).</summary>
        private static Sprite[] Frames(string key)
        {
            if (frameCache.TryGetValue(key, out var f)) return f;
            var list = new List<Sprite>(Resources.LoadAll<Sprite>("VFX/" + key));
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return frameCache[key] = list.ToArray();
        }

        private void OnDied(Health h)
        {
            if (marked)
            {
                marked = false;
                MarkedAlive = Mathf.Max(0, MarkedAlive - 1);
                if (marker != null && Time.time < markUntil) WeaponTraits.OnMarkedKill(marker, transform.position);
            }
            if (icon != null) icon.enabled = false;
        }
    }
}
