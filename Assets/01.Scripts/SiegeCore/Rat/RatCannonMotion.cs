using System;
using DG.Tweening;
using SiegeCore.Cannon;
using SiegeCore.Player;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatCannonMotion
    {
        private readonly Transform _transform;
        private readonly Rigidbody2D _body;
        private readonly Collider2D _collider;
        private readonly CarryableObject _carryable;
        private readonly RatPresenter _presentation;
        private readonly RatAgent _rat;
        private readonly PhysicsMaterial2D _groundMaterial;

        private Tween _loadingTween;
        private Action _loadingCompleted;
        private Vector3 _loadingStartPosition;
        private Quaternion _loadingStartRotation;
        private float _flightTime;
        private float _flightDuration;
        private float _arcHeight;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;

        public RatCannonMotion(
            Transform transform,
            Rigidbody2D body,
            Collider2D collider,
            CarryableObject carryable,
            RatPresenter presentation,
            RatAgent rat,
            PhysicsMaterial2D groundMaterial)
        {
            _transform = transform;
            _body = body;
            _collider = collider;
            _carryable = carryable;
            _presentation = presentation;
            _rat = rat;
            _groundMaterial = groundMaterial;
        }

        public bool HasLoadingTween { get { return _loadingTween != null; } }
        public float Height { get; private set; }
        public CannonTrajectoryType TrajectoryType { get; private set; }

        public void Reset()
        {
            KillLoadingTween();
            _loadingCompleted = null;
            _flightTime = 0f;
            _flightDuration = 0f;
            _arcHeight = 0f;
            _startPosition = Vector2.zero;
            _targetPosition = Vector2.zero;
            Height = 0f;
            TrajectoryType = default;
        }

        public void BeginLoading(
            Transform storagePoint,
            float loadingDuration,
            Action loadingCompleted)
        {
            _carryable.RememberWorldParent();
            _loadingStartPosition = _transform.position;
            _loadingStartRotation = _transform.rotation;
            _loadingCompleted = loadingCompleted;

            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.simulated = false;
            _collider.enabled = false;

            _transform.SetParent(storagePoint, true);
            _loadingTween = _transform
                .DOLocalMove(Vector3.zero, Mathf.Max(0.01f, loadingDuration))
                .SetEase(Ease.InOutCubic)
                .OnComplete(CompleteLoading);

            _presentation.ResetVisualHeight();
            _presentation.SetCannonStorageVisibility(true);
        }

        public void CancelLoading()
        {
            KillLoadingTween();
            _loadingCompleted = null;
            _carryable.Detach();
            _transform.position = _loadingStartPosition;
            _transform.rotation = _loadingStartRotation;
            _body.position = _loadingStartPosition;
            _collider.enabled = true;
            PrepareGroundPhysics();
            _presentation.SetCannonStorageVisibility(true);
        }

        public void Launch(
            Vector3 muzzlePosition,
            Vector3 targetPosition,
            CannonTrajectoryType trajectoryType,
            float flightDuration,
            float arcHeight)
        {
            _carryable.Detach();
            _collider.enabled = true;
            _body.simulated = true;
            _presentation.SetCannonStorageVisibility(true);

            _transform.position = muzzlePosition;
            _body.position = muzzlePosition;
            TrajectoryType = trajectoryType;
            _flightDuration = Mathf.Max(0.01f, flightDuration);
            _arcHeight = Mathf.Max(0f, arcHeight);
            _flightTime = 0f;
            _startPosition = muzzlePosition;
            _targetPosition = targetPosition;
            Height = 0f;

            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = true;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _groundMaterial;
        }

        public bool UpdateFlight(float deltaTime)
        {
            _flightTime += deltaTime;
            float progress = Mathf.Clamp01(_flightTime / _flightDuration);
            Vector2 groundPosition = Vector2.Lerp(
                _startPosition,
                _targetPosition,
                progress);

            Height = 0f;
            if (TrajectoryType == CannonTrajectoryType.Arc)
            {
                Height = 4f * _arcHeight * progress * (1f - progress);
            }

            _body.MovePosition(groundPosition + Vector2.up * Height);
            if (progress < 1f)
            {
                return false;
            }

            _body.position = _targetPosition;
            return true;
        }

        private void CompleteLoading()
        {
            _loadingTween = null;
            if (_rat == null || _rat.State != RatState.CannonLoading)
            {
                _loadingCompleted = null;
                return;
            }

            _presentation.SetCannonStorageVisibility(false);

            Action completed = _loadingCompleted;
            _loadingCompleted = null;
            if (completed != null)
            {
                completed();
            }
        }

        private void KillLoadingTween()
        {
            if (_loadingTween == null)
            {
                return;
            }

            _loadingTween.Kill();
            _loadingTween = null;
        }

        private void PrepareGroundPhysics()
        {
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            _body.simulated = true;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _groundMaterial;
        }
    }
}
