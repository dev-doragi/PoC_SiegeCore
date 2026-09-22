using UnityEngine;

namespace SiegeCore.Combat
{
    public interface IGroundCombatTarget : IDamageable
    {
        Transform TargetTransform { get; }
        int AttackPriority { get; }
    }
}
