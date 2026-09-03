using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Only used by CodexFrameworkScene. Framework services contain no demo UI.
public sealed class FrameworkDemo : MonoBehaviour
{
    [SerializeField] private GameFlowManager _flow;
    private TMP_Text _status;
    private TMP_Text _pauseLabel;
    private Button _pause;
    private RectTransform _marker;
    private float _elapsed;
    private GameManager _game;

    private void Start()
    {
        CreateCamera();
        Transform canvasTransform = CreateCanvas();
        CreateEventSystem();
        CreateControls(canvasTransform);
        ConnectGame();
    }

    private void CreateCamera()
    {
        GameObject cameraObject = new GameObject("DemoCamera", typeof(Camera));
        cameraObject.transform.SetParent(transform);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.05f, 0.085f);
        camera.cullingMask = 0;
    }

    private Transform CreateCanvas()
    {
        GameObject canvasObject = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasObject.transform;
    }

    private void CreateEventSystem()
    {
        GameObject events = new GameObject("DemoEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.transform.SetParent(transform);
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void CreateControls(Transform canvasTransform)
    {
        Label(canvasTransform, "FRAMEWORK DEMO", 170, 36);
        Label(canvasTransform, "Escape 또는 버튼으로 일시정지 / 재개", 115, 22);
        _status = Label(canvasTransform, "초기화 확인 중...", 30, 24);

        GameObject buttonObject = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvasTransform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240, 58);
        rect.anchoredPosition = new Vector2(0, -100);
        buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.6f);
        _pause = buttonObject.GetComponent<Button>();
        _pauseLabel = Label(buttonObject.transform, "일시정지", 0, 24);
        _pauseLabel.rectTransform.sizeDelta = rect.sizeDelta;
        _pause.onClick.AddListener(TogglePause);

        GameObject markerObject = new GameObject("TimeMarker", typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(canvasTransform, false);
        _marker = markerObject.GetComponent<RectTransform>();
        _marker.sizeDelta = new Vector2(18, 18);
        markerObject.GetComponent<Image>().color = new Color(0.3f, 0.85f, 0.7f);
        Label(canvasTransform, "정지하면 시간과 움직임이 멈추고, 재개하면 이어집니다.", -225, 19);
    }

    private void ConnectGame()
    {
        Bootstrapper app = Bootstrapper.Instance;
        if (app == null || !app.IsReady)
        {
            return;
        }

        _game = app.Game;
        if (_flow == null || _flow.CurrentState != InGameState.Initializing)
        {
            return;
        }

        _flow.TryChangeState(InGameState.Ready);
        _flow.TryChangeState(InGameState.Running);
    }

    private TMP_Text Label(Transform parent, string value, float y, float size)
    {
        GameObject item = new GameObject(value, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TextMeshProUGUI label = item.GetComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri9 SDF");
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = value;
        label.rectTransform.sizeDelta = new Vector2(900, 90);
        label.rectTransform.anchoredPosition = new Vector2(0, y);
        return label;
    }

    private void Update()
    {
        if (_status == null)
        {
            return;
        }
        bool ready = _game != null && _flow != null && _flow.IsInitialized;
        _pause.interactable = ready && (_game.CurrentState == GameState.Playing || _game.CurrentState == GameState.Paused);
        if (!ready)
        {
            _status.text = "초기화 실패: Console의 오류를 확인하세요.";
            return;
        }
        _elapsed += Time.deltaTime;
        _status.text = $"게임: {_game.CurrentState}  |  진행: {_flow.CurrentState}\n경과 시간: {_elapsed:0.0}초";
        _pauseLabel.text = _game.CurrentState == GameState.Paused ? "재개" : "일시정지";
        _marker.anchoredPosition = new Vector2(Mathf.Sin(_elapsed * 1.5f) * 240, -175);
    }

    private void TogglePause()
    {
        _game?.TogglePause();
    }
}
