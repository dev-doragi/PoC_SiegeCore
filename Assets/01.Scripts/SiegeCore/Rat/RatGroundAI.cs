using System.Collections.Generic;
using SiegeCore.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(RatAgent))]
    [RequireComponent(typeof(CarryableObject))]
    public sealed class RatGroundAI : MonoBehaviour
    {
        [Header("Idle")]
        [SerializeField] private int _idleRadiusCells = 3;
        [SerializeField]
        private Vector2 _idleMoveSpeedRange =
            new Vector2(0.6f, 1.6f);
        [SerializeField]
        private Vector2 _idleDelayRange =
            new Vector2(0.25f, 1.2f);

        [Header("Path")]
        [SerializeField, Min(0.05f)]
        private float _repathInterval = 0.3f;

        private RatAgent _agent;
        private CarryableObject _carryable;
        private RatBattlefield _battlefield;

        private readonly List<Vector3> _path =
            new List<Vector3>();

        private int _waypointIndex;

        private float _idleMoveSpeed;
        private float _idleRestUntil;
        private float _nextPathTime;
        private float _nextAttackTime;

        public bool IsMoving { get; private set; }

        public Vector2 MoveDirection { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
            _carryable = GetComponent<CarryableObject>();
        }

        private void OnEnable()
        {
            _agent.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _agent.StateChanged -= HandleStateChanged;
            StopMovement();
        }

        private void FixedUpdate()
        {
            ResolveBattlefield();

            if (_battlefield == null)
            {
                return;
            }

            switch (_agent.State)
            {
                case RatState.Idle:
                    UpdateIdle();
                    break;

                case RatState.GroundCombat:
                    UpdateCombat();
                    break;
            }
        }

        private void HandleStateChanged(
            RatAgent agent,
            RatState previousState,
            RatState nextState)
        {
            StopMovement();

            _nextPathTime = 0f;
            _nextAttackTime = 0f;

            if (nextState == RatState.Idle)
            {
                BeginIdleRest();
            }
        }

        private void ResolveBattlefield()
        {
            if (_battlefield != null)
            {
                return;
            }

            if (_agent.Factory != null)
            {
                _battlefield = _agent.Factory.Battlefield;
            }
        }

        // ------------------------------------------------------------
        // Idle
        // ------------------------------------------------------------

        private void UpdateIdle()
        {
            if (Time.time < _idleRestUntil)
            {
                StopMovement();
                return;
            }

            if (_waypointIndex >= _path.Count)
            {
                if (!ChooseIdleDestination())
                {
                    BeginIdleRest();
                    return;
                }
            }

            MoveAlongPath(_idleMoveSpeed);

            if (_waypointIndex >= _path.Count)
            {
                BeginIdleRest();
            }
        }

        private bool ChooseIdleDestination()
        {
            Tilemap ground = _battlefield.Ground;

            if (ground == null)
            {
                return false;
            }

            Vector3Int currentCell =
                ground.WorldToCell(transform.position);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int x = Random.Range(
                    -_idleRadiusCells,
                    _idleRadiusCells + 1);

                int y = Random.Range(
                    -_idleRadiusCells,
                    _idleRadiusCells + 1);

                if (x == 0 && y == 0)
                {
                    continue;
                }

                Vector3Int targetCell =
                    currentCell + new Vector3Int(x, y, 0);

                if (!_battlefield.IsWalkable(
                        targetCell,
                        _agent.Faction))
                {
                    continue;
                }

                Vector3 target =
                    ground.GetCellCenterWorld(targetCell);

                _path.Clear();

                if (!_battlefield.TryPath(
                        transform.position,
                        target,
                        _agent.Faction,
                        _path))
                {
                    continue;
                }

                if (_path.Count == 0)
                {
                    continue;
                }

                _waypointIndex = 0;

                _idleMoveSpeed = Random.Range(
                    _idleMoveSpeedRange.x,
                    _idleMoveSpeedRange.y);

                return true;
            }

            return false;
        }

        private void BeginIdleRest()
        {
            _path.Clear();
            _waypointIndex = 0;

            _idleRestUntil =
                Time.time
                + Random.Range(
                    _idleDelayRange.x,
                    _idleDelayRange.y);

            StopMovement();
        }

        // ------------------------------------------------------------
        // Combat
        // ------------------------------------------------------------

        private void UpdateCombat()
        {
            RatAgent enemy = FindNearestEnemy();

            if (enemy != null)
            {
                UpdateRatTarget(enemy);
                return;
            }

            RatStructure structure = FindStructureTarget();

            if (structure != null)
            {
                UpdateStructureTarget(structure);
                return;
            }

            StopMovement();
        }

        private RatAgent FindNearestEnemy()
        {
            RatAgent nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (RatAgent candidate in RatAgent.Active)
            {
                if (candidate == null
                    || candidate == _agent
                    || candidate.Faction == _agent.Faction
                    || candidate.State != RatState.GroundCombat)
                {
                    continue;
                }

                float distance =
                    ((Vector2)candidate.transform.position
                    - _carryable.PhysicsPosition).sqrMagnitude;

                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearest = candidate;
                nearestDistance = distance;
            }

            return nearest;
        }

        private void UpdateRatTarget(RatAgent target)
        {
            float distance = Vector2.Distance(
                _carryable.PhysicsPosition,
                target.transform.position);

            if (distance <= _agent.Definition.AttackRange)
            {
                StopMovement();
                TryAttackRat(target);
                return;
            }

            MoveToward(target.transform.position);
        }

        private void TryAttackRat(RatAgent target)
        {
            if (Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime =
                Time.time + _agent.Definition.AttackInterval;

            target.TakeDamage(
                _agent.Definition.AttackDamage);
        }

        private RatStructure FindStructureTarget()
        {
            RatStructure nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (RatStructure structure in RatStructure.Active)
            {
                if (structure == null
                    || structure.IsDestroyed
                    || structure.Faction == _agent.Faction)
                {
                    continue;
                }

                if (structure.IsEntrance)
                {
                    return structure;
                }

                float distance =
                    (structure.transform.position
                    - transform.position).sqrMagnitude;

                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearest = structure;
                nearestDistance = distance;
            }

            return nearest;
        }

        private void UpdateStructureTarget(
            RatStructure target)
        {
            float distance = Vector2.Distance(
                _carryable.PhysicsPosition,
                target.transform.position);

            if (distance <= _agent.Definition.AttackRange)
            {
                StopMovement();
                TryAttackStructure(target);
                return;
            }

            MoveToward(target.transform.position);
        }

        private void TryAttackStructure(
            RatStructure target)
        {
            if (Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime =
                Time.time + _agent.Definition.AttackInterval;

            target.TakeDamage(
                _agent.Faction,
                _agent.Definition.AttackDamage);
        }

        // ------------------------------------------------------------
        // Movement
        // ------------------------------------------------------------

        private void MoveToward(Vector3 target)
        {
            if (Time.time >= _nextPathTime)
            {
                _nextPathTime =
                    Time.time + _repathInterval;

                _path.Clear();

                _battlefield.TryPath(
                    transform.position,
                    target,
                    _agent.Faction,
                    _path,
                    _agent.Definition.AttackRange);

                _waypointIndex = 0;
            }

            if (_waypointIndex >= _path.Count)
            {
                StopMovement();
                return;
            }

            MoveAlongPath(
                _agent.Definition.MoveSpeed);
        }

        private void MoveAlongPath(float speed)
        {
            if (_waypointIndex >= _path.Count)
            {
                StopMovement();
                return;
            }

            Vector2 current = _carryable.PhysicsPosition;
            Vector2 target = _path[_waypointIndex];

            Vector2 delta = target - current;

            if (delta.sqrMagnitude > 0.0001f)
            {
                MoveDirection = delta.normalized;
                IsMoving = true;
            }

            Vector2 next = Vector2.MoveTowards(
                current,
                target,
                speed * Time.fixedDeltaTime);

            if (!_carryable.TryMoveOnGround(next))
            {
                StopMovement();
                return;
            }

            if (Vector2.Distance(next, target) <= 0.02f)
            {
                _waypointIndex++;
            }
        }

        private void StopMovement()
        {
            IsMoving = false;
            MoveDirection = Vector2.zero;
        }
    }
}
