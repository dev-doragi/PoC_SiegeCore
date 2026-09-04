using System;
using System.Collections.Generic;
using SiegeCore.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Cannon
{
    public sealed class AmmoDispenser : MonoBehaviour
    {
        [Serializable]
        private sealed class AmmoEntry
        {
            public CarryableObject Prefab = null;
            [Min(1)] public int Count = 30;
        }

        private sealed class Stock
        {
            public string Key;
            public int Capacity;
            public readonly List<CarryableObject> Active = new List<CarryableObject>();
        }

        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private Transform _outlet;
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private AmmoEntry[] _ammo = Array.Empty<AmmoEntry>();
        [SerializeField] private bool _automatic = true;
        [SerializeField, Min(0.05f)] private float _interval = 2f;
        [Tooltip("World-space XY ejection direction.")]
        [SerializeField] private Vector2 _ejectDirection = Vector2.right;
        [SerializeField, Min(0f)] private float _ejectSpeed = 2f;

        private readonly List<Stock> _stocks = new List<Stock>();
        private bool _ready;
        private float _nextDispenseTime;

        private void Start()
        {
            if (_poolManager == null || _outlet == null || _groundTilemap == null || _ammo.Length == 0)
            {
                Debug.LogError("[AmmoDispenser] Assign PoolManager, Outlet, Ground Tilemap and Ammo entries.", this);
                enabled = false;
                return;
            }
            foreach (AmmoEntry entry in _ammo)
            {
                if (entry == null || entry.Prefab == null)
                {
                    Debug.LogError("[AmmoDispenser] Every Ammo entry requires a CarryableObject prefab.", this);
                    enabled = false;
                    return;
                }
            }

            if (!_poolManager.IsInitialized) _poolManager.Initialize();
            for (int index = 0; index < _ammo.Length; index++)
            {
                AmmoEntry entry = _ammo[index];
                Stock stock = new Stock
                {
                    Key = $"SiegeAmmo:{GetInstanceID()}:{index}",
                    Capacity = Mathf.Max(1, entry.Count)
                };
                _poolManager.RegisterPool(stock.Key, entry.Prefab.gameObject, stock.Capacity, stock.Capacity);
                // Hold all instances until creation finishes so the pool contains Count distinct objects.
                List<GameObject> warmup = new List<GameObject>(stock.Capacity);
                for (int count = 0; count < stock.Capacity; count++)
                    warmup.Add(_poolManager.Spawn(stock.Key, _outlet.position, Quaternion.identity));
                foreach (GameObject instance in warmup) _poolManager.Despawn(instance);
                _stocks.Add(stock);
            }
            _ready = true;
            _nextDispenseTime = Time.time + Mathf.Max(0.05f, _interval);
        }

        private void Update()
        {
            if (!_automatic || Time.timeScale <= 0f || Time.time < _nextDispenseTime) return;
            _nextDispenseTime = Time.time + Mathf.Max(0.05f, _interval);
            TryDispense();
        }

        public bool TryDispense()
        {
            if (!_ready || !isActiveAndEnabled || _poolManager == null || !_poolManager.IsInitialized || _outlet == null) return false;

            int available = 0;
            foreach (Stock stock in _stocks)
            {
                for (int index = stock.Active.Count - 1; index >= 0; index--)
                {
                    CarryableObject item = stock.Active[index];
                    if (item == null || !item.gameObject.activeSelf) stock.Active.RemoveAt(index);
                }
                available += stock.Capacity - stock.Active.Count;
            }
            if (available == 0) return false;

            // Each remaining ammunition unit gets the same probability, including mixed stock counts.
            int choice = UnityEngine.Random.Range(0, available);
            foreach (Stock stock in _stocks)
            {
                int remaining = stock.Capacity - stock.Active.Count;
                if (choice >= remaining)
                {
                    choice -= remaining;
                    continue;
                }
                GameObject instance = _poolManager.Spawn(stock.Key, _outlet.position, Quaternion.identity);
                CarryableObject item = instance.GetComponent<CarryableObject>();
                if (!item.TryDispense(_groundTilemap, _ejectDirection, _ejectSpeed))
                {
                    _poolManager.Despawn(instance);
                    Debug.LogError("[AmmoDispenser] Ammo prefab requires a valid separate Visual child.", this);
                    return false;
                }
                stock.Active.Add(item);
                return true;
            }
            return false;
        }
    }
}
