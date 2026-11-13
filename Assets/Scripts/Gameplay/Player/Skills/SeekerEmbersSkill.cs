using System;
using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Seeker Embers" — bộ Mantra Con Mắt, ô I: <see cref="maxEmbers"/> cầu Ether xanh bay quanh Ren trong <see cref="duration"/> giây.
    /// Cầu chạm quái: sát thương con đó + nổ nhỏ (sát thương + Burn cho quái xung quanh), rồi cầu mất.
    /// Dùng lại khi còn cầu: bù cho đủ <see cref="maxEmbers"/> và làm mới thời gian (không chồng thêm). Xử lý ở <see cref="EmberOrbit"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Seeker Embers", fileName = "EyeOrbs")]
    public class SeekerEmbersSkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Summon - Burn";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Embers", maxEmbers.ToString());
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Touch damage", hitDamage.ToString("0"));
            Stat(o, "Burst", blastDamage.ToString("0") + " in " + Tiles(blastRadius));
            Stat(o, "Burn", burnDps.ToString("0.#") + " /s for " + Secs(burnTime));
        }

        [Tooltip("Số cầu tối đa quanh người (không chồng).")]
        public int maxEmbers = 5;
        [Tooltip("Giây các cầu tồn tại.")]
        public float duration = 10f;
        [Tooltip("Bán kính vòng bay quanh chân (đơn vị Unity; 1 ô = 0,32).")]
        public float orbitRadius = 0.55f;
        [Tooltip("Độ / giây.")]
        public float spinSpeed = 200f;
        [Tooltip("Hình cầu vẽ cao hơn mặt đất bao nhiêu (cho ngang thân).")]
        public float lift = 0.2f;
        [Tooltip("Bán kính chạm quái của mỗi cầu.")]
        public float contactRadius = 0.15f;
        [Tooltip("Sát thương lên con quái bị cầu chạm.")]
        public float hitDamage = 8f;
        [Tooltip("Sát thương nổ lên mọi quái trong bán kính nổ (kể cả con bị chạm).")]
        public float blastDamage = 6f;
        public float blastRadius = 0.6f;
        public float burnDps = 4f;
        public float burnTime = 3f;
        [Tooltip("Giây đứng yên khi gọi cầu.")]
        public float castLock = 0.35f;
        [Tooltip("Giây từ lúc bấm tới khi cầu hiện ra (lúc tay giơ lên trong hoạt ảnh). Lăn trước lúc đó = huỷ, hoàn hồi chiêu.")]
        public float spawnDelay = 0.3f;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(castLock);
            if (p.PlayEmbersCast() && spawnDelay > 0f)
            {
                yield return new WaitForSeconds(spawnDelay);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            EmberOrbit.For(p).Refill(this);
        }
    }
}
