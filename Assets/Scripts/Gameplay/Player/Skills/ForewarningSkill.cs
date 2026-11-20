using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§252) "Forewarning" — bộ "Mantras of the Ear", ô III. Ren nghe được đòn trước khi tới: mở thế đỡ <see cref="window"/> s
    /// (Health.ParryWindow — chỉ chặn đòn CẬN CHIẾN, đạn không chặn). Bị đánh trong lúc đó: đòn bị huỷ hoàn toàn, Ren biến mất trong
    /// vespers của Morrow, hiện ra SAU LƯNG kẻ đánh và chém một nhát chắc trúng = <see cref="strikeMul"/> × sát thương nhát 1 của kiếm
    /// (nhân cả nội tại kiếm). Bật sai nhịp: không có gì, hồi chiêu ngắn. Khác Counterstroke: thưởng việc đứng yên chịu đòn đúng nhịp.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Forewarning", fileName = "EarForewarning")]
    public class ForewarningSkill : SkillDefinition
    {
        public override string Kind => "Self - Counter";
        public override void Stats(List<KeyValuePair<string, string>> o)
        {
            Stat(o, "Window", Secs(window));
            Stat(o, "Counter", "x" + strikeMul.ToString("0.#") + " cut from behind");
        }

        public float window = 0.8f;
        public float strikeMul = 2f;
        [Tooltip("Hiện ra cách lưng kẻ đánh bấy nhiêu đv.")]
        public float behind = 0.42f;
        [Tooltip("Bất tử ngắn sau khi dịch chuyển.")]
        public float iframes = 0.35f;
        public float findRadius = 1.8f;
        public float castLock = 0.15f;
        public Color tint = new Color(0.65f, 0.82f, 1f, 1f);

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            p.LockActions(castLock);
            var h = p.Health;
            if (h == null) yield break;
            bool parried = false;
            Vector2 hitDir = Vector2.zero;
            Action<float, Vector2> on = (amount, dir) => { parried = true; hitDir = dir; };
            h.Parried += on;
            h.ParryWindow(window);
            int layer = p.VfxLayer, order = p.PlayerOrder;
            var ring = EtherVfx.Spawn("Forewarning", EtherVfx.Ring, p.transform.position, tint, layer, order - 1);
            float end = Time.time + window;
            while (Time.time < end && !parried && !h.IsDead)
            {
                if (ring != null)
                {
                    float k = 1f - (end - Time.time) / window;
                    ring.transform.position = p.transform.position;
                    ring.transform.localScale = new Vector3(1f, 0.6f, 1f) * Mathf.Lerp(0.9f, 0.55f, k);
                    ring.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.85f, 0.35f, k));
                }
                yield return null;
            }
            h.Parried -= on;
            if (ring != null) Destroy(ring.gameObject);
            if (!parried) { h.EndParry(); yield break; }

            // tìm kẻ vừa đánh: gần + nằm phía đòn đánh tới
            Vector2 me = p.transform.position;
            Vector2 src = hitDir.sqrMagnitude > 0.0001f ? -hitDir.normalized : Vector2.zero;
            EnemyAI atk = null;
            float best = float.MaxValue;
            foreach (var ai in SkillKit.EnemiesIn(me, findRadius))
            {
                Vector2 to = (Vector2)ai.transform.position - me;
                float score = to.magnitude - (src != Vector2.zero && to.sqrMagnitude > 0.0001f ? Vector2.Dot(to.normalized, src) * 0.8f : 0f);
                if (score < best) { best = score; atk = ai; }
            }
            h.Shield(iframes);
            if (atk == null) yield break;

            if (p.showVfx) { var v = SpriteVfx.Spawn("m3_vespers", me, 0.55f, layer, order + 1, false, 18f); if (v != null) v.Tint(tint); }
            Vector2 ap = atk.transform.position;
            Vector2 away = ap - me;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
            away.Normalize();
            Vector2 spot = SkillKit.FreeSpot(ap + away * behind, ap);
            var rb = p.GetComponent<Rigidbody2D>();
            p.transform.position = spot;
            if (rb != null) { rb.position = spot; rb.linearVelocity = Vector2.zero; }
            if (p.showVfx) { var v = SpriteVfx.Spawn("m3_vespers", spot, 0.55f, layer, order + 1, false, 18f); if (v != null) v.Tint(tint); }

            Vector2 dir = ap - spot;
            if (dir.sqrMagnitude < 0.0001f) dir = -away;
            p.SetFacing(dir);
            p.PlaySlashPose(dir);
            float baseDmg = p.Weapon != null && p.Weapon.combo != null && p.Weapon.combo.Length > 0 ? p.Weapon.combo[0].damage : 16f;
            float dmg = baseDmg * strikeMul * WeaponTraits.MeleeMul(p, false);
            if (atk.Health != null && atk.Health.TakeDamage(dmg, dir, 1.6f))
            {
                SkillKit.HitFx(p, atk, dir);
                WeaponTraits.AfterMeleeHit(p, dmg, 1);
                p.HitStopNow();
                p.KickCamera(dir.normalized, 1.2f, 0.2f);
            }
            PlayerCombat.RaiseStruck(spot + dir.normalized * 0.3f, 0.4f);
        }
    }
}
