using System;
using UnityEngine;

namespace SiegeCore.Rat
{
    public enum RatType
    {
        Basic,
        Bomber,
        Tank
    }

    public enum RatRank
    {
        Rank1 = 1,
        Rank2 = 2,
        Rank3 = 3
    }

    public enum RatWeightRank
    {
        Light,
        Medium,
        Heavy
    }

    public enum RatState
    {
        Idle,
        Carried,
        Airborne,
        Groggy,
        Loaded,
        CannonFlight,
        GroundCombat,
        Dead
    }

    public enum RatCondition
    {
        Normal,
        Groggy,
        Dead
    }

    public enum RatGroundMode
    {
        Idle,
        Combat
    }

    [Serializable]
    public struct GroundStats
    {
        [Min(1f)] public float Health;
        [Min(0f)] public float AttackDamage;
        [Min(0.1f)] public float AttackInterval;
        [Min(0.1f)] public float MoveSpeed;
        [Min(0.1f)] public float AttackRange;
        [Min(0.1f)] public float DetectionRadius;
        [Min(0)] public int BlockCapacity;
        [Min(1)] public int BlockRequired;
        public int AttackPriority;
    }

    [Serializable]
    public struct ProjectileStats
    {
        [Min(0f)] public float Damage;
        public RatWeightRank Weight;
    }

    [CreateAssetMenu(menuName = "SiegeCore/Rat Definition")]
    public sealed class RatDefinition : ScriptableObject
    {
        public RatType Type = RatType.Basic;
        public RatRank Rank = RatRank.Rank1;

        [Header("Presentation")]
        public Sprite DeadSprite;

        [Header("Ground Combat")]
        public GroundStats Ground = new GroundStats
        {
            Health = 10f, AttackDamage = 3f, AttackInterval = 1f,
            MoveSpeed = 2f, AttackRange = 0.9f, DetectionRadius = 4f,
            BlockCapacity = 1, BlockRequired = 1, AttackPriority = 0
        };

        [Header("Cannon")]
        public ProjectileStats Projectile = new ProjectileStats { Damage = 10f, Weight = RatWeightRank.Light };
        public ProjectileAbility ProjectileAbility;
        public GroundDeathAbility GroundDeathAbility;

        public float VerticalImpulseMultiplier
        {
            get
            {
                if (Projectile.Weight == RatWeightRank.Medium) return 0.95f;
                if (Projectile.Weight == RatWeightRank.Heavy) return 0.8f;
                return 1f;
            }
        }

    }
}
