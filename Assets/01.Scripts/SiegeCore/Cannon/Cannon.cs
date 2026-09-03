using System.Collections;
using System.Collections.Generic;
using SiegeCore.Player;
using UnityEngine;

namespace SiegeCore.Cannon
{
    public sealed class Cannon : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _storagePoint;
        [SerializeField] private Transform _muzzle;

        [Header("Loading")]
        [SerializeField, Min(1)] private int _maxLoadCount = 3;

        [Header("Fire")]
        [SerializeField, Min(0.01f)] private float _fireInterval = 0.4f;
        [Tooltip("World-space firing direction.")]
        [SerializeField] private Vector2 _fireDirection = Vector2.right;

        private readonly Queue<CarryableObject> _loadedObjects = new Queue<CarryableObject>();
        private Coroutine _fireRoutine;

        public int LoadedCount
        {
            get
            {
                RemoveMissingObjects();
                return _loadedObjects.Count;
            }
        }
        public bool IsFull => LoadedCount >= Mathf.Max(1, _maxLoadCount);

        private void Awake()
        {
            if (_storagePoint == null || _muzzle == null || _fireDirection.sqrMagnitude < 0.0001f)
            {
                Debug.LogError("[Cannon] Assign StoragePoint, Muzzle and a nonzero Fire Direction.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            StartFiringIfNeeded();
        }

        private void OnDisable()
        {
            if (_fireRoutine != null) StopCoroutine(_fireRoutine);
            _fireRoutine = null;
            // Keep the queue so re-enabling the cannon resumes firing.
        }

        public bool TryLoad(CarryableObject item)
        {
            if (!isActiveAndEnabled || item == null || IsFull || _storagePoint == null
                || _muzzle == null || _fireDirection.sqrMagnitude < 0.0001f) return false;
            if (!item.TryEnterCannon(_storagePoint)) return false;

            _loadedObjects.Enqueue(item);
            StartFiringIfNeeded();
            return true;
        }

        private void StartFiringIfNeeded()
        {
            if (_fireRoutine == null && LoadedCount > 0)
                _fireRoutine = StartCoroutine(FireRoutine());
        }

        private IEnumerator FireRoutine()
        {
            while (LoadedCount > 0)
            {
                // Wait before dequeueing: the pending shot still occupies a storage slot.
                yield return new WaitForSeconds(Mathf.Max(0.01f, _fireInterval));
                if (LoadedCount == 0) break;
                if (_muzzle == null || _fireDirection.sqrMagnitude < 0.0001f)
                {
                    Debug.LogError("[Cannon] Cannot fire without a muzzle and direction.", this);
                    enabled = false;
                    break;
                }
                CarryableObject item = _loadedObjects.Dequeue();
                item.LaunchFromCannon(_muzzle.position, _fireDirection.normalized);
            }
            _fireRoutine = null;
        }

        private void RemoveMissingObjects()
        {
            int count = _loadedObjects.Count;
            for (int index = 0; index < count; index++)
            {
                CarryableObject item = _loadedObjects.Dequeue();
                if (item != null && item.IsLoaded) _loadedObjects.Enqueue(item);
            }
        }
    }
}
