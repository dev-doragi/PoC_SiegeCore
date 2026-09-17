using System;
using System.Collections.Generic;
using SiegeCore.Cannon;
using SiegeCore.Player;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(CarryableObject))]
    public sealed class RatAgent : MonoBehaviour
    {
        public static readonly HashSet<RatAgent> Active = new HashSet<RatAgent>();

        [Header("Definition")]
        [SerializeField] private RatDefinition _definition;

        [Header("Groggy")]
        [SerializeField, Min(0f)] private float _allyGroggyDuration = 0.2f;
        [SerializeField, Min(0f)] private float _enemyGroggyDuration = 3f;

        private CarryableObject _carryable;
        private RatFactory _factory;

        private RatState _state;
        private RatState _groundReturnState = RatState.Idle;
        private RatState _landingState = RatState.Idle;

        private float _landingGroggyDuration;
        private float _groggyUntil;

        private bool _burst;
        private bool _suppressCarryEvent;

        public event Action<RatAgent, RatState, RatState> StateChanged;
        public event Action<RatAgent> Died;

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

        public VehicleSide Faction { get; private set; }

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

            if (_definition == null)
            {
                Debug.LogError("[RatAgent] RatDefinition is not assigned.", this);
                enabled = false;
                return;
            }

            _carryable.StateChanged += HandleCarryStateChanged;
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void OnDestroy()
        {
            if (_carryable != null)
            {
                _carryable.StateChanged -= HandleCarryStateChanged;
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
            _factory = factory;

            Faction = faction;
            AttackSide = faction;

            Health = _definition.Health;

            _burst = false;
            _groggyUntil = 0f;
            _landingGroggyDuration = 0f;

            RatState groundState =
                combat ? RatState.GroundCombat : RatState.Idle;

            _groundReturnState = groundState;
            _landingState = groundState;

            RatGroundAI groundAI = GetComponent<RatGroundAI>();
            if (groundAI != null)
            {
                groundAI.ResetForSpawn();
            }

            _suppressCarryEvent = true;
            _carryable.ResetForRat(factory.Battlefield.Ground);
            _suppressCarryEvent = false;

            if (falling)
            {
                BeginAirborne(groundState, 0f);

                _suppressCarryEvent = true;
                _carryable.BeginRatFall(
                    factory.Battlefield.Ground,
                    1.5f);
                _suppressCarryEvent = false;

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

            StateChanged?.Invoke(
                this,
                previousState,
                nextState);
        }

        private void HandleCarryStateChanged()
        {
            if (_suppressCarryEvent || _state == RatState.Dead)
            {
                return;
            }

            if (_carryable.IsCannonFlight)
            {
                ChangeState(RatState.CannonFlight);
                return;
            }

            if (_carryable.IsLoaded)
            {
                EnterLoaded();
                return;
            }

            if (_carryable.IsCarried)
            {
                EnterCarried();
                return;
            }

            if (_carryable.IsAirborne)
            {
                HandleAirborneStarted();
                return;
            }

            HandleGrounded();
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

            bool launched = _carryable.TryDispense(
                _factory.Battlefield.Ground,
                direction.normalized,
                throwHeight);

            if (!launched)
            {
                _landingGroggyDuration = 0f;
                ChangeState(returnState);
            }
        }

        // ���� RatMelee ȣȯ��.
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

            // ���� ��� �ִٰ� Groggy�� Ǯ���� �������´�.
            if (_state == RatState.Carried
                && Faction == VehicleSide.Enemy
                && _factory != null)
            {
                Vector3 floor =
                    _factory.Battlefield.NearestFloor(
                        transform.position,
                        Faction);

                _suppressCarryEvent = true;
                _carryable.Drop(floor);
                _suppressCarryEvent = false;

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

            _suppressCarryEvent = true;
            _carryable.ResetForRat(
                factory.Battlefield.Ground);
            _suppressCarryEvent = false;

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

            transform.position = position;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
            }
        }

        public bool TryLoadIntoCannon(SiegeCore.Cannon.Cannon cannon)
        {
            if (_state != RatState.Idle
                || cannon == null
                || _factory == null)
            {
                return false;
            }

            BeginAirborne(RatState.Idle, 0f);

            _suppressCarryEvent = true;
            _carryable.BeginRatFall(
                _factory.Battlefield.Ground,
                0.1f);
            _suppressCarryEvent = false;

            if (cannon.TryLoad(_carryable))
            {
                EnterLoaded();
                return true;
            }

            _carryable.ResetForRat(
                _factory.Battlefield.Ground);
            ChangeState(RatState.Idle);
            return false;
        }

        // --------------------------------------------------------------------
        // Cannon
        // --------------------------------------------------------------------

        private void EnterLoaded()
        {
            _groggyUntil = 0f;
            ChangeState(RatState.Loaded);
        }

        public void LaunchFromCannon(
            Vector3 startPosition,
            Vector3 targetPosition,
            VehicleSide attackSide,
            CannonSlot sourceSlot,
            CannonTrajectoryType trajectoryType,
            float flightDuration,
            float arcHeight)
        {
            if (_state == RatState.Dead
                || !_carryable.IsLoaded)
            {
                return;
            }

            AttackSide = attackSide;
            ProjectileSourceSlot = sourceSlot;
            _groggyUntil = 0f;

            _suppressCarryEvent = true;

            _carryable.LaunchFromCannon(
                startPosition,
                targetPosition,
                trajectoryType,
                flightDuration,
                arcHeight);

            _suppressCarryEvent = false;

            ChangeState(RatState.CannonFlight);
        }

        // --------------------------------------------------------------------
        // Health / Death
        // --------------------------------------------------------------------

        public void TakeDamage(float damage)
        {
            if (_state == RatState.Dead || damage <= 0f)
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

        private void Die()
        {
            if (_state == RatState.Dead)
            {
                return;
            }

            ChangeState(RatState.Dead);

            TryBurstContents();

            Died?.Invoke(this);
        }

        public void TryBurstContents()
        {
            if (_burst
                || !_definition.CanBurst
                || _factory == null)
            {
                return;
            }

            _burst = true;

            _factory.Burst(
                Faction,
                transform.position);
        }

        public void Release()
        {
            PooledObject pooledObject =
                GetComponent<PooledObject>();

            if (pooledObject != null
                && pooledObject.Owner != null)
            {
                pooledObject.Return();
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
