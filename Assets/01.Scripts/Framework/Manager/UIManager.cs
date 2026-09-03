using UnityEngine;

[DefaultExecutionOrder(-760)]
public class UIManager : ManagedBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private GameObject _loadingPanel;
    [SerializeField] private GameObject _gameOverPanel;

    private GameManager _game;

    public void Configure(GameManager game)
    {
        _game = game;
    }

    protected override void OnStartService()
    {
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        if (_game != null)
        {
            OnGameStateChanged(new GameStateChangedEvent { NewState = _game.CurrentState });
        }
    }

    protected override void OnStopService()
    {
        EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        HideAllPanels();

        switch (evt.NewState)
        {
            case GameState.MainMenu:
                if (_mainMenuPanel != null)
                {
                    _mainMenuPanel.SetActive(true);
                }
                break;
            case GameState.Paused:
                if (_pausePanel != null)
                {
                    _pausePanel.SetActive(true);
                }
                break;
            case GameState.Loading:
                if (_loadingPanel != null)
                {
                    _loadingPanel.SetActive(true);
                }
                break;
            case GameState.GameOver:
                if (_gameOverPanel != null)
                {
                    _gameOverPanel.SetActive(true);
                }
                break;
        }
    }

    public void HideAllPanels()
    {
        if (_mainMenuPanel != null)
        {
            _mainMenuPanel.SetActive(false);
        }
        if (_pausePanel != null)
        {
            _pausePanel.SetActive(false);
        }
        if (_loadingPanel != null)
        {
            _loadingPanel.SetActive(false);
        }
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(false);
        }
    }
}
