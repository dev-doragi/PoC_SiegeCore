using System;
public abstract class Singleton<T> : ManagedBehaviour where T : Singleton<T>
{
    public static T Instance { get; private set; }
    public static bool IsExisted => Instance != null;

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            throw new InvalidOperationException($"Duplicate {typeof(T).Name}.");
        }
        Instance = (T)this;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
