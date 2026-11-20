using System;
using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Veil of the Ascended" — bộ Mantra Con Mắt, ô II.
    /// 08/11 (§191, gỡ trùng): trước đây bất tử 3 s (Health.Shield) — chồng vai cú lăn, và xoá cái giá "đứng yên" của Judgement Ray.
    /// Nay: màn Ether nâng Ren <see cref="duration"/> s (PixelLab sk_veil sau lưng): đòn trúng chỉ còn <see cref="damageTaken"/> sức
    /// (Health.Guard — vẫn bị đánh trúng, vẫn khựng / bị đẩy) và trong lúc có màn, Judgement Ray cho phép lướt chậm
    /// (JudgementRaySkill.veiledMove). Veil là chiêu HỖ TRỢ tia, không thay cú lăn. Nửa giây cuối màn nhấp nháy báo sắp hết.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Veil of the Ascended", fileName = "EyeVeil")]
    public class VeilSkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Ward";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Damage taken", Pct(damageTaken));
            Stat(o, "With Judgement Ray", "glide while firing");
        }

        public float duration = 4f;
        [Tooltip("Hệ số sát thương nhận khi có màn (0,5 = còn một nửa).")]
        [Range(0f, 1f)] public float damageTaken = 0.5f;
        [Tooltip("Đường kính vòng dự phòng (khi thiếu ảnh sk_veil) so với chiều cao nhân vật.")]
        public float ringScale = 1.3f;
        [Tooltip("Giây đứng yên khi chắp tay niệm (màn có hiệu lực ngay khi bấm).")]
        public float castLock = 0.3f;
        public string vfx = "sk_veil";
        [Tooltip("Chiều cao ảnh màn so với chiều cao thân Ren.")]
        public float vfxScale = 1.25f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            p.Health.Guard(damageTaken, duration);
            p.BeginAction();
            if (p.PlayVeilCast()) p.LockActions(castLock);
            // đo thân Ren lúc gọi: tâm màn = giữa thân, cỡ theo chiều cao hình
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
                baseScale = size * vfxScale / 0.75f;                     // ảnh màn cao 75 px = 0,75 đv
            }
            else
            {
                look = EtherVfx.Spawn("Ether Veil", EtherVfx.Ring, (Vector2)p.transform.position + offset, EtherVfx.Blue, p.VfxLayer, p.PlayerOrder + 1);
                baseScale = size * ringScale / 0.64f;                    // ảnh vòng 64 px = 0,64 đv
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
