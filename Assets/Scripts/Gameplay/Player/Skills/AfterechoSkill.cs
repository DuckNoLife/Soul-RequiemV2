using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§252) "Afterecho" — bộ "Mantras of the Ear" (đôi tai thánh rơi từ Precentor Morrow), ô I.
    /// Bật chiêu: trong <see cref="duration"/> s mọi nhát chém kiếm được "ghi lại"; <see cref="echoDelay"/> s sau mỗi nhát, một bóng kiếm
    /// màu hymn (xanh băng của Morrow) chém lặp lại ĐÚNG chỗ đó với <see cref="echoMul"/> sát thương — Ren chém xong lăn đi, tiếng vọng vẫn đánh.
    /// Nghe sự kiện <see cref="PlayerCombat.MeleeSwung"/> (PlayerMelee bắn sau mỗi nhát, kể cả chém hụt). Tiếng vọng không tự vọng lại.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Afterecho", fileName = "EarAfterecho")]
    public class AfterechoSkill : SkillDefinition
    {
        public override string Kind => "Self - Echo";
        public override void Stats(List<KeyValuePair<string, string>> o)
        {
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Echo", Pct(echoMul) + " of each cut, " + Secs(echoDelay) + " later");
        }

        public float duration = 4f;
        public float echoDelay = 1f;
        [Tooltip("Sát thương tiếng vọng so với nhát gốc (0,5 = 50%).")]
        public float echoMul = 0.5f;
        public float castLock = 0.25f;
        [Tooltip("Màu bóng kiếm vọng (xanh băng hymn của Morrow).")]
        public Color echoTint = new Color(0.65f, 0.82f, 1f, 0.8f);

        private static readonly string[] Keys = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };
        private static readonly Dictionary<PlayerCombat, KeyValuePair<AfterechoSkill, float>> active = new Dictionary<PlayerCombat, KeyValuePair<AfterechoSkill, float>>();
        private static bool hooked;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            p.LockActions(castLock);
            p.PlayEmbersCast();
            if (!hooked) { PlayerCombat.MeleeSwung += OnSwung; hooked = true; }
            float end = Time.time + duration;
            active[p] = new KeyValuePair<AfterechoSkill, float>(this, end);
            if (p.showVfx)
            {
                var v = SpriteVfx.Spawn("m3_vespers", p.transform.position, 0.55f, p.VfxLayer, p.PlayerOrder + 1, false, 16f);
                if (v != null) v.Tint(echoTint);
            }
            p.StartCoroutine(SkillKit.TintWhile(p.Sprite, new Color(0.82f, 0.9f, 1f), () => p != null && Time.time < end));
            p.RaiseSkillCast(p.transform.position, 0.6f);
            yield break;
        }

        private static void OnSwung(PlayerCombat p, Vector2 pivot, Vector2 dir, float range, float dmg, float knock)
        {
            if (p == null || !active.TryGetValue(p, out var kv) || kv.Key == null || Time.time > kv.Value) return;
            p.StartCoroutine(kv.Key.Echo(p, pivot, dir, range, dmg * kv.Key.echoMul, knock * 0.5f));
        }

        private IEnumerator Echo(PlayerCombat p, Vector2 pivot, Vector2 d, float range, float dmg, float knock)
        {
            yield return new WaitForSeconds(echoDelay);
            if (p == null || p.Health == null || p.Health.IsDead) yield break;
            if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
            d.Normalize();
            if (p.showVfx)
            {
                float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                int i = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
                var v = SpriteVfx.Spawn("slash_" + Keys[i], pivot, p.vfxScale * Mathf.Max(0.3f, range / 0.75f), p.VfxLayer, p.PlayerOrder + 1, false, 20f);
                if (v != null) v.Tint(echoTint);
            }
            int n = p.HitEnemies(pivot + d * range * 0.5f, range * 0.6f, dmg, knock);
            if (n > 0) p.KickCamera(d, 0.4f, 0.1f);
        }
    }
}
