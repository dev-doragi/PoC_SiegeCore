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
        private bool _actionMovementLocked;
        private float _actionMovementSpeedMultiplier = 1f;
        private Vector2 _knockbackVelocity = Vector2.zero;
        private float _knockbackUntil;

        public Vector2 Velocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
        public bool IsMovementLocked => _actionMovementLocked || Time.time < _movementLockedUntil;
        public bool IsKnockedBack => Time.time < _knockbackUntil;
        public bool IsMoving => !IsMovementLocked && _moveInput.sqrMagnitude > 0.0001f;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
        }

        private void FixedUpdate()
        {
            if (IsKnockedBack)
            {
                _rigidbody.linearVelocity = _knockbackVelocity;
                return;
            }

            if (IsMovementLocked)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 currentVelocity = _rigidbody.linearVelocity;
            float movementMultiplier = Mathf.Clamp01(_actionMovementSpeedMultiplier);
            Vector2 targetVelocity = _moveInput * _maxSpeed * movementMultiplier;

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

        /// <summary>차지처럼 종료 시점이 정해지지 않은 행동이 이동을 잠글 때 사용한다.</summary>
        public void SetActionMovementLocked(bool locked)
        {
            _actionMovementLocked = locked;
            if (locked && _rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>차지처럼 이동은 허용하면서 속도만 제한하는 행동에 사용한다.</summary>
        public void SetActionMovementSpeedMultiplier(float multiplier)
        {
            _actionMovementSpeedMultiplier = Mathf.Clamp01(multiplier);
        }

        /// <summary>지정 시간 동안 입력 이동보다 우선하는 플레이어 반동을 적용한다.</summary>
        public void ApplyKnockback(Vector2 direction, float speed, float duration)
        {
            if (direction.sqrMagnitude < 0.0001f || speed <= 0f || duration <= 0f)
            {
                return;
            }

            _knockbackVelocity = direction.normalized * speed;
            _knockbackUntil = Time.time + duration;
            _rigidbody.linearVelocity = _knockbackVelocity;
        }

        private void OnDisable()
        {
            _movementLockedUntil = 0f;
            _actionMovementLocked = false;
            _actionMovementSpeedMultiplier = 1f;
            _knockbackUntil = 0f;
            _knockbackVelocity = Vector2.zero;
            _moveInput = Vector2.zero;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }
    }
}
