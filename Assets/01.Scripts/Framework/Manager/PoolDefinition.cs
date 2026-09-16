using UnityEngine;

[CreateAssetMenu(menuName = "Framework/Pool Definition")]
public sealed class PoolDefinition : ScriptableObject
{
    [SerializeField] private GameObject _prefab;
    [SerializeField, Min(1)] private int _defaultCapacity = 8;
    [SerializeField, Min(1)] private int _maxSize = 64;

    public GameObject Prefab => _prefab;
    public int DefaultCapacity => _defaultCapacity;
    public int MaxSize => _maxSize;
}
