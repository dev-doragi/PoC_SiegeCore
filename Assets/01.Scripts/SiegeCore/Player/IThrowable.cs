using UnityEngine;

namespace SiegeCore.Player
{
    public interface IThrowable : ICarryable
    {
        event System.Action<IThrowable> GroundSortingRequested;
        bool TryThrow(Vector2 direction, Vector3 groundPosition, Collider2D[] throwerColliders);
    }
}
