using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Earthshatter" — chiêu dậm đất hiện tại của Ren (trước đây viết cứng trong PlayerCombat.Skill):
    /// hoạt ảnh Stomp, chạm đất thì gây sát thương + đẩy lùi quanh người, VFX ground_impact, rung camera, dừng hình nếu trúng.
    /// Cơ quan trên map vẫn nghe sự kiện PlayerCombat.SkillCast (đốt Ichor...).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Ground Slam", fileName = "GroundSlam")]
    public class GroundSlamSkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Area - Melee";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Radius", Tiles(radius));
            Stat(o, "Knockback", knockback.ToString("0.#"));
            Stat(o, "Strikes after", Secs(impactTime));
            Stat(o, "Rooted for", Secs(lockTime));
        }

        public float damage = 40f;
        public float radius = 1.1f;
        public float knockback = 3f;
        [Tooltip("Giây từ lúc bấm tới khung chạm đất (khung 4 của hoạt ảnh Stomp).")]
        public float impactTime = 0.31f;
        [Tooltip("Giây đứng yên khi dậm.")]
        public float lockTime = 0.5f;
        public string vfx = "ground_impact";
        [Tooltip("Tỉ lệ VFX; 1.4 cho vòng sóng ~ bán kính 1.1.")]
        public float vfxScale = 1.4f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            if (p.PlaySkillAnimation())
            {
                p.LockActions(lockTime);
                yield return new WaitForSeconds(impactTime);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            Vector2 c = p.transform.position;
            int n = p.HitEnemies(c, radius, damage, knockback);
            p.RaiseSkillCast(c, radius);
            // §213: sóng xung kích toả ra từ MŨI KIẾM cắm đất (hoạt ảnh stomp khung 3), không phải từ giữa chân
            p.SpawnGroundVfx(vfx, c + BladeTip(p), vfxScale * radius / 1.1f, new Color(0.6f, 0.85f, 1f, 0.5f), radius);
            p.KickCamera(Vector2.down, 1.4f, 0.22f);
            if (n > 0) p.HitStopNow();
        }

        // §213: vị trí mũi kiếm cắm đất so với chân (px trên sprite stomp khung 3, đo tự động; +x phải, +y xuống) theo 8 hướng
        // thứ tự E, NE, N, NW, W, SW, S, SE (góc 0°, 45°, ... như PlayerMelee)
        private static readonly Vector2[] TipPx =
        {
            new Vector2(29, 2), new Vector2(18, -10), new Vector2(0, -8), new Vector2(-8, -7),
            new Vector2(-24, 2), new Vector2(-19, 6), new Vector2(5, 2), new Vector2(24, 5),
        };

        private static Vector2 BladeTip(PlayerCombat p)
        {
            Vector2 f = p.Facing.sqrMagnitude > 0.0001f ? p.Facing : Vector2.down;
            int i = ((Mathf.RoundToInt(Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg / 45f) % 8) + 8) % 8;
            var sr = p.GetComponentInChildren<SpriteRenderer>();
            float k = (sr != null ? Mathf.Abs(sr.transform.lossyScale.x) : 1f) / 100f;   // sprite Ren 100 px / đơn vị
            return new Vector2(TipPx[i].x, -TipPx[i].y) * k;
        }
    }
}
