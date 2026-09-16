using UnityEngine;

namespace SiegeCore.Rat
{
    public enum RatForm
    {
        Basic = 1,
        BB = 2,
        BBB = 3
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

    [CreateAssetMenu(menuName = "SiegeCore/Rat Definition")]
    public sealed class RatDefinition : ScriptableObject
    {
        public RatForm Form = RatForm.Basic;

        [Header("Presentation")]
        public Sprite DeadSprite;

        [Header("Ground Combat")]
        [Min(1f)] public float Health = 10f;
        [Min(0f)] public float AttackDamage = 3f;
        [Min(0.1f)] public float AttackInterval = 1f;
        [Min(0.1f)] public float MoveSpeed = 2f;
        [Min(0.1f)] public float AttackRange = 0.9f;
        [Min(0.1f)] public float DetectionRadius = 4f;

        [Header("Cannon")]
        [Min(0f)] public float ProjectileDamage = 10f;

        public int BasicCount
        {
            get { return (int)Form; }
        }

        public bool CanBurst
        {
            get { return Form == RatForm.BBB; }
        }
    }
}
