using System;
using System.Collections.Generic;
using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatFactory : MonoBehaviour
    {
        [SerializeField] private RatBattlefield _battlefield;
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private RatCatalog _catalog;

        private readonly Dictionary<RatDefinition, PoolDefinition> _poolLookup =
            new Dictionary<RatDefinition, PoolDefinition>();
        private bool _lookupBuilt;
        private bool _lookupValid;

        public RatBattlefield Battlefield { get { return _battlefield; } }
        public RatDefinition FallbackDefinition
        {
            get { return _catalog == null ? null : _catalog.FallbackDefinition; }
        }

        public bool IsReady
        {
            get { return _battlefield != null && EnsureLookup() && _poolManager != null && _poolManager.IsInitialized; }
        }

        private void Awake() { EnsureLookup(); }
        private void OnValidate() { _lookupBuilt = false; }

        private bool EnsureLookup()
        {
            if (_lookupBuilt) return _lookupValid;
            _lookupBuilt = true;
            _lookupValid = _catalog != null
                && _catalog.Entries != null
                && _catalog.Entries.Length > 0;
            _poolLookup.Clear();
            if (!_lookupValid) return false;

            if (!_catalog.HasValidFallback)
            {
                Debug.LogError(
                    "[RatFactory] RatCatalog needs a Basic Rank1 fallback Definition.",
                    this);
                _lookupValid = false;
            }

            foreach (RatCatalogEntry entry in _catalog.Entries)
            {
                RatAgent prefab = entry.Pool != null && entry.Pool.Prefab != null
                    ? entry.Pool.Prefab.GetComponent<RatAgent>() : null;
                if (entry.Definition == null || prefab == null || prefab.Definition != entry.Definition
                    || _poolLookup.ContainsKey(entry.Definition))
                {
                    Debug.LogError("[RatFactory] Each entry needs a unique Definition and a pool prefab with the same Definition.", this);
                    _lookupValid = false;
                    continue;
                }
                _poolLookup.Add(entry.Definition, entry.Pool);
            }

            if (_lookupValid && !_poolLookup.ContainsKey(_catalog.FallbackDefinition))
            {
                Debug.LogError(
                    "[RatFactory] RatCatalog fallback Definition must also have a registered PoolDefinition.",
                    this);
                _lookupValid = false;
            }

            return _lookupValid;
        }

        public RatAgent Spawn(RatDefinition definition, VehicleSide faction, Vector3 position,
            bool combat = false, bool falling = false)
        {
            if (!IsReady || definition == null || !_poolLookup.TryGetValue(definition, out PoolDefinition pool))
                return null;

            GameObject instance = _poolManager.Spawn(pool, position, Quaternion.identity);
            if (instance == null) return null;

            // A pooled rat can have been parented to a carry/projectile transform
            // with a different local scale. Restore the root scale from the
            // exact prefab that belongs to this pool before reinitializing it.
            instance.transform.localScale = pool.Prefab.transform.localScale;

            RatAgent rat = instance.GetComponent<RatAgent>();
            if (rat == null || rat.Definition != definition)
            {
                PooledObject pooledObject = instance.GetComponent<PooledObject>();
                if (pooledObject != null) pooledObject.Return();
                else instance.SetActive(false);
                Debug.LogError("[RatFactory] Spawned rat does not match the requested Definition.", this);
                return null;
            }

            rat.transform.SetPositionAndRotation(position, Quaternion.identity);
            rat.ResetRat(faction, this, combat, falling);
            return rat;
        }

        public RatAgent SpawnIdle(RatDefinition definition, VehicleSide faction, Vector3 position)
        {
            return Spawn(definition, faction, position);
        }

        public RatAgent SpawnGroundCombat(RatDefinition definition, VehicleSide faction, Vector3 position)
        {
            return Spawn(definition, faction, position, true, false);
        }
    }
}
