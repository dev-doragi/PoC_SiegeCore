using System.Collections.Generic;
using SiegeCore.Player;
using SiegeCore.Combat;
using CannonController = SiegeCore.Cannon.Cannon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(RatAgent))]
    [RequireComponent(typeof(CarryableObject))]
    public sealed class RatGroundAI : RatGroundBehaviour
    {
        public enum EnemyAssignment
        {
            None,
            Ground,
            Cannon
        }

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
        [SerializeField, Min(0.05f)]
        private float _assignmentArrivalDistance = 0.35f;

        private RatAgent _agent;
        private RatFlightMotion _motion;
        private GroundBlocker _blocker;
        private GroundBlockTarget _blockTarget;
        private RatBattlefield _battlefield;

        private readonly List<Vector3> _path =
            new List<Vector3>();
        private readonly List<Vector3> _targetPath = new List<Vector3>();

        private int _waypointIndex;

        private float _idleMoveSpeed;
        private float _idleRestUntil;
        private float _nextPathTime;
        private float _nextAttackTime;
        private bool _isInfiltrated;
        private GroundGate _assignedGate;
        private CannonController _assignedCannon;

        public EnemyAssignment Assignment { get; private set; }
        public override bool IsInfiltrated { get { return _isInfiltrated; } }

        private bool _isMoving;
        public override bool IsMoving { get { return _isMoving; } }

        private Vector2 _moveDirection;
        public override Vector2 MoveDirection { get { return _moveDirection; } }

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
            _motion = GetComponent<RatFlightMotion>();
            _blocker = GetComponent<GroundBlocker>();
            _blockTarget = GetComponent<GroundBlockTarget>();
        }





        private void FixedUpdate()
        {
            if (!_agent.IsSpawned) return;
            ResolveBattlefield();

            if (_battlefield == null)
            {
                return;
            }

            switch (_agent.State)
            {
                case RatState.Idle:
                    if (Assignment == EnemyAssignment.None)
                    {
                        UpdateIdle();
                    }
                    else
                    {
                        UpdateAssignment();
                    }
                    break;

                case RatState.GroundCombat:
                    UpdateCombat();
                    break;
            }
        }

        public override void OnRatStateChanged(
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
            RatAgent blockingEnemy = GetBlockingEnemy();
            if (blockingEnemy != null)
            {
                UpdateRatTarget(blockingEnemy);
                return;
            }

            RatAgent enemy = FindNearestEnemy();

            if (enemy != null)
            {
                UpdateRatTarget(enemy);
                return;
            }

            if (_isInfiltrated)
            {
                RatStructure facility = FindProductionFacility();
                if (facility != null) UpdateStructureTarget(facility);
                else StopMovement();
                return;
            }

            GroundGate enemyGate = _battlefield.GetEnemyGate(
                _agent.Faction);

            RatStructure structure = enemyGate != null
                ? enemyGate.Entrance
                : null;

            if (structure != null && !structure.IsDestroyed)
            {
                UpdateStructureTarget(structure);
                return;
            }

            if (enemyGate != null)
            {
                UpdateDestroyedEntrance(enemyGate);
                return;
            }

            StopMovement();
        }

        private RatAgent FindNearestEnemy()
        {
            RatAgent nearest = null;
            float nearestDistance = float.PositiveInfinity;
            int nearestPriority = int.MinValue;

            foreach (RatAgent candidate in RatAgent.Active)
            {
                if (candidate == null
                    || candidate == _agent
                    || candidate.Faction == _agent.Faction
                    || !IsGroundTarget(candidate))
                {
                    continue;
                }

                float distance =
                    ((Vector2)candidate.transform.position
                    - _motion.PhysicsPosition).sqrMagnitude;

                float detectionRadius =
                    _agent.Definition.Ground.DetectionRadius;

                if (distance > detectionRadius * detectionRadius)
                {
                    continue;
                }

                int priority = candidate.Definition.Ground.AttackPriority;
                if (priority < nearestPriority
                    || (priority == nearestPriority
                        && distance >= nearestDistance))
                {
                    continue;
                }

                if (_isInfiltrated && !CanReach(candidate.transform.position)) continue;

                nearest = candidate;
                nearestDistance = distance;
                nearestPriority = priority;
            }

            return nearest;
        }

        private bool IsGroundTarget(RatAgent candidate)
        {
            if (!_isInfiltrated) return candidate.State == RatState.GroundCombat;
            return IsInsideEnemyBase(candidate.transform.position)
                && (candidate.State == RatState.GroundCombat
                    || candidate.State == RatState.Idle
                    || candidate.State == RatState.Groggy);
        }

        private bool IsInsideEnemyBase(Vector3 position)
        {
            GroundGate gate = _battlefield.GetEnemyGate(_agent.Faction);
            return gate != null && gate.ContainsBasePosition(position);
        }

        private bool CanReach(Vector3 position)
        {
            return _battlefield.TryPath(transform.position, position,
                _agent.Faction, _targetPath, _agent.Definition.Ground.AttackRange);
        }

        private RatStructure FindProductionFacility()
        {
            RatStructure nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (RatStructure facility in RatStructure.Active)
            {
                if (facility == null || facility.IsEntrance || facility.IsDestroyed
                    || facility.Faction == _agent.Faction
                    || !IsInsideEnemyBase(facility.transform.position)) continue;
                float distance = (facility.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance || !CanReach(facility.transform.position)) continue;
                nearest = facility;
                nearestDistance = distance;
            }
            return nearest;
        }

        private void UpdateRatTarget(RatAgent target)
        {
            if (target == null || target.IsDead)
            {
                StopMovement();
                return;
            }

            float distance = Vector2.Distance(
                _motion.PhysicsPosition,
                target.transform.position);

            if (distance <= _agent.Definition.Ground.AttackRange)
            {
                StopMovement();

                if (_blocker != null && target.GroundBlockTarget != null)
                {
                    _blocker.TryBlock(target.GroundBlockTarget);
                }

                TryAttackRat(target);
                return;
            }

            MoveToward(target.transform.position);
        }

        private void TryAttackRat(RatAgent target)
        {
            if (target == null || target.IsDead || Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime =
                Time.time + _agent.Definition.Ground.AttackInterval;

            ApplyAttack(target);
        }

        private RatAgent GetBlockingEnemy()
        {
            if (_blockTarget == null || _blockTarget.Blocker == null)
            {
                return null;
            }

            RatAgent blocker = _blockTarget.Blocker.Agent;
            if (blocker == null
                || blocker.IsDead
                || blocker.Faction == _agent.Faction
                || !IsGroundTarget(blocker))
            {
                _blockTarget.ReleaseBlocker();
                return null;
            }

            return blocker;
        }

        private void UpdateStructureTarget(
            RatStructure target)
        {
            float distance = Vector2.Distance(
                _motion.PhysicsPosition,
                target.transform.position);

            if (distance <= _agent.Definition.Ground.AttackRange)
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
                Time.time + _agent.Definition.Ground.AttackInterval;

            ApplyAttack(target);
        }

        private void ApplyAttack(IDamageable target)
        {
            target.TakeDamage(new DamageData
            {
                AttackerSide = _agent.Faction,
                Damage = _agent.Definition.Ground.AttackDamage,
                HitPoint = transform.position
            });
        }

        private void UpdateDestroyedEntrance(GroundGate gate)
        {
            Vector3 target = gate.Entrance != null
                ? gate.Entrance.transform.position
                : gate.BasePosition;

            if (Vector2.Distance(
                    _motion.PhysicsPosition,
                    target) <= _agent.Definition.Ground.AttackRange)
            {
                StopMovement();
                gate.TryEnterBase(_agent);
                return;
            }

            MoveToward(target);
        }

        public override void BeginArenaCombat()
        {
            Assignment = EnemyAssignment.None;
            _assignedGate = null;
            _assignedCannon = null;
            _isInfiltrated = false;
            ResetPath();
        }

        public override void ResetForSpawn()
        {
            Assignment = EnemyAssignment.None;
            _assignedGate = null;
            _assignedCannon = null;
            _isInfiltrated = false;
            _battlefield = null;
            ResetPath();
            StopMovement();
            _idleMoveSpeed = 0f;
            _nextAttackTime = 0f;
            BeginIdleRest();
        }

        public override void BeginBaseInfiltration()
        {
            Assignment = EnemyAssignment.None;
            _assignedGate = null;
            _assignedCannon = null;
            _isInfiltrated = true;
            ResetPath();
        }

        public bool AssignGround(GroundGate gate)
        {
            if (_agent.State != RatState.Idle || gate == null)
            {
                return false;
            }

            Assignment = EnemyAssignment.Ground;
            _assignedGate = gate;
            _assignedCannon = null;
            ResetPath();
            return true;
        }

        public bool AssignCannon(CannonController cannon)
        {
            if (_agent.State != RatState.Idle || cannon == null)
            {
                return false;
            }

            Assignment = EnemyAssignment.Cannon;
            _assignedGate = null;
            _assignedCannon = cannon;
            ResetPath();
            return true;
        }

        private void UpdateAssignment()
        {
            Vector3 target;

            if (Assignment == EnemyAssignment.Ground)
            {
                if (_assignedGate == null)
                {
                    ClearAssignment();
                    return;
                }

                target = _assignedGate.BasePosition;
            }
            else
            {
                if (_assignedCannon == null || _assignedCannon.IsFull)
                {
                    ClearAssignment();
                    return;
                }

                target = _assignedCannon.GetLoadingPosition();
            }

            if (Vector2.Distance(
                    _motion.PhysicsPosition,
                    target) <= _assignmentArrivalDistance)
            {
                StopMovement();

                if (Assignment == EnemyAssignment.Ground)
                {
                    _assignedGate.TryEnterArena(_agent);
                }
                else if (!_agent.TryLoadIntoCannon(_assignedCannon))
                {
                    ClearAssignment();
                }

                return;
            }

            MoveToward(target, _assignmentArrivalDistance);
        }

        private void ClearAssignment()
        {
            Assignment = EnemyAssignment.None;
            _assignedGate = null;
            _assignedCannon = null;
            ResetPath();
            BeginIdleRest();
        }

        private void ResetPath()
        {
            _path.Clear();
            _waypointIndex = 0;
            _nextPathTime = 0f;
        }

        // ------------------------------------------------------------
        // Movement
        // ------------------------------------------------------------

        private void MoveToward(Vector3 target)
        {
            MoveToward(
                target,
                _agent.Definition.Ground.AttackRange);
        }

        private void MoveToward(
            Vector3 target,
            float stopRange)
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
                    stopRange);

                _waypointIndex = 0;
            }

            if (_waypointIndex >= _path.Count)
            {
                StopMovement();
                return;
            }

            MoveAlongPath(
                _agent.Definition.Ground.MoveSpeed);
        }

        private void MoveAlongPath(float speed)
        {
            if (_waypointIndex >= _path.Count)
            {
                StopMovement();
                return;
            }

            Vector2 current = _motion.PhysicsPosition;
            Vector2 target = _path[_waypointIndex];

            Vector2 delta = target - current;

            if (delta.sqrMagnitude > 0.0001f)
            {
                _moveDirection = delta.normalized;
                _isMoving = true;
            }

            Vector2 next = Vector2.MoveTowards(
                current,
                target,
                speed * Time.fixedDeltaTime);

            if (!_motion.TryMoveOnGround(next))
            {
                ResetPath();
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
            _isMoving = false;
            _moveDirection = Vector2.zero;
        }
    }
}
