using SiegeCore.Cannon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class GroundGate : MonoBehaviour
    {
        [SerializeField] private VehicleSide _faction;
        [SerializeField] private RatFactory _factory;
        [SerializeField] private Transform _baseEndpoint;
        [SerializeField] private Transform _arenaEndpoint;
        [SerializeField] private RatStructure _entrance;
        [SerializeField] private BoxCollider2D _baseArea;
        [SerializeField, Min(0f)] private float _spawnSpread = 0.75f;
        [SerializeField, Min(0f)] private float _airborneExitSpeed = 2f;
        [SerializeField, Min(0f)] private float _airborneExitBounceSpeed = 2.5f;

        public VehicleSide Faction { get { return _faction; } }
        public RatStructure Entrance { get { return _entrance; } }
        public Vector3 BasePosition { get { return BaseEndpoint.position; } }
        public Vector3 ArenaPosition { get { return _arenaEndpoint.position; } }

        private Transform BaseEndpoint
        {
            get
            {
                if (_baseEndpoint != null)
                {
                    return _baseEndpoint;
                }

                return transform;
            }
        }

        public bool ContainsBasePosition(Vector3 position)
        {
            return _baseArea != null && _baseArea.OverlapPoint(position);
        }

        public bool TryMoveBaseToArena(RatAgent rat)
        {
            if (!CanMoveBaseToArena(rat))
            {
                return false;
            }

            Vector3 destination = FindArrivalPosition(
                _factory.Battlefield.BattlefieldGround,
                _arenaEndpoint.position);
            CompleteArenaEntry(rat, destination);
            return true;
        }

        public bool TryMoveArenaToBase(RatAgent rat)
        {
            if (rat == null || rat.Faction == _faction || _entrance == null
                || !_entrance.IsDestroyed || _factory == null)
            {
                return false;
            }

            Vector3 destination = FindArrivalPosition(
                _factory.Battlefield.Ground,
                BaseEndpoint.position);
            rat.EnterGroundCombat(_factory, destination, _factory.Battlefield.Ground);

            RatGroundBehaviour groundBehaviour = rat.GroundBehaviour;
            if (groundBehaviour != null)
            {
                groundBehaviour.BeginBaseInfiltration();
            }

            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            RatAgent rat = other.GetComponentInParent<RatAgent>();
            if (!CanMoveBaseToArena(rat))
            {
                return;
            }

            if (rat.State == RatState.Airborne)
            {
                BeginAirborneArenaTransfer(rat);
                return;
            }

            RatGroundAI groundAI = rat.GetComponent<RatGroundAI>();
            if (rat.State == RatState.Idle && groundAI != null
                && groundAI.Assignment == RatGroundAI.EnemyAssignment.Ground)
            {
                TryMoveBaseToArena(rat);
            }
        }

        private bool CanMoveBaseToArena(RatAgent rat)
        {
            return rat != null && rat.Faction == _faction
                && !rat.Carryable.IsCannonFlight && _factory != null
                && _factory.Battlefield != null
                && _factory.Battlefield.BattlefieldGround != null
                && _arenaEndpoint != null;
        }

        private void BeginAirborneArenaTransfer(RatAgent rat)
        {
            Vector3 destination = FindArrivalPosition(
                _factory.Battlefield.BattlefieldGround,
                _arenaEndpoint.position);
            Vector2 exitDirection = GetArenaExitDirection();
            Vector2 exitVelocity = exitDirection * _airborneExitSpeed;

            rat.TransferAirborneToArena(
                _factory,
                destination,
                _factory.Battlefield.BattlefieldGround,
                exitVelocity,
                _airborneExitBounceSpeed);

            RatGroundBehaviour groundBehaviour = rat.GroundBehaviour;
            if (groundBehaviour != null)
            {
                groundBehaviour.BeginArenaCombat();
            }
        }

        private Vector2 GetArenaExitDirection()
        {
            if (_faction == VehicleSide.Ally)
            {
                return Vector2.right;
            }

            return Vector2.left;
        }

        private Vector3 FindArrivalPosition(Tilemap ground, Vector3 endpoint)
        {
            Vector3 center = FindNearestTileCenter(ground, endpoint);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 candidate = center;
                candidate.y += Random.Range(-_spawnSpread, _spawnSpread);
                Vector3Int cell = ground.WorldToCell(candidate);
                if (ground.HasTile(cell))
                {
                    return ground.GetCellCenterWorld(cell);
                }
            }

            return center;
        }

        private static Vector3 FindNearestTileCenter(Tilemap ground, Vector3 position)
        {
            Vector3 nearest = position;
            float nearestDistance = float.PositiveInfinity;
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                if (!ground.HasTile(cell))
                {
                    continue;
                }

                Vector3 candidate = ground.GetCellCenterWorld(cell);
                float distance = (candidate - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void CompleteArenaEntry(RatAgent rat, Vector3 destination)
        {
            rat.EnterGroundCombat(
                _factory,
                destination,
                _factory.Battlefield.BattlefieldGround);

            RatGroundBehaviour groundBehaviour = rat.GroundBehaviour;
            if (groundBehaviour != null)
            {
                groundBehaviour.BeginArenaCombat();
            }
        }

    }
}
