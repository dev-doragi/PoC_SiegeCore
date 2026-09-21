using System;
using DG.Tweening;
using SiegeCore.Cannon;
using SiegeCore.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(CarryController))]
    public sealed class RatStacking : MonoBehaviour
    {
        [SerializeField] private RatFactory _factory;
        [SerializeField] private RatMergeResolver _mergeResolver;
        [SerializeField, Min(0.05f)]
        private float _stageDuration = 0.6f;

        private CarryController _carry;
        private InputAction _stackAction;

        private RatAgent _upper;
        private RatAgent _lower;

        private Tween _mergeTween;

        private float _elapsed;
        private bool _inputLocked;

        public bool IsStacking { get; private set; }

        public float Progress
        {
            get
            {
                if (!IsStacking)
                {
                    return 0f;
                }

                return Mathf.Clamp01(
                    _elapsed / _stageDuration);
            }
        }

        public event Action<float> ProgressChanged;
        public event Action<RatDefinition> StageCompleted;
        public event Action StageCancelled;

        private void Awake()
        {
            _carry = GetComponent<CarryController>();
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<BattlefieldViewChangedEvent>(HandleBattlefieldViewChanged);

            _stackAction = new InputAction(
                "Stack",
                InputActionType.Button,
                "<Keyboard>/e");

            _stackAction.Enable();
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<BattlefieldViewChangedEvent>(HandleBattlefieldViewChanged);
            Cancel();
            _inputLocked = false;

            if (_stackAction != null)
            {
                _stackAction.Dispose();
                _stackAction = null;
            }
        }

        private void Update()
        {
            if (_inputLocked || Time.timeScale <= 0f)
            {
                return;
            }

            Tick(
                _stackAction.IsPressed(),
                Time.deltaTime);
        }

        private void HandleBattlefieldViewChanged(BattlefieldViewChangedEvent eventMessage)
        {
            _inputLocked = eventMessage.IsActive;
            if (_inputLocked)
            {
                Cancel();
            }
        }

        public bool CanStack()
        {
            if (_factory == null
                || !_factory.IsReady
                || !_factory.Battlefield.IsSafe(transform.position))
            {
                return false;
            }

            if (_mergeResolver == null || _carry.HeldCount < 2)
            {
                return false;
            }

            RatDefinition accumulated = null;

            for (int i = _carry.HeldCount - 1; i >= 0; i--)
            {
                Component component =
                    _carry.GetHeld(i) as Component;

                if (component == null)
                {
                    return false;
                }

                RatAgent rat =
                    component.GetComponent<RatAgent>();

                if (rat == null
                    || rat.Faction != VehicleSide.Ally || rat.Definition == null)
                {
                    return false;
                }

                if (accumulated == null) accumulated = rat.Definition;
                else if (!_mergeResolver.TryResolve(accumulated, rat.Definition, out accumulated)) return false;
            }

            return true;
        }

        public void Tick(
            bool held,
            float deltaTime)
        {
            if (!held || !CanStack())
            {
                Cancel();
                return;
            }

            if (!IsStacking)
            {
                BeginStage();
            }

            _elapsed += deltaTime;

            ProgressChanged?.Invoke(Progress);

            if (_elapsed >= _stageDuration)
            {
                CompleteStage();
            }
        }

        private void BeginStage()
        {
            PlayerAttackController attack = GetComponent<PlayerAttackController>();
            if (attack != null)
            {
                attack.CancelCharge();
            }

            Component upperComponent =
                _carry.GetHeld(
                    _carry.HeldCount - 1) as Component;

            Component lowerComponent =
                _carry.GetHeld(
                    _carry.HeldCount - 2) as Component;

            if (upperComponent == null
                || lowerComponent == null)
            {
                return;
            }

            _upper =
                upperComponent.GetComponent<RatAgent>();

            _lower =
                lowerComponent.GetComponent<RatAgent>();

            if (_upper == null || _lower == null)
            {
                return;
            }

            IsStacking = true;
            _carry.InteractionLocked = true;
            _elapsed = 0f;

            Vector3 target =
                _upper.transform.parent
                    .InverseTransformPoint(
                        _lower.transform.position);

            _mergeTween = _upper.transform
                .DOLocalMove(
                    target,
                    _stageDuration)
                .SetEase(Ease.InQuad);
        }

        private void CompleteStage()
        {
            if (_upper == null || _lower == null || _mergeResolver == null
                || !_mergeResolver.TryResolve(_upper.Definition, _lower.Definition, out RatDefinition resultDefinition))
            {
                Cancel();
                return;
            }

            if (_mergeTween != null)
            {
                _mergeTween.Kill();
                _mergeTween = null;
            }

            RatAgent result = _factory.Spawn(
                resultDefinition,
                VehicleSide.Ally,
                transform.position);

            if (result == null
                || !_carry.ReplaceTopPair(result))
            {
                if (result != null)
                {
                    result.Release();
                }

                Cancel();
                return;
            }

            _upper = null;
            _lower = null;

            IsStacking = false;
            _elapsed = 0f;
            _carry.InteractionLocked = false;

            StageCompleted?.Invoke(resultDefinition);
            ProgressChanged?.Invoke(0f);
        }

        public void Cancel()
        {
            if (!IsStacking)
            {
                return;
            }

            if (_mergeTween != null)
            {
                _mergeTween.Kill();
                _mergeTween = null;
            }

            if (_upper != null
                && _upper.Carryable.IsCarried)
            {
                _upper.transform.localPosition =
                    Vector3.zero;
            }

            IsStacking = false;
            _elapsed = 0f;

            _carry.InteractionLocked = false;

            _upper = null;
            _lower = null;

            ProgressChanged?.Invoke(0f);
            StageCancelled?.Invoke();
        }
    }
}
