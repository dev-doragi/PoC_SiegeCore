using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Projectile
{
    public sealed class SiegeHealth : MonoBehaviour
    {
        [SerializeField] private VehicleSide _side;
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        public VehicleSide Side => _side;
        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDestroyed { get; private set; }

        private void Awake()
        {
            CurrentHealth = Mathf.Max(1f, _maxHealth);
        }

        public void TakeProjectileDamage(VehicleSide attackerSide, float damage, Vector3 hitPosition)
        {
            if (IsDestroyed || attackerSide == _side || damage <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            EventBus.Instance.Publish(new SiegeHealthChangedEvent
            {
                Siege = this,
                CurrentHealth = CurrentHealth,
                MaxHealth = MaxHealth
            });
            Debug.Log($"[SiegeHealth] {_side} Siege HP: {CurrentHealth:0.##}/{MaxHealth:0.##}", this);
            if (CurrentHealth > 0f) return;

            IsDestroyed = true;
            EventBus.Instance.Publish(new SiegeDestroyedEvent
            {
                Siege = this,
                WinningSide = attackerSide,
                HitPosition = hitPosition
            });
            string result = attackerSide == VehicleSide.Ally ? "Ally Victory" : "Ally Defeat";
            Debug.Log($"[SiegeHealth] {_side} Siege destroyed. {result}.", this);
        }
    }

    public struct SiegeHealthChangedEvent
    {
        public SiegeHealth Siege;
        public float CurrentHealth;
        public float MaxHealth;
    }

    public struct SiegeDestroyedEvent
    {
        public SiegeHealth Siege;
        public VehicleSide WinningSide;
        public Vector3 HitPosition;
    }
}
