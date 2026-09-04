using UnityEngine;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _maxSpeed = 6f;
        [SerializeField, Min(0f)] private float _acceleration = 45f;
        [SerializeField, Min(0f)] private float _deceleration = 55f;

        private Rigidbody2D _rigidbody;
        private Vector2 _moveInput;
        private float _movementLockedUntil;

        public Vector2 Velocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
        public bool IsMovementLocked => Time.time < _movementLockedUntil;
        public bool IsMoving => !IsMovementLocked && _moveInput.sqrMagnitude > 0.0001f;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
        }

        private void FixedUpdate()
        {
            if (IsMovementLocked)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 currentVelocity = _rigidbody.linearVelocity;
            Vector2 targetVelocity = _moveInput * _maxSpeed;

            bool hasMoveInput = _moveInput.sqrMagnitude > 0.0001f;
            float changeRate = hasMoveInput ? _acceleration : _deceleration;

            Vector2 nextVelocity = Vector2.MoveTowards(
                currentVelocity,
                targetVelocity,
                changeRate * Time.fixedDeltaTime
            );

            _rigidbody.linearVelocity = nextVelocity;
        }

        public void SetMoveInput(Vector2 input)
        {
            _moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void StopMovementFor(float duration)
        {
            _movementLockedUntil = Mathf.Max(_movementLockedUntil, Time.time + Mathf.Max(0f, duration));
            if (_rigidbody != null) _rigidbody.linearVelocity = Vector2.zero;
            // Retain the latest input so held movement keys resume after the action.
        }

        private void OnDisable()
        {
            _movementLockedUntil = 0f;
            _moveInput = Vector2.zero;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }
    }
}
