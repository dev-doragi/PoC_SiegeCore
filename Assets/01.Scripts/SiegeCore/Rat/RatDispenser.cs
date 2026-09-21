using System.Collections.Generic;
using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatDispenser : MonoBehaviour
    {
        [SerializeField] private RatFactory _factory;
        [SerializeField] private RatLoadoutDefinition _loadout;
        [SerializeField] private VehicleSide _faction;
        [Header("Spawn")]
        [SerializeField] private Transform _outlet;
        [SerializeField, Min(0.1f)] private float _spawnInterval = 2f;
        [Header("Economy")]
        [SerializeField] private RatStructure[] _productionFacilities = new RatStructure[0];
        [SerializeField, Range(0.01f, 1f)] private float _minimumProductionRatio = 0.25f;
        [SerializeField, Min(1)] private int _populationCap = 30;
        [Header("Ejection")]
        [SerializeField] private Vector2 _ejectDirection = new Vector2(1f, -0.2f);
        [SerializeField, Min(0f)] private float _ejectSpeed = 2f;

        private float _progress;
        private RatLoadoutRuntime _loadoutRuntime;
        private bool _loadoutConfigured;

        public VehicleSide Faction { get { return _faction; } }
        public RatLoadoutDefinition LoadoutDefinition { get { return _loadout; } }
        public float ProductionRatio { get; private set; } = 1f;
        public float Progress { get { return _progress; } }
        public int PopulationCap { get { return _populationCap; } }
        public int Population
        {
            get
            {
                int count = 0;
                foreach (RatAgent rat in RatAgent.Active)
                {
                    if (rat != null && !rat.IsDead && rat.Faction == _faction
                        && rat.Definition != null) count += (int)rat.Definition.Rank;
                }
                return count;
            }
        }

        private void OnEnable()
        {
            foreach (RatStructure facility in _productionFacilities)
            {
                if (facility != null) facility.Destroyed += HandleFacilityDestroyed;
            }
            RefreshProductionRatio();
        }

        private void Start()
        {
            if (!_loadoutConfigured) ConfigureLoadout();
            RefreshProductionRatio();
        }

        private void OnDisable()
        {
            foreach (RatStructure facility in _productionFacilities)
            {
                if (facility != null) facility.Destroyed -= HandleFacilityDestroyed;
            }
        }

        private void HandleFacilityDestroyed(RatStructure facility) { RefreshProductionRatio(); }

        private void RefreshProductionRatio()
        {
            int total = 0;
            int alive = 0;
            foreach (RatStructure facility in _productionFacilities)
            {
                if (facility == null || facility.IsEntrance || facility.Faction != _faction) continue;
                total++;
                if (!facility.IsDestroyed) alive++;
            }
            ProductionRatio = total == 0 ? 1f
                : Mathf.Lerp(_minimumProductionRatio, 1f, (float)alive / total);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (Population >= _populationCap)
            {
                _progress = 0f;
                return;
            }
            if (_factory == null || !_factory.IsReady || _outlet == null) return;
            _progress += Time.deltaTime * ProductionRatio / Mathf.Max(0.1f, _spawnInterval);
            if (_progress < 1f) return;
            _progress = 0f;
            SpawnRat();
        }

        public void SpawnRat()
        {
            if (Time.timeScale <= 0f || Population >= _populationCap
                || _factory == null || !_factory.IsReady || _outlet == null) return;
            if (_loadoutRuntime == null) ConfigureLoadout();
            if (_loadoutRuntime == null
                || !_loadoutRuntime.TryPeekNext(out RatDefinition definition)) return;

            RatAgent rat = _factory.SpawnIdle(definition, _faction, _outlet.position);
            if (rat == null) return;

            _loadoutRuntime.ConsumeNext();
            rat.Carryable.TryDispense(_factory.Battlefield.Ground,
                _ejectDirection.normalized, _ejectSpeed);
        }

        public void ApplyLoadout(RatLoadoutDefinition loadout)
        {
            _loadout = loadout;
            ConfigureLoadout();
        }

        public void ApplyLoadout(IList<RatLoadoutEntry> entries)
        {
            if (_loadoutRuntime == null) _loadoutRuntime = new RatLoadoutRuntime();
            RatDefinition fallback = _factory == null ? null : _factory.FallbackDefinition;
            _loadoutRuntime.ReplaceEntries(entries, fallback);
            _loadoutConfigured = true;
        }

        private void ConfigureLoadout()
        {
            if (_loadoutRuntime == null) _loadoutRuntime = new RatLoadoutRuntime();
            _loadoutConfigured = true;

            RatDefinition fallback = _factory == null ? null : _factory.FallbackDefinition;
            _loadoutRuntime.Configure(_loadout, fallback);

            if (_loadoutRuntime.UsingFallback)
            {
                Debug.LogWarning(
                    "[RatDispenser] No valid Rank1 loadout was assigned. Using the Basic Rank1 fallback.",
                    this);
            }
            else if (!_loadoutRuntime.HasCards)
            {
                Debug.LogError(
                    "[RatDispenser] Loadout has no usable cards and RatFactory has no valid Basic Rank1 fallback.",
                    this);
            }
        }
    }
}
