using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : ManagedBehaviour
{
    [SerializeField] private Transform _poolRoot;
    private Transform _creationRoot;
    private readonly Dictionary<PoolDefinition, ObjectPool<GameObject>> _pools = new Dictionary<PoolDefinition, ObjectPool<GameObject>>();
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

    private ObjectPool<GameObject> GetOrCreatePool(PoolDefinition definition)
    {
        if (!IsInitialized || definition == null || definition.Prefab == null)
        {
            throw new System.InvalidOperationException("Pool definition is invalid.");
        }

        if (_pools.TryGetValue(definition, out ObjectPool<GameObject> existingPool))
        {
            return existingPool;
        }

        int defaultCapacity = Mathf.Max(1, definition.DefaultCapacity);
        int maxSize = Mathf.Max(defaultCapacity, definition.MaxSize);
        ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
            createFunc: () => CreatePooledObject(definition),
            actionOnGet: null,
            actionOnRelease: ReleasePooledObject,
            actionOnDestroy: DestroyPooledObject,
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);

        _pools.Add(definition, pool);
        return pool;
    }

    private GameObject CreatePooledObject(PoolDefinition definition)
    {
        // 소유 풀을 연결하기 전에 OnEnable이 실행되지 않도록 비활성 부모에서 생성합니다.
        GameObject instance = Instantiate(definition.Prefab, _creationRoot);
        instance.SetActive(false);
        instance.transform.SetParent(_poolRoot);

        PooledObject handle = instance.GetComponent<PooledObject>();
        if (handle == null)
        {
            handle = instance.AddComponent<PooledObject>();
        }

        handle.Configure(this, definition);
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

    public GameObject Spawn(PoolDefinition definition, Vector3 position, Quaternion rotation)
    {
        ObjectPool<GameObject> pool = GetOrCreatePool(definition);
        GameObject instance = pool.Get();
        PooledObject handle = instance.GetComponent<PooledObject>();
        _active.Add(handle);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public void Despawn(PoolDefinition definition, GameObject instance)
    {
        if (instance == null)
        {
            return;
        }
        PooledObject handle = instance.GetComponent<PooledObject>();
        if (handle == null || handle.Owner != this || handle.Definition != definition || !_active.Remove(handle))
        {
            Debug.LogError("[PoolManager] Foreign object or duplicate return.", this);
            return;
        }
        if (!_pools.TryGetValue(definition, out ObjectPool<GameObject> pool))
        {
            Debug.LogError("[PoolManager] Missing pool definition.", this);
            return;
        }
        pool.Release(instance);
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
        Despawn(handle.Definition, instance);
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
