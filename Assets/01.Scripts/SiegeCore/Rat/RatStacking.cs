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
        [SerializeField, Min(0.05f)]
        private float _stageDuration = 0.6f;

        private CarryController _carry;
        private InputAction _stackAction;

        private RatAgent _upper;
        private RatAgent _lower;

        private Tween _mergeTween;

        private float _elapsed;

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
        public event Action<RatForm> StageCompleted;
        public event Action StageCancelled;

        private void Awake()
        {
            _carry = GetComponent<CarryController>();
        }

        private void OnEnable()
        {
            _stackAction = new InputAction(
                "Stack",
                InputActionType.Button,
                "<Keyboard>/e");

            _stackAction.Enable();
        }

        private void OnDisable()
        {
            Cancel();

            if (_stackAction != null)
            {
                _stackAction.Dispose();
                _stackAction = null;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            Tick(
                _stackAction.IsPressed(),
                Time.deltaTime);
        }

        public bool CanStack()
        {
            if (_factory == null
                || !_factory.IsReady
                || !_factory.Battlefield.IsSafe(transform.position))
            {
                return false;
            }

            if (_carry.HeldCount < 2)
            {
                return false;
            }

            int totalBasicCount = 0;

            for (int i = 0; i < _carry.HeldCount; i++)
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
                    || rat.Faction != VehicleSide.Ally)
                {
                    return false;
                }

                totalBasicCount +=
                    rat.Definition.BasicCount;
            }

            return totalBasicCount <= 3;
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
            int count =
                _upper.Definition.BasicCount
                + _lower.Definition.BasicCount;

            if (count < 2 || count > 3)
            {
                Cancel();
                return;
            }

            RatForm resultForm =
                (RatForm)count;

            if (_mergeTween != null)
            {
                _mergeTween.Kill();
                _mergeTween = null;
            }

            RatAgent result = _factory.Spawn(
                resultForm,
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

            StageCompleted?.Invoke(resultForm);
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