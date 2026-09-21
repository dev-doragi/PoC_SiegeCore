using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-820)]
public class InputReader : Singleton<InputReader>
{
    [Header("Action Maps")]
    [SerializeField] private string _playerActionMapName = "Player";
    [SerializeField] private string _uiActionMapName = "UI";

    [Header("Player Actions")]
    [SerializeField] private string _moveActionName = "Move";
    [SerializeField] private string _lookActionName = "Look";
    [SerializeField] private string _primaryActionName = "PrimaryAction";
    [SerializeField] private string _secondaryActionName = "SecondaryAction";

    [Header("UI/System Actions")]
    [SerializeField] private string _submitActionName = "Submit";
    [SerializeField] private string _cancelActionName = "Cancel";
    [SerializeField] private string _pauseActionName = "Pause";
    [SerializeField] private string _battlefieldViewActionName = "BattlefieldView";

    [SerializeField] private InputActionAsset _actions;
    private InputActionAsset _runtimeActions;
    private InputActionMap _playerMap;
    private InputActionMap _uiMap;

    private InputAction _move;
    private InputAction _look;
    private InputAction _primary;
    private InputAction _secondary;
    private InputAction _submit;
    private InputAction _cancel;
    private InputAction _pause;
    private InputAction _battlefieldView;

    private GameManager _game;

    public void Configure(GameManager game)
    {
        _game = game;
    }

    protected override void OnInitialize()
    {
        if (_actions == null)
        {
            Debug.LogError("[InputReader] PlayerInput or InputActionAsset is missing.", this);
            throw new System.InvalidOperationException("Input configuration is invalid.");
        }

        _runtimeActions = Instantiate(_actions);
        _playerMap = _runtimeActions.FindActionMap(_playerActionMapName, false);
        _uiMap = _runtimeActions.FindActionMap(_uiActionMapName, false);

        if (_playerMap == null || _uiMap == null)
        {
            Debug.LogError("[InputReader] Required action map is missing. Expected: Player/UI", this);
            throw new System.InvalidOperationException("Input configuration is invalid.");
        }

        _move = RequireAction(_playerMap, _moveActionName);
        _look = RequireAction(_playerMap, _lookActionName);
        _primary = RequireAction(_playerMap, _primaryActionName);
        _secondary = RequireAction(_playerMap, _secondaryActionName);
        _submit = RequireAction(_uiMap, _submitActionName);
        _cancel = RequireAction(_uiMap, _cancelActionName);
        _pause = RequireAction(_uiMap, _pauseActionName);
        _battlefieldView = RequireAction(_uiMap, _battlefieldViewActionName);

    }

    protected override void OnStartService()
    {
        Bind();
        _playerMap.Enable();
        _uiMap.Enable();

        InputSystem.onDeviceChange += OnDeviceChanged;
    }

    protected override void OnStopService()
    {
        Unbind();

        InputSystem.onDeviceChange -= OnDeviceChanged;

        _playerMap?.Disable();
        _uiMap?.Disable();
    }

    private InputAction RequireAction(InputActionMap map, string actionName)
    {
        InputAction action = map.FindAction(actionName, false);
        if (action == null)
        {
            Debug.LogError($"[InputReader] Missing required action '{actionName}' in map '{map.name}'.", this);
        }

        if (action == null)
        {
            throw new System.InvalidOperationException($"Missing input action: {actionName}");
        }
        return action;
    }

    private void Bind()
    {
        _move.performed += OnMovePerformed;
        _move.canceled += OnMoveCanceled;
        _look.performed += OnLookPerformed;
        _look.canceled += OnLookCanceled;

        _primary.started += OnPrimaryStarted;
        _primary.canceled += OnPrimaryCanceled;
        _secondary.started += OnSecondaryStarted;
        _secondary.canceled += OnSecondaryCanceled;

        _submit.performed += OnSubmit;
        _cancel.performed += OnCancel;
        _pause.performed += OnPause;
        _battlefieldView.performed += OnBattlefieldView;
    }

    private void Unbind()
    {
        if (_move != null)
        {
            _move.performed -= OnMovePerformed;
            _move.canceled -= OnMoveCanceled;
        }

        if (_look != null)
        {
            _look.performed -= OnLookPerformed;
            _look.canceled -= OnLookCanceled;
        }

        if (_primary != null)
        {
            _primary.started -= OnPrimaryStarted;
            _primary.canceled -= OnPrimaryCanceled;
        }

        if (_secondary != null)
        {
            _secondary.started -= OnSecondaryStarted;
            _secondary.canceled -= OnSecondaryCanceled;
        }

        if (_submit != null)
        {
            _submit.performed -= OnSubmit;
        }
        if (_cancel != null)
        {
            _cancel.performed -= OnCancel;
        }
        if (_pause != null)
        {
            _pause.performed -= OnPause;
        }
        if (_battlefieldView != null)
        {
            _battlefieldView.performed -= OnBattlefieldView;
        }
    }

    protected override void OnShutdown()
    {
        if (_runtimeActions != null)
        {
            Destroy(_runtimeActions);
        }
        _runtimeActions = null;
    }

    private static void OnDeviceChanged(InputDevice device, InputDeviceChange change)
    {
        EventBus.Instance.Publish(new InputDeviceChangedEvent { DeviceName = device.displayName });
    }

    private static void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        EventBus.Instance.Publish(new MoveInputEvent { Value = ctx.ReadValue<Vector2>() });
    }

    private static void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        EventBus.Instance.Publish(new MoveInputEvent { Value = Vector2.zero });
    }

    private static void OnLookPerformed(InputAction.CallbackContext ctx)
    {
        EventBus.Instance.Publish(new LookInputEvent { Value = ctx.ReadValue<Vector2>() });
    }

    private static void OnLookCanceled(InputAction.CallbackContext ctx)
    {
        EventBus.Instance.Publish(new LookInputEvent { Value = Vector2.zero });
    }

    private static void OnPrimaryStarted(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new PrimaryActionInputEvent { IsPressed = true });
    }

    private static void OnPrimaryCanceled(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new PrimaryActionInputEvent { IsPressed = false });
    }

    private static void OnSecondaryStarted(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new SecondaryActionInputEvent { IsPressed = true });
    }

    private static void OnSecondaryCanceled(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new SecondaryActionInputEvent { IsPressed = false });
    }

    private static void OnSubmit(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new SubmitInputEvent());
    }

    private static void OnCancel(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new CancelInputEvent());
    }

    private void OnPause(InputAction.CallbackContext _)
    {
        _game.TogglePause();
    }

    private static void OnBattlefieldView(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new BattlefieldViewInputEvent());
    }

    public void SetPlayerActionsEnabled(bool enabled)
    {
        if (_playerMap == null)
        {
            return;
        }

        if (enabled)
        {
            _playerMap.Enable();
            return;
        }

        _playerMap.Disable();
        EventBus.Instance.Publish(new MoveInputEvent { Value = Vector2.zero });
    }
}
