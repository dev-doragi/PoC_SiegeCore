using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class Bootstrapper : MonoBehaviour
{
    public static Bootstrapper Instance { get; private set; }
    public bool IsReady { get; private set; }
    public GameManager Game { get; private set; }
    public TimeManager TimeService { get; private set; }
    private ManagedBehaviour[] _services = Array.Empty<ManagedBehaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRoot()
    {
        Bootstrapper prefab = Resources.Load<Bootstrapper>("AppRoot");
        if (prefab == null)
        {
            throw new InvalidOperationException("Resources/AppRoot is required. Run Framework/Create Setup.");
        }
        Instantiate(prefab);
    }

    private T Require<T>() where T : ManagedBehaviour
    {
        T[] matches = GetComponentsInChildren<T>(true);
        if (matches.Length != 1 || !matches[0].enabled || !matches[0].gameObject.activeInHierarchy)
        {
            throw new InvalidOperationException($"AppRoot requires one active {typeof(T).Name}.");
        }
        return matches[0];
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 자식 매니저의 Awake/OnEnable이 끝난 뒤 초기화를 시작합니다.
    private void Start()
    {
        if (Instance != this)
        {
            return;
        }
        try
        {
            ConfigureServices();
            InitializeServices();
            StartServices();
            Game.BeginBoot();
            IsReady = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            StopServices();
            gameObject.SetActive(false);
        }
    }

    private void ConfigureServices()
    {
        Game = Require<GameManager>();
        TimeService = Require<TimeManager>();
        SoundManager sound = Require<SoundManager>();
        SceneLoader loader = Require<SceneLoader>();
        InputReader input = Require<InputReader>();

        TimeService.Configure(Game);
        loader.Configure(Game, TimeService);
        input.Configure(Game);

        _services = new ManagedBehaviour[] { Game, TimeService, sound, loader, input };
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

    private void StopServices()
    {
        IsReady = false;
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
        if (Instance != this)
        {
            return;
        }
        StopServices();
        Instance = null;
    }
}
