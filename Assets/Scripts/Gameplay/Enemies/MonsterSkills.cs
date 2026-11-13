using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Bộ chiêu riêng của một loại quái: EnemyAI (kind Custom / Beholder) gọi Act mỗi lượt khi đã gây hấn.</summary>
    public interface IEnemySkills
    {
        IEnumerator Act(EnemyAI ai);
    }
}
