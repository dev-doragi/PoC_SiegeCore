using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class CarryableObject : MonoBehaviour
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

        [Header("Cannon Fire")]
        [SerializeField, Min(0.01f)] private float _cannonSpeed = 12f;
        [SerializeField, Min(0.01f)] private float _cannonFlightDuration = 3f;

        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
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
        private Tween _snapTween;
        private float _cannonFlightTime;

        public bool IsCarried { get; private set; }
        public bool IsAirborne { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsCannonFlight { get; private set; }
        public float Height => _height;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
            _originalMaterial = _collider.sharedMaterial;
            _flightMaterial = new PhysicsMaterial2D("Carryable flight")
            {
                bounciness = _wallRestitution,
                friction = 0f
            };

            if (_visual == null || _visual == transform)
            {
                Debug.LogError("[CarryableObject] Assign a separate Visual child for height display.", this);
                enabled = false;
                return;
            }

            _visualRestPosition = _visual.localPosition;
            if (_shadow != null) _shadowRestScale = _shadow.transform.localScale;
            SettleOnGround();
        }

        private void FixedUpdate()
        {
            if (!IsAirborne) return;
            if (IsCannonFlight)
            {
                // Straight cannon fire must not enter the zero-height bounce solver.
                _cannonFlightTime -= Time.fixedDeltaTime;
                if (_cannonFlightTime <= 0f) SettleOnGround();
                return;
            }

            // Solve height independently of XY, including the time left after each impact.
            float remainingTime = Time.fixedDeltaTime;
            float gravity = Mathf.Max(0.01f, _heightGravity);
            while (remainingTime > 0f && IsAirborne)
            {
                float impactSpeed = Mathf.Sqrt(_verticalSpeed * _verticalSpeed + 2f * gravity * _height);
                float impactTime = (_verticalSpeed + impactSpeed) / gravity;
                if (impactTime > remainingTime)
                {
                    _height += _verticalSpeed * remainingTime - 0.5f * gravity * remainingTime * remainingTime;
                    _verticalSpeed -= gravity * remainingTime;
                    break;
                }

                remainingTime -= impactTime;
                _height = 0f;
                _verticalSpeed = impactSpeed * Mathf.Clamp(_bounceRestitution, 0f, 0.9f);
                _rigidbody.linearVelocity *= Mathf.Clamp01(_groundSpeedRetention);
                // Select once at the first bounce, using the expected resting position.
                // Keeping one target prevents switching cells at tile boundaries.
                if (!_hasSnapTarget) TryChooseSnapTarget();
                if (_verticalSpeed < Mathf.Max(0.01f, _minimumBounceSpeed)) SettleOnGround(snapToTile: true);
            }

            if (IsAirborne && _hasSnapTarget)
            {
                float travelTime = Mathf.Max(Time.fixedDeltaTime, GetRemainingTravelTime());
                Vector2 desiredVelocity = (_snapTarget - _rigidbody.position) / travelTime;
                desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, Mathf.Max(0.1f, _throwSpeed * 1.5f));
                float blend = 1f - Mathf.Exp(-Mathf.Max(0f, _snapSteering) * Time.fixedDeltaTime);
                _rigidbody.linearVelocity = Vector2.Lerp(_rigidbody.linearVelocity, desiredVelocity, blend);
            }
        }

        private void LateUpdate()
        {
            UpdatePresentation();
        }

        private void UpdatePresentation()
        {
            if (_visual == null || _visual == transform) return;
            // World-unit height remains correct even if the prefab is scaled.
            _visual.position = transform.TransformPoint(_visualRestPosition) + Vector3.up * _height;
            if (_shadow == null) return;
            _shadow.enabled = !IsCarried && !IsLoaded;
            _shadow.transform.localScale = _shadowRestScale * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(_height / 2f));
        }

        public bool TryPickUp(Transform holdPoint)
        {
            if (IsCarried || IsAirborne || IsLoaded || holdPoint == null || !isActiveAndEnabled) return false;
            CancelSnap();
            _worldParent = transform.parent;
            _rigidbody.simulated = false;
            IsCarried = true;
            transform.SetParent(holdPoint, true);
            transform.localPosition = Vector3.zero;
            UpdatePresentation();
            return true;
        }

        public bool TryThrow(Vector2 direction, Vector3 groundPosition, Collider2D[] throwerColliders)
        {
            if (!IsCarried || direction.sqrMagnitude < 0.0001f || !isActiveAndEnabled) return false;
            CancelSnap();
            _height = Mathf.Max(0f, transform.position.y - groundPosition.y);
            _verticalSpeed = _upwardSpeed;
            transform.SetParent(_worldParent, true);
            transform.position = groundPosition;
            _rigidbody.position = groundPosition;
            IsCarried = false;
            IsAirborne = true;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.linearDamping = 0f;
            _collider.isTrigger = false;
            _flightMaterial.bounciness = _wallRestitution;
            _collider.sharedMaterial = _flightMaterial;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = direction.normalized * _throwSpeed;

            // The throw starts close to the player, so ignore the thrower until landing.
            _ignoredColliders = throwerColliders;
            if (_ignoredColliders != null)
            {
                foreach (Collider2D other in _ignoredColliders)
                {
                    if (other != null && other != _collider) Physics2D.IgnoreCollision(_collider, other, true);
                }
            }
            UpdatePresentation();
            return true;
        }

        // Cleanup only: normal interaction uses TryThrow instead of placing the item.
        public void Drop(Vector3 worldPosition)
        {
            if (!IsCarried) return;
            transform.SetParent(_worldParent, true);
            transform.position = worldPosition;
            _rigidbody.position = worldPosition;
            IsCarried = false;
            SettleOnGround();
            UpdatePresentation();
        }

        private void SettleOnGround(bool snapToTile = false)
        {
            IsAirborne = false;
            IsCannonFlight = false;
            _height = 0f;
            _verticalSpeed = 0f;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;
            _rigidbody.simulated = true;
            RestoreThrowerCollisions();
            if (snapToTile) SnapToTileCenter();
        }

        private float GetRemainingTravelTime()
        {
            float gravity = Mathf.Max(0.01f, _heightGravity);
            float impactSpeed = Mathf.Sqrt(_verticalSpeed * _verticalSpeed + 2f * gravity * Mathf.Max(0f, _height));
            float travelTime = Mathf.Max(0f, (_verticalSpeed + impactSpeed) / gravity);
            float reboundSpeed = impactSpeed * Mathf.Clamp(_bounceRestitution, 0f, 0.9f);
            float speedWeight = Mathf.Clamp01(_groundSpeedRetention);
            // Weight each future hop by the horizontal speed it retains.
            while (reboundSpeed >= Mathf.Max(0.01f, _minimumBounceSpeed))
            {
                travelTime += 2f * reboundSpeed / gravity * speedWeight;
                reboundSpeed *= Mathf.Clamp(_bounceRestitution, 0f, 0.9f);
                speedWeight *= Mathf.Clamp01(_groundSpeedRetention);
            }
            return travelTime;
        }

        private void TryChooseSnapTarget()
        {
            if (_groundTilemap == null) return;
            Vector2 expectedPosition = _rigidbody.position + _rigidbody.linearVelocity * GetRemainingTravelTime();
            Vector3 worldPosition = new Vector3(expectedPosition.x, expectedPosition.y, transform.position.z);
            Vector3Int cellPosition = _groundTilemap.WorldToCell(worldPosition);
            if (!_groundTilemap.HasTile(cellPosition)) return;
            _snapTarget = _groundTilemap.GetCellCenterWorld(cellPosition);
            _hasSnapTarget = true;
        }

        private void SnapToTileCenter()
        {
            if (!_hasSnapTarget) TryChooseSnapTarget();
            if (!_hasSnapTarget) return;

            _snapTween = _rigidbody.DOMove(_snapTarget, Mathf.Max(0.01f, _snapFinishDuration))
                .SetEase(Ease.OutCubic)
                .SetUpdate(UpdateType.Fixed)
                .OnComplete(() => _snapTween = null);
        }

        private void CancelSnap()
        {
            _snapTween?.Kill();
            _snapTween = null;
            _hasSnapTarget = false;
        }

        private void RestoreThrowerCollisions()
        {
            if (_ignoredColliders == null) return;
            foreach (Collider2D other in _ignoredColliders)
            {
                if (other != null && _collider != null && other != _collider)
                    Physics2D.IgnoreCollision(_collider, other, false);
            }
            _ignoredColliders = null;
        }

        public bool TransferTo(Transform destination)
        {
            if (!IsCarried || destination == null || destination.IsChildOf(transform)) return false;
            transform.SetParent(destination, true);
            transform.localPosition = Vector3.zero;
            return true;
        }

        public bool TryEnterCannon(Transform storagePoint)
        {
            if (!isActiveAndEnabled || !IsAirborne || IsCarried || IsLoaded || IsCannonFlight
                || storagePoint == null || storagePoint.IsChildOf(transform)) return false;

            CancelSnap();
            RestoreThrowerCollisions();
            IsAirborne = false;
            IsLoaded = true;
            _height = 0f;
            _verticalSpeed = 0f;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = false;
            transform.SetParent(storagePoint, true);
            transform.localPosition = Vector3.zero;
            _visual.gameObject.SetActive(false);
            if (_shadow != null) _shadow.enabled = false;
            return true;
        }

        public void LaunchFromCannon(Vector3 muzzlePosition, Vector2 direction)
        {
            if (!IsLoaded || direction.sqrMagnitude < 0.0001f) return;

            CancelSnap();
            transform.SetParent(_worldParent, true);
            transform.position = muzzlePosition;
            _rigidbody.position = muzzlePosition;
            IsLoaded = false;
            IsAirborne = true;
            IsCannonFlight = true;
            _cannonFlightTime = Mathf.Max(0.01f, _cannonFlightDuration);
            _height = 0f;
            _verticalSpeed = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.linearDamping = 0f;
            _collider.isTrigger = false;
            _collider.sharedMaterial = _originalMaterial;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = direction.normalized * Mathf.Max(0.01f, _cannonSpeed);
            _visual.gameObject.SetActive(true);
            UpdatePresentation();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Temporary PoC endpoint; damage and projectile cancellation come later.
            if (IsCannonFlight) SettleOnGround();
        }

        private void OnDisable()
        {
            CancelSnap();
            if (_rigidbody != null && !IsCarried && !IsLoaded) SettleOnGround();
        }

        private void OnDestroy()
        {
            CancelSnap();
            RestoreThrowerCollisions();
            if (_flightMaterial != null) Destroy(_flightMaterial);
        }
    }
}
