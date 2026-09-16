using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatDispenser : MonoBehaviour
    {
        [SerializeField] private RatFactory _factory;
        [SerializeField] private VehicleSide _faction;

        [Header("Spawn")]
        [SerializeField] private Transform _outlet;
        [SerializeField, Min(0.1f)]
        private float _spawnInterval = 2f;

        [Header("Ejection")]
        [SerializeField]
        private Vector2 _ejectDirection =
            new Vector2(1f, -0.2f);

        [SerializeField, Min(0f)]
        private float _ejectSpeed = 2f;

        private float _nextSpawnTime;

        private void Start()
        {
            _nextSpawnTime =
                Time.time + _spawnInterval;
        }

        private void Update()
        {
            if (Time.time < _nextSpawnTime)
            {
                return;
            }

            _nextSpawnTime =
                Time.time + _spawnInterval;

            SpawnRat();
        }

        public void SpawnRat()
        {
            if (_factory == null
                || !_factory.IsReady
                || _outlet == null)
            {
                return;
            }

            RatAgent rat =
                _factory.SpawnIdle(
                    _faction,
                    _outlet.position);

            if (rat == null)
            {
                return;
            }

            rat.Carryable.TryDispense(
                _factory.Battlefield.Ground,
                _ejectDirection.normalized,
                _ejectSpeed);
        }
    }
}
