using UnityEngine;
public sealed class PooledObject : MonoBehaviour
{
    public PoolManager Owner { get; private set; }
    public string Key { get; private set; }
    internal void Configure(PoolManager owner, string key)
    {
        Owner = owner;
        Key = key;
    }

    public void Return()
    {
        if (Owner != null)
        {
            Owner.Despawn(Key, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
