using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatFactory : MonoBehaviour
    {
        [SerializeField]
        private RatBattlefield _battlefield;

        [Header("Pools")]
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private PoolDefinition _basicPool;
        [SerializeField] private PoolDefinition _bbPool;
        [SerializeField] private PoolDefinition _bbbPool;

        public RatBattlefield Battlefield
        {
            get { return _battlefield; }
        }

        public bool IsReady
        {
            get
            {
                return _battlefield != null
                    && _basicPool != null
                    && _bbPool != null
                    && _bbbPool != null
                    && EnsurePoolManager();
            }
        }

        public RatAgent Spawn(
            RatForm form,
            VehicleSide faction,
            Vector3 position,
            bool combat = false,
            bool falling = false)
        {
            if (!EnsurePoolManager())
            {
                return null;
            }

            PoolDefinition definition =
                GetPoolDefinition(form);

            if (definition == null)
            {
                return null;
            }

            GameObject instance =
                _poolManager.Spawn(
                    definition,
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

                Vector3 landingPosition =
                    _battlefield.FloorBelow(
                        spawnPosition,
                        faction);

                RatAgent rat = Spawn(
                    RatForm.Basic,
                    faction,
                    landingPosition,
                    true,
                    true);

                if (rat != null)
                {
                    float fallHeight = Mathf.Max(
                        0.1f,
                        spawnPosition.y - landingPosition.y);

                    rat.Carryable.BeginRatFall(
                        _battlefield.Ground,
                        fallHeight);
                }
            }
        }

        private bool EnsurePoolManager()
        {
            if (_basicPool == null
                || _bbPool == null
                || _bbbPool == null
                || _basicPool.Prefab == null
                || _bbPool.Prefab == null
                || _bbbPool.Prefab == null)
            {
                return false;
            }

            return _poolManager != null
                && _poolManager.IsInitialized;
        }

        private PoolDefinition GetPoolDefinition(
            RatForm form)
        {
            switch (form)
            {
                case RatForm.Basic:
                    return _basicPool;

                case RatForm.BB:
                    return _bbPool;

                case RatForm.BBB:
                    return _bbbPool;

                default:
                    return null;
            }
        }
    }
}
