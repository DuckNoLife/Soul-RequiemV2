using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Hand of the Vessel" — "Mantras of the Vessel" set, slot II. Ren raises a hand: darkness spreads under the enemy closest to the aim point
    /// (≤ <see cref="searchRadius"/> around it, aim point ≤ <see cref="range"/> u), follows it for <see cref="markTime"/> s, then locks;
    /// a giant corpse hand (Specimen Zero's PixelLab art, Resources/VFX/specimen/hand_*) bursts up and grabs: <see cref="damage"/> +
    /// stun <see cref="stun"/> s (bosses are stun-immune; Bound targets: +<see cref="boundBonus"/>). No enemy nearby: the hand
    /// still bursts up at the aim point. The shadow locks <see cref="lockBefore"/> s before — fast enemies running out of it dodge (counterplay).
    /// Balance: 75 / 15 s = 5 DPS single target, burst + control; it has a delay, so it is not an instant interrupt.
    /// Targets that are BOUND (Grasping Roots) take <see cref="boundBonus"/> extra damage (75 -> 112) — the Roots -> Hand combo.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Hand of the Vessel", fileName = "VesselHand")]
    public class VesselHandSkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Targeted - Burst";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Range", Tiles(range));
            Stat(o, "Stun", Secs(stun));
            Stat(o, "Vs Bound", "+" + Pct(boundBonus) + " damage");
        }

        public float range = 3f;
        public float searchRadius = 1.2f;
        public float markTime = 0.5f;
        public float lockBefore = 0.12f;
        public float damage = 75f;
        public float hitRadius = 0.45f;
        public float stun = 1.5f;
        [Tooltip("Extra damage on a Bound target (0.5 = +50%).")]
        public float boundBonus = 0.5f;
        public float castLock = 0.4f;
        [Tooltip("Seconds from the press until the shadow appears. Rolling before that = cancel, cooldown refunded.")]
        public float castDelay = 0.2f;
        public float handScale = 0.65f;
        public Color handTint = new Color(0.92f, 0.85f, 1f);

        private static Sprite shadow;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(castLock);
            Vector2 aim = SkillKit.AimPoint(p, range);
            Vector2 d0 = aim - (Vector2)p.transform.position;
            if (d0.sqrMagnitude > 0.0001f) p.SetFacing(d0);
            if (p.PlayEmbersCast() && castDelay > 0f)
            {
                yield return new WaitForSeconds(castDelay);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            Vector2 feet = p.transform.position;
            EnemyAI target = null;
            float best = float.MaxValue;
            foreach (var ai in SkillKit.EnemiesIn(aim, searchRadius, feet))
            {
                float dd = ((Vector2)ai.transform.position - aim).sqrMagnitude;
                if (dd < best) { best = dd; target = ai; }
            }
            int layer = p.VfxLayer, order = p.PlayerOrder;

            // darkness spreads, follows the target
            if (shadow == null) shadow = MakeShadow();
            var go = new GameObject("Vessel Shadow");
            var sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sprite = shadow; sr.sortingLayerID = layer; sr.sortingOrder = order - 1;
            Vector2 at = target != null ? (Vector2)target.transform.position : aim;
            for (float t = 0f; t < markTime; t += Time.deltaTime)
            {
                if (target != null && target.Health != null && !target.Health.IsDead && t < markTime - lockBefore) at = target.transform.position;
                float u = t / markTime;
                go.transform.position = at;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.1f, u) * (1f + 0.06f * Mathf.Sin(t * 30f));
                sr.color = new Color(0.12f, 0.03f, 0.18f, Mathf.Lerp(0.3f, 0.8f, u));
                yield return null;
            }

            // the hand bursts up
            var frames = SkillKit.Frames("VFX/specimen/hand_", 8);
            if (frames.Length > 0) p.StartCoroutine(SkillKit.PlayFrames(frames, at, handScale, 16f, handTint, layer, order + 2, UnityEngine.Random.value < 0.5f));
            yield return new WaitForSeconds(0.12f);
            int n = 0;
            foreach (var ai in SkillKit.EnemiesIn(at, hitRadius))
            {
                float dmg = SkillKit.IsBound(ai) ? damage * (1f + boundBonus) : damage;
                if (!ai.Health.TakeDamage(dmg, Vector2.up, 1.5f)) continue;
                SkillKit.HitFx(p, ai, Vector2.up);
                SkillKit.Control(ai, stun, 0f, 0f, layer, order);
                n++;
            }
            PlayerCombat.RaiseStruck(at, hitRadius);
            p.KickCamera(Vector2.up, 1f, 0.2f);
            if (n > 0) p.HitStopNow();
            for (float t = 0f; t < 0.35f && go != null; t += Time.deltaTime)
            {
                sr.color = new Color(0.12f, 0.03f, 0.18f, 0.8f * (1f - t / 0.35f));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // soft elliptical shadow 48x20 (drawn in code, PPU 100)
        private static Sprite MakeShadow()
        {
            const int w = 48, h = 20;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - (w - 1) * 0.5f) / (w * 0.5f), dy = (y - (h - 1) * 0.5f) / (h * 0.5f);
                    float r = dx * dx + dy * dy;
                    float a = r > 1f ? 0f : r > 0.7f ? 0.45f : r > 0.4f ? 0.75f : 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
