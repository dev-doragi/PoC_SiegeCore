using System;
using UnityEngine;

public abstract class ManagedBehaviour : MonoBehaviour
{
    public bool IsInitialized { get; private set; }
    // 비활성화는 일시 중단이며, 소유자가 요청한 시작 상태는 유지합니다.
    private bool _startRequested;
    private bool _isRunning;
    private bool _initializationAttempted;

    protected virtual void Awake()
    {
    }

    public void Initialize()
    {
        if (IsInitialized)
        {
            return;
        }
        if (_initializationAttempted)
        {
            throw new InvalidOperationException($"{name}: previous initialization failed.");
        }
        _initializationAttempted = true;
        OnInitialize();
        IsInitialized = true;
    }

    public void StartService()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException($"{name}: not initialized.");
        }
        _startRequested = true;
        Resume();
    }

    private void Resume()
    {
        if (!_startRequested || _isRunning || !isActiveAndEnabled)
        {
            return;
        }
        _isRunning = true;
        try
        {
            OnStartService();
        }
        catch
        {
            Pause();
            throw;
        }
    }

    private void Pause()
    {
        if (!_isRunning)
        {
            return;
        }
        _isRunning = false;
        OnStopService();
    }

    public void Shutdown()
    {
        _startRequested = false;
        Pause();
        if (!_initializationAttempted)
        {
            return;
        }
        _initializationAttempted = false;
        IsInitialized = false;
        OnShutdown();
    }

    protected virtual void OnEnable()
    {
        Resume();
    }

    protected virtual void OnDisable()
    {
        Pause();
    }

    protected virtual void OnDestroy()
    {
        Shutdown();
    }

    protected virtual void OnInitialize()
    {
    }

    protected virtual void OnStartService()
    {
    }

    protected virtual void OnStopService()
    {
    }

    protected virtual void OnShutdown()
    {
    }
}
