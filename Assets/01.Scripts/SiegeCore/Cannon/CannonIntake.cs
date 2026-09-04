using SiegeCore.Player;
using UnityEngine;

namespace SiegeCore.Cannon
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CannonIntake : MonoBehaviour
    {
        [SerializeField] private Cannon _cannon;
        private float _nextBlockedLogTime;

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
            TryLoad(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryLoad(other);
        }

        private void TryLoad(Collider2D other)
        {
            if (!isActiveAndEnabled || _cannon == null) return;
            CarryableObject item = other.GetComponentInParent<CarryableObject>();
            if (item == null || !item.CanEnterCannon) return;
            if (_cannon.TryLoad(item, out string reason))
            {
                Debug.Log($"[CannonIntake] Loaded {item.name}. Queue: {_cannon.LoadedCount}", this);
            }
            else if (Time.time >= _nextBlockedLogTime)
            {
                _nextBlockedLogTime = Time.time + 1f;
                Debug.LogWarning($"[CannonIntake] Overlapping {item.name}, loading blocked: {reason}", this);
            }
        }
    }
}
