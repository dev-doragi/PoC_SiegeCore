using System.Collections;
using DG.Tweening;
using SiegeCore.Player;
using SiegeCore.Rat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Cannon
{
    public sealed class Cannon : MonoBehaviour, IThrowable
    {
        [Header("References")]
        [SerializeField] private Transform _storagePoint;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _visual;
        [SerializeField] private CannonSlot _sourceSlot;
        [SerializeField] private Tilemap _groundTilemap;

        [Header("Loading")]
        [SerializeField, Min(1)] private int _maxLoadCount = 3;
        [SerializeField, Min(0.01f)] private float _ratLoadingDuration = 0.2f;

        [Header("Intruder Damage")]
        [SerializeField, Range(0f, 1f)]
        private float _intruderDamageRatioPerSecond = 0.05f;
        [SerializeField, Min(0.05f)]
        private float _intruderDamageInterval = 1f;

        [Header("Fire")]
        [SerializeField] private CannonTrajectoryType _trajectoryType = CannonTrajectoryType.Straight;
        [SerializeField, Min(0.01f)] private float _minimumFireInterval = 0.2f;
        [SerializeField, Min(0.01f)] private float _projectileFlightDuration = 3f;
        [SerializeField, Min(0f)] private float _projectileArcHeight = 2f;

        [Header("Carry")]
        [SerializeField] private Vector3 _carriedScale = Vector3.one;
        [SerializeField, Min(0.01f)] private float _carryTweenDuration = 0.2f;
        [SerializeField, Min(0f)] private float _slotSearchRadius = 1f;
        [SerializeField, Min(0f)] private float _throwSpeed = 5.5f;
        [SerializeField, Min(0f)] private float _throwUpwardSpeed = 4f;
        [SerializeField, Min(0.01f)] private float _throwHeightGravity = 16f;
        [SerializeField, Range(0f, 1f)] private float _throwBounceRestitution = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _throwGroundSpeedRetention = 0.65f;
        [SerializeField, Min(0.01f)] private float _minimumThrowBounceSpeed = 0.8f;

        private CannonMagazine _magazine;
        private Coroutine _fireRoutine;
        private bool _hasFired;
        private float _lastFireTime;
        private float _nextIntruderDamageTime;
        private Tween _carryTween;
        private Tween _scaleTween;
        private Transform _worldParent;
        private Vector3 _originalScale;
        private Vector3 _visualRestPosition;
        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private PhysicsMaterial2D _throwMaterial;
        private PhysicsMaterial2D _originalMaterial;
        private Collider2D[] _ignoredColliders;
        private CannonSlot _pendingInstallSlot;
        private bool _isThrown;
        private float _height;
        private float _verticalSpeed;

        public CannonState State { get; private set; } = CannonState.Placed;
        public event System.Action<IThrowable> GroundSortingRequested;
        public Transform CarryTransform { get { return transform; } }
        public bool IsCarried { get { return State == CannonState.Carried; } }
        public int LoadedCount
        {
            get
            {
                return _magazine == null ? 0 : _magazine.Count;
            }
        }
        public bool IsFull { get { return _magazine != null && _magazine.IsFull; } }
        public CannonSlot SourceSlot { get { return _sourceSlot; } }
        public CannonTrajectoryType TrajectoryType { get { return _trajectoryType; } }
        public float ProjectileFlightDuration { get { return _projectileFlightDuration; } }
        public float ProjectileArcHeight { get { return _projectileArcHeight; } }

        public bool IsInstalledFor(VehicleSide side)
        {
            return State == CannonState.Installed
                && _sourceSlot != null
                && _sourceSlot.VehicleSide == side;
        }

        public Vector3 GetLoadingPosition()
        {
            if (_storagePoint != null)
            {
                return _storagePoint.position;
            }

            return transform.position;
        }

        private void Awake()
        {
            _originalScale = transform.localScale;
            _magazine = new CannonMagazine(_maxLoadCount);
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (_storagePoint == null || _muzzle == null || _visual == null || _rigidbody == null || _collider == null)
            {
                Debug.LogError("[Cannon] Assign StoragePoint, Muzzle and Visual, and add Rigidbody2D and Collider2D.", this);
                enabled = false;
                return;
            }

            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _originalMaterial = _collider.sharedMaterial;
            _visualRestPosition = _visual.localPosition;
            _throwMaterial = new PhysicsMaterial2D("Cannon throw")
            {
                bounciness = _throwBounceRestitution,
                friction = 0f
            };
        }

        private void Start()
        {
            // Slot Tilemaps finish Awake before their painted cells are queried.
            CannonSlot slot = FindNearestSlot(_storagePoint.position, false);
            if (slot == null || !slot.TryGetWorldPosition(out Vector3 slotPosition))
            {
                _sourceSlot = null;
                State = CannonState.Placed;
                Debug.LogWarning("[Cannon] No active painted slot found at startup. Remaining Placed.", this);
                return;
            }

            Vector3 rootPosition = transform.position + slotPosition - _storagePoint.position;
            transform.position = rootPosition;
            _rigidbody.position = rootPosition;
            SetInstalled(slot);
            Debug.Log($"[Cannon] Installed at startup: {slot.VehicleSide}/{slot.SlotType} ({slot.name}).", this);
        }

        private void OnEnable()
        {
            if (State == CannonState.Installed) StartFiringIfNeeded();
        }

        private void OnDisable()
        {
            if (_magazine != null)
            {
                _magazine.CancelLoading();
            }

            if (_sourceSlot != null)
            {
                _sourceSlot.UnregisterCannon(this);
            }
            GroundSortingRequested?.Invoke(this);
            if (_fireRoutine != null) StopCoroutine(_fireRoutine);
            _fireRoutine = null;
            // Keep the queue so re-enabling the cannon resumes firing.
        }

        private void FixedUpdate()
        {
            if (!_isThrown) return;

            float remainingTime = Time.fixedDeltaTime;
            float gravity = Mathf.Max(0.01f, _throwHeightGravity);
            while (remainingTime > 0f && _isThrown)
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
                GroundSortingRequested?.Invoke(this);
                _height = 0f;
                _verticalSpeed = impactSpeed * Mathf.Clamp(_throwBounceRestitution, 0f, 0.9f);
                _rigidbody.linearVelocity *= Mathf.Clamp01(_throwGroundSpeedRetention);

                CannonSlot slot = FindNearestSlot(_storagePoint.position);
                if (slot != null && BeginInstallFromThrow(slot)) return;
                if (_verticalSpeed < Mathf.Max(0.01f, _minimumThrowBounceSpeed)) SettleThrownOnGround();
            }
        }

        private void Update()
        {
            if (State != CannonState.Installed
                || _sourceSlot == null
                || _magazine == null
                || Time.timeScale <= 0f
                || _intruderDamageRatioPerSecond <= 0f
                || _intruderDamageInterval <= 0f
                || Time.time < _nextIntruderDamageTime)
            {
                return;
            }

            bool hasIntruder = _magazine.ApplyIntruderDamage(
                _sourceSlot.VehicleSide,
                _intruderDamageRatioPerSecond,
                _intruderDamageInterval);
            if (hasIntruder)
            {
                _nextIntruderDamageTime =
                    Time.time + _intruderDamageInterval;
            }
            else
            {
                _nextIntruderDamageTime = Time.time;
            }
        }

        private void LateUpdate()
        {
            if (_visual == null) return;
            _visual.position = transform.TransformPoint(_visualRestPosition) + Vector3.up * _height;
        }

        public bool TryLoad(CarryableObject item)
        {
            return TryLoad(item, out string reason);
        }

        public bool TryLoad(CarryableObject item, out string reason)
        {
            return TryLoadRat(item != null ? item.Agent : null, false, out reason);
        }

        public bool TryLoadIdle(RatAgent rat)
        {
            return TryLoadRat(rat, true, out string reason);
        }

        private bool TryLoadRat(RatAgent rat, bool fromIdle, out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled) reason = "Cannon is disabled";
            else if (_storagePoint == null) reason = "StoragePoint is missing";
            else if (IsFull) reason = "Queue is full";
            else if (rat == null) reason = "Ammo is not eligible for loading";
            if (reason != null) return false;

            // Reserve the slot before the Rat commits its loading state.
            if (!_magazine.Enqueue(rat))
            {
                reason = "Queue reservation failed";
                return false;
            }

            if (!rat.TryEnterCannon(
                    _storagePoint,
                    fromIdle,
                    _ratLoadingDuration))
            {
                _magazine.Remove(rat);
                reason = "Ammo is not eligible for loading";
                return false;
            }

            StartFiringIfNeeded();
            return true;
        }

        private void StartFiringIfNeeded()
        {
            if (State == CannonState.Installed && _fireRoutine == null && LoadedCount > 0)
                _fireRoutine = StartCoroutine(FireRoutine());
        }

        private IEnumerator FireRoutine()
        {
            while (State == CannonState.Installed && LoadedCount > 0)
            {
                if (!_magazine.TryPeekReady(
                    out RatAgent rat,
                    out float readyTime))
                {
                    yield return null;
                    continue;
                }

                float fireTime = readyTime;
                if (_hasFired)
                {
                    fireTime = Mathf.Max(
                        fireTime,
                        _lastFireTime + Mathf.Max(0.01f, _minimumFireInterval));
                }

                float waitTime = fireTime - Time.time;
                if (waitTime > 0f)
                {
                    yield return new WaitForSeconds(waitTime);
                }

                if (State != CannonState.Installed
                    || LoadedCount == 0)
                {
                    break;
                }

                if (_muzzle == null || _sourceSlot == null || _sourceSlot.TargetSlot == null)
                {
                    Debug.LogError("[Cannon] Cannot fire without a muzzle, source slot and target slot.", this);
                    yield return null;
                    continue;
                }
                if (!TryGetShotTarget(out Vector3 targetPosition))
                {
                    Debug.LogError("[Cannon] Target cannon or Target Slot must provide a fire position.", this);
                    yield return null;
                    continue;
                }
                if (rat.LaunchFromCannon(_muzzle.position, targetPosition, _sourceSlot.VehicleSide,
                    _sourceSlot, _trajectoryType, _projectileFlightDuration, _projectileArcHeight))
                {
                    _magazine.Remove(rat);
                    _lastFireTime = Time.time;
                    _hasFired = true;
                }
                else
                {
                    yield return null;
                }
            }
            _fireRoutine = null;
        }

        private bool TryGetShotTarget(out Vector3 targetPosition)
        {
            Cannon targetCannon = _sourceSlot.TargetSlot.InstalledCannon;
            if (targetCannon != null && targetCannon._muzzle != null)
            {
                targetPosition = targetCannon._muzzle.position;
                return true;
            }

            return _sourceSlot.TargetSlot.TryGetWorldPosition(out targetPosition);
        }

        public void Drop(Vector3 worldPosition)
        {
            if (State != CannonState.Carried) return;

            KillCarryTweens();
            transform.SetParent(_worldParent, true);
            transform.position = worldPosition;
            transform.localScale = _originalScale;

            CannonSlot slot = FindNearestSlot(worldPosition);
            if (slot != null)
            {
                Vector3 rootOffset = transform.position - _storagePoint.position;
                if (!slot.TryGetWorldPosition(out Vector3 slotPosition))
                {
                    ClearSourceSlot();
                    State = CannonState.Placed;
                    SnapToGround();
                    return;
                }
                transform.position = slotPosition + rootOffset;
                SetInstalled(slot);
                return;
            }

            ClearSourceSlot();
            State = CannonState.Placed;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.linearVelocity = Vector2.zero;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;
            SnapToGround();
        }

        public bool TryThrow(Vector2 direction, Vector3 groundPosition, Collider2D[] throwerColliders)
        {
            if (State != CannonState.Carried || direction.sqrMagnitude < 0.0001f || !isActiveAndEnabled) return false;

            KillCarryTweens();
            transform.SetParent(_worldParent, true);
            transform.position = groundPosition;
            transform.localScale = _originalScale;
            ClearSourceSlot();
            State = CannonState.Placed;
            _isThrown = true;
            _height = 0f;
            _verticalSpeed = _throwUpwardSpeed;
            _rigidbody.position = groundPosition;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.linearDamping = 0f;
            _collider.isTrigger = false;
            _throwMaterial.bounciness = _throwBounceRestitution;
            _collider.sharedMaterial = _throwMaterial;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = direction.normalized * Mathf.Max(0.01f, _throwSpeed);
            IgnoreThrowerCollisions(throwerColliders);
            return true;
        }

        private CannonSlot FindNearestSlot(Vector3 worldPosition, bool limitRange = true)
        {
            CannonSlot[] slots = FindObjectsByType<CannonSlot>(FindObjectsSortMode.None);
            CannonSlot nearest = null;
            float nearestDistance = limitRange
                ? Mathf.Max(0f, _slotSearchRadius) * Mathf.Max(0f, _slotSearchRadius)
                : float.PositiveInfinity;
            foreach (CannonSlot slot in slots)
            {
                if (!slot.isActiveAndEnabled) continue;
                if (!slot.TryGetWorldPosition(out Vector3 slotPosition)) continue;
                float distance = ((Vector2)slotPosition - (Vector2)worldPosition).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearest = slot;
                nearestDistance = distance;
            }
            return nearest;
        }

        private void SetInstalled(CannonSlot slot)
        {
            ClearSourceSlot();
            _sourceSlot = slot;
            _sourceSlot.RegisterCannon(this);
            State = CannonState.Installed;
            _nextIntruderDamageTime = Time.time;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.linearVelocity = Vector2.zero;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;
            StartFiringIfNeeded();
        }

        private void SnapToGround()
        {
            if (_groundTilemap == null) return;

            Vector3Int cellPosition = _groundTilemap.WorldToCell(transform.position);
            if (!_groundTilemap.HasTile(cellPosition)) return;
            Vector3 targetPosition = _groundTilemap.GetCellCenterWorld(cellPosition);
            _carryTween = transform.DOMove(targetPosition, Mathf.Max(0.01f, _carryTweenDuration))
                .OnComplete(() => { _carryTween = null; });
        }

        private bool BeginInstallFromThrow(CannonSlot slot)
        {
            if (!slot.TryGetWorldPosition(out Vector3 slotPosition)) return false;

            _isThrown = false;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;
            RestoreIgnoredCollisions();

            Vector3 rootOffset = transform.position - _storagePoint.position;
            Vector3 targetPosition = slotPosition + rootOffset;
            _pendingInstallSlot = slot;
            _carryTween = transform.DOMove(targetPosition, Mathf.Max(0.01f, _carryTweenDuration))
                .OnComplete(FinishThrownInstallation);
            return true;
        }

        private void ClearSourceSlot()
        {
            if (_sourceSlot != null)
            {
                _sourceSlot.UnregisterCannon(this);
                _sourceSlot = null;
            }
        }

        private void FinishThrownInstallation()
        {
            CannonSlot slot = _pendingInstallSlot;
            _pendingInstallSlot = null;

            if (slot != null)
            {
                SetInstalled(slot);
            }
        }

        private void StopFiring()
        {
            if (_fireRoutine != null) StopCoroutine(_fireRoutine);
            _fireRoutine = null;
        }

        private void SettleThrownOnGround()
        {
            _isThrown = false;
            _height = 0f;
            _verticalSpeed = 0f;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;
            RestoreIgnoredCollisions();
            SnapToGround();
        }

        private void KillCarryTweens()
        {
            _carryTween?.Kill();
            _scaleTween?.Kill();
            _carryTween = null;
            _scaleTween = null;
        }

        private void IgnoreThrowerCollisions(Collider2D[] throwerColliders)
        {
            _ignoredColliders = throwerColliders;
            if (_ignoredColliders == null) return;
            foreach (Collider2D other in _ignoredColliders)
            {
                if (other != null && other != _collider) Physics2D.IgnoreCollision(_collider, other, true);
            }
        }

        private void RestoreIgnoredCollisions()
        {
            if (_ignoredColliders == null) return;
            foreach (Collider2D other in _ignoredColliders)
            {
                if (other != null && _collider != null && other != _collider)
                    Physics2D.IgnoreCollision(_collider, other, false);
            }
            _ignoredColliders = null;
        }

        private void OnDestroy()
        {
            if (_magazine != null) _magazine.ReleaseAll();
            KillCarryTweens();
            RestoreIgnoredCollisions();
            if (_throwMaterial != null) Destroy(_throwMaterial);
        }
    }
}
