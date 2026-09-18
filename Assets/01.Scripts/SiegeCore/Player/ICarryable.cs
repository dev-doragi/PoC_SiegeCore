using UnityEngine;

namespace SiegeCore.Player
{
    public interface ICarryable
    {
        Transform CarryTransform { get; }
        bool IsCarried { get; }

        void Drop(Vector3 worldPosition);
    }
}
