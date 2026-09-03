public class GameManager : Singleton<GameManager>
{
    public GameState CurrentState { get; private set; } = GameState.None;

    public bool BeginBoot()
    {
        return Transition(GameState.None, GameState.Booting);
    }

    public bool RequestPause()
    {
        return Transition(GameState.Playing, GameState.Paused);
    }

    public bool RequestResume()
    {
        return Transition(GameState.Paused, GameState.Playing);
    }

    public void TogglePause()
    {
        if (CurrentState == GameState.Paused)
        {
            RequestResume();
            return;
        }

        RequestPause();
    }

    public bool BeginLoading()
    {
        if (CurrentState == GameState.None || CurrentState == GameState.Loading)
        {
            return false;
        }
        ChangeState(GameState.Loading);
        return true;
    }

    public bool CompleteLoading(GameState destination)
    {
        if (CurrentState != GameState.Loading && CurrentState != GameState.Booting)
        {
            return false;
        }
        if (destination != GameState.Playing && destination != GameState.MainMenu)
        {
            return false;
        }
        ChangeState(destination);
        return true;
    }

    public bool EndGame()
    {
        if (CurrentState != GameState.Playing && CurrentState != GameState.Paused)
        {
            return false;
        }
        ChangeState(GameState.GameOver);
        return true;
    }

    internal void CancelLoading(GameState previous)
    {
        if (CurrentState == GameState.Loading)
        {
            ChangeState(previous);
        }
    }

    private bool Transition(GameState expected, GameState next)
    {
        if (CurrentState != expected)
        {
            return false;
        }
        ChangeState(next);
        return true;
    }

    private void ChangeState(GameState next)
    {
        GameState previous = CurrentState;
        CurrentState = next;
        EventBus.Instance.Publish(new GameStateChangedEvent { PreviousState = previous, NewState = next });
    }

    protected override void OnStartService()
    {
        EventBus.Instance.Subscribe<PauseRequestedEvent>(OnPauseRequested);
    }

    protected override void OnStopService()
    {
        EventBus.Instance.Unsubscribe<PauseRequestedEvent>(OnPauseRequested);
    }

    private void OnPauseRequested(PauseRequestedEvent request)
    {
        if (request.Pause)
        {
            RequestPause();
            return;
        }

        RequestResume();
    }
}
