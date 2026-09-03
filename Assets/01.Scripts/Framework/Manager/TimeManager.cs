using UnityEngine;

[DefaultExecutionOrder(-780)]
public class TimeManager : Singleton<TimeManager>
{
    [SerializeField, Range(0f, 1f)] private float _defaultHitStopTimeScale = 0.15f;

    private bool _isStatePaused;
    private float _hitStopTimer;
    private float _hitStopTimeScale;
    private float _slowMotionTimer;
    private float _slowMotionScale = 1f;

    private GameManager _game;
    private float _baseFixedDeltaTime;

    public void Configure(GameManager game)
    {
        _game = game;
    }

    protected override void OnInitialize()
    {
        _baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    protected override void OnStartService()
    {
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Subscribe<HitStopRequestedEvent>(OnHitStopRequested);
        EventBus.Instance.Subscribe<SlowMotionRequestedEvent>(OnSlowMotionRequested);
        ResetTime();
    }

    protected override void OnStopService()
    {
        EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Unsubscribe<HitStopRequestedEvent>(OnHitStopRequested);
        EventBus.Instance.Unsubscribe<SlowMotionRequestedEvent>(OnSlowMotionRequested);
    }

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }
        if (_hitStopTimer > 0f)
        {
            _hitStopTimer = Mathf.Max(0f, _hitStopTimer - Time.unscaledDeltaTime);
        }

        if (_slowMotionTimer > 0f)
        {
            _slowMotionTimer = Mathf.Max(0f, _slowMotionTimer - Time.unscaledDeltaTime);
        }

        ApplyTimeScale();
    }

    public void ResetTime()
    {
        _hitStopTimer = 0f;
        _slowMotionTimer = 0f;
        _slowMotionScale = 1f;
        _hitStopTimeScale = _defaultHitStopTimeScale;
        _isStatePaused = IsPauseState(_game.CurrentState);
        ApplyTimeScale();
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        _isStatePaused = IsPauseState(evt.NewState);
        ApplyTimeScale();
    }

    private void OnHitStopRequested(HitStopRequestedEvent evt)
    {
        _hitStopTimer = Mathf.Max(0f, evt.Duration);
        _hitStopTimeScale = Mathf.Clamp(evt.TimeScale <= 0f ? _defaultHitStopTimeScale : evt.TimeScale, 0f, 1f);
        ApplyTimeScale();
    }

    private void OnSlowMotionRequested(SlowMotionRequestedEvent evt)
    {
        _slowMotionTimer = Mathf.Max(0f, evt.Duration);
        _slowMotionScale = Mathf.Clamp(evt.TimeScale, 0f, 1f);
        ApplyTimeScale();
    }

    private void ApplyTimeScale()
    {
        float target = 1f;

        if (_isStatePaused)
        {
            target = 0f;
        }
        else if (_hitStopTimer > 0f)
        {
            target = _hitStopTimeScale;
        }
        else if (_slowMotionTimer > 0f)
        {
            target = _slowMotionScale;
        }

        if (!Mathf.Approximately(Time.timeScale, target))
        {
            Time.timeScale = target;
            Time.fixedDeltaTime = _baseFixedDeltaTime * (target > 0f ? target : 1f);
            EventBus.Instance.Publish(new TimeScaleChangedEvent { NewTimeScale = target });
        }
    }

    protected override void OnShutdown()
    {
        Time.timeScale = 1f;
        if (_baseFixedDeltaTime > 0f)
        {
            Time.fixedDeltaTime = _baseFixedDeltaTime;
        }
    }

    private static bool IsPauseState(GameState state)
    {
        return state == GameState.Paused || state == GameState.GameOver;
    }
}
