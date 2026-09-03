using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : Singleton<SceneLoader>
{
    private GameManager _game;
    private TimeManager _time;
    private AsyncOperation _operation;
    public bool IsLoading => _operation != null;

    public void Configure(GameManager game, TimeManager time)
    {
        _game = game;
        _time = time;
    }

    protected override void OnStartService()
    {
        EventBus.Instance.Subscribe<SceneLoadRequestedEvent>(OnRequest);
    }

    protected override void OnStopService()
    {
        EventBus.Instance.Unsubscribe<SceneLoadRequestedEvent>(OnRequest);
    }

    private void OnRequest(SceneLoadRequestedEvent request)
    {
        RequestLoad(request.SceneName);
    }

    public void RequestLoad(string sceneName)
    {
        if (!CanRequestLoad(sceneName))
        {
            Debug.LogError($"[SceneLoader] Cannot load scene: {sceneName}", this);
            return;
        }
        GameState previous = _game.CurrentState;
        if (!_game.BeginLoading())
        {
            return;
        }
        _time.ResetTime();
        try
        {
            _operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (_operation == null)
            {
                throw new System.InvalidOperationException("Scene load did not start.");
            }
            _operation.completed += OnCompleted;
        }
        catch (System.Exception exception)
        {
            _operation = null;
            _game.CancelLoading(previous);
            Debug.LogException(exception, this);
        }
    }

    private bool CanRequestLoad(string sceneName)
    {
        if (!IsInitialized || !isActiveAndEnabled || IsLoading)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private void OnCompleted(AsyncOperation operation)
    {
        operation.completed -= OnCompleted;
        _operation = null;
        EventBus.Instance.Publish(new SceneLoadedEvent { SceneName = SceneManager.GetActiveScene().name });
        // SceneContext completes the state transition after initializing the destination.
    }

    protected override void OnShutdown()
    {
        if (_operation != null)
        {
            _operation.completed -= OnCompleted;
        }
        _operation = null;
    }
}
