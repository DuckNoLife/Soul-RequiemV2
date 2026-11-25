using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Skill set of one enemy type: EnemyAI (kind Custom / Beholder) calls Act every tick once the enemy is aggressive.</summary>
    public interface IEnemySkills
    {
        IEnumerator Act(EnemyAI ai);
    }
}
