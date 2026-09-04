using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;
using SiegeCore.Cannon;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class CarryableObject : MonoBehaviour, IThrowable
    {
        [Header("Landing")]
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField, Min(0f)] private float _snapSteering = 12f;
        [SerializeField, Min(0.01f)] private float _snapFinishDuration = 0.12f;
        [SerializeField, Min(0.1f)] private float _occupiedCellBounceHeight = 0.3f;

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
        [SerializeField, Min(0.01f)] private float _cannonFlightDuration = 3f;
        [SerializeField, Min(0f)] private float _cannonArcHeight = 2f;

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
        private Vector2 _cannonStartPosition;
        private Vector2 _cannonTargetPosition;
        private static readonly HashSet<CarryableObject> ActiveItems = new HashSet<CarryableObject>();
        private Vector2 _lastGroundPosition;
        private bool _hasGroundPosition;
        private readonly HashSet<Vector3Int> _occupiedCells = new HashSet<Vector3Int>();
        private readonly Queue<Vector3Int> _searchQueue = new Queue<Vector3Int>();
        private readonly Dictionary<Vector3Int, Vector3Int> _searchParents = new Dictionary<Vector3Int, Vector3Int>();
        private readonly List<Vector2> _landingPath = new List<Vector2>();
        private int _landingPathIndex;
        private float _nextLandingSearchTime;

        private void OnEnable()
        {
            ActiveItems.Add(this);
            _hasGroundPosition = false;
        }

        public bool IsCarried { get; private set; }
        public event System.Action<IThrowable> GroundSortingRequested;
        public bool IsAirborne { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsCannonFlight { get; private set; }
        public CannonTrajectoryType TrajectoryType { get; private set; }
        public float Height => _height;
        public bool CanEnterCannon => isActiveAndEnabled && !IsCarried && !IsLoaded && !IsCannonFlight
            && IsAirborne;
        public Transform CarryTransform => transform;
        public bool CanBePickedUp => !IsCarried && !IsAirborne && !IsLoaded && isActiveAndEnabled;

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
                _cannonFlightTime -= Time.fixedDeltaTime;
                float duration = Mathf.Max(0.01f, _cannonFlightDuration);
                float progress = Mathf.Clamp01(1f - _cannonFlightTime / duration);
                Vector2 position = Vector2.Lerp(_cannonStartPosition, _cannonTargetPosition, progress);
                _rigidbody.MovePosition(position);
                _height = TrajectoryType == CannonTrajectoryType.Arc
                    ? 4f * _cannonArcHeight * progress * (1f - progress)
                    : 0f;
                if (_cannonFlightTime <= 0f)
                {
                    _rigidbody.position = _cannonTargetPosition;
                    _height = 0f;
                    SettleOnGround();
                }
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
                GroundSortingRequested?.Invoke(this);
                _height = 0f;
                _verticalSpeed = impactSpeed * Mathf.Clamp(_bounceRestitution, 0f, 0.9f);
                _rigidbody.linearVelocity *= Mathf.Clamp01(_groundSpeedRetention);
                if (!_hasSnapTarget) TryChooseSnapTarget();
                if (_landingPathIndex < _landingPath.Count
                    && Vector2.Distance(_rigidbody.position, _landingPath[_landingPathIndex]) <= 0.1f)
                    _landingPathIndex++;
                if (_verticalSpeed < Mathf.Max(0.01f, _minimumBounceSpeed))
                {
                    if (_groundTilemap != null && (!_hasSnapTarget
                        || _landingPathIndex < _landingPath.Count
                        || Vector2.Distance(_rigidbody.position, _snapTarget) > 0.1f))
                        _verticalSpeed = Mathf.Sqrt(2f * gravity * Mathf.Max(0.1f, _occupiedCellBounceHeight));
                    else SettleOnGround(snapToTile: true);
                }
            }

            if (IsAirborne && _hasSnapTarget)
            {
                float travelTime = Mathf.Max(Time.fixedDeltaTime, GetRemainingTravelTime());
                Vector2 waypoint = _landingPathIndex < _landingPath.Count
                    ? _landingPath[_landingPathIndex] : _snapTarget;
                Vector2 desiredVelocity = (waypoint - _rigidbody.position) / travelTime;
                desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, Mathf.Max(0.1f, _throwSpeed * 1.5f));
                float blend = 1f - Mathf.Exp(-Mathf.Max(0f, _snapSteering) * Time.fixedDeltaTime);
                _rigidbody.linearVelocity = Vector2.Lerp(_rigidbody.linearVelocity, desiredVelocity, blend);
            }
            ConstrainToGround();
        }

        private void LateUpdate()
        {
            if (IsAirborne && !IsCannonFlight) ConstrainToGround();
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
            _collider.isTrigger = true; // Detect intake overlaps without physical pushing.
            _flightMaterial.bounciness = _wallRestitution;
            _collider.sharedMaterial = _flightMaterial;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = direction.normalized * _throwSpeed;
            ConstrainToGround();

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
            if (_groundTilemap == null || Time.time < _nextLandingSearchTime) return;
            CollectOccupiedCells();
            Vector2 expectedPosition = _rigidbody.position + _rigidbody.linearVelocity * GetRemainingTravelTime();
            expectedPosition = TraceGround(_rigidbody.position, expectedPosition);
            Vector3Int cellPosition = GroundCell(expectedPosition);
            if (TryReserveCell(cellPosition)) return;

            // BFS crosses occupied cells while airborne, reserving only the free endpoint.
            // Each edge is one hop; the first free cell has the minimum hop count.
            Vector3Int start = GroundCell(_rigidbody.position);
            _searchQueue.Clear();
            _searchParents.Clear();
            _searchQueue.Enqueue(start);
            _searchParents.Add(start, start);
            int first = Random.Range(0, 8);
            while (_searchQueue.Count > 0)
            {
                Vector3Int current = _searchQueue.Dequeue();
                if (_groundTilemap.HasTile(current) && !_occupiedCells.Contains(current))
                {
                    _snapTarget = _groundTilemap.GetCellCenterWorld(current);
                    _hasSnapTarget = true;
                    _landingPath.Clear();
                    Vector3Int cursor = current;
                    while (cursor != start)
                    {
                        _landingPath.Add(_groundTilemap.GetCellCenterWorld(cursor));
                        cursor = _searchParents[cursor];
                    }
                    _landingPath.Add(_groundTilemap.GetCellCenterWorld(start));
                    _landingPath.Reverse();
                    _landingPathIndex = 0;
                    _verticalSpeed = Mathf.Max(_verticalSpeed,
                        Mathf.Sqrt(2f * Mathf.Max(0.01f, _heightGravity) * Mathf.Max(0.1f, _occupiedCellBounceHeight)));
                    return;
                }
                for (int offset = 0; offset < 8; offset++)
                {
                    int index = (first + offset) % 8;
                    int flatIndex = index < 4 ? index : index + 1;
                    Vector3Int direction = new Vector3Int(flatIndex % 3 - 1, flatIndex / 3 - 1, 0);
                    Vector3Int neighbor = current + direction;
                    if (_searchParents.ContainsKey(neighbor) || !_groundTilemap.HasTile(neighbor)) continue;
                    // Do not cut diagonally across missing ground at a corner.
                    if (direction.x != 0 && direction.y != 0
                        && (!_groundTilemap.HasTile(current + new Vector3Int(direction.x, 0, 0))
                            || !_groundTilemap.HasTile(current + new Vector3Int(0, direction.y, 0)))) continue;
                    _searchParents.Add(neighbor, current);
                    _searchQueue.Enqueue(neighbor);
                }
            }
            // Only a completely full connected region must wait. Avoid searching every impact.
            _nextLandingSearchTime = Time.time + 0.5f;
            _rigidbody.linearVelocity = Vector2.zero;
        }

        private Vector3Int GroundCell(Vector2 position)
        {
            return _groundTilemap.WorldToCell(new Vector3(position.x, position.y, transform.position.z));
        }

        private bool TryReserveCell(Vector3Int cell)
        {
            if (!_groundTilemap.HasTile(cell) || _occupiedCells.Contains(cell)) return false;
            Vector2 center = _groundTilemap.GetCellCenterWorld(cell);
            if ((TraceGround(_rigidbody.position, center) - center).sqrMagnitude > 0.000001f) return false;
            _snapTarget = center;
            _hasSnapTarget = true;
            return true;
        }

        private void CollectOccupiedCells()
        {
            _occupiedCells.Clear();
            foreach (CarryableObject other in ActiveItems)
            {
                if (other == null || other == this || !other.isActiveAndEnabled
                    || other.IsCarried || other.IsLoaded || other.IsCannonFlight
                    || other._groundTilemap != _groundTilemap) continue;
                if (other._hasSnapTarget) _occupiedCells.Add(GroundCell(other._snapTarget));
                if (!other.IsAirborne) _occupiedCells.Add(GroundCell(other.transform.position));
            }
        }

        // Sample the whole segment, including gaps between valid endpoints.
        private Vector2 TraceGround(Vector2 start, Vector2 end)
        {
            Vector3 origin = _groundTilemap.GetCellCenterWorld(Vector3Int.zero);
            float cellWidth = Vector3.Distance(origin, _groundTilemap.GetCellCenterWorld(Vector3Int.right));
            float cellHeight = Vector3.Distance(origin, _groundTilemap.GetCellCenterWorld(Vector3Int.up));
            float step = Mathf.Max(0.001f, Mathf.Min(cellWidth, cellHeight) * 0.1f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / step));
            Vector2 valid = start;
            for (int index = 1; index <= steps; index++)
            {
                Vector2 point = Vector2.Lerp(start, end, (float)index / steps);
                if (!_groundTilemap.HasTile(GroundCell(point))) break;
                valid = point;
            }
            return valid;
        }

        private void ConstrainToGround()
        {
            if (_groundTilemap == null) return;
            Vector2 position = _rigidbody.position;
            if (!_hasGroundPosition)
            {
                if (!_groundTilemap.HasTile(GroundCell(position)))
                {
                    float closest = float.PositiveInfinity;
                    foreach (Vector3Int cell in _groundTilemap.cellBounds.allPositionsWithin)
                    {
                        if (!_groundTilemap.HasTile(cell)) continue;
                        Vector2 center = _groundTilemap.GetCellCenterWorld(cell);
                        float distance = (center - position).sqrMagnitude;
                        if (distance >= closest) continue;
                        closest = distance;
                        _lastGroundPosition = center;
                    }
                    if (float.IsPositiveInfinity(closest)) { _rigidbody.linearVelocity = Vector2.zero; return; }
                }
                else _lastGroundPosition = position;
                _hasGroundPosition = true;
            }
            Vector2 allowed = TraceGround(_lastGroundPosition, position);
            if ((allowed - position).sqrMagnitude > 0.000001f) _rigidbody.position = allowed;
            _lastGroundPosition = allowed;
            Vector2 next = allowed + _rigidbody.linearVelocity * Time.fixedDeltaTime;
            Vector2 bounded = TraceGround(allowed, next);
            if ((bounded - next).sqrMagnitude > 0.000001f)
                _rigidbody.linearVelocity = -_rigidbody.linearVelocity * Mathf.Clamp01(_wallRestitution);
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
            _hasGroundPosition = false;
            _landingPath.Clear();
            _landingPathIndex = 0;
            _nextLandingSearchTime = 0f;
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
            if (!CanEnterCannon
                || storagePoint == null || storagePoint.IsChildOf(transform)) return false;

            GroundSortingRequested?.Invoke(this);
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

        public void LaunchFromCannon(Vector3 muzzlePosition, Vector3 targetPosition,
            CannonTrajectoryType trajectoryType = CannonTrajectoryType.Straight)
        {
            if (!IsLoaded) return;

            CancelSnap();
            transform.SetParent(_worldParent, true);
            transform.position = muzzlePosition;
            _rigidbody.position = muzzlePosition;
            IsLoaded = false;
            IsAirborne = true;
            IsCannonFlight = true;
            TrajectoryType = trajectoryType;
            _cannonFlightTime = Mathf.Max(0.01f, _cannonFlightDuration);
            _cannonStartPosition = muzzlePosition;
            _cannonTargetPosition = targetPosition;
            _height = 0f;
            _verticalSpeed = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = Vector2.zero;
            _visual.gameObject.SetActive(true);
            UpdatePresentation();
        }

        public void ConsumeFromCannon()
        {
            if (!IsLoaded) return;
            IsLoaded = false;
            IsAirborne = false;
            IsCannonFlight = false;
            PooledObject handle = GetComponent<PooledObject>();
            if (handle != null && handle.Owner != null) handle.Return();
            else gameObject.SetActive(false);
        }

        public bool TryDispense(Tilemap groundTilemap, Vector2 direction, float speed)
        {
            if (_visual == null || _visual == transform || _flightMaterial == null) return false;
            CancelSnap();
            RestoreThrowerCollisions();
            enabled = true;
            _groundTilemap = groundTilemap;
            _worldParent = transform.parent;
            IsCarried = false;
            IsLoaded = false;
            IsCannonFlight = false;
            IsAirborne = true;
            _height = 0f;
            _verticalSpeed = _upwardSpeed;
            _cannonFlightTime = 0f;
            _rigidbody.position = transform.position;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.linearDamping = 0f;
            _rigidbody.angularVelocity = 0f;
            _collider.isTrigger = true; // Detect intake overlaps without physical pushing.
            _flightMaterial.bounciness = _wallRestitution;
            _collider.sharedMaterial = _flightMaterial;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = direction.normalized * Mathf.Max(0f, speed);
            ConstrainToGround();
            _visual.gameObject.SetActive(true);
            UpdatePresentation();
            return true;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Temporary PoC endpoint; damage and projectile cancellation come later.
            if (IsCannonFlight) SettleOnGround();
        }

        private void OnDisable()
        {
            GroundSortingRequested?.Invoke(this);
            ActiveItems.Remove(this);
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
