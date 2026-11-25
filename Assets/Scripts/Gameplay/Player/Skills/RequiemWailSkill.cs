using System;
using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Requiem Wail" — "Mantras of the Vessel" set, slot III (ultimate). Ren channels with joined hands for <see cref="channel"/> s
    /// (violet glow, standing still — a Roll cancels and refunds the cooldown); a violet soul vortex (PixelLab sk_wail_pull) closes in on Ren's feet while channelling.
    /// Done: every enemy within <see cref="radius"/> u is PULLED to Ren's feet (stopping <see cref="pullTo"/> u away) + <see cref="damage"/> +
    /// SPENT (+<see cref="spentBonus"/> damage taken for <see cref="spentTime"/> s — bosses too). Ren heals <see cref="healPerHit"/> HP
    /// per enemy hit (max <see cref="healMax"/>). Walls block it. Bosses (CC-immune) are not pulled but still become Spent.
    /// It GATHERS enemies (Earthshatter is the push-away skill): the Vessel set plays as "gather (Wail) — root (Roots) — crush (Hand)";
    /// in exchange there is no shield, pulling enemies into melee is a risk.
    /// Balance: 45 AoE + 30% × 48 DPS × 4 s ≈ 103 on a boss every 30 s; normal enemies are pulled into the 140° slash arc.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Requiem Wail", fileName = "VesselWail")]
    public class RequiemWailSkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Area - Pull";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Radius", Tiles(radius));
            Stat(o, "Spent", "+" + Pct(spentBonus) + " damage taken for " + Secs(spentTime));
            Stat(o, "Heal", healPerHit.ToString("0") + " per foe (max " + healMax.ToString("0") + ")");
        }

        public float channel = 0.8f;
        [Tooltip("Gather + damage radius (units).")]
        public float radius = 2f;
        [Tooltip("Enemies are pulled to about this distance from Ren's feet (units).")]
        public float pullTo = 0.45f;
        public float damage = 45f;
        public float spentBonus = 0.3f;
        public float spentTime = 4f;
        public float healPerHit = 3f;
        public float healMax = 15f;
        public string vfx = "sk_wail_pull";
        [Tooltip("Vortex image scale (outer ring ~1.8 u at scale 1) — the default matches radius.")]
        public float vfxScale = 1.1f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.PlayVeilCast();
            float end = Time.time + channel;
            Vector2 feet = p.transform.position;
            // the vortex closes exactly when the channel ends (11 frames spread over the channel time)
            SpriteVfx swirl = p.showVfx ? SpriteVfx.Spawn(vfx, feet, vfxScale, p.VfxLayer, p.PlayerOrder - 1, false, 11f / Mathf.Max(0.2f, channel)) : null;
            p.StartCoroutine(SkillKit.TintWhile(p.Sprite, SkillKit.SpentTint, () => Time.time < end && p.ActionAlive(id)));
            float nextShake = 0f;
            while (Time.time < end)
            {
                p.LockActions(0.1f);
                if (!p.ActionAlive(id)) { if (swirl != null) swirl.Stop(); cancel?.Invoke(); yield break; }
                if (Time.time >= nextShake) { nextShake = Time.time + 0.2f; p.KickCamera(UnityEngine.Random.insideUnitCircle.normalized, 0.25f, 0.08f); }
                yield return null;
            }
            p.LockActions(0.25f);
            feet = p.transform.position;
            int n = 0;
            foreach (var ai in SkillKit.EnemiesIn(feet, radius, feet))
            {
                Vector2 toward = feet - (Vector2)ai.transform.position;
                if (!ai.Health.TakeDamage(damage, toward, 0f)) continue;
                SkillKit.HitFx(p, ai, toward);
                SkillKit.Vulnerable(ai, spentBonus, spentTime, SkillKit.SpentTint, p.VfxLayer, p.PlayerOrder);
                SkillKit.Pull(ai, feet, pullTo);
                n++;
            }
            PlayerCombat.RaiseStruck(feet, radius);
            float heal = Mathf.Min(healMax, n * healPerHit);
            if (heal > 0f)
            {
                p.Health.Heal(heal);
                if (p.showVfx) SpriteVfx.Spawn("heal_front", feet, 0.6f, p.VfxLayer, p.PlayerOrder + 1, false, 14f)?.Follow(p.transform);
            }
            p.KickCamera(Vector2.down, 1.6f, 0.3f);
            if (n > 0) p.HitStopNow();
        }
    }
}
