using UnityEngine;
public sealed class PooledObject : MonoBehaviour
{
    public PoolManager Owner { get; private set; }
    public PoolDefinition Definition { get; private set; }

    internal void Configure(PoolManager owner, PoolDefinition definition)
    {
        Owner = owner;
        Definition = definition;
    }

    public void Return()
    {
        if (Owner != null)
        {
            Owner.Despawn(Definition, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
