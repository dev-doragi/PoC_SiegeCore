using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : ManagedBehaviour
{
    [SerializeField] private Transform _poolRoot;
    private Transform _creationRoot;
    private readonly Dictionary<string, ObjectPool<GameObject>> _pools = new Dictionary<string, ObjectPool<GameObject>>();
    private readonly HashSet<PooledObject> _active = new HashSet<PooledObject>();

    protected override void OnInitialize()
    {
        GameObject staging = new GameObject("PoolCreation");
        staging.SetActive(false);
        staging.transform.SetParent(transform);
        _creationRoot = staging.transform;
        if (_poolRoot == null)
        {
            _poolRoot = transform;
        }
        if (_poolRoot != transform && !_poolRoot.IsChildOf(transform))
        {
            throw new System.InvalidOperationException("Pool root must belong to this scene pool.");
        }
    }

    public void RegisterPool(string key, GameObject prefab, int defaultCapacity = 8, int maxSize = 64)
    {
        if (!IsInitialized || string.IsNullOrWhiteSpace(key) || prefab == null || _pools.ContainsKey(key))
        {
            throw new System.InvalidOperationException("Invalid or duplicate pool registration.");
        }
        ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
            createFunc: () => CreatePooledObject(key, prefab),
            actionOnGet: null,
            actionOnRelease: ReleasePooledObject,
            actionOnDestroy: DestroyPooledObject,
            collectionCheck: true,
            defaultCapacity: Mathf.Max(1, defaultCapacity),
            maxSize: Mathf.Max(1, maxSize));

        _pools.Add(key, pool);
    }

    private GameObject CreatePooledObject(string key, GameObject prefab)
    {
        // 소유 풀을 연결하기 전에 OnEnable이 실행되지 않도록 비활성 부모에서 생성합니다.
        GameObject instance = Instantiate(prefab, _creationRoot);
        instance.SetActive(false);
        instance.transform.SetParent(_poolRoot);

        PooledObject handle = instance.GetComponent<PooledObject>();
        if (handle == null)
        {
            handle = instance.AddComponent<PooledObject>();
        }

        handle.Configure(this, key);
        return instance;
    }

    private void ReleasePooledObject(GameObject instance)
    {
        instance.SetActive(false);
        instance.transform.SetParent(_poolRoot);
    }

    private void DestroyPooledObject(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Destroy(instance);
    }

    public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
    {
        if (!_pools.TryGetValue(key, out ObjectPool<GameObject> pool))
        {
            throw new System.InvalidOperationException($"Pool not found: {key}");
        }
        GameObject instance = pool.Get();
        PooledObject handle = instance.GetComponent<PooledObject>();
        _active.Add(handle);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public void Despawn(string key, GameObject instance)
    {
        if (instance == null)
        {
            return;
        }
        PooledObject handle = instance.GetComponent<PooledObject>();
        if (handle == null || handle.Owner != this || handle.Key != key || !_active.Remove(handle))
        {
            Debug.LogError("[PoolManager] Foreign object or duplicate return.", this);
            return;
        }
        _pools[key].Release(instance);
    }

    public void Despawn(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }
        PooledObject handle = instance.GetComponent<PooledObject>();
        if (handle == null)
        {
            Debug.LogError("[PoolManager] Missing pool ownership.", this);
            return;
        }
        Despawn(handle.Key, instance);
    }

    public void ClearAllPools()
    {
        foreach (PooledObject handle in _active)
        {
            if (handle == null)
            {
                continue;
            }

            Destroy(handle.gameObject);
        }
        _active.Clear();
        foreach (ObjectPool<GameObject> pool in _pools.Values)
        {
            pool.Dispose();
        }
        _pools.Clear();
    }

    protected override void OnShutdown()
    {
        ClearAllPools();
        if (_creationRoot != null)
        {
            Destroy(_creationRoot.gameObject);
        }
    }
}
