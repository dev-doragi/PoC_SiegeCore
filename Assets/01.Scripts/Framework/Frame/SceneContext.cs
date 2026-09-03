using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-800)]
public sealed class SceneContext : MonoBehaviour
{
    [SerializeField] private GameState _initialState = GameState.Playing;
    [SerializeField] private ManagedBehaviour[] _services = Array.Empty<ManagedBehaviour>();

    private void Start()
    {
        try
        {
            Bootstrapper app = Bootstrapper.Instance;
            ValidateApp(app);
            ConfigureServices(app.Game);
            InitializeServices();
            StartServices();

            if (!app.Game.CompleteLoading(_initialState))
            {
                throw new InvalidOperationException("Load scenes through SceneLoader.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Shutdown();
            gameObject.SetActive(false);
        }
    }

    private void ValidateApp(Bootstrapper app)
    {
        if (app == null || !app.IsReady)
        {
            throw new InvalidOperationException("AppRoot is not ready.");
        }

        if (_initialState != GameState.Playing && _initialState != GameState.MainMenu)
        {
            throw new InvalidOperationException("Initial state must be Playing or MainMenu.");
        }
    }

    private void ConfigureServices(GameManager game)
    {
        HashSet<ManagedBehaviour> registeredServices = new HashSet<ManagedBehaviour>();

        foreach (ManagedBehaviour service in _services)
        {
            ValidateService(service, registeredServices);
            ConfigureService(service, game);
        }
    }

    private void ValidateService(ManagedBehaviour service, HashSet<ManagedBehaviour> registeredServices)
    {
        if (service == null || !service.isActiveAndEnabled)
        {
            throw new InvalidOperationException("Scene services must exist and be active.");
        }

        if (!service.transform.IsChildOf(transform))
        {
            throw new InvalidOperationException("Scene services must be children of SceneContext.");
        }

        if (!registeredServices.Add(service))
        {
            throw new InvalidOperationException("A scene service cannot be registered twice.");
        }
    }

    private void ConfigureService(ManagedBehaviour service, GameManager game)
    {
        if (service is GameFlowManager flow)
        {
            flow.Configure(game);
            return;
        }

        if (service is UIManager ui)
        {
            ui.Configure(game);
            return;
        }

        if (service is CameraManager || service is PoolManager)
        {
            return;
        }

        throw new InvalidOperationException("Only scene-owned services belong in SceneContext.");
    }

    private void InitializeServices()
    {
        foreach (ManagedBehaviour service in _services)
        {
            service.Initialize();
        }
    }

    private void StartServices()
    {
        foreach (ManagedBehaviour service in _services)
        {
            service.StartService();
        }
    }

    private void Shutdown()
    {
        // 생성할 때 사용한 의존성이 해제 시에도 남도록 역순으로 정리합니다.
        for (int index = _services.Length - 1; index >= 0; index--)
        {
            ManagedBehaviour service = _services[index];
            if (service == null)
            {
                continue;
            }

            service.Shutdown();
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
