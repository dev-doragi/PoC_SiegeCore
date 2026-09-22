using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using SiegeCore.Cannon;
using SiegeCore.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class RatFlightMotion : MonoBehaviour
    {
        private enum BatFlightPhase
        {
            None,
            NormalKnockback,
            FullChargeRoute,
            WallReturn
        }

        [Header("Landing")]
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField, Min(0f)] private float _snapSteering = 12f;
        [SerializeField, Min(0.01f)] private float _snapFinishDuration = 0.12f;

        [Header("Throw")]
        [SerializeField, Min(0f)] private float _throwSpeed = 5.5f;
        [SerializeField, Min(0f)] private float _upwardSpeed = 4f;
        [SerializeField, Min(1), Tooltip("머리 위에서 던질 때 이동할 8방향 타일 수입니다.")]
        private int _throwCellDistance = 2;
        [SerializeField, Min(0.01f)] private float _heightGravity = 16f;
        [SerializeField, Range(0f, 0.9f)] private float _bounceRestitution = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _groundSpeedRetention = 0.65f;
        [SerializeField, Min(0.01f)] private float _minimumBounceSpeed = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _wallRestitution = 0.5f;

        [Header("Full Charge Impact")]
        [SerializeField, Min(0f), Tooltip("풀차지 타격과 고속 충돌에서 해당 Rat만 멈추는 시간입니다.")]
        private float _impactStopDuration = 0.06f;

        [SerializeField, Min(0f), Tooltip("스쿼시된 외형이 원래 크기로 돌아오는 시간입니다.")]
        private float _squashRecoveryDuration = 0.08f;

        [SerializeField, Min(0f), Tooltip("벽 충돌 히트스톱이 발생하는 최소 수평 속도입니다.")]
        private float _impactStopSpeedThreshold = 3f;

        [Header("Flight Weight")]
        [SerializeField, Min(0f), Tooltip("등급 한 단계마다 비행 중 추가되는 초당 수평 감속입니다. B는 추가 감속이 없습니다.")]
        private float _flightDecelerationPerRank = 1.5f;

        [Header("Wall Popup")]
        [SerializeField, Min(0f), Tooltip("벽 충돌 직전 수평 속력을 팝업 상승 속도로 바꾸는 배율입니다.")]
        private float _popupVerticalSpeedMultiplier = 0.6f;

        [SerializeField, Min(0f), Tooltip("벽 팝업의 최소 상승 속도입니다.")]
        private float _minimumPopupVerticalSpeed = 11f;

        [SerializeField, Min(0f), Tooltip("벽 팝업의 최대 상승 속도입니다.")]
        private float _maximumPopupVerticalSpeed = 12f;

        [SerializeField, Range(0f, 1f), Tooltip("예상 낙하지점을 현재 플레이어 위치 쪽으로 보정하는 비율입니다.")]
        private float _popupPlayerBlend = 0.35f;

        [SerializeField, Min(0f), Tooltip("팝업 후 플레이어 쪽으로 이동할 수 있는 최대 수평 속도입니다.")]
        private float _maximumPopupReturnSpeed = 8f;

        [Header("Cannon")]
        [SerializeField, Min(0.01f)] private float _defaultCannonFlightDuration = 3f;
        [SerializeField, Min(0f)] private float _defaultCannonArcHeight = 2f;

        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private RatAgent _rat;

        private PhysicsMaterial2D _flightMaterial;
        private PhysicsMaterial2D _originalMaterial;

        private Collider2D[] _ignoredColliders;

        private float _height;
        private float _verticalSpeed;
        private float _peakHeight;
        private bool _hasReachedPeak;

        private Vector2 _snapTarget;
        private bool _hasSnapTarget;
        private bool _isSnapping;
        private Tween _snapTween;

        private Vector2 _lastGroundPosition;
        private bool _hasGroundPosition;

        private bool _isHitStopped;
        private int _batSwingId;
        private BatFlightPhase _batFlightPhase = BatFlightPhase.None;
        private bool _canCollisionFuse;
        private float _collisionFusionMinimumSpeedForFlight;
        private Transform _batReturnTarget = null;
        private Coroutine _impactRoutine;
        private Vector2 _velocityBeforePhysicsStep = Vector2.zero;

        private static readonly HashSet<RatFlightMotion> ActiveItems =
            new HashSet<RatFlightMotion>();

        private readonly HashSet<Vector3Int> _occupiedCells =
            new HashSet<Vector3Int>();

        private readonly Queue<Vector3Int> _searchQueue =
            new Queue<Vector3Int>();

        private readonly HashSet<Vector3Int> _visitedCells =
            new HashSet<Vector3Int>();

        private float _cannonFlightTime;
        private float _cannonFlightDuration;
        private float _cannonArcHeight;

        private Vector2 _cannonStartPosition;
        private Vector2 _cannonTargetPosition;

        public event Action Settled;
        private CarryableObject _carryable;
        private RatPresenter _presentation;
        private RatCollisionFusion _fusion;
        internal Rigidbody2D Body { get { return _rigidbody; } }
        internal Collider2D Collider { get { return _collider; } }
        internal float VerticalSpeed { get { return _verticalSpeed; } }
        internal int SwingId { get { return _batSwingId; } }
        internal float FusionMinimumSpeed { get { return _collisionFusionMinimumSpeedForFlight; } }
        internal Transform ReturnTarget { get { return _batReturnTarget; } }
        internal bool IsFullChargeRoute { get { return _batFlightPhase == BatFlightPhase.FullChargeRoute; } }
        internal bool CanCollisionFuse { get { return _canCollisionFuse; } }

        public CarryState State { get; private set; } = CarryState.Grounded;
        public FlightType Flight { get; private set; } = FlightType.None;
        public bool IsCarried { get { return State == CarryState.Carried; } }
        public bool IsAirborne { get { return State == CarryState.Airborne; } }
        public bool IsLoaded { get { return State == CarryState.Loaded; } }
        public bool IsCannonFlight
        {
            get { return State == CarryState.Airborne && Flight == FlightType.Cannon; }
        }
        public bool IsHitStopped { get { return _isHitStopped; } }
        public bool IsFusionLocked { get { return _fusion != null && _fusion.IsLocked; } }
        public bool IsDescending { get { return IsAirborne && _verticalSpeed <= 0f; } }
        public float FallDistanceFromPeak => _hasReachedPeak
            ? Mathf.Max(0f, _peakHeight - _height)
            : 0f;
        public bool CanBeCaughtInFlight
        {
            get
            {
                if (!IsAirborne || !IsDescending || IsCannonFlight)
                {
                    return false;
                }

                // Full charge remains a transport/fusion route until its wall-return popup begins.
                if (_batFlightPhase == BatFlightPhase.FullChargeRoute)
                {
                    return false;
                }

                return !_isHitStopped && !IsFusionLocked && !_isSnapping;
            }
        }

        private void SetState(CarryState state, FlightType flight)
        {
            State = state;
            Flight = state == CarryState.Airborne ? flight : FlightType.None;
        }

        public CannonTrajectoryType TrajectoryType { get; private set; }

        public float Height
        {
            get { return _height; }
        }

        public Vector2 PhysicsPosition
        {
            get
            {
                return _rigidbody != null
                    ? _rigidbody.position
                    : (Vector2)transform.position;
            }
        }

        public Vector2 CatchVisualPosition
        {
            get
            {
                if (_presentation.Visual == null) return transform.position;
                Vector3 visualLocalPosition = IsAirborne
                    ? _presentation.RestPosition + Vector3.up * _height
                    : _presentation.RestPosition;
                return transform.TransformPoint(visualLocalPosition);
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
            _carryable = GetComponent<CarryableObject>();
            _presentation = GetComponent<RatPresenter>();
            _fusion = GetComponent<RatCollisionFusion>();

            _carryable.RememberWorldParent();

            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;

            _originalMaterial = _collider.sharedMaterial;

            _flightMaterial = new PhysicsMaterial2D("Carryable Flight")
            {
                bounciness = _wallRestitution,
                friction = 0f
            };

            if (_presentation == null || !_presentation.InitializeMotionVisual())
            {
                Debug.LogError("[RatFlightMotion] RatPresenter is required.", this);
                enabled = false;
                return;
            }

            CompleteSettle(false);
        }

        private void FixedUpdate()
        {
            if (!IsAirborne || _isSnapping || _isHitStopped)
            {
                return;
            }

            if (IsCannonFlight)
            {
                UpdateCannonFlight();
                return;
            }

            // 단계별 비행 감속은 현재 사용하지 않는다.
            // B, BB, BBB 모두 같은 수평 속도를 유지한다.
            // ApplyRankFlightDeceleration();
            UpdateThrowFlight();
            if (!IsAirborne) return;
            UpdateCatchArc();
            _velocityBeforePhysicsStep = _rigidbody.linearVelocity;
            if (_fusion != null && _fusion.isActiveAndEnabled && _fusion.TryResolveOverlap())
            {
                return;
            }

            ConstrainToGround();
        }

        // --------------------------------------------------------------------
        // Reset
        // --------------------------------------------------------------------

        internal void ResetForRat(Tilemap groundTilemap)
        {
            ResetMotion();
            _groundTilemap = groundTilemap;
            _carryable.RememberWorldParent();
            _rigidbody.simulated = true;
        }

        private void ResetMotion()
        {
            CancelSnap();
            _carryable.CompleteCatchTween();
            ResetBatFlightState();
            RestoreThrowerCollisions();

            SetState(CarryState.Grounded, FlightType.None);

            _height = 0f;
            _verticalSpeed = 0f;
            _peakHeight = 0f;
            _hasReachedPeak = false;
            _cannonFlightTime = 0f;
            _cannonFlightDuration = 0f;
            _cannonArcHeight = 0f;
            _cannonStartPosition = Vector2.zero;
            _cannonTargetPosition = Vector2.zero;
            TrajectoryType = default;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.simulated = false;

            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;

            _presentation.ResetVisualHeight();

            // Reset is not a landing, impact, or carry transition.
        }

        // --------------------------------------------------------------------
        // Physical carry preparation; attachment belongs to CarryableObject
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        internal void PrepareCarry()
        {
            CancelSnap();
            ResetBatFlightState();
            SetState(CarryState.Carried, FlightType.None);
            _height = 0f;
            _verticalSpeed = 0f;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.simulated = false;
            RefreshRatCollisionPairs();
            _presentation.ResetVisualHeight();
        }

        // Player Throw
        // --------------------------------------------------------------------

        internal bool TryThrow(
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

            CancelSnap();
            _carryable.CompleteCatchTween();
            RestoreThrowerCollisions();

            float initialHeight =
                Mathf.Max(0f, transform.position.y - groundPosition.y);

            _carryable.Detach();
            transform.position = groundPosition;
            _rigidbody.position = groundPosition;

            SetState(CarryState.Airborne, FlightType.Throw);

            _height = initialHeight;
            _verticalSpeed = _upwardSpeed;
            BeginCatchArc();

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.linearDamping = 0f;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = true;

            _collider.isTrigger = false;
            _flightMaterial.bounciness = _wallRestitution;
            _collider.sharedMaterial = _flightMaterial;
            RefreshRatCollisionPairs();

            IgnoreThrowerCollisions(throwerColliders);

            // Rat 상태 전환 중 Ground AI가 기존 지상 이동 속도를 정리한 뒤
            // 실제 Throw impulse를 마지막에 적용합니다.

            if (!TrySetGridLandingVelocity(direction, _throwCellDistance))
            {
                _rigidbody.linearVelocity = direction.normalized * _throwSpeed;
            }

            return true;
        }

        public bool TryMoveOnGround(Vector2 position)
        {
            if (!isActiveAndEnabled
                || _rigidbody == null
                || IsCarried
                || IsAirborne
                || IsLoaded
                || IsCannonFlight
                || _isSnapping)
            {
                return false;
            }

            if (_groundTilemap == null)
            {
                return false;
            }

            bool hitX;
            bool hitY;
            Vector2 bounded = ResolveGroundMotion(
                _rigidbody.position,
                position,
                out hitX,
                out hitY);

            if (hitX
                || hitY
                || (bounded - position).sqrMagnitude > 0.000001f)
            {
                return false;
            }

            _rigidbody.MovePosition(position);
            return true;
        }

        internal bool TryDispense(
            Tilemap groundTilemap,
            Vector2 direction,
            float speed)
        {
            if (!isActiveAndEnabled
                || IsCarried
                || IsLoaded
                || IsCannonFlight
                || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            CancelSnap();
            RestoreThrowerCollisions();

            _groundTilemap = groundTilemap;

            SetState(CarryState.Airborne, FlightType.Throw);

            _height = 0f;
            _verticalSpeed = _upwardSpeed;

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.linearDamping = 0f;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = true;

            _collider.isTrigger = false;
            _flightMaterial.bounciness = _wallRestitution;
            _collider.sharedMaterial = _flightMaterial;
            RefreshRatCollisionPairs();

            _rigidbody.linearVelocity =
                direction.normalized * Mathf.Max(0f, speed);
            _velocityBeforePhysicsStep = _rigidbody.linearVelocity;

            return true;
        }

        /// <summary>
        /// 빠따 발사 정보를 비행 객체에 기록한다. 풀차지는 로컬 히트스톱이 끝난 뒤 출발한다.
        /// </summary>
        internal bool TryBatLaunch(
            Tilemap groundTilemap,
            Vector2 direction,
            float horizontalSpeed,
            float verticalSpeed,
            int landingCellDistance,
            float collisionFusionMinimumSpeed,
            int swingId,
            bool isFullCharge,
            Transform returnTarget)
        {
            // 벽 팝업이나 기존 히트스톱 중 다시 맞으면 이전 비행 문맥을 먼저 끝낸다.
            // 이전 코루틴이 새 타격 속도를 나중에 덮어쓰는 것을 방지한다.
            ResetBatFlightState();

            if (!TryDispense(groundTilemap, direction, horizontalSpeed))
            {
                return false;
            }

            SetState(CarryState.Airborne, FlightType.Bat);

            _batSwingId = swingId;
            _canCollisionFuse = isFullCharge;
            _collisionFusionMinimumSpeedForFlight =
                Mathf.Max(0f, collisionFusionMinimumSpeed);
            _batReturnTarget = returnTarget;
            _verticalSpeed = Mathf.Max(0f, verticalSpeed);
            BeginCatchArc();

            if (isFullCharge)
            {
                _batFlightPhase = BatFlightPhase.FullChargeRoute;
                Vector2 launchVelocity =
                    direction.normalized * Mathf.Max(0f, horizontalSpeed);
                BeginImpactStop(launchVelocity, -direction.normalized);
            }
            else
            {
                _batFlightPhase = BatFlightPhase.NormalKnockback;
                TrySetGridLandingVelocity(direction, landingCellDistance);
            }

            return true;
        }

        public float HeightGravity { get { return _heightGravity; } }

        /// <summary>Airborne Rat을 머리 슬롯에 붙이는 전용 경로다.</summary>

        /// <summary>
        /// 충돌 합성 결과를 원본 Rat의 방향과 속도로 계속 비행시킨다.
        /// 벽 팝업 전에는 풀차지 비행 문맥도 전달해 다음 Rat과 연쇄 합성할 수 있게 한다.
        /// </summary>
        internal void BeginCollisionFusionFlight(
            Tilemap groundTilemap,
            Vector3 groundPosition,
            float height,
            float verticalSpeed,
            Vector2 velocity,
            int swingId,
            float collisionFusionMinimumSpeed,
            Transform returnTarget)
        {
            CancelSnap();
            ResetBatFlightState();
            RestoreThrowerCollisions();

            _groundTilemap = groundTilemap;
            _carryable.RememberWorldParent();
            transform.position = groundPosition;
            _rigidbody.position = groundPosition;

            SetState(CarryState.Airborne, FlightType.Bat);
            _height = Mathf.Max(0f, height);
            _verticalSpeed = verticalSpeed;
            BeginCatchArc();
            _batSwingId = swingId;
            _batReturnTarget = returnTarget;
            _batFlightPhase = BatFlightPhase.FullChargeRoute;
            _canCollisionFuse = true;
            _collisionFusionMinimumSpeedForFlight =
                Mathf.Max(0f, collisionFusionMinimumSpeed);

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.linearDamping = 0f;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = true;
            _collider.isTrigger = false;
            _flightMaterial.bounciness = _wallRestitution;
            _collider.sharedMaterial = _flightMaterial;
            RefreshRatCollisionPairs();

            _rigidbody.linearVelocity = velocity;
            _velocityBeforePhysicsStep = velocity;
        }

        internal void BeginRatFall(
            Tilemap groundTilemap,
            float initialHeight)
        {
            CancelSnap();

            _groundTilemap = groundTilemap;

            SetState(CarryState.Airborne, FlightType.Bat);

            _height = Mathf.Max(0f, initialHeight);
            _verticalSpeed = 0f;
            BeginCatchArc();

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.simulated = true;
            _rigidbody.linearVelocity = Vector2.zero;

            _collider.isTrigger = true;
            _collider.sharedMaterial = _flightMaterial;

        }

        private void BeginCatchArc()
        {
            float gravity = Mathf.Max(0.01f, _heightGravity);
            float upwardSpeed = Mathf.Max(0f, _verticalSpeed);
            _peakHeight = _height + upwardSpeed * upwardSpeed / (2f * gravity);
            _hasReachedPeak = _verticalSpeed <= 0f;
        }

        private void UpdateCatchArc()
        {
            if (!_hasReachedPeak && _verticalSpeed <= 0f)
            {
                _hasReachedPeak = true;
            }
        }

        private bool TrySetGridLandingVelocity(Vector2 direction, int cellDistance)
        {
            if (_groundTilemap == null || cellDistance <= 0)
            {
                return false;
            }

            Vector3Int cellDirection = new Vector3Int(
                Mathf.RoundToInt(direction.x),
                Mathf.RoundToInt(direction.y),
                0);
            if (cellDirection == Vector3Int.zero)
            {
                return false;
            }

            Vector3Int startCell = _groundTilemap.WorldToCell(PhysicsPosition);
            Vector3Int targetCell = startCell + cellDirection * cellDistance;
            Vector2 targetPosition = _groundTilemap.GetCellCenterWorld(targetCell);
            targetPosition = FindNearestValidGroundPosition(_groundTilemap, targetPosition);

            float gravity = Mathf.Max(0.01f, _heightGravity);
            float impactSpeed = Mathf.Sqrt(
                _verticalSpeed * _verticalSpeed + 2f * gravity * Mathf.Max(0f, _height));
            float flightTime = (_verticalSpeed + impactSpeed) / gravity;
            if (flightTime <= 0.001f)
            {
                return false;
            }

            _rigidbody.linearVelocity = (targetPosition - PhysicsPosition) / flightTime;
            _velocityBeforePhysicsStep = _rigidbody.linearVelocity;
            return true;
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

                if (_batFlightPhase == BatFlightPhase.FullChargeRoute)
                {
                    // 풀차지 비행은 벽에 도달하기 전까지 지면 착지로 종료하지 않는다.
                    // 가상 높이만 지면에 고정하고 수평 운동량은 그대로 유지한다.
                    _verticalSpeed = 0f;
                    break;
                }

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

            CollectOccupiedCells();

            Vector2 expectedPosition =
                _rigidbody.position
                + _rigidbody.linearVelocity
                * GetRemainingTravelTime();

            expectedPosition = TraceGround(
                _rigidbody.position,
                expectedPosition);

            Vector3 worldPosition = new Vector3(
                expectedPosition.x,
                expectedPosition.y,
                transform.position.z);

            Vector3Int cell =
                _groundTilemap.WorldToCell(worldPosition);

            if (!IsAvailableCell(cell)
                && !TryFindNearestEmptyCell(cell, out cell))
            {
                return;
            }

            SetSnapTarget(cell);
        }

        private void CollectOccupiedCells()
        {
            _occupiedCells.Clear();

            foreach (RatFlightMotion other in ActiveItems)
            {
                if (other == null
                    || other == this
                    || !other.isActiveAndEnabled
                    || other._groundTilemap != _groundTilemap)
                {
                    continue;
                }

                if (other.IsCarried
                    || other.IsLoaded
                    || other.IsCannonFlight)
                {
                    continue;
                }

                if (!other.IsAirborne)
                {
                    _occupiedCells.Add(
                        _groundTilemap.WorldToCell(other.transform.position));
                }

                if (other._hasSnapTarget)
                {
                    _occupiedCells.Add(
                        _groundTilemap.WorldToCell(other._snapTarget));
                }
            }
        }

        private bool TryFindNearestEmptyCell(
            Vector3Int start,
            out Vector3Int result)
        {
            _searchQueue.Clear();
            _visitedCells.Clear();

            _searchQueue.Enqueue(start);
            _visitedCells.Add(start);

            Vector3Int[] directions =
            {
                Vector3Int.right,
                Vector3Int.left,
                Vector3Int.up,
                Vector3Int.down,
                new Vector3Int(1, 1, 0),
                new Vector3Int(1, -1, 0),
                new Vector3Int(-1, 1, 0),
                new Vector3Int(-1, -1, 0)
            };

            while (_searchQueue.Count > 0)
            {
                Vector3Int current = _searchQueue.Dequeue();

                if (IsAvailableCell(current))
                {
                    result = current;
                    return true;
                }

                for (int index = 0; index < directions.Length; index++)
                {
                    Vector3Int next = current + directions[index];

                    if (_visitedCells.Contains(next)
                        || !_groundTilemap.HasTile(next))
                    {
                        continue;
                    }

                    _visitedCells.Add(next);
                    _searchQueue.Enqueue(next);
                }
            }

            result = default;
            return false;
        }

        private bool IsAvailableCell(Vector3Int cell)
        {
            return _groundTilemap.HasTile(cell)
                && !_occupiedCells.Contains(cell)
                && IsGroundFootprintValid(_groundTilemap.GetCellCenterWorld(cell));
        }

        private void SetSnapTarget(Vector3Int cell)
        {
            _snapTarget = _groundTilemap.GetCellCenterWorld(cell);
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

        private Vector3Int GroundCell(Vector2 position)
        {
            Vector3 worldPosition = new Vector3(
                position.x,
                position.y,
                transform.position.z);

            return _groundTilemap.WorldToCell(worldPosition);
        }

        private Vector2 TraceGround(Vector2 start, Vector2 end)
        {
            bool hitX;
            bool hitY;

            return ResolveGroundMotion(
                start,
                end,
                out hitX,
                out hitY);
        }

        private Vector2 ResolveGroundMotion(
            Vector2 start,
            Vector2 end,
            out bool hitX,
            out bool hitY)
        {
            Vector3 origin =
                _groundTilemap.GetCellCenterWorld(Vector3Int.zero);

            float cellWidth = Vector3.Distance(
                origin,
                _groundTilemap.GetCellCenterWorld(Vector3Int.right));

            float cellHeight = Vector3.Distance(
                origin,
                _groundTilemap.GetCellCenterWorld(Vector3Int.up));

            float step = Mathf.Max(
                0.001f,
                Mathf.Min(cellWidth, cellHeight) * 0.1f);

            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(Vector2.Distance(start, end) / step));

            Vector2 resolved = start;
            Vector2 segment = (end - start) / steps;
            bool moveX = Mathf.Abs(segment.x) > 0.000001f;
            bool moveY = Mathf.Abs(segment.y) > 0.000001f;

            hitX = false;
            hitY = false;

            for (int index = 1; index <= steps; index++)
            {
                if (moveX && !hitX)
                {
                    Vector2 xCandidate =
                        resolved + new Vector2(segment.x, 0f);

                    if (IsGroundFootprintValid(xCandidate))
                    {
                        resolved.x = xCandidate.x;
                    }
                    else
                    {
                        hitX = true;
                    }
                }

                if (moveY && !hitY)
                {
                    Vector2 yCandidate =
                        resolved + new Vector2(0f, segment.y);

                    if (IsGroundFootprintValid(yCandidate))
                    {
                        resolved.y = yCandidate.y;
                    }
                    else
                    {
                        hitY = true;
                    }
                }

                if ((!moveX || hitX)
                    && (!moveY || hitY))
                {
                    break;
                }
            }

            return resolved;
        }

        private bool IsGroundFootprintValid(Vector2 bodyPosition)
        {
            if (_groundTilemap == null || _collider == null)
            {
                return false;
            }

            Bounds bounds = _collider.bounds;
            Vector2 currentBodyPosition = _rigidbody.position;
            Vector2 positionOffset = bodyPosition - currentBodyPosition;
            Vector2 center = (Vector2)bounds.center + positionOffset;
            Vector2 extents = bounds.extents;

            if (!HasGroundAt(center))
            {
                return false;
            }

            return HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(-extents.x, 0f))
                && HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(extents.x, 0f))
                && HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(0f, -extents.y))
                && HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(0f, extents.y))
                && HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(-extents.x, -extents.y))
                && HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(-extents.x, extents.y))
                && HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(extents.x, -extents.y))
                && HasGroundAtFootprintPoint(
                       center,
                       positionOffset,
                       new Vector2(extents.x, extents.y));
        }

        /// <summary>
        /// 합성으로 Collider 크기가 바뀐 직후에도 전체 발자국이 타일 안에 들어오는 가장 가까운 위치를 찾는다.
        /// 유효한 셀 중심을 찾은 뒤 원래 합성 지점 쪽으로 보간해 불필요한 위치 이동을 줄인다.
        /// </summary>
        public Vector2 FindNearestValidGroundPosition(
            Tilemap groundTilemap,
            Vector2 desiredPosition)
        {
            _groundTilemap = groundTilemap;
            // 풀에서 꺼내 이동한 직후에도 새 위치와 크기로 물리 쿼리를 수행한다.
            Physics2D.SyncTransforms();
            if (_groundTilemap == null || IsGroundFootprintValid(desiredPosition))
            {
                return desiredPosition;
            }

            Vector2 nearestValidPosition = desiredPosition;
            float nearestDistance = float.PositiveInfinity;

            foreach (Vector3Int cell in _groundTilemap.cellBounds.allPositionsWithin)
            {
                if (!_groundTilemap.HasTile(cell))
                {
                    continue;
                }

                Vector2 candidate = _groundTilemap.GetCellCenterWorld(cell);
                if (!IsGroundFootprintValid(candidate))
                {
                    continue;
                }

                float distance = (candidate - desiredPosition).sqrMagnitude;
                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestValidPosition = candidate;
            }

            if (float.IsPositiveInfinity(nearestDistance))
            {
                return desiredPosition;
            }

            Vector2 validPosition = nearestValidPosition;
            Vector2 invalidPosition = desiredPosition;
            const int interpolationSteps = 8;
            for (int index = 0; index < interpolationSteps; index++)
            {
                Vector2 candidate = Vector2.Lerp(validPosition, invalidPosition, 0.5f);
                if (IsGroundFootprintValid(candidate))
                {
                    validPosition = candidate;
                }
                else
                {
                    invalidPosition = candidate;
                }
            }

            return validPosition;
        }

        private bool HasGroundAtFootprintPoint(
            Vector2 predictedCenter,
            Vector2 positionOffset,
            Vector2 boundsOffset)
        {
            Vector2 currentBoundsCenter =
                predictedCenter - positionOffset;

            Vector2 closest = _collider.ClosestPoint(
                currentBoundsCenter + boundsOffset);

            Vector2 predictedPoint = closest + positionOffset;
            predictedPoint = Vector2.MoveTowards(
                predictedPoint,
                predictedCenter,
                0.001f);

            return HasGroundAt(predictedPoint);
        }

        private bool HasGroundAt(Vector2 point)
        {
            return _groundTilemap.HasTile(GroundCell(point));
        }

        private void ConstrainToGround()
        {
            if (_groundTilemap == null)
            {
                return;
            }

            Vector2 position = _rigidbody.position;

            if (!_hasGroundPosition || !IsGroundFootprintValid(_lastGroundPosition))
            {
                _lastGroundPosition = FindNearestValidGroundPosition(
                    _groundTilemap,
                    position);
                _hasGroundPosition = IsGroundFootprintValid(_lastGroundPosition);
                if (!_hasGroundPosition)
                {
                    _rigidbody.linearVelocity = Vector2.zero;
                    return;
                }
            }

            bool currentHitX;
            bool currentHitY;
            Vector2 allowed = ResolveGroundMotion(
                _lastGroundPosition,
                position,
                out currentHitX,
                out currentHitY);

            if ((allowed - position).sqrMagnitude > 0.000001f)
            {
                _rigidbody.position = allowed;
            }

            _lastGroundPosition = allowed;

            Vector2 next =
                allowed
                + _rigidbody.linearVelocity * Time.fixedDeltaTime;

            bool nextHitX;
            bool nextHitY;
            ResolveGroundMotion(
                allowed,
                next,
                out nextHitX,
                out nextHitY);

            Vector2 velocity = _rigidbody.linearVelocity;
            float restitution = Mathf.Clamp01(_wallRestitution);
            bool hitX = currentHitX || nextHitX;
            bool hitY = currentHitY || nextHitY;
            bool hitWall = hitX || hitY;

            if (hitWall
                && _batSwingId != 0
                && _batFlightPhase == BatFlightPhase.FullChargeRoute)
            {
                Vector2 impactNormal = GetImpactNormal(hitX, hitY, velocity);
                float incomingSpeed = velocity.magnitude;
                Vector2 popupVelocity = CalculateWallPopupVelocity(allowed, incomingSpeed);
                _batFlightPhase = BatFlightPhase.WallReturn;

                if (incomingSpeed >= _impactStopSpeedThreshold)
                {
                    BeginImpactStop(popupVelocity, impactNormal);
                }
                else
                {
                    _rigidbody.linearVelocity = popupVelocity;
                    _velocityBeforePhysicsStep = popupVelocity;
                }

                return;
            }

            if (hitWall
                && _batSwingId != 0
                && _batFlightPhase == BatFlightPhase.WallReturn)
            {
                if (hitX)
                {
                    velocity.x = 0f;
                }

                if (hitY)
                {
                    velocity.y = 0f;
                }

                _rigidbody.linearVelocity = velocity;
                return;
            }

            if (hitX)
            {
                velocity.x = -velocity.x * restitution;
            }

            if (hitY)
            {
                velocity.y = -velocity.y * restitution;
            }

            _rigidbody.linearVelocity = velocity;
        }

        /// <summary>
        /// 풀차지 비행의 첫 벽 충돌 운동량을 수직 팝업과 플레이어 쪽 회수 이동으로 변환한다.
        /// </summary>
        private Vector2 CalculateWallPopupVelocity(
            Vector2 wallPosition,
            float incomingSpeed)
        {
            float minimumPopupSpeed = Mathf.Max(0f, _minimumPopupVerticalSpeed);
            float maximumPopupSpeed = Mathf.Max(minimumPopupSpeed, _maximumPopupVerticalSpeed);
            float popupVerticalSpeed = Mathf.Clamp(
                incomingSpeed * Mathf.Max(0f, _popupVerticalSpeedMultiplier),
                minimumPopupSpeed,
                maximumPopupSpeed);
            if (_rat != null && _rat.Definition != null)
            {
                popupVerticalSpeed *= _rat.Definition.VerticalImpulseMultiplier;
            }
            _verticalSpeed = popupVerticalSpeed;
            BeginCatchArc();

            if (_batReturnTarget == null)
            {
                return Vector2.zero;
            }

            Vector2 playerPosition = _batReturnTarget.position;
            Vector2 desiredLandingPosition = Vector2.Lerp(
                wallPosition,
                playerPosition,
                Mathf.Clamp01(_popupPlayerBlend));
            desiredLandingPosition = FindNearestValidGroundPosition(
                _groundTilemap,
                desiredLandingPosition);

            float gravity = Mathf.Max(0.01f, _heightGravity);
            float impactSpeed = Mathf.Sqrt(
                popupVerticalSpeed * popupVerticalSpeed
                + 2f * gravity * Mathf.Max(0f, _height));
            float flightTime = Mathf.Max(
                0.01f,
                (popupVerticalSpeed + impactSpeed) / gravity);
            Vector2 returnVelocity =
                (desiredLandingPosition - wallPosition) / flightTime;
            return Vector2.ClampMagnitude(
                returnVelocity,
                Mathf.Max(0f, _maximumPopupReturnSpeed));
        }

        internal void RelocateAirborne(Vector2 position)
        {
            if (!IsAirborne || IsCannonFlight)
            {
                return;
            }

            CancelSnap();

            transform.position = position;
            _rigidbody.position = position;
            _rigidbody.linearVelocity = Vector2.zero;

            _lastGroundPosition = position;
            _hasGroundPosition = IsGroundFootprintValid(position);
        }

        private void PrepareGroundPhysics()
        {
            _height = 0f;
            _verticalSpeed = 0f;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            _rigidbody.simulated = true;

            _collider.isTrigger = true;
            _collider.sharedMaterial = _originalMaterial;

            RestoreThrowerCollisions();
        }

        internal void CompleteSettle(bool notify)
        {
            CancelSnap();
            ResetBatFlightState();

            PrepareGroundPhysics();

            SetState(CarryState.Grounded, FlightType.None);
            RefreshRatCollisionPairs();

            _hasSnapTarget = false;

            _presentation.ResetVisualHeight();

            _carryable.NotifyGroundSorting(); // �ٴڿ� ������ �������� ��

            if (notify) Settled?.Invoke();
        }

        // --------------------------------------------------------------------
        // Cannon
        // --------------------------------------------------------------------

        internal bool TryEnterCannon(
            Transform storagePoint, bool fromIdle = false)
        {
            if (!isActiveAndEnabled
                || (!IsAirborne && !fromIdle)
                || IsCarried
                || IsLoaded
                || IsCannonFlight
                || storagePoint == null
                || storagePoint.IsChildOf(transform))
            {
                return false;
            }

            CancelSnap();
            ResetBatFlightState();
            RestoreThrowerCollisions();
            _carryable.NotifyGroundSorting();

            SetState(CarryState.Loaded, FlightType.None);

            _height = 0f;
            _verticalSpeed = 0f;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = false;

            transform.SetParent(storagePoint, true);
            transform.localPosition = Vector3.zero;

            _presentation.ResetVisualHeight();

            return true;
        }

        internal void LaunchFromCannon(
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

        internal void LaunchFromCannon(
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

            _carryable.Detach();

            transform.position = muzzlePosition;
            _rigidbody.position = muzzlePosition;

            SetState(CarryState.Airborne, FlightType.Cannon);
            RefreshRatCollisionPairs();

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

        }

        private void UpdateCannonFlight()
        {
            _cannonFlightTime += Time.fixedDeltaTime;

            float progress = Mathf.Clamp01(
                _cannonFlightTime
                / _cannonFlightDuration);

            Vector2 groundPosition = Vector2.Lerp(
                _cannonStartPosition,
                _cannonTargetPosition,
                progress);

            _height =
                TrajectoryType == CannonTrajectoryType.Arc
                    ? 4f
                      * _cannonArcHeight
                      * progress
                      * (1f - progress)
                    : 0f;

            Vector2 flightPosition = groundPosition
                + Vector2.up * _height;

            _rigidbody.MovePosition(flightPosition);

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

        // --------------------------------------------------------------------
        // Full-charge impact / collision fusion
        // --------------------------------------------------------------------

        /// <summary>
        /// Rat Collider끼리는 물리 충돌하지 않는다. 풀차지 Rat이 첫 벽 팝업에 도달하기 전
        /// 충분한 속도로 다른 Rat과 겹쳤을 때만 연쇄 합성 조건을 검사한다.
        /// </summary>

        private void ApplyRankFlightDeceleration()
        {
            if (_rat == null || _rat.Definition == null)
            {
                return;
            }

            int additionalRanks = Mathf.Max(0, (int)_rat.Definition.Rank - 1);
            if (additionalRanks == 0)
            {
                return;
            }

            float deceleration = _flightDecelerationPerRank * additionalRanks;
            _rigidbody.linearVelocity = Vector2.MoveTowards(
                _rigidbody.linearVelocity,
                Vector2.zero,
                deceleration * Time.fixedDeltaTime);
        }

        private void BeginImpactStop(
            Vector2 resumeVelocity,
            Vector2 impactNormal)
        {
            if (_impactRoutine != null)
            {
                StopCoroutine(_impactRoutine);
            }

            _impactRoutine = StartCoroutine(ImpactStopRoutine(
                resumeVelocity,
                impactNormal));
        }

        private IEnumerator ImpactStopRoutine(
            Vector2 resumeVelocity,
            Vector2 impactNormal)
        {
            _isHitStopped = true;
            _rigidbody.linearVelocity = Vector2.zero;
            _presentation.ApplySquash(impactNormal);

            yield return new WaitForSeconds(Mathf.Max(0f, _impactStopDuration));

            float recoveryDuration = Mathf.Max(0.001f, _squashRecoveryDuration);
            float elapsed = 0f;
            Vector3 squashedScale = _presentation.Visual.localScale;
            while (elapsed < recoveryDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / recoveryDuration);
                _presentation.Visual.localScale = Vector3.Lerp(squashedScale, _presentation.RestScale, progress);
                yield return null;
            }

            _presentation.ResetVisualScale();
            _isHitStopped = false;
            _rigidbody.linearVelocity = resumeVelocity;
            _velocityBeforePhysicsStep = resumeVelocity;
            _impactRoutine = null;
        }

        private Vector2 GetImpactNormal(bool hitX, bool hitY, Vector2 velocity)
        {
            Vector2 normal = Vector2.zero;
            if (hitX)
            {
                if (velocity.x > 0f)
                {
                    normal.x = -1f;
                }
                else
                {
                    normal.x = 1f;
                }
            }

            if (hitY)
            {
                if (velocity.y > 0f)
                {
                    normal.y = -1f;
                }
                else
                {
                    normal.y = 1f;
                }
            }

            return normal.normalized;
        }

        private void ResetBatFlightState()
        {
            if (_impactRoutine != null)
            {
                StopCoroutine(_impactRoutine);
                _impactRoutine = null;
            }

            _isHitStopped = false;
            if (_fusion != null) _fusion.ResetFusion();
            _batSwingId = 0;
            _batFlightPhase = BatFlightPhase.None;
            _canCollisionFuse = false;
            _collisionFusionMinimumSpeedForFlight = 0f;
            _batReturnTarget = null;
            _velocityBeforePhysicsStep = Vector2.zero;
            _presentation.ResetVisualScale();
        }

        /// <summary>
        /// Rat끼리는 물리적으로 밀거나 도탄시키지 않는다. 합성은 별도의 겹침 검사로 처리한다.
        /// </summary>
        private static void RefreshRatCollisionPairs()
        {
            foreach (RatFlightMotion first in ActiveItems)
            {
                if (first == null || first._rat == null || first._collider == null)
                {
                    continue;
                }

                foreach (RatFlightMotion second in ActiveItems)
                {
                    if (second == null
                        || second._rat == null
                        || second._collider == null
                        || first.GetInstanceID() >= second.GetInstanceID())
                    {
                        continue;
                    }

                    // 일반 Rat끼리는 통과시키되, 대포 비행 중인 Rat은
                    // Projectile의 Trigger 충돌과 상쇄 판정을 받을 수 있어야 한다.
                    bool shouldIgnore = !first.IsCannonFlight
                        && !second.IsCannonFlight;
                    Physics2D.IgnoreCollision(
                        first._collider,
                        second._collider,
                        shouldIgnore);
                }
            }
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

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
            _hasGroundPosition = false;

        }

        private void OnDisable()
        {
            _carryable.NotifyGroundSorting();
            ResetMotion();
            _carryable.ResetCarry();
            _groundTilemap = null;
            ActiveItems.Remove(this);
            RefreshRatCollisionPairs();
        }

        private void OnEnable()
        {
            ActiveItems.Add(this);
            RefreshRatCollisionPairs();
        }

        private void OnDestroy()
        {
            CancelSnap();
            _carryable.CompleteCatchTween();
            RestoreThrowerCollisions();

            if (_flightMaterial != null)
            {
                Destroy(_flightMaterial);
            }
        }
    }
}
