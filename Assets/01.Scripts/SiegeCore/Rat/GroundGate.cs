using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class GroundGate : MonoBehaviour
    {
        [SerializeField] private VehicleSide _faction;
        [SerializeField] private RatFactory _factory;
        [SerializeField] private Transform _exitPoint;

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            RatAgent rat =
                other.GetComponentInParent<RatAgent>();

            if (rat == null
                || rat.Faction != _faction
                || rat.State != RatState.Airborne
                || rat.Carryable.IsCannonFlight
                || _factory == null
                || _exitPoint == null)
            {
                return;
            }

            rat.EnterGroundCombat(
                _factory,
                _exitPoint.position);
        }
    }
}