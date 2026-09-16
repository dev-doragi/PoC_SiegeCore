using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatFactory : MonoBehaviour
    {
        [SerializeField]
        private RatBattlefield _battlefield;

        [Header("Prefabs")]
        [SerializeField] private RatAgent _basicPrefab;
        [SerializeField] private RatAgent _bbPrefab;
        [SerializeField] private RatAgent _bbbPrefab;

        [Header("Pool")]
        [SerializeField, Min(1)] private int _defaultPoolCapacity = 16;
        [SerializeField, Min(1)] private int _maxPoolSize = 128;

        private PoolManager _poolManager;
        private bool _poolsRegistered;

        private string _basicPoolKey;
        private string _bbPoolKey;
        private string _bbbPoolKey;

        public RatBattlefield Battlefield
        {
            get { return _battlefield; }
        }

        public bool IsReady
        {
            get
            {
                return _battlefield != null
                    && _basicPrefab != null
                    && _bbPrefab != null
                    && _bbbPrefab != null
                    && EnsurePools();
            }
        }

        public RatAgent Spawn(
            RatForm form,
            VehicleSide faction,
            Vector3 position,
            bool combat = false,
            bool falling = false)
        {
            if (!EnsurePools())
            {
                return null;
            }

            string key =
                GetPoolKey(form);

            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            GameObject instance =
                _poolManager.Spawn(
                    key,
                    position,
                    Quaternion.identity);

            RatAgent rat =
                instance.GetComponent<RatAgent>();

            if (rat == null)
            {
                PooledObject pooledObject =
                    instance.GetComponent<PooledObject>();

                if (pooledObject != null)
                {
                    pooledObject.Return();
                }
                else
                {
                    instance.SetActive(false);
                }

                Debug.LogError(
                    "[RatFactory] Rat prefab must contain RatAgent.",
                    this);

                return null;
            }

            rat.transform.SetPositionAndRotation(
                position,
                Quaternion.identity);

            rat.ResetRat(
                faction,
                this,
                combat,
                falling);

            return rat;
        }

        public RatAgent SpawnIdle(
            VehicleSide faction,
            Vector3 position)
        {
            return Spawn(
                RatForm.Basic,
                faction,
                position);
        }

        public RatAgent SpawnGroundCombat(
            RatForm form,
            VehicleSide faction,
            Vector3 position)
        {
            return Spawn(
                form,
                faction,
                position,
                true,
                false);
        }

        public void Burst(
            VehicleSide faction,
            Vector3 position)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset =
                    Random.insideUnitCircle * 0.2f;

                Vector3 spawnPosition =
                    position
                    + new Vector3(
                        offset.x,
                        offset.y,
                        0f);

                Spawn(
                    RatForm.Basic,
                    faction,
                    spawnPosition,
                    true,
                    true);
            }
        }

        private bool EnsurePools()
        {
            if (_poolsRegistered)
            {
                return true;
            }

            if (_basicPrefab == null
                || _bbPrefab == null
                || _bbbPrefab == null)
            {
                return false;
            }

            if (_poolManager == null)
            {
                _poolManager =
                    FindFirstObjectByType<PoolManager>();
            }

            if (_poolManager == null
                || !_poolManager.IsInitialized)
            {
                return false;
            }

            string prefix =
                "RatFactory."
                + GetInstanceID()
                + ".";

            _basicPoolKey = prefix + "Basic";
            _bbPoolKey = prefix + "BB";
            _bbbPoolKey = prefix + "BBB";

            int defaultCapacity =
                Mathf.Max(1, _defaultPoolCapacity);

            int maxSize =
                Mathf.Max(defaultCapacity, _maxPoolSize);

            _poolManager.RegisterPool(
                _basicPoolKey,
                _basicPrefab.gameObject,
                defaultCapacity,
                maxSize);

            _poolManager.RegisterPool(
                _bbPoolKey,
                _bbPrefab.gameObject,
                defaultCapacity,
                maxSize);

            _poolManager.RegisterPool(
                _bbbPoolKey,
                _bbbPrefab.gameObject,
                defaultCapacity,
                maxSize);

            _poolsRegistered = true;
            return true;
        }

        private string GetPoolKey(
            RatForm form)
        {
            switch (form)
            {
                case RatForm.Basic:
                    return _basicPoolKey;

                case RatForm.BB:
                    return _bbPoolKey;

                case RatForm.BBB:
                    return _bbbPoolKey;

                default:
                    return null;
            }
        }
    }
}
