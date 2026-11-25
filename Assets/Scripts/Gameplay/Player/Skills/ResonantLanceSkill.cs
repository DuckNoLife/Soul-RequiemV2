using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Resonant Lance" — "Mantras of the Ear" set (the holy ears dropped by Precentor Morrow), slot I.
    /// Ren cuts toward the aim and flings a crescent of sound (PixelLab sk_ear_lance, stained-glass rainbow) that flies <see cref="range"/> u
    /// at <see cref="speed"/> u/s, PIERCING every enemy on its path (<see cref="damage"/> each, once per enemy). Where it stops (range or a wall)
    /// it bursts into rainbow rings (sk_ear_lance_burst): <see cref="burstDamage"/> to every enemy within <see cref="burstRadius"/> u.
    /// Balance: 44 on the line + 30 in the burst every 10 s; the burst lets it hit a pack from a safe distance.
    /// Uses: SkillKit (aim, wall distance, hit FX), PlayerCombat.RaiseStruck (map mechanisms react to the burst).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Resonant Lance", fileName = "EarLance")]
    public class ResonantLanceSkill : SkillDefinition
    {
        public override string Kind => "Projectile - Pierce";
        public override void Stats(List<KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0") + " (pierces)");
            Stat(o, "Burst", burstDamage.ToString("0") + " in " + Tiles(burstRadius));
            Stat(o, "Range", Tiles(range));
        }

        public float damage = 44f;
        public float burstDamage = 30f;
        [Tooltip("Flight distance (units) before the crescent bursts.")]
        public float range = 2.4f;
        public float speed = 7f;
        [Tooltip("Half-height of the crescent: enemies within this distance of its path are hit.")]
        public float hitRadius = 0.32f;
        public float burstRadius = 0.9f;
        public float knockback = 1.2f;
        [Tooltip("Wind-up before the crescent leaves the blade.")]
        public float windup = 0.12f;
        public float castLock = 0.3f;
        [Tooltip("Scale of the flying crescent sprite (72x48 px art).")]
        public float lanceScale = 1.2f;
        [Tooltip("Scale of the burst (outer ring 0.8 u at scale 1).")]
        public float burstScale = 1.15f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(castLock);
            Vector2 dir = SkillKit.AimDir(p, p.Facing);
            p.SetFacing(dir);
            p.PlaySlashPose(dir);
            Sfx.Play("bell_toll_2", p.transform.position, 0.45f);
            float wind = Time.time + windup;
            while (Time.time < wind)
            {
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
                yield return null;
            }
            Vector2 from = (Vector2)p.transform.position + Vector2.up * 0.25f + dir * 0.3f;
            float len = SkillKit.WallDistance(from, dir, range, 0.12f);
            p.StartCoroutine(Fly(p, from, dir, len));
            p.RaiseSkillCast(from, 0.5f);
        }

        private IEnumerator Fly(PlayerCombat p, Vector2 from, Vector2 dir, float len)
        {
            int layer = p.VfxLayer, order = p.PlayerOrder + 2;
            var body = SpriteVfx.Spawn("sk_ear_lance", from, lanceScale, layer, order, false, 14f, false);
            if (body != null)
            {
                body.Looping(len / speed + 0.5f);
                body.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
            var hit = new HashSet<EnemyAI>();
            float travelled = 0f;
            Vector2 pos = from;
            while (travelled < len)
            {
                float step = Mathf.Min(speed * Time.deltaTime, len - travelled);
                travelled += step;
                pos = from + dir * travelled;
                if (body != null) body.transform.position = pos;
                foreach (var ai in SkillKit.EnemiesIn(pos, hitRadius))
                {
                    if (!hit.Add(ai)) continue;
                    if (p != null && ai.Health.TakeDamage(damage, dir, knockback)) SkillKit.HitFx(p, ai, dir);
                }
                yield return null;
            }
            if (body != null) body.Stop();
            Burst(p, pos, dir, layer, order);
        }

        private void Burst(PlayerCombat p, Vector2 at, Vector2 dir, int layer, int order)
        {
            SpriteVfx.Spawn("sk_ear_lance_burst", at, burstScale, layer, order, false, 16f, false);
            Sfx.Play("hymn_pop", at, 0.9f);
            Sfx.Play("shockwave", at, 0.5f);
            int n = 0;
            foreach (var ai in SkillKit.EnemiesIn(at, burstRadius, at))
            {
                Vector2 away = (Vector2)ai.transform.position - at;
                if (away.sqrMagnitude < 0.0001f) away = dir;
                if (!ai.Health.TakeDamage(burstDamage, away, knockback)) continue;
                if (p != null) SkillKit.HitFx(p, ai, away);
                n++;
            }
            PlayerCombat.RaiseStruck(at, burstRadius);
            if (p == null) return;
            p.KickCamera(dir, n > 0 ? 1f : 0.5f, 0.18f);
            if (n > 0) p.HitStopNow();
        }
    }
}
