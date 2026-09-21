using System.Collections;
using System.Collections.Generic;
using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class GroundGate : MonoBehaviour
    {
        [SerializeField] private VehicleSide _faction;
        [SerializeField] private RatFactory _factory;
        [SerializeField] private Transform _exitPoint;
        [SerializeField] private RatStructure _entrance;
        [SerializeField] private BoxCollider2D _baseArea;
        [SerializeField, Min(0f)] private float _spawnSpread = 0.75f;

        private readonly HashSet<RatAgent> _pendingRats =
            new HashSet<RatAgent>();

        public VehicleSide Faction { get { return _faction; } }
        public RatStructure Entrance { get { return _entrance; } }
        public Vector3 BasePosition { get { return transform.position; } }

        public bool ContainsBasePosition(Vector3 position)
        {
            return _baseArea != null && _baseArea.OverlapPoint(position);
        }

        public bool TryEnterArena(RatAgent rat)
        {
            if (rat == null
                || rat.Faction != _faction
                || _factory == null
                || _exitPoint == null)
            {
                return false;
            }

            CompleteArenaEntry(rat, FindSpawnPosition(rat));
            return true;
        }

        public bool TryEnterBase(RatAgent rat)
        {
            if (rat == null
                || rat.Faction == _faction
                || _entrance == null
                || !_entrance.IsDestroyed
                || _factory == null)
            {
                return false;
            }

            rat.EnterGroundCombat(
                _factory,
                _factory.Battlefield.NearestFloor(
                    transform.position,
                    rat.Faction));

            RatGroundAI groundAI = rat.GetComponent<RatGroundAI>();
            if (groundAI != null)
            {
                groundAI.BeginBaseInfiltration();
            }

            return true;
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            RatAgent rat =
                other.GetComponentInParent<RatAgent>();

            if (rat == null
                || rat.Faction != _faction
                || rat.Carryable.IsCannonFlight
                || _factory == null
                || _exitPoint == null)
            {
                return;
            }

            if (rat.State == RatState.Airborne)
            {
                if (_pendingRats.Add(rat))
                {
                    Vector3 destination = FindSpawnPosition(rat);
                    rat.TeleportAirborne(destination);
                    StartCoroutine(
                        CompleteEntryAfterLanding(
                            rat,
                            destination));
                }

                return;
            }

            RatGroundAI groundAI = rat.GetComponent<RatGroundAI>();
            if (rat.State == RatState.Idle
                && groundAI != null
                && groundAI.Assignment
                    == RatGroundAI.EnemyAssignment.Ground)
            {
                TryEnterArena(rat);
            }
        }

        private IEnumerator CompleteEntryAfterLanding(
            RatAgent rat,
            Vector3 destination)
        {
            while (rat != null
                   && rat.isActiveAndEnabled
                   && rat.State == RatState.Airborne)
            {
                yield return null;
            }

            _pendingRats.Remove(rat);

            if (rat == null
                || !rat.isActiveAndEnabled
                || rat.State == RatState.Dead
                || rat.State == RatState.Carried
                || rat.State == RatState.Loaded
                || rat.State == RatState.CannonFlight)
            {
                yield break;
            }

            CompleteArenaEntry(rat, destination);
        }

        private void OnDisable()
        {
            _pendingRats.Clear();
        }

        private Vector3 FindSpawnPosition(RatAgent rat)
        {
            RatBattlefield battlefield = _factory.Battlefield;
            Vector2 forward = (transform.position - _exitPoint.position).normalized;
            Vector2 perpendicular = new Vector2(-forward.y, forward.x);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float offset = Random.Range(-_spawnSpread, _spawnSpread);
                Vector3 candidate = _exitPoint.position
                    + (Vector3)(perpendicular * offset);
                Vector3Int cell = battlefield.Ground.WorldToCell(candidate);

                if (battlefield.IsWalkable(cell, rat.Faction))
                {
                    return battlefield.Ground.GetCellCenterWorld(cell);
                }
            }

            return battlefield.NearestFloor(
                _exitPoint.position,
                rat.Faction);
        }

        private void CompleteArenaEntry(
            RatAgent rat,
            Vector3 destination)
        {
            rat.EnterGroundCombat(_factory, destination);

            RatGroundAI groundAI = rat.GetComponent<RatGroundAI>();
            if (groundAI != null)
            {
                groundAI.BeginArenaCombat();
            }
        }
    }
}
