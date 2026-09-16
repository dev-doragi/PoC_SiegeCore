using System;
using DG.Tweening;
using SiegeCore.Cannon;
using SiegeCore.Rat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class CarryableObject : MonoBehaviour, IThrowable
    {
        [Header("Landing")]
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField, Min(0f)] private float _snapSteering = 12f;
        [SerializeField, Min(0.01f)] private float _snapFinishDuration = 0.12f;

        [Header("Presentation")]
        [SerializeField] private Transform _visual;
        [SerializeField] private SpriteRenderer _shadow;

        [Header("Throw")]
        [SerializeField, Min(0f)] private float _throwSpeed = 5.5f;
        [SerializeField, Min(0f)] private float _upwardSpeed = 4f;
        [SerializeField, Min(0.01f)] private float _heightGravity = 16f;
        [SerializeField, Range(0f, 0.9f)] private float _bounceRestitution = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _groundSpeedRetention = 0.65f;
        [SerializeField, Min(0.01f)] private float _minimumBounceSpeed = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _wallRestitution = 0.5f;

        [Header("Cannon")]
        [SerializeField, Min(0.01f)] private float _defaultCannonFlightDuration = 3f;
        [SerializeField, Min(0f)] private float _defaultCannonArcHeight = 2f;

        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private RatAgent _rat;

        private Transform _worldParent;

        private Vector3 _visualRestPosition;
        private Vector3 _shadowRestScale;

        private PhysicsMaterial2D _flightMaterial;
        private PhysicsMaterial2D _originalMaterial;

        private Collider2D[] _ignoredColliders;

        private float _height;
        private float _verticalSpeed;

        private Vector2 _snapTarget;
        private bool _hasSnapTarget;
        private bool _isSnapping;
        private Tween _snapTween;

        private float _cannonFlightTime;
        private float _cannonFlightDuration;
        private float _cannonArcHeight;

        private Vector2 _cannonStartPosition;
        private Vector2 _cannonTargetPosition;

        public event Action StateChanged;
        public event Action<IThrowable> GroundSortingRequested;

        public bool IsCarried { get; private set; }
        public bool IsAirborne { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsCannonFlight { get; private set; }

        public CannonTrajectoryType TrajectoryType { get; private set; }

        public float Height
        {
            get { return _height; }
        }

        public Transform CarryTransform
        {
            get { return transform; }
        }

        public bool CanBePickedUp
        {
            get
            {
                if (!isActiveAndEnabled
                    || IsCarried
                    || IsAirborne
                    || IsLoaded
                    || IsCannonFlight)
                {
                    return false;
                }

                if (_rat != null)
                {
                    return _rat.CanPickUp;
                }

                return true;
            }
        }

        public bool CanEnterCannon
        {
            get
            {
                return isActiveAndEnabled
                    && IsAirborne
                    && !IsCarried
                    && !IsLoaded
                    && !IsCannonFlight;
            }
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _rat = GetComponent<RatAgent>();

            _worldParent = transform.parent;

            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;

            _originalMaterial = _collider.sharedMaterial;

            _flightMaterial = new PhysicsMaterial2D("Carryable Flight")
            {
                bounciness = _wallRestitution,
                friction = 0f
            };

            if (_visual == null || _visual == transform)
            {
                Debug.LogError(
                    "[CarryableObject] Assign a separate Visual child.",
                    this);

                enabled = false;
                return;
            }

            _visualRestPosition = _visual.localPosition;

            if (_shadow != null)
            {
                _shadowRestScale = _shadow.transform.localScale;
            }

            CompleteSettle(false);
        }

        private void FixedUpdate()
        {
            if (!IsAirborne || _isSnapping)
            {
                return;
            }

            if (IsCannonFlight)
            {
                UpdateCannonFlight();
                return;
            }

            UpdateThrowFlight();
        }

        private void LateUpdate()
        {
            UpdateHeightPresentation();
        }

        // --------------------------------------------------------------------
        // Reset
        // --------------------------------------------------------------------

        public void ResetForRat(Tilemap groundTilemap)
        {
            CancelSnap();
            RestoreThrowerCollisions();

            _groundTilemap = groundTilemap;
            _worldParent = transform.parent;

            IsCarried = false;
            IsAirborne = false;
            IsLoaded = false;
            IsCannonFlight = false;

            _height = 0f;
            _verticalSpeed = 0f;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.simulated = true;

            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;

            ResetVisualHeight();

            NotifyStateChanged();
        }

        // --------------------------------------------------------------------
        // Pickup
        // --------------------------------------------------------------------

        public bool TryPickUp(Transform holdPoint)
        {
            if (!CanBePickedUp
                || holdPoint == null
                || holdPoint.IsChildOf(transform))
            {
                return false;
            }

            CancelSnap();

            _worldParent = transform.parent;

            IsCarried = true;
            IsAirborne = false;
            IsLoaded = false;
            IsCannonFlight = false;

            _height = 0f;
            _verticalSpeed = 0f;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.simulated = false;

            transform.SetParent(holdPoint, true);
            transform.localPosition = Vector3.zero;

            ResetVisualHeight();
            NotifyStateChanged();

            return true;
        }

        // --------------------------------------------------------------------
        // Player Throw
        // --------------------------------------------------------------------

        public bool TryThrow(
            Vector2 direction,
            Vector3 groundPosition,
            Collider2D[] throwerColliders)
        {
            if (!IsCarried
                || direction.sqrMagnitude < 0.0001f
                || !isActiveAndEnabled)
            {
                return false;
            }

            float initialHeight =
                Mathf.Max(0f, transform.position.y - groundPosition.y);

            transform.SetParent(_worldParent, true);
            transform.position = groundPosition;
            _rigidbody.position = groundPosition;

            BeginThrow(
                direction.normalized,
                initialHeight,
                _upwardSpeed,
                throwerColliders);

            return true;
        }

        public bool TryDispense(
            Tilemap groundTilemap,
            Vector2 direction,
            float upwardSpeed)
        {
            if (!isActiveAndEnabled
                || IsCarried
                || IsLoaded
                || IsCannonFlight
                || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            _groundTilemap = groundTilemap;

            BeginThrow(
                direction.normalized,
                0f,
                upwardSpeed,
                null);

            return true;
        }

        public void BeginRatFall(
            Tilemap groundTilemap,
            float initialHeight)
        {
            CancelSnap();

            _groundTilemap = groundTilemap;

            IsCarried = false;
            IsLoaded = false;
            IsCannonFlight = false;
            IsAirborne = true;

            _height = Mathf.Max(0f, initialHeight);
            _verticalSpeed = 0f;

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = Vector2.zero;

            _collider.isTrigger = false;
            _collider.sharedMaterial = _flightMaterial;

            NotifyStateChanged();
        }

        private void BeginThrow(
            Vector2 direction,
            float initialHeight,
            float upwardSpeed,
            Collider2D[] ignoredColliders)
        {
            CancelSnap();

            IsCarried = false;
            IsLoaded = false;
            IsCannonFlight = false;
            IsAirborne = true;

            _height = initialHeight;
            _verticalSpeed = Mathf.Max(0f, upwardSpeed);

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.linearDamping = 0f;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity =
                direction * _throwSpeed;

            _collider.isTrigger = false;
            _collider.sharedMaterial = _flightMaterial;

            IgnoreThrowerCollisions(ignoredColliders);

            NotifyStateChanged();
        }

        // --------------------------------------------------------------------
        // Bounce
        // --------------------------------------------------------------------

        private void UpdateThrowFlight()
        {
            float remainingTime = Time.fixedDeltaTime;
            float gravity = Mathf.Max(0.01f, _heightGravity);

            while (remainingTime > 0f
                   && IsAirborne
                   && !_isSnapping)
            {
                float impactSpeed = Mathf.Sqrt(
                    _verticalSpeed * _verticalSpeed
                    + 2f * gravity * _height);

                float impactTime =
                    (_verticalSpeed + impactSpeed) / gravity;

                if (impactTime > remainingTime)
                {
                    _height +=
                        _verticalSpeed * remainingTime
                        - 0.5f
                        * gravity
                        * remainingTime
                        * remainingTime;

                    _verticalSpeed -= gravity * remainingTime;
                    break;
                }

                remainingTime -= impactTime;

                _height = 0f;

                _verticalSpeed =
                    impactSpeed
                    * Mathf.Clamp(
                        _bounceRestitution,
                        0f,
                        0.9f);

                _rigidbody.linearVelocity *=
                    Mathf.Clamp01(_groundSpeedRetention);

                if (!_hasSnapTarget)
                {
                    TryChooseSnapTarget();
                }

                if (_verticalSpeed
                    < Mathf.Max(
                        0.01f,
                        _minimumBounceSpeed))
                {
                    BeginSnapToGround();
                    return;
                }
            }

            if (_hasSnapTarget)
            {
                SteerTowardSnapTarget();
            }
        }

        // --------------------------------------------------------------------
        // Snap
        // --------------------------------------------------------------------

        private void TryChooseSnapTarget()
        {
            if (_groundTilemap == null)
            {
                return;
            }

            Vector2 expectedPosition =
                _rigidbody.position
                + _rigidbody.linearVelocity
                * GetRemainingTravelTime();

            Vector3 worldPosition = new Vector3(
                expectedPosition.x,
                expectedPosition.y,
                transform.position.z);

            Vector3Int cell =
                _groundTilemap.WorldToCell(worldPosition);

            if (!_groundTilemap.HasTile(cell))
            {
                return;
            }

            _snapTarget =
                _groundTilemap.GetCellCenterWorld(cell);

            _hasSnapTarget = true;
        }

        private void SteerTowardSnapTarget()
        {
            float travelTime = Mathf.Max(
                Time.fixedDeltaTime,
                GetRemainingTravelTime());

            Vector2 desiredVelocity =
                (_snapTarget - _rigidbody.position)
                / travelTime;

            desiredVelocity = Vector2.ClampMagnitude(
                desiredVelocity,
                Mathf.Max(0.1f, _throwSpeed * 1.5f));

            float blend =
                1f - Mathf.Exp(
                    -Mathf.Max(0f, _snapSteering)
                    * Time.fixedDeltaTime);

            _rigidbody.linearVelocity =
                Vector2.Lerp(
                    _rigidbody.linearVelocity,
                    desiredVelocity,
                    blend);
        }

        private void BeginSnapToGround()
        {
            if (!_hasSnapTarget)
            {
                TryChooseSnapTarget();
            }

            PrepareGroundPhysics();

            if (!_hasSnapTarget)
            {
                CompleteSettle(true);
                return;
            }

            _isSnapping = true;

            _snapTween = _rigidbody
                .DOMove(
                    _snapTarget,
                    Mathf.Max(
                        0.01f,
                        _snapFinishDuration))
                .SetEase(Ease.OutCubic)
                .SetUpdate(UpdateType.Fixed)
                .OnComplete(() =>
                {
                    _snapTween = null;
                    _isSnapping = false;

                    CompleteSettle(true);
                });
        }

        private float GetRemainingTravelTime()
        {
            float gravity =
                Mathf.Max(0.01f, _heightGravity);

            float impactSpeed = Mathf.Sqrt(
                _verticalSpeed * _verticalSpeed
                + 2f
                * gravity
                * Mathf.Max(0f, _height));

            float travelTime =
                Mathf.Max(
                    0f,
                    (_verticalSpeed + impactSpeed)
                    / gravity);

            float reboundSpeed =
                impactSpeed
                * Mathf.Clamp(
                    _bounceRestitution,
                    0f,
                    0.9f);

            float speedWeight =
                Mathf.Clamp01(
                    _groundSpeedRetention);

            while (reboundSpeed
                   >= Mathf.Max(
                       0.01f,
                       _minimumBounceSpeed))
            {
                travelTime +=
                    2f
                    * reboundSpeed
                    / gravity
                    * speedWeight;

                reboundSpeed *=
                    Mathf.Clamp(
                        _bounceRestitution,
                        0f,
                        0.9f);

                speedWeight *=
                    Mathf.Clamp01(
                        _groundSpeedRetention);
            }

            return travelTime;
        }

        // --------------------------------------------------------------------
        // Ground
        // --------------------------------------------------------------------

        public void Drop(Vector3 worldPosition)
        {
            if (!IsCarried)
            {
                return;
            }

            transform.SetParent(_worldParent, true);
            transform.position = worldPosition;
            _rigidbody.position = worldPosition;

            IsCarried = false;

            CompleteSettle(true);
        }

        private void PrepareGroundPhysics()
        {
            _height = 0f;
            _verticalSpeed = 0f;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.simulated = true;

            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;

            RestoreThrowerCollisions();
        }

        private void CompleteSettle(bool notify)
        {
            CancelSnap();

            PrepareGroundPhysics();

            IsCarried = false;
            IsAirborne = false;
            IsLoaded = false;
            IsCannonFlight = false;

            _hasSnapTarget = false;

            ResetVisualHeight();

            GroundSortingRequested?.Invoke(this); // �ٴڿ� ������ �������� ��

            if (notify)
            {
                NotifyStateChanged();
            }
        }

        // --------------------------------------------------------------------
        // Cannon
        // --------------------------------------------------------------------

        public bool TryEnterCannon(
            Transform storagePoint)
        {
            if (!isActiveAndEnabled
                || !IsAirborne
                || IsCarried
                || IsLoaded
                || IsCannonFlight
                || storagePoint == null
                || storagePoint.IsChildOf(transform))
            {
                return false;
            }

            CancelSnap();
            RestoreThrowerCollisions();

            IsCarried = false;
            IsAirborne = false;
            IsLoaded = true;
            IsCannonFlight = false;

            _height = 0f;
            _verticalSpeed = 0f;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = false;

            transform.SetParent(storagePoint, true);
            transform.localPosition = Vector3.zero;

            ResetVisualHeight();
            NotifyStateChanged();

            return true;
        }

        public void LaunchFromCannon(
            Vector3 muzzlePosition,
            Vector3 targetPosition,
            CannonTrajectoryType trajectoryType)
        {
            LaunchFromCannon(
                muzzlePosition,
                targetPosition,
                trajectoryType,
                _defaultCannonFlightDuration,
                _defaultCannonArcHeight);
        }

        public void LaunchFromCannon(
            Vector3 muzzlePosition,
            Vector3 targetPosition,
            CannonTrajectoryType trajectoryType,
            float flightDuration,
            float arcHeight)
        {
            if (!IsLoaded)
            {
                return;
            }

            CancelSnap();

            transform.SetParent(_worldParent, true);

            transform.position = muzzlePosition;
            _rigidbody.position = muzzlePosition;

            IsCarried = false;
            IsLoaded = false;
            IsAirborne = true;
            IsCannonFlight = true;

            TrajectoryType = trajectoryType;

            _cannonFlightDuration =
                Mathf.Max(0.01f, flightDuration);

            _cannonArcHeight =
                Mathf.Max(0f, arcHeight);

            _cannonFlightTime = 0f;

            _cannonStartPosition = muzzlePosition;
            _cannonTargetPosition = targetPosition;

            _height = 0f;
            _verticalSpeed = 0f;

            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.simulated = true;

            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;

            NotifyStateChanged();
        }

        private void UpdateCannonFlight()
        {
            _cannonFlightTime += Time.fixedDeltaTime;

            float progress = Mathf.Clamp01(
                _cannonFlightTime
                / _cannonFlightDuration);

            Vector2 position = Vector2.Lerp(
                _cannonStartPosition,
                _cannonTargetPosition,
                progress);

            _rigidbody.MovePosition(position);

            _height =
                TrajectoryType == CannonTrajectoryType.Arc
                    ? 4f
                      * _cannonArcHeight
                      * progress
                      * (1f - progress)
                    : 0f;

            if (progress < 1f)
            {
                return;
            }

            _rigidbody.position =
                _cannonTargetPosition;

            CompleteSettle(true);
        }

        // --------------------------------------------------------------------
        // Presentation position only
        // --------------------------------------------------------------------

        private void UpdateHeightPresentation()
        {
            if (_visual == null)
            {
                return;
            }

            if (IsAirborne)
            {
                _visual.position =
                    transform.TransformPoint(
                        _visualRestPosition)
                    + Vector3.up * _height;
            }

            if (_shadow == null)
            {
                return;
            }

            _shadow.enabled =
                !IsCarried && !IsLoaded;

            _shadow.transform.localScale =
                _shadowRestScale
                * Mathf.Lerp(
                    1f,
                    0.6f,
                    Mathf.Clamp01(_height / 2f));
        }

        private void ResetVisualHeight()
        {
            if (_visual != null)
            {
                _visual.localPosition =
                    _visualRestPosition;
            }
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        public bool TransferTo(Transform destination)
        {
            if (!IsCarried
                || destination == null
                || destination.IsChildOf(transform))
            {
                return false;
            }

            transform.SetParent(destination, true);
            transform.localPosition = Vector3.zero;

            return true;
        }

        private void IgnoreThrowerCollisions(
            Collider2D[] colliders)
        {
            _ignoredColliders = colliders;

            if (_ignoredColliders == null)
            {
                return;
            }

            foreach (Collider2D other in _ignoredColliders)
            {
                if (other == null || other == _collider)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(
                    _collider,
                    other,
                    true);
            }
        }

        private void RestoreThrowerCollisions()
        {
            if (_ignoredColliders == null)
            {
                return;
            }

            foreach (Collider2D other in _ignoredColliders)
            {
                if (other == null
                    || _collider == null
                    || other == _collider)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(
                    _collider,
                    other,
                    false);
            }

            _ignoredColliders = null;
        }

        private void CancelSnap()
        {
            if (_snapTween != null)
            {
                _snapTween.Kill();
                _snapTween = null;
            }

            _isSnapping = false;
            _hasSnapTarget = false;
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void OnDisable()
        {
            CancelSnap();
            RestoreThrowerCollisions();
        }

        private void OnDestroy()
        {
            CancelSnap();
            RestoreThrowerCollisions();

            if (_flightMaterial != null)
            {
                Destroy(_flightMaterial);
            }
        }
    }
}