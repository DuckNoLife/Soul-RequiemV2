using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// STATUS on one enemy caused by katana passives (WeaponTraits.ApplyHitStatus). Added automatically on the first hit.
    ///   Bleed : bleed stacks (each expires on its own), every 0.5 s loses (stacks × statusPower × 0.5) HP; red flash.
    ///   Burn  : handed to Health.ApplyBurn (holy gold colour).
    ///   Bind / Chill : Health.Chill (CC-immune enemies: no slow; bosses: half slow); violet / icy blue flash.
    ///   Mark  : blood-hunger mark — an enemy dying while marked -> WeaponTraits.OnMarkedKill (Hunger heals). <see cref="MarkedAlive"/> counted for the HUD.
    /// VISUALS: currently only a body colour flash. Status icons / particles (blood drop, fire, ice, roots, mark) can be drawn in PixelLab: put frame strips in
    ///   Resources/VFX/status_&lt;bleed|burn|bind|chill|mark&gt; (frames sliced in .meta like other VFX) -> shown over the enemy automatically, no code change.
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
            // gentle steady flash while a lasting status is active (so the player notices)
            if (hp != null && !hp.IsDead && now >= nextPulse)
            {
                nextPulse = now + 0.6f;
                if (now < bindUntil) hp.TintPulse(BindTint, 0.12f);
                else if (now < chillUntil) hp.TintPulse(ChillTint, 0.12f);
                else if (marked) hp.TintPulse(MarkTint, 0.12f);
            }
            UpdateIcon(now);
        }

        // ---- icon over the head (only shown once PixelLab images exist in Resources/VFX/status_*)
        private void UpdateIcon(float now)
        {
            string key = BleedStacks > 0 ? "status_bleed" : now < bindUntil ? "status_bind" : now < chillUntil ? "status_chill"
                       : marked ? "status_mark" : hp != null && hp.IsBurning ? "status_burn" : "";
            if (key != iconKey)
            {
                iconKey = key;
                iconFrames = key.Length > 0 ? Frames(key) : null;
            }
            bool show = iconFrames != null && iconFrames.Length > 0 && body != null && body.enabled && hp != null && !hp.IsDead;   // hidden boss (BossSkillsBase.SetHidden) -> no icon
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
        /// <summary>Frame strip Resources/VFX/&lt;key&gt; (no image yet = empty array, no error — PixelLab images can be added later).</summary>
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
