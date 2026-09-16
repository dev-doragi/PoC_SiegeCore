using System.Collections.Generic;
using SiegeCore.Cannon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Rat
{
    public sealed class RatBattlefield : MonoBehaviour
    {
        [SerializeField] private Tilemap _ground;
        [SerializeField] private BoxCollider2D _safeZone;
        [SerializeField] private Transform _allyExit;
        [SerializeField] private Transform _enemyExit;
        [SerializeField] private GroundGate _allyGate;
        [SerializeField] private GroundGate _enemyGate;
        public Tilemap Ground { get { return _ground; } }
        public GroundGate AllyGate { get { return _allyGate; } }
        public GroundGate EnemyGate { get { return _enemyGate; } }
        private static readonly Vector3Int[] Directions = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };

        public bool IsSafe(Vector3 position)
        {
            return _safeZone != null && _safeZone.OverlapPoint(position);
        }

        public bool IsOnLane(Vector3 position)
        {
            bool isInsideHorizontalRange = position.x >= _allyExit.position.x
                && position.x <= _enemyExit.position.x;
            bool isOnLaneHeight = Mathf.Abs(position.y - _allyExit.position.y) < 0.6f;
            return isInsideHorizontalRange && isOnLaneHeight;
        }
        public Vector3 ProjectToLane(Vector3 position)
        {
            float x = Mathf.Clamp(position.x, _allyExit.position.x, _enemyExit.position.x);
            return new Vector3(x, _allyExit.position.y, 0f);
        }

        public bool IsWalkable(Vector3Int cell, VehicleSide faction)
        {
            if (!_ground.HasTile(cell))
            {
                return false;
            }
            foreach (RatStructure structure in RatStructure.Active)
            {
                if (structure.IsEntrance && !structure.IsDestroyed && structure.Faction != faction
                    && cell == _ground.WorldToCell(structure.transform.position))
                {
                    return false;
                }
            }
            return true;
        }

        // Four-neighbour navigation keeps agents on the same floor as the player.
        public bool TryPath(Vector3 from, Vector3 target, VehicleSide faction, List<Vector3> path, float stopRange = 0.1f)
        {
            path.Clear();
            Vector3Int start = _ground.WorldToCell(from);
            Queue<Vector3Int> open = new Queue<Vector3Int>();
            Dictionary<Vector3Int, Vector3Int> parents = new Dictionary<Vector3Int, Vector3Int>();
            open.Enqueue(start);
            parents[start] = start;
            while (open.Count > 0 && parents.Count < 4096)
            {
                Vector3Int cell = open.Dequeue();
                Vector3 center = _ground.GetCellCenterWorld(cell);
                if (Vector2.Distance(center, target) <= Mathf.Max(0.1f, stopRange)
                    || (cell == _ground.WorldToCell(target) && IsWalkable(cell, faction)))
                {
                    while (cell != start)
                    {
                        path.Add(_ground.GetCellCenterWorld(cell));
                        cell = parents[cell];
                    }
                    path.Reverse();
                    // A unit can enter the goal cell before reaching attack range.
                    // Do not return an empty route until its actual position is close enough.
                    if (path.Count == 0 && Vector2.Distance(from, target) > stopRange)
                    {
                        path.Add(center);
                    }
                    return true;
                }
                foreach (Vector3Int direction in Directions)
                {
                    Vector3Int next = cell + direction;
                    if (parents.ContainsKey(next) || !IsWalkable(next, faction))
                    {
                        continue;
                    }
                    parents[next] = cell;
                    open.Enqueue(next);
                }
            }
            return false;
        }

        public Vector3 NearestFloor(Vector3 position, VehicleSide faction)
        {
            Vector3 best = ProjectToLane(position);
            float distance = float.PositiveInfinity;
            foreach (Vector3Int cell in _ground.cellBounds.allPositionsWithin)
            {
                if (!IsWalkable(cell, faction))
                {
                    continue;
                }
                Vector3 candidate = _ground.GetCellCenterWorld(cell);
                float next = (candidate - position).sqrMagnitude;
                if (next < distance)
                {
                    distance = next;
                    best = candidate;
                }
            }
            return best;
        }

        public Vector3 FloorBelow(
            Vector3 position,
            VehicleSide faction)
        {
            Vector3 best = Vector3.zero;
            float bestHorizontalDistance = float.PositiveInfinity;
            float bestVerticalDistance = float.PositiveInfinity;
            bool found = false;

            foreach (Vector3Int cell in _ground.cellBounds.allPositionsWithin)
            {
                if (!IsWalkable(cell, faction))
                {
                    continue;
                }

                Vector3 candidate = _ground.GetCellCenterWorld(cell);
                if (candidate.y > position.y)
                {
                    continue;
                }

                float horizontalDistance =
                    Mathf.Abs(candidate.x - position.x);
                float verticalDistance = position.y - candidate.y;

                if (horizontalDistance > bestHorizontalDistance
                    || (Mathf.Approximately(
                            horizontalDistance,
                            bestHorizontalDistance)
                        && verticalDistance >= bestVerticalDistance))
                {
                    continue;
                }

                best = candidate;
                bestHorizontalDistance = horizontalDistance;
                bestVerticalDistance = verticalDistance;
                found = true;
            }

            return found
                ? best
                : NearestFloor(position, faction);
        }

        public GroundGate GetOwnGate(VehicleSide faction)
        {
            return faction == VehicleSide.Ally
                ? _allyGate
                : _enemyGate;
        }

        public GroundGate GetEnemyGate(VehicleSide faction)
        {
            return faction == VehicleSide.Ally
                ? _enemyGate
                : _allyGate;
        }
    }
}
