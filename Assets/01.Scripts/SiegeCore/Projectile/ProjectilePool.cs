using System.Collections;
using System.Collections.Generic;
using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Projectile
{
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private Projectile _projectilePrefab;
        [SerializeField] private string _poolKey = "SiegeProjectile";
        [SerializeField, Min(1)] private int _defaultCapacity = 12;
        [SerializeField, Min(1)] private int _maxSize = 64;
        [SerializeField, Min(0f)] private float _cancellationDistance = 0.4f;
        [SerializeField, Min(0f)] private float _cancellationHeightDifference = 0.35f;

        private readonly List<Projectile> _activeProjectiles = new List<Projectile>();
        private bool _isRegistered;

        private void Start()
        {
            StartCoroutine(RegisterWhenPoolIsReady());
        }

        private IEnumerator RegisterWhenPoolIsReady()
        {
            if (_poolManager == null || _projectilePrefab == null)
            {
                Debug.LogError("[ProjectilePool] Assign PoolManager and Projectile Prefab.", this);
                yield break;
            }

            if (!_poolManager.IsInitialized) _poolManager.Initialize();
            _poolManager.RegisterPool(_poolKey, _projectilePrefab.gameObject, _defaultCapacity, _maxSize);
            _isRegistered = true;
        }

        private void Update()
        {
            float cancellationDistanceSqr = _cancellationDistance * _cancellationDistance;
            for (int firstIndex = _activeProjectiles.Count - 1; firstIndex >= 0; firstIndex--)
            {
                Projectile first = _activeProjectiles[firstIndex];
                if (first == null || !first.IsCannonFlight)
                {
                    _activeProjectiles.RemoveAt(firstIndex);
                    continue;
                }

                for (int secondIndex = firstIndex - 1; secondIndex >= 0; secondIndex--)
                {
                    Projectile second = _activeProjectiles[secondIndex];
                    if (second == null || !second.IsCannonFlight) continue;
                    if (first.Side == second.Side) continue;
                    if (((Vector2)first.transform.position - (Vector2)second.transform.position).sqrMagnitude > cancellationDistanceSqr) continue;
                    if (Mathf.Abs(first.Height - second.Height) > _cancellationHeightDifference) continue;

                    first.ReturnToPool();
                    second.ReturnToPool();
                    return;
                }
            }
        }

        public bool TrySpawn(Vector3 startPosition, Vector3 targetPosition, VehicleSide side,
            CannonTrajectoryType trajectoryType, float flightDuration, float arcHeight, float damage)
        {
            if (!_isRegistered || _poolManager == null) return false;

            GameObject instance = _poolManager.Spawn(_poolKey, startPosition, Quaternion.identity);
            Projectile projectile = instance.GetComponent<Projectile>();
            if (projectile == null)
            {
                _poolManager.Despawn(instance);
                Debug.LogError("[ProjectilePool] Projectile Prefab requires a Projectile component.", this);
                return false;
            }

            projectile.Launch(this, startPosition, targetPosition, side, trajectoryType, flightDuration, arcHeight, damage);
            _activeProjectiles.Add(projectile);
            return true;
        }

        public void Return(Projectile projectile)
        {
            if (projectile == null) return;
            _activeProjectiles.Remove(projectile);
            PooledObject handle = projectile.GetComponent<PooledObject>();
            if (handle != null) handle.Return();
        }
    }
}
