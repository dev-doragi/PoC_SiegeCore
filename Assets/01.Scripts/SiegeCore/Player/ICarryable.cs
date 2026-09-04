using UnityEngine;

namespace SiegeCore.Player
{
    public interface ICarryable
    {
        Transform CarryTransform { get; }
        bool IsCarried { get; }
        bool CanBePickedUp { get; }

        bool TryPickUp(Transform carryPoint);
        void Drop(Vector3 worldPosition);
    }
}
