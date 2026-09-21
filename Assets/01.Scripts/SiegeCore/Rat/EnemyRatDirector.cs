using SiegeCore.Cannon;
using UnityEngine;
using CannonController = SiegeCore.Cannon.Cannon;

namespace SiegeCore.Rat
{
    public sealed class EnemyRatDirector : MonoBehaviour
    {
        private enum Strategy
        {
            Balanced,
            Push,
            Bombard
        }

        [System.Serializable]
        private struct StrategyWeights
        {
            [Min(0f)] public float Ground;
            [Min(0f)] public float Cannon;
        }

        [Header("References")]
        [SerializeField] private RatBattlefield _battlefield;
        [SerializeField] private GroundGate _enemyExit;
        [SerializeField] private CannonController[] _enemyCannons;

        [Header("Strategy Weights")]
        [SerializeField] private StrategyWeights _balanced =
            new StrategyWeights { Ground = 50f, Cannon = 50f };
        [SerializeField] private StrategyWeights _push =
            new StrategyWeights { Ground = 75f, Cannon = 25f };
        [SerializeField] private StrategyWeights _bombard =
            new StrategyWeights { Ground = 25f, Cannon = 75f };

        [Header("Timing")]
        [SerializeField, Min(0.2f)] private float _decisionInterval = 5f;
        [SerializeField, Min(0f)] private float _minimumStrategyDuration = 12f;
        [SerializeField, Min(0.1f)] private float _assignmentInterval = 0.75f;
        [SerializeField, Min(0f)] private float _scoreRandomness = 0.15f;

        private Strategy _strategy = Strategy.Balanced;
        private float _nextDecisionTime;
        private float _nextAssignmentTime;
        private float _strategyChangedTime;
        private int _cannonIndex;

        private void OnEnable()
        {
            _strategyChangedTime = Time.time;
            _nextDecisionTime = Time.time + _decisionInterval;
            _nextAssignmentTime = Time.time;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (_battlefield == null || _enemyExit == null)
            {
                return;
            }

            if (Time.time >= _nextDecisionTime)
            {
                _nextDecisionTime = Time.time + _decisionInterval;
                EvaluateStrategy();
            }

            if (Time.time >= _nextAssignmentTime)
            {
                _nextAssignmentTime = Time.time + _assignmentInterval;
                AssignOneRat();
            }
        }

        private void EvaluateStrategy()
        {
            if (Time.time < _strategyChangedTime + _minimumStrategyDuration)
            {
                return;
            }

            int allyArena = CountArenaRats(VehicleSide.Ally);
            int enemyArena = CountArenaRats(VehicleSide.Enemy);
            float pressure = Mathf.Clamp((allyArena - enemyArena) * 0.2f, -1f, 1f);

            float balancedScore = 1f - Mathf.Abs(pressure) * 0.35f;
            float pushScore = 1f + pressure;
            float bombardScore = 1f - pressure * 0.5f;

            GroundGate allyGate = _battlefield.AllyGate;
            GroundGate enemyGate = _battlefield.EnemyGate;

            if (allyGate != null && allyGate.Entrance != null)
            {
                pushScore += 1f - GetHealthRatio(allyGate.Entrance);
            }

            if (enemyGate != null && enemyGate.Entrance != null)
            {
                pushScore += 1f - GetHealthRatio(enemyGate.Entrance);
            }

            if (FindAvailableCannon() == null)
            {
                bombardScore -= 2f;
            }

            balancedScore += Random.Range(-_scoreRandomness, _scoreRandomness);
            pushScore += Random.Range(-_scoreRandomness, _scoreRandomness);
            bombardScore += Random.Range(-_scoreRandomness, _scoreRandomness);

            Strategy next = Strategy.Balanced;
            float bestScore = balancedScore;

            if (pushScore > bestScore)
            {
                next = Strategy.Push;
                bestScore = pushScore;
            }

            if (bombardScore > bestScore)
            {
                next = Strategy.Bombard;
            }

            if (next != _strategy)
            {
                _strategy = next;
                _strategyChangedTime = Time.time;
            }
        }

        private void AssignOneRat()
        {
            RatGroundAI candidate = FindUnassignedEnemy();
            if (candidate == null)
            {
                return;
            }

            StrategyWeights weights = GetWeights(_strategy);
            CannonController cannon = FindAvailableCannon();
            float total = Mathf.Max(0f, weights.Ground)
                + Mathf.Max(0f, weights.Cannon);
            bool useCannon = cannon != null
                && total > 0f
                && Random.value < weights.Cannon / total;

            if (useCannon && candidate.AssignCannon(cannon))
            {
                return;
            }

            candidate.AssignGround(_enemyExit);
        }

        private RatGroundAI FindUnassignedEnemy()
        {
            foreach (RatAgent rat in RatAgent.Active)
            {
                if (rat == null
                    || rat.Faction != VehicleSide.Enemy
                    || rat.State != RatState.Idle)
                {
                    continue;
                }

                RatGroundAI groundAI = rat.GetComponent<RatGroundAI>();
                if (groundAI != null
                    && groundAI.Assignment == RatGroundAI.EnemyAssignment.None)
                {
                    return groundAI;
                }
            }

            return null;
        }

        private int CountArenaRats(VehicleSide faction)
        {
            int count = 0;

            foreach (RatAgent rat in RatAgent.Active)
            {
                if (rat != null
                    && rat.Faction == faction
                    && rat.State == RatState.GroundCombat)
                {
                    RatGroundAI groundAI = rat.GetComponent<RatGroundAI>();
                    if (groundAI != null && !groundAI.IsInfiltrated)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private CannonController FindAvailableCannon()
        {
            if (_enemyCannons == null)
            {
                return null;
            }

            for (int offset = 0; offset < _enemyCannons.Length; offset++)
            {
                int index = (_cannonIndex + offset) % _enemyCannons.Length;
                CannonController cannon = _enemyCannons[index];

                if (cannon == null
                    || !cannon.IsInstalledFor(VehicleSide.Enemy)
                    || cannon.IsFull)
                {
                    continue;
                }

                _cannonIndex = (index + 1) % _enemyCannons.Length;
                return cannon;
            }

            return null;
        }

        private StrategyWeights GetWeights(Strategy strategy)
        {
            switch (strategy)
            {
                case Strategy.Push:
                    return _push;
                case Strategy.Bombard:
                    return _bombard;
                default:
                    return _balanced;
            }
        }

        private float GetHealthRatio(RatStructure structure)
        {
            return structure.MaxHealth > 0f
                ? structure.Health / structure.MaxHealth
                : 0f;
        }
    }
}
