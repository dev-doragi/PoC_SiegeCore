using UnityEngine;

namespace SiegeCore.Rat
{
    /// <summary>One optional ground behaviour per Rat; RatAgent owns its lifecycle.</summary>
    public abstract class RatGroundBehaviour : MonoBehaviour
    {
        public abstract bool IsMoving { get; }
        public abstract Vector2 MoveDirection { get; }
        public abstract bool IsInfiltrated { get; }
        public abstract void ResetForSpawn();
        public abstract void OnRatStateChanged(RatState previousState, RatState nextState);
        public abstract void BeginArenaCombat();
        public abstract void BeginBaseInfiltration();
    }
}
