using Unity.Cinemachine;
using UnityEngine;

namespace SiegeCore.Cameras
{
    public sealed class BattlefieldViewController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _gameplayCamera;
        [SerializeField] private CinemachineCamera _battlefieldCamera;
        [SerializeField] private string _gameplayCameraName = "GameplayCamera";
        [SerializeField] private string _battlefieldCameraName = "BattlefieldCamera";

        public bool IsBattlefieldView { get; private set; }

        private void Awake()
        {
            ResolveCameras();
            ApplyCameraPriorities(false);
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<BattlefieldViewInputEvent>(HandleBattlefieldViewInput);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<BattlefieldViewInputEvent>(HandleBattlefieldViewInput);
            }

            if (IsBattlefieldView && InputReader.IsExisted)
            {
                InputReader.Instance.SetPlayerActionsEnabled(true);
            }

            IsBattlefieldView = false;
        }

        private void ResolveCameras()
        {
            if (_gameplayCamera == null)
            {
                GameObject gameplayObject = GameObject.Find(_gameplayCameraName);
                if (gameplayObject != null)
                {
                    _gameplayCamera = gameplayObject.GetComponent<CinemachineCamera>();
                }
            }

            if (_battlefieldCamera == null)
            {
                GameObject battlefieldObject = GameObject.Find(_battlefieldCameraName);
                if (battlefieldObject != null)
                {
                    _battlefieldCamera = battlefieldObject.GetComponent<CinemachineCamera>();
                }
            }

            if (_gameplayCamera == null || _battlefieldCamera == null)
            {
                Debug.LogError("[BattlefieldViewController] GameplayCamera and BattlefieldCamera are required.", this);
            }
        }

        private void HandleBattlefieldViewInput(BattlefieldViewInputEvent _)
        {
            if (_battlefieldCamera == null || _gameplayCamera == null)
            {
                return;
            }

            SetBattlefieldView(!IsBattlefieldView);
        }

        private void SetBattlefieldView(bool active)
        {
            IsBattlefieldView = active;
            ApplyCameraPriorities(active);

            EventBus.Instance.Publish(new BattlefieldViewChangedEvent { IsActive = active });

            if (InputReader.IsExisted)
            {
                InputReader.Instance.SetPlayerActionsEnabled(!active);
            }
        }

        private void ApplyCameraPriorities(bool battlefieldActive)
        {
            if (_gameplayCamera != null)
            {
                _gameplayCamera.Priority = battlefieldActive ? 10 : 20;
            }

            if (_battlefieldCamera != null)
            {
                _battlefieldCamera.Priority = battlefieldActive ? 20 : 0;
            }
        }
    }
}
