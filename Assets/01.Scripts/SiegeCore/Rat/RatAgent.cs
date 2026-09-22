using System;
using System.Collections.Generic;
using SiegeCore.Cannon;
using SiegeCore.Combat;
using SiegeCore.Player;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(CarryableObject))]
    public sealed class RatAgent : MonoBehaviour, IDamageable
    {
        public static readonly HashSet<RatAgent> Active = new HashSet<RatAgent>();

        private const int StateHistoryCapacity = 16;

        [Serializable]
        public struct StateTransition
        {
            public float Time;
            public int Frame;
            public RatState PreviousState;
            public RatState NextState;

            public StateTransition(
                float time,
                int frame,
                RatState previousState,
                RatState nextState)
            {
                Time = time;
                Frame = frame;
                PreviousState = previousState;
                NextState = nextState;
            }
        }

        [Header("Definition")]
        [SerializeField] private RatDefinition _definition;

        [Header("Groggy")]
        [SerializeField, Min(0f)] private float _allyGroggyDuration = 0.2f;
        [SerializeField, Min(0f)] private float _enemyGroggyDuration = 3f;

        private CarryableObject _carryable;
        private RatFlightMotion _motion;
        public RatFlightMotion Motion { get { return _motion; } }
        public bool IsSpawned { get; private set; }
        private RatFactory _factory;
        private RatGroundBehaviour _groundBehaviour;
        public RatGroundBehaviour GroundBehaviour { get { return _groundBehaviour; } }
        private GroundBlocker _groundBlocker;
        private GroundBlockTarget _groundBlockTarget;
        private PooledObject _pooledObject;
        private SiegeCore.Projectile.Projectile _projectile;

        private RatState _state;
        private RatState _groundReturnState = RatState.Idle;
        private RatState _landingState = RatState.Idle;

        private float _landingGroggyDuration;
        private float _groggyUntil;

        [NonSerialized]
        private readonly List<StateTransition> _stateHistory =
            new List<StateTransition>(StateHistoryCapacity);

        public event Action<RatAgent, RatState, RatState> StateChanged;
        public event Action<RatAgent> Died;
        public event Action<RatAgent> Released;

        public RatDefinition Definition
        {
            get { return _definition; }
        }

        public CarryableObject Carryable
        {
            get { return _carryable; }
        }

        public RatFactory Factory
        {
            get { return _factory; }
        }

        public RatState State
        {
            get { return _state; }
        }

        public IReadOnlyList<StateTransition> StateHistory
        {
            get { return _stateHistory; }
        }

        public RatCondition Condition
        {
            get
            {
                if (_state == RatState.Dead) return RatCondition.Dead;
                if (_state == RatState.Groggy) return RatCondition.Groggy;
                return RatCondition.Normal;
            }
        }

        public RatGroundMode GroundMode
        {
            get
            {
                return _groundReturnState == RatState.GroundCombat
                    ? RatGroundMode.Combat
                    : RatGroundMode.Idle;
            }
        }

        public VehicleSide Faction { get; private set; }

        public VehicleSide Side
        {
            get { return Faction; }
        }

        public bool IsDead
        {
            get { return _state == RatState.Dead; }
        }

        // ���� Projectile���� ���.
        public VehicleSide AttackSide { get; private set; }
        public CannonSlot ProjectileSourceSlot { get; private set; }

        public float Health { get; private set; }

        public bool IsGroggy
        {
            get { return _state == RatState.Groggy; }
        }

        public bool CanFight
        {
            get { return _state == RatState.GroundCombat; }
        }

        public GroundBlocker GroundBlocker
        {
            get { return _groundBlocker; }
        }

        public GroundBlockTarget GroundBlockTarget
        {
            get { return _groundBlockTarget; }
        }

        /// <summary>지상 행동 중이거나 빠따 비행 중인 Rat을 타격할 수 있다.</summary>
        public bool CanBeHitByBat
        {
            get
            {
                return _state == RatState.Idle
                    || _state == RatState.GroundCombat
                    || _state == RatState.Groggy
                    || _state == RatState.Airborne;
            }
        }

        /// <summary>적 Rat은 빠따로 부여된 제압 시간이 남은 공중 상태에서만 받을 수 있다.</summary>
        public bool CanBeCaught
        {
            get
            {
                if (_state != RatState.Airborne || !_motion.CanBeCaughtInFlight)
                {
                    return false;
                }

                if (Faction == VehicleSide.Ally)
                {
                    return true;
                }

                return _groggyUntil > Time.time;
            }
        }

        public bool CanPickUp
        {
            get
            {
                if (_state == RatState.Dead
                    || _state == RatState.Carried
                    || _state == RatState.Airborne
                    || _state == RatState.Loaded
                    || _state == RatState.CannonFlight)
                {
                    return false;
                }

                if (Faction == VehicleSide.Enemy)
                {
                    return _state == RatState.Groggy;
                }

                return _state == RatState.Idle
                    || _state == RatState.GroundCombat
                    || _state == RatState.Groggy;
            }
        }

        private void Awake()
        {
            _carryable = GetComponent<CarryableObject>();
            _motion = GetComponent<RatFlightMotion>();
            _groundBehaviour = GetComponent<RatGroundBehaviour>();
            _groundBlocker = GetComponent<GroundBlocker>();
            _groundBlockTarget = GetComponent<GroundBlockTarget>();
            _pooledObject = GetComponent<PooledObject>();
            _projectile = GetComponent<SiegeCore.Projectile.Projectile>();

            if (_definition == null || _motion == null)
            {
                Debug.LogError("[RatAgent] RatDefinition and RatFlightMotion are required.", this);
                enabled = false;
                return;
            }

            _motion.Settled += HandleGrounded;
        }

        private void OnEnable()
        {
            _stateHistory.Clear();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            IsSpawned = false;
            Released?.Invoke(this);
            if (_groundBehaviour != null) _groundBehaviour.ResetForSpawn();
            // Lifecycle cleanup must not publish gameplay transitions.
            _state = RatState.Idle;
            _groundReturnState = RatState.Idle;
            _landingState = RatState.Idle;
            _groggyUntil = 0f;
            _landingGroggyDuration = 0f;

            ProjectileSourceSlot = null;
            AttackSide = Faction;
            Health = 0f;
            _factory = null;

            if (_groundBlocker != null)
            {
                _groundBlocker.ReleaseAll();
            }

            if (_groundBlockTarget != null)
            {
                _groundBlockTarget.ReleaseBlocker();
            }
        }

        internal void CompleteSpawn()
        {
            IsSpawned = true;
            Active.Add(this);
            if (_groundBehaviour != null && _groundBehaviour.enabled) _groundBehaviour.OnRatStateChanged(_state, _state);
            StateChanged?.Invoke(this, _state, _state);
        }

        public bool CanLoadIntoCannon
        {
            get { return IsSpawned && !IsDead && _motion.CanEnterCannon; }
        }

        public bool TryCatch(Transform holdPoint, float duration)
        {
            if (!IsSpawned || !CanBeCaught || !_carryable.Attach(holdPoint, true, duration)) return false;
            EnterCarried();
            return true;
        }

        public bool TryAttachToCarrySlot(Transform holdPoint)
        {
            if (!IsSpawned || !CanPickUp || !_carryable.Attach(holdPoint, false)) return false;
            EnterCarried();
            return true;
        }

        public bool TryThrow(Vector2 direction, Vector3 groundPosition, Collider2D[] throwerColliders)
        {
            if (!IsSpawned || _state != RatState.Carried || !_motion.TryThrow(direction, groundPosition, throwerColliders)) return false;
            HandleAirborneStarted();
            return true;
        }

        public bool TryDispense(Vector2 direction, float speed)
        {
            if (!IsSpawned || IsDead || !_motion.TryDispense(_factory.Battlefield.Ground, direction, speed)) return false;
            HandleAirborneStarted();
            return true;
        }

        public void BeginFall(float height)
        {
            if (!IsSpawned || IsDead) return;
            BeginAirborne(_groundReturnState, 0f);
            _motion.BeginRatFall(_factory.Battlefield.Ground, height);
        }

        internal bool TryEnterCannon(Transform storagePoint, bool fromIdle = false)
        {
            if (!IsSpawned || IsDead || _projectile == null) return false;
            if (fromIdle && _state != RatState.Idle) return false;
            if (!fromIdle && !CanLoadIntoCannon) return false;
            if (!_motion.TryEnterCannon(storagePoint, fromIdle)) return false;
            EnterLoaded();
            return true;
        }

        public void ClearStateHistory()
        {
            _stateHistory.Clear();
        }

        private void OnDestroy()
        {
            if (_motion != null)
            {
                _motion.Settled -= HandleGrounded;
            }
        }

        private void Update()
        {
            UpdateGroggy();
        }

        // --------------------------------------------------------------------
        // Initialize
        // --------------------------------------------------------------------

        public void ResetRat(
            VehicleSide faction,
            RatFactory factory,
            bool combat,
            bool falling)
        {
            if (_projectile != null)
            {
                _projectile.ResetForPool();
            }

            _factory = factory;

            _stateHistory.Clear();

            Faction = faction;
            AttackSide = faction;
            ProjectileSourceSlot = null;

            Health = _definition.Ground.Health;
            _groggyUntil = 0f;
            _landingGroggyDuration = 0f;

            RatState groundState =
                combat ? RatState.GroundCombat : RatState.Idle;

            _groundReturnState = groundState;
            _landingState = groundState;

            if (_groundBehaviour != null)
            {
                _groundBehaviour.ResetForSpawn();
            }

            _motion.ResetForRat(factory.Battlefield.Ground);

            if (falling)
            {
                BeginAirborne(groundState, 0f);

                _motion.BeginRatFall(
                    factory.Battlefield.Ground,
                    1.5f);

                return;
            }

            ChangeState(groundState);
        }

        // --------------------------------------------------------------------
        // State
        // --------------------------------------------------------------------

        private void ChangeState(RatState nextState)
        {
            if (_state == nextState)
            {
                return;
            }

            RatState previousState = _state;
            _state = nextState;

            if (_stateHistory.Count >= StateHistoryCapacity)
            {
                _stateHistory.RemoveAt(0);
            }

            _stateHistory.Add(new StateTransition(
                Time.time,
                Time.frameCount,
                previousState,
                nextState));

            if (_groundBehaviour != null && _groundBehaviour.enabled) _groundBehaviour.OnRatStateChanged(previousState, nextState);

            StateChanged?.Invoke(
                this,
                previousState,
                nextState);
        }

        // --------------------------------------------------------------------
        // Carry / Throw / Landing
        // --------------------------------------------------------------------

        private void EnterCarried()
        {
            if (_state == RatState.Idle
                || _state == RatState.GroundCombat)
            {
                _groundReturnState = _state;
            }

            ChangeState(RatState.Carried);
        }

        private void HandleAirborneStarted()
        {
            if (_state == RatState.Carried)
            {
                if (Faction == VehicleSide.Enemy)
                {
                    // 적의 제압 시간은 운반과 재투척으로 갱신하지 않는다.
                    BeginAirborne(_groundReturnState, 0f);
                    return;
                }

                BeginAirborne(
                    _groundReturnState,
                    GetGroggyDuration());

                return;
            }

            if (_state == RatState.Idle
                || _state == RatState.GroundCombat)
            {
                BeginAirborne(_state, 0f);
                return;
            }

            if (_state != RatState.Airborne)
            {
                ChangeState(RatState.Airborne);
            }
        }

        public void BeginAirborne(
            RatState landingState,
            float groggyDuration)
        {
            _landingState =
                NormalizeGroundState(landingState);

            _landingGroggyDuration =
                Mathf.Max(0f, groggyDuration);

            ChangeState(RatState.Airborne);
        }

        private void HandleGrounded()
        {
            // ��ź�� ����� Projectile�� ����Ѵ�.
            if (_state == RatState.CannonFlight)
            {
                return;
            }

            if (_state != RatState.Airborne)
            {
                return;
            }

            CompleteLanding();
        }

        private void CompleteLanding()
        {
            RatState destination =
                NormalizeGroundState(_landingState);

            if (Faction == VehicleSide.Enemy && _groggyUntil > Time.time)
            {
                float remainingDuration = _groggyUntil - Time.time;
                EnterGroggy(destination, remainingDuration);
                return;
            }

            if (_landingGroggyDuration > 0f)
            {
                EnterGroggy(
                    destination,
                    _landingGroggyDuration);

                return;
            }

            _groundReturnState = destination;
            _landingGroggyDuration = 0f;

            ChangeState(destination);
        }

        // --------------------------------------------------------------------
        // Groggy / Knockback
        // --------------------------------------------------------------------

        public void KnockbackToGroggy(
            Vector2 direction,
            float throwHeight = 1.5f)
        {
            if (_state == RatState.Dead
                || _state == RatState.Carried
                || _state == RatState.Loaded
                || _state == RatState.CannonFlight
                || _state == RatState.Airborne)
            {
                return;
            }

            if (_factory == null)
            {
                return;
            }

            RatState returnState =
                _state == RatState.Groggy
                    ? _groundReturnState
                    : NormalizeGroundState(_state);

            BeginAirborne(
                returnState,
                GetGroggyDuration());

            bool launched = _motion.TryDispense(
                _factory.Battlefield.Ground,
                direction.normalized,
                throwHeight);

            if (!launched)
            {
                _landingGroggyDuration = 0f;
                ChangeState(returnState);
            }
        }

        /// <summary>
        /// 빠따 전용 발사 진입점이다. 적의 제압 시간은 착지가 아닌 타격 순간부터 흐른다.
        /// </summary>
        public bool LaunchFromBat(
            Vector2 direction,
            float horizontalSpeed,
            float verticalSpeed,
            int landingCellDistance,
            float collisionFusionMinimumSpeed,
            int swingId,
            bool isFullCharge,
            Transform returnTarget)
        {
            if (!CanBeHitByBat || _factory == null || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            RatState returnState = NormalizeGroundState(_state);
            if (_state == RatState.Groggy)
            {
                returnState = _groundReturnState;
            }
            else if (_state == RatState.Airborne)
            {
                // 공중에서 다시 맞아도 최초 비행이 끝난 뒤 돌아갈 지상 상태는 유지한다.
                returnState = NormalizeGroundState(_landingState);
            }

            _landingState = returnState;
            _landingGroggyDuration = 0f;

            if (Faction == VehicleSide.Enemy)
            {
                _groggyUntil = Time.time + Mathf.Max(0f, _enemyGroggyDuration);
            }
            else
            {
                _groggyUntil = 0f;
            }

            ChangeState(RatState.Airborne);

            bool launched = _motion.TryBatLaunch(
                _factory.Battlefield.Ground,
                direction.normalized,
                horizontalSpeed,
                verticalSpeed,
                landingCellDistance,
                collisionFusionMinimumSpeed,
                swingId,
                isFullCharge,
                returnTarget);

            if (launched)
            {
                return true;
            }

            _groggyUntil = 0f;
            ChangeState(returnState);
            return false;
        }

        /// <summary>충돌 합성 결과를 원본 비행 속도와 높이로 이어서 날린다.</summary>
        public void ContinueAfterCollisionFusion(
            Vector3 groundPosition,
            float height,
            float verticalSpeed,
            Vector2 velocity,
            int swingId,
            float collisionFusionMinimumSpeed,
            Transform returnTarget)
        {
            if (_factory == null || _state == RatState.Dead)
            {
                return;
            }

            _groundReturnState = RatState.Idle;
            _landingState = RatState.Idle;
            _landingGroggyDuration = 0f;
            _groggyUntil = 0f;
            ChangeState(RatState.Airborne);

            _motion.BeginCollisionFusionFlight(
                _factory.Battlefield.Ground,
                groundPosition,
                height,
                verticalSpeed,
                velocity,
                swingId,
                collisionFusionMinimumSpeed,
                returnTarget);

        }

        // 이전 RatMelee 호출과의 호환 진입점.
        public void Stun(Vector2 direction)
        {
            KnockbackToGroggy(direction);
        }

        private void EnterGroggy(
            RatState returnState,
            float duration)
        {
            _groundReturnState =
                NormalizeGroundState(returnState);

            _landingGroggyDuration = 0f;

            if (duration <= 0f)
            {
                ChangeState(_groundReturnState);
                return;
            }

            _groggyUntil = Time.time + duration;

            ChangeState(RatState.Groggy);
        }

        private void UpdateGroggy()
        {
            if (_groggyUntil <= 0f
                || Time.time < _groggyUntil
                || _state == RatState.Dead)
            {
                return;
            }

            if (_state == RatState.Loaded
                || _state == RatState.CannonFlight)
            {
                _groggyUntil = 0f;
                return;
            }

            _groggyUntil = 0f;

            if (_state == RatState.Groggy)
            {
                ChangeState(_groundReturnState);
                return;
            }

            // 운반 중 적이 깨어나면 운반자가 전체 Drop과 플레이어 반동을 처리한다.
            if (_state == RatState.Carried
                && Faction == VehicleSide.Enemy
                && _factory != null)
            {
                CarryController carryController = GetComponentInParent<CarryController>();
                if (carryController != null)
                {
                    carryController.HandleCarriedEnemyRecovered(this);
                    return;
                }

                Vector3 floor = _factory.Battlefield.NearestFloor(transform.position, Faction);
                _carryable.DropToGround(floor);
                ChangeState(_groundReturnState);
            }
        }

        private float GetGroggyDuration()
        {
            return Faction == VehicleSide.Enemy
                ? _enemyGroggyDuration
                : _allyGroggyDuration;
        }

        // --------------------------------------------------------------------
        // Ground Combat
        // --------------------------------------------------------------------

        public void EnterGroundCombat(
            RatFactory factory,
            Vector3 position)
        {
            if (_state == RatState.Dead)
            {
                return;
            }

            _factory = factory;

            _motion.ResetForRat(
                factory.Battlefield.Ground);

            transform.position = position;

            _groggyUntil = 0f;
            _landingGroggyDuration = 0f;

            _groundReturnState = RatState.GroundCombat;
            _landingState = RatState.GroundCombat;

            ChangeState(RatState.GroundCombat);
        }

        // ���� GroundGate ȣȯ��.
        public void Deploy(
            RatFactory factory,
            Vector3 position)
        {
            EnterGroundCombat(factory, position);
        }

        public void TeleportAirborne(Vector3 position)
        {
            if (_state != RatState.Airborne)
            {
                return;
            }

            _motion.RelocateAirborne(position);
        }

        /// <summary>강제 전체 Drop에서 Carry 상태와 실제 Carryable 상태를 함께 정리한다.</summary>
        public void DropFromCarry(Vector3 position)
        {
            if (_state != RatState.Carried || !_motion.IsCarried)
            {
                return;
            }

            Vector3 floor = position;
            if (_factory != null)
            {
                floor = _factory.Battlefield.NearestFloor(position, Faction);
            }

            _carryable.DropToGround(floor);

            _groggyUntil = 0f;
            ChangeState(_groundReturnState);
        }

        public bool TryLoadIntoCannon(SiegeCore.Cannon.Cannon cannon)
        {
            return IsSpawned && _state == RatState.Idle && cannon != null && cannon.TryLoadIdle(this);
        }

        // --------------------------------------------------------------------
        // Cannon
        // --------------------------------------------------------------------

        private void EnterLoaded()
        {
            _groggyUntil = 0f;
            ChangeState(RatState.Loaded);
        }

        public bool LaunchFromCannon(
            Vector3 startPosition,
            Vector3 targetPosition,
            VehicleSide attackSide,
            CannonSlot sourceSlot,
            CannonTrajectoryType trajectoryType,
            float flightDuration,
            float arcHeight)
        {
            if (!IsSpawned || _state != RatState.Loaded
                || !_motion.IsLoaded)
            {
                return false;
            }

            AttackSide = attackSide;
            ProjectileSourceSlot = sourceSlot;
            _groggyUntil = 0f;

            _motion.LaunchFromCannon(
                startPosition,
                targetPosition,
                trajectoryType,
                flightDuration,
                arcHeight);

            ChangeState(RatState.CannonFlight);
            return true;
        }

        // --------------------------------------------------------------------
        // Health / Death
        // --------------------------------------------------------------------

        public void TakeDamage(float damage)
        {
            if (!IsSpawned || _state == RatState.Dead || damage <= 0f)
            {
                return;
            }

            Health = Mathf.Max(
                0f,
                Health - damage);

            if (Health <= 0f)
            {
                Die();
            }
        }

        public void TakeDamage(DamageData damageData)
        {
            if (!damageData.IsNeutral && damageData.AttackerSide == Faction)
            {
                return;
            }

            TakeDamage(damageData.Damage);
        }

        private void Die()
        {
            if (_state == RatState.Dead)
            {
                return;
            }

            bool groundDeath = _state == RatState.GroundCombat
                || ((_state == RatState.Airborne || _state == RatState.Groggy)
                    && (_groundReturnState == RatState.GroundCombat
                        || _landingState == RatState.GroundCombat));
            bool infiltrated = _groundBehaviour != null && _groundBehaviour.IsInfiltrated;
            ChangeState(RatState.Dead);

            if (groundDeath && _definition.GroundDeathAbility != null)
            {
                _definition.GroundDeathAbility.OnGroundDeath(
                    new GroundDeathAbilityContext(_factory, Faction, transform.position, infiltrated));
            }

            Died?.Invoke(this);
        }

        public void Release()
        {
            if (_pooledObject != null
                && _pooledObject.Owner != null)
            {
                _pooledObject.Return();
                return;
            }

            gameObject.SetActive(false);
        }

        private RatState NormalizeGroundState(
            RatState state)
        {
            if (state == RatState.GroundCombat)
            {
                return RatState.GroundCombat;
            }

            return RatState.Idle;
        }
    }
}
