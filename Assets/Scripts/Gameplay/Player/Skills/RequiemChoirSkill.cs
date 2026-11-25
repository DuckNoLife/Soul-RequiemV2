using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Requiem Choir" — "Mantras of the Ear" set, slot III (the strongest). Three ghost choristers (PixelLab sk_ear_singer) rise around the aim point
    /// (≤ <see cref="range"/> u) and sing for <see cref="singTime"/> s; Ren is free to move meanwhile:
    ///   • every <see cref="pillarEvery"/> s a stained-glass hymn pillar (sk_ear_pillar) slams onto a random enemy inside the zone
    ///     (radius <see cref="zoneRadius"/>): <see cref="pillarDamage"/> to enemies within <see cref="pillarRadius"/> u of the strike;
    ///   • then the chorus closes with a rose-window burst (sk_ear_finale): <see cref="finaleDamage"/> to every enemy in the zone + stun
    ///     <see cref="finaleStun"/> s (bosses are not stunned).
    /// Balance: 6 pillars x 16 + 60 ≈ 156 on one enemy every 24 s; spread over a pack the pillars split, the finale does not.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Requiem Choir", fileName = "EarChoir")]
    public class RequiemChoirSkill : SkillDefinition
    {
        public override string Kind => "Area - Summon";
        public override void Stats(List<KeyValuePair<string, string>> o)
        {
            Stat(o, "Pillars", Mathf.FloorToInt(singTime / pillarEvery + 0.001f) + " x " + pillarDamage.ToString("0"));
            Stat(o, "Finale", finaleDamage.ToString("0") + " + stun " + Secs(finaleStun));
            Stat(o, "Zone", Tiles(zoneRadius) + ", cast up to " + Tiles(range));
        }

        public float range = 3.2f;
        public float zoneRadius = 1.8f;
        public float singTime = 3f;
        public float pillarEvery = 0.5f;
        public float pillarDamage = 16f;
        public float pillarRadius = 0.4f;
        public float finaleDamage = 60f;
        public float finaleStun = 0.6f;
        public float castLock = 0.4f;
        [Tooltip("Choristers stand on this ring around the zone centre (units, flattened for the top-down view).")]
        public float singerRing = 0.95f;
        public float singerScale = 1.2f;
        public float pillarScale = 1.1f;
        [Tooltip("Outer ring of the finale art is 1.72 u at scale 1.")]
        public float finaleArtRadius = 1.72f;

        private const float PillarImpact = 0.13f;     // pillar art lands on frame 2 at 16 fps
        private const float FinaleImpact = 0.34f;     // finale burst is frame 6 of 16 at 16 fps

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(castLock);
            p.PlayVeilCast();
            Vector2 c = SkillKit.AimPoint(p, range);
            Vector2 feet = p.transform.position;
            if ((c - feet).sqrMagnitude > 0.0001f) p.SetFacing(c - feet);
            float settle = Time.time + castLock * 0.6f;
            while (Time.time < settle)
            {
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
                yield return null;
            }
            p.StartCoroutine(Sing(p, c));
            p.RaiseSkillCast(c, zoneRadius);
        }

        private IEnumerator Sing(PlayerCombat p, Vector2 c)
        {
            int layer = p.VfxLayer, order = p.PlayerOrder;
            var singers = new List<SpriteVfx>();
            for (int i = 0; i < 3; i++)
            {
                float a = (-90f + i * 120f) * Mathf.Deg2Rad;
                Vector2 at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * singerRing;
                var s = SpriteVfx.Spawn("sk_ear_singer", at, singerScale, layer, order + (at.y < c.y ? 1 : -1), false, 10f, false);
                if (s != null) { s.Looping(singTime + FinaleImpact + 0.6f); s.Tint(new Color(1f, 1f, 1f, 0f)); singers.Add(s); }
            }
            Sfx.Play("bell_toll_1", c, 0.7f);
            float start = Time.time, nextPillar = start + pillarEvery;
            while (Time.time < start + singTime)
            {
                Fade(singers, Mathf.Clamp01((Time.time - start) / 0.4f));
                if (Time.time >= nextPillar)
                {
                    nextPillar += pillarEvery;
                    p.StartCoroutine(Pillar(p, PickTarget(c), layer, order));
                }
                yield return null;
            }
            SpriteVfx.Spawn("sk_ear_finale", c, zoneRadius / finaleArtRadius, layer, order - 2, false, 16f, false);
            Sfx.Play("bell_toll_2", c, 0.9f);
            float burstAt = Time.time + FinaleImpact;
            while (Time.time < burstAt) yield return null;
            Sfx.Play("shockwave", c, 0.8f);
            int n = 0;
            foreach (var ai in SkillKit.EnemiesIn(c, zoneRadius, c))
            {
                Vector2 away = (Vector2)ai.transform.position - c;
                if (!ai.Health.TakeDamage(finaleDamage, away, 1.4f)) continue;
                SkillKit.Control(ai, finaleStun, 0f, 0f, layer, order);
                if (p != null) SkillKit.HitFx(p, ai, away);
                n++;
            }
            PlayerCombat.RaiseStruck(c, zoneRadius);
            if (p != null) { p.KickCamera(Vector2.down, n > 0 ? 1.6f : 0.8f, 0.3f); if (n > 0) p.HitStopNow(); }
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { Fade(singers, 1f - t / 0.5f); yield return null; }
            foreach (var s in singers) if (s != null) s.Stop();
        }

        /// <summary>A random enemy inside the zone (in sight of its centre); none -> a random spot in the zone.</summary>
        private Vector2 PickTarget(Vector2 c)
        {
            var list = SkillKit.EnemiesIn(c, zoneRadius, c);
            if (list.Count > 0) return list[UnityEngine.Random.Range(0, list.Count)].transform.position;
            Vector2 r = UnityEngine.Random.insideUnitCircle * zoneRadius * 0.8f;
            return c + new Vector2(r.x, r.y * 0.6f);
        }

        private IEnumerator Pillar(PlayerCombat p, Vector2 at, int layer, int order)
        {
            SpriteVfx.Spawn("sk_ear_pillar", at, pillarScale, layer, order + 3, false, 16f, false);
            Sfx.Play("holy_beam", at, 0.5f);
            float hitAt = Time.time + PillarImpact;
            while (Time.time < hitAt) yield return null;
            foreach (var ai in SkillKit.EnemiesIn(at, pillarRadius))
            {
                if (!ai.Health.TakeDamage(pillarDamage, Vector2.down, 0.4f)) continue;
                if (p != null) SkillKit.HitFx(p, ai, Vector2.down);
            }
        }

        private static void Fade(List<SpriteVfx> singers, float a)
        {
            foreach (var s in singers) if (s != null) s.Tint(new Color(1f, 1f, 1f, a));
        }
    }
}
