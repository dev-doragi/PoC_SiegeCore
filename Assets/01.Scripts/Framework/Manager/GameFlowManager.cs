using UnityEngine;

[DefaultExecutionOrder(-795)]
public class GameFlowManager : ManagedBehaviour
{
    public InGameState CurrentState { get; private set; } = InGameState.None;

    private GameManager _game;

    public void Configure(GameManager game)
    {
        _game = game;
    }

    protected override void OnStartService()
    {
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

        OnGameStateChanged(new GameStateChangedEvent { NewState = _game.CurrentState });
    }

    protected override void OnStopService()
    {
        EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
    }

    public bool TryChangeState(InGameState newState)
    {
        if (!CanEnterState(newState))
        {
            return false;
        }

        ChangeState(newState);
        return true;
    }

    private void ChangeState(InGameState newState)
    {
        if (CurrentState == newState)
        {
            return;
        }

        InGameState previous = CurrentState;
        CurrentState = newState;
        EventBus.Instance.Publish(new InGameStateChangedEvent { PreviousState = previous, NewState = newState });
    }

    private bool CanEnterState(InGameState nextState)
    {
        if (_game.CurrentState != GameState.Playing)
        {
            return false;
        }
        switch (CurrentState)
        {
            case InGameState.Initializing:
                return nextState == InGameState.Ready;
            case InGameState.Ready:
                return nextState == InGameState.Running;
            case InGameState.Running:
                return nextState == InGameState.Suspended
                    || nextState == InGameState.Completed
                    || nextState == InGameState.Failed;
            case InGameState.Suspended:
                return nextState == InGameState.Running;
            default:
                return false;
        }
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.Playing)
        {
            if (CurrentState == InGameState.None)
            {
                ChangeState(InGameState.Initializing);
            }

            return;
        }

        if (evt.NewState == GameState.Paused)
        {
            return;
        }

        if (CurrentState != InGameState.None)
        {
            ChangeState(InGameState.None);
        }
    }
}
