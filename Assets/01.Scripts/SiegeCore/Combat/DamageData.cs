using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Combat
{
    public struct DamageData
    {
        public float Damage;
        public VehicleSide AttackerSide;
        public Vector2 HitPoint;
        public Vector2 KnockbackForce;
        public bool IsPiercing;
    }
}
