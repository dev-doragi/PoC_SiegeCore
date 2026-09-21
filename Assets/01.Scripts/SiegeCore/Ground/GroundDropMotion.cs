using System;
using UnityEngine;

namespace SiegeCore.Ground
{
    /// <summary>
    /// Tilemap 바닥 위에서 오브젝트를 낙하시키는 공통 모션입니다.
    /// CarryableObject의 투척 낙하와 같은 방식으로 중력과 반발을 계산합니다.
    /// </summary>
    public sealed class GroundDropMotion
    {
        public const float DefaultGravity = 16f;
        public const float DefaultBounceRestitution = 0.45f;
        public const float DefaultMinimumBounceSpeed = 0.8f;

        private readonly Rigidbody2D _rigidbody;

        private Vector2 _landingPosition;
        private float _height;
        private float _verticalSpeed;
        private float _gravity;
        private float _bounceRestitution;
        private float _minimumBounceSpeed;
        private Action _onLanded;

        public bool IsDropping { get; private set; }

        public GroundDropMotion(Rigidbody2D rigidbody)
        {
            _rigidbody = rigidbody;
        }

        public void Begin(Vector2 landingPosition, Action onLanded)
        {
            if (_rigidbody == null)
            {
                return;
            }

            _landingPosition = landingPosition;
            _height = Mathf.Max(0f, _rigidbody.position.y - landingPosition.y);
            _verticalSpeed = 0f;
            _gravity = DefaultGravity;
            _bounceRestitution = DefaultBounceRestitution;
            _minimumBounceSpeed = DefaultMinimumBounceSpeed;
            _onLanded = onLanded;
            IsDropping = true;

            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = true;
            SetPosition(_height);
        }

        public void Tick(float deltaTime)
        {
            if (!IsDropping || _rigidbody == null)
            {
                return;
            }

            float remainingTime = Mathf.Max(0f, deltaTime);
            while (remainingTime > 0f && IsDropping)
            {
                float impactSpeed = Mathf.Sqrt(
                    _verticalSpeed * _verticalSpeed
                    + 2f * _gravity * _height);
                float impactTime = _gravity <= 0.001f
                    ? (_verticalSpeed > 0.001f
                        ? _height / _verticalSpeed
                        : 0f)
                    : (_verticalSpeed + impactSpeed) / _gravity;

                if (impactTime > remainingTime)
                {
                    _height = Mathf.Max(
                        0f,
                        _height
                        - _verticalSpeed * remainingTime
                        - 0.5f * _gravity * remainingTime * remainingTime);
                    _verticalSpeed += _gravity * remainingTime;
                    remainingTime = 0f;
                    SetPosition(_height);
                    continue;
                }

                remainingTime -= impactTime;
                _height = 0f;
                SetPosition(0f);

                float bounceSpeed = impactSpeed * _bounceRestitution;
                if (bounceSpeed < _minimumBounceSpeed)
                {
                    Complete();
                    continue;
                }

                _verticalSpeed = bounceSpeed;
            }
        }

        public void Cancel()
        {
            IsDropping = false;
            _onLanded = null;
        }

        private void SetPosition(float height)
        {
            Vector2 position = new Vector2(
                _landingPosition.x,
                _landingPosition.y + height);
            _rigidbody.position = position;
        }

        private void Complete()
        {
            IsDropping = false;
            _verticalSpeed = 0f;
            SetPosition(0f);

            Action onLanded = _onLanded;
            _onLanded = null;
            if (onLanded != null)
            {
                onLanded();
            }
        }
    }
}
