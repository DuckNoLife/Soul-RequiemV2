using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Sanctuary Bell" — "Mantras of the Ear" set, slot II (defence). A stained-glass dome with a golden bell on top rises where Ren stands
    /// (PixelLab sk_ear_bell_rise -> sk_ear_bell loop -> sk_ear_bell_shatter) and stays there for <see cref="duration"/> s, radius <see cref="radius"/> u:
    ///   • every hostile bullet entering it is REFLECTED back the way it came, <see cref="reflectMul"/> times stronger, hitting only enemies (BulletWard);
    ///   • while Ren stands inside, hits on him are cut by <see cref="damageCut"/> (Health.Guard, refreshed every frame).
    /// When it ends the dome shatters and shoves normal enemies inside it outward (bosses are not moved).
    /// Uses: BulletWard (Projectile / HallBullet / DuoBullet), Health.Guard.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Sanctuary Bell", fileName = "EarBell")]
    public class SanctuaryBellSkill : SkillDefinition
    {
        public override string Kind => "Area - Ward";
        public override void Stats(List<KeyValuePair<string, string>> o)
        {
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Radius", Tiles(radius));
            Stat(o, "Bullets", "reflected, x" + reflectMul.ToString("0.#") + " damage");
            Stat(o, "Inside", "-" + Pct(damageCut) + " damage taken");
        }

        public float duration = 5f;
        public float radius = 1.1f;
        public float reflectMul = 2f;
        [Tooltip("Share of incoming damage removed while Ren is inside (0.3 = -30%).")]
        public float damageCut = 0.3f;
        [Tooltip("Shove given to normal enemies inside when the dome shatters (u/s).")]
        public float shatterPush = 3f;
        public float castLock = 0.35f;
        [Tooltip("Dome art radius is 1.01 u at scale 1.")]
        public float vfxScalePerUnit = 0.99f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            p.LockActions(castLock);
            p.PlayVeilCast();
            Vector2 c = p.transform.position;
            int layer = p.VfxLayer, order = p.PlayerOrder - 1;
            float scale = radius * vfxScalePerUnit;
            SpriteVfx.Spawn("sk_ear_bell_rise", c, scale, layer, order, false, 16f, false);
            Sfx.Play("bell_toll_1", c, 0.8f);
            p.RaiseSkillCast(c, radius);
            float end = Time.time + duration;
            float loopAt = Time.time + 0.6f;
            SpriteVfx dome = null;
            var h = p.Health;
            float nextPing = 0f;
            while (Time.time < end && p != null && h != null && !h.IsDead)
            {
                if (dome == null && Time.time >= loopAt)
                {
                    dome = SpriteVfx.Spawn("sk_ear_bell", c, scale, layer, order, false, 12f, false);
                    if (dome != null) dome.Looping(end - Time.time);
                }
                BulletWard.Reflect(c, radius, reflectMul, at =>
                {
                    SpriteVfx.Spawn("sk_ear_bell_ping", at, 1f, layer, order + 4, false, 18f, false);
                    if (Time.time >= nextPing) { nextPing = Time.time + 0.08f; Sfx.Play("hymn_pop", at, 0.6f); }
                });
                if (((Vector2)p.transform.position - c).sqrMagnitude <= radius * radius) h.Guard(1f - damageCut, 0.12f);
                yield return null;
            }
            if (dome != null) dome.Stop();
            SpriteVfx.Spawn("sk_ear_bell_shatter", c, scale, layer, order, false, 16f, false);
            Sfx.Play("glass_break_1", c, 0.8f);
            foreach (var ai in SkillKit.EnemiesIn(c, radius))
            {
                if (ai.Health.ImmuneToCrowdControl) continue;
                Vector2 away = (Vector2)ai.transform.position - c;
                ai.Health.AddImpulse((away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up) * shatterPush);
            }
        }
    }
}
