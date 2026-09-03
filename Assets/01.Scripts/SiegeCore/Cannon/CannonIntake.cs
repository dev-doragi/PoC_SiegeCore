using SiegeCore.Player;
using UnityEngine;

namespace SiegeCore.Cannon
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CannonIntake : MonoBehaviour
    {
        [SerializeField] private Cannon _cannon;

        private void Awake()
        {
            if (_cannon == null) _cannon = GetComponentInParent<Cannon>();
            if (_cannon == null || !GetComponent<Collider2D>().isTrigger)
            {
                Debug.LogError("[CannonIntake] Assign a Cannon and enable Is Trigger on the intake collider.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActiveAndEnabled || _cannon == null) return;
            CarryableObject item = other.GetComponentInParent<CarryableObject>();
            if (item == null || !item.IsAirborne || item.IsCannonFlight) return;
            _cannon.TryLoad(item);
        }
    }
}
