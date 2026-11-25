using System;
using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Veil of the Ascended" — Mantras of the Eye set, slot II.
    /// An Ether veil lifts Ren for <see cref="duration"/> s (PixelLab sk_veil behind him): hits land at only <see cref="damageTaken"/> strength
    /// (Health.Guard — still hit, still flinches / gets pushed) and while the veil lasts Judgement Ray allows slow gliding
    /// (JudgementRaySkill.veiledMove). The Veil SUPPORTS the beam; it does not replace the roll. The veil blinks in its last half second.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Veil of the Ascended", fileName = "EyeVeil")]
    public class VeilSkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Ward";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Damage taken", Pct(damageTaken));
            Stat(o, "With Judgement Ray", "glide while firing");
        }

        public float duration = 4f;
        [Tooltip("Damage-taken multiplier while veiled (0.5 = half).")]
        [Range(0f, 1f)] public float damageTaken = 0.5f;
        [Tooltip("Fallback ring diameter (when the sk_veil image is missing) relative to the character height.")]
        public float ringScale = 1.3f;
        [Tooltip("Seconds standing still while joining hands (the veil takes effect on the press).")]
        public float castLock = 0.3f;
        public string vfx = "sk_veil";
        [Tooltip("Veil image height relative to Ren's body height.")]
        public float vfxScale = 1.25f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            p.Health.Guard(damageTaken, duration);
            p.BeginAction();
            if (p.PlayVeilCast()) p.LockActions(castLock);
            // measure Ren's body at cast time: veil centre = middle of the body, size from the sprite height
            var body = p.Sprite;
            Vector2 offset = new Vector2(0f, 0.25f);
            float size = 0.8f;
            if (body != null)
            {
                Bounds b = body.bounds;
                offset = (Vector2)(b.center - p.transform.position);
                size = Mathf.Max(b.size.y, b.size.x * 1.2f);
            }
            SpriteRenderer look;
            float baseScale;
            var mantle = p.showVfx ? SpriteVfx.Spawn(vfx, (Vector2)p.transform.position + offset, 1f, p.VfxLayer, p.PlayerOrder - 1, false, 12f) : null;
            if (mantle != null)
            {
                mantle.Looping(duration);
                look = mantle.GetComponent<SpriteRenderer>();
                baseScale = size * vfxScale / 0.75f;                     // veil image 75 px tall = 0.75 u
            }
            else
            {
                look = EtherVfx.Spawn("Ether Veil", EtherVfx.Ring, (Vector2)p.transform.position + offset, EtherVfx.Blue, p.VfxLayer, p.PlayerOrder + 1);
                baseScale = size * ringScale / 0.64f;                    // ring image 64 px = 0.64 u
            }
            float start = Time.time, end = start + duration;
            while (Time.time < end && look != null && !p.Health.IsDead)
            {
                look.transform.position = (Vector3)((Vector2)p.transform.position + offset);
                float left = end - Time.time;
                float a = Mathf.Clamp01((Time.time - start) / 0.15f);
                if (left < 0.6f) a *= 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 18f));
                Color c = mantle != null ? Color.white : EtherVfx.Blue; c.a = (mantle != null ? 0.9f : 0.8f) * a;
                look.color = c;
                look.transform.localScale = Vector3.one * baseScale * (1f + 0.03f * Mathf.Sin(Time.time * 6f));
                yield return null;
            }
            if (look != null) Destroy(look.gameObject);
        }
    }
}
