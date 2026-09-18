using UnityEngine;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(PlayerMotor2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        private PlayerMotor2D _motor;

        public Vector2 MoveInput { get; private set; }
        public bool IsMoving => _motor != null && _motor.IsMoving;
        public bool IsKnockedBack => _motor != null && _motor.IsKnockedBack;
        public Vector2 Velocity => _motor != null ? _motor.Velocity : Vector2.zero;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
        }

        private void OnEnable()
        {
            if (EventBus.Instance == null)
            {
                Debug.LogError(
                    "[PlayerController] EventBus is not ready. Run the scene with the framework AppRoot.",
                    this
                );
                enabled = false;
                return;
            }

            EventBus.Instance.Subscribe<MoveInputEvent>(HandleMoveInput);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<MoveInputEvent>(HandleMoveInput);
            }

            MoveInput = Vector2.zero;

            if (_motor != null)
            {
                _motor.SetMoveInput(Vector2.zero);
            }
        }

        private void HandleMoveInput(MoveInputEvent inputEvent)
        {
            MoveInput = Vector2.ClampMagnitude(inputEvent.Value, 1f);
            _motor.SetMoveInput(MoveInput);

        }

        public void StopMovementFor(float duration)
        {
            if (_motor != null) _motor.StopMovementFor(duration);
        }

        public void SetActionMovementLocked(bool locked)
        {
            if (_motor != null)
            {
                _motor.SetActionMovementLocked(locked);
            }
        }

        public void SetActionMovementSpeedMultiplier(float multiplier)
        {
            if (_motor != null)
            {
                _motor.SetActionMovementSpeedMultiplier(multiplier);
            }
        }

        public void ApplyKnockback(Vector2 direction, float speed, float duration)
        {
            if (_motor != null)
            {
                _motor.ApplyKnockback(direction, speed, duration);
            }
        }

        public void ApplyForwardStep(Vector2 direction, float distance, float duration)
        {
            if (_motor != null)
            {
                _motor.ApplyForwardStep(direction, distance, duration);
            }
        }
    }
}
