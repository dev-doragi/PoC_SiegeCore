using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace SiegeCore.Player
{
    [DefaultExecutionOrder(100)]
    public sealed class CarryStackAnimator : MonoBehaviour
    {
        [SerializeField] private CarryController _carryController;
        [Header("Horizontal")]
        [SerializeField, Min(0f)] private float _horizontalSwayPerLevel = 0.1f;
        [SerializeField, Min(0f)] private float _horizontalLag = 0.04f;
        [SerializeField, Min(0.01f)] private float _returnDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _overshootAmount = 0.08f;
        [Header("Vertical")]
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.1f;
        [SerializeField, Min(0f)] private float _bobFrequency = 1.2f;
        [SerializeField, Min(0f)] private float _bobPhaseDelay = 0.08f;
        [SerializeField, Min(0f)] private float _impulseStrength = 0f;
        [SerializeField, Min(0f)] private float _impulsePropagationDelay = 0.06f;
        [SerializeField, Min(0.01f)] private float _impulseDamping = 8f;

        [Header("Stack Landing")]
        [SerializeField, Min(0f)] private float _catchStackCompression = 0.08f;
        [SerializeField, Min(0.01f)] private float _catchStackRecoveryDuration = 0.12f;
        [SerializeField, Min(0f)] private float _catchPropagationDelay = 0.025f;
        [SerializeField, Range(0.5f, 1f)] private float _catchSquashRatio = 0.7f;
        [SerializeField, Min(1f)] private float _catchSquashWidth = 1.12f;

        private sealed class Landing
        {
            public CarryableObject Item;
            public Transform Visual;
            public Vector3 RestScale;
            public Tween Tween;
            public System.Action StateChanged;
        }
        private readonly List<Landing> _landings = new List<Landing>();

        private sealed class Slot
        {
            public Transform Point;
            public Vector3 Rest;
            public float X;
            public float Kick;
            public float Bob;
            public float Lift;
            public float Compression;
            public Tween CompressionTween;
            public float KickSourceVelocity;
            public Tween SwayTween;
            public Tween KickTween;
            public Tween BobTween;
            public Tween LiftTween;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private Vector2 _previousVelocity;
        private Vector2 _lastMoveDirection;
        private int _horizontalDirection;
        private bool _moving;
        private bool _decelerating;
        private volatile bool _refreshRequested;

        private void OnValidate()
        {
            // Validation may run outside the main thread; defer all tween operations.
            _refreshRequested = true;
        }

        private void Awake()
        {
            if (_carryController == null)
            {
                Debug.LogError("[CarryStackAnimator] Assign CarryController.", this);
                enabled = false;
                return;
            }
            if (_carryController.HoldPointCount == 0)
            {
                Debug.LogError("[CarryStackAnimator] Assign HoldPoints on CarryController.", this);
                enabled = false;
                return;
            }
            HashSet<Transform> unique = new HashSet<Transform>();
            for (int index = 0; index < _carryController.HoldPointCount; index++)
            {
                Transform point = _carryController.GetHoldPoint(index);
                if (point == null || point == transform || !point.IsChildOf(transform) || !unique.Add(point))
                {
                    Debug.LogError("[CarryStackAnimator] Assign distinct HoldPoint children in bottom-to-top order.", this);
                    enabled = false;
                    return;
                }
                _slots.Add(new Slot { Point = point, Rest = point.localPosition });
            }
        }

        private void OnEnable()
        {
            _refreshRequested = false;
            // Keep one continuous wave through movement, stops and direction changes.
            for (int index = 0; index < _slots.Count; index++)
                SetBobbing(_slots[index], index);
        }

        private void LateUpdate()
        {
            for (int index = _landings.Count - 1; index >= 0; index--)
            {
                Landing landing = _landings[index];
                if (landing.Item == null || !landing.Item.isActiveAndEnabled || !landing.Item.IsCarried)
                    StopLanding(landing);
            }
            if (Time.deltaTime <= 0f) return;
            bool refresh = _refreshRequested;
            _refreshRequested = false;
            PlayerController player = _carryController.Player;
            Vector2 velocity = player != null ? player.Velocity : Vector2.zero;
            bool moving = velocity.sqrMagnitude > 0.01f;
            bool started = moving && !_moving;
            bool turned = moving && _lastMoveDirection.sqrMagnitude > 0f
                && Vector2.Dot(velocity.normalized, _lastMoveDirection) < 0f;
            int direction = Mathf.Abs(velocity.x) > 0.1f ? (velocity.x > 0f ? 1 : -1) : 0;
            float deltaX = velocity.x - _previousVelocity.x;
            // Trigger once per deceleration episode, including a reversal through zero.
            // Unchanged render-frame samples must not reset the episode between physics ticks.
            bool changedSpeed = Mathf.Abs(deltaX) > 0.0001f;
            bool decelerating = changedSpeed && Mathf.Abs(_previousVelocity.x) > 0.1f
                && deltaX * _previousVelocity.x < 0f;
            bool kick = decelerating && !_decelerating;

            for (int index = 0; index < _slots.Count; index++)
            {
                Slot slot = _slots[index];
                float level = index + 1f;
                float duration = Mathf.Max(0.01f, _returnDuration);
                if (refresh) SetBobbing(slot, index);
                if (refresh || direction != _horizontalDirection)
                {
                    slot.SwayTween?.Kill();
                    slot.SwayTween = DOTween.To(() => slot.X, value => slot.X = value,
                        -direction * _horizontalSwayPerLevel * level, duration)
                        .SetDelay(_horizontalLag * level)
                        .SetEase(Ease.OutBack, _overshootAmount * 1.7f);
                }
                if (kick || (refresh && slot.KickTween != null && slot.KickTween.IsActive()
                    && !slot.KickTween.IsComplete()))
                {
                    if (kick) slot.KickSourceVelocity = _previousVelocity.x;
                    slot.KickTween?.Kill();
                    float amount = slot.KickSourceVelocity * _horizontalSwayPerLevel * level
                        * _overshootAmount * (1f + index * 0.1f);
                    Sequence recoil = DOTween.Sequence();
                    recoil.AppendInterval(_horizontalLag * level);
                    recoil.Append(DOTween.To(() => slot.Kick, value => slot.Kick = value,
                        amount, duration * 0.3f).SetEase(Ease.OutQuad));
                    recoil.Append(DOTween.To(() => slot.Kick, value => slot.Kick = value,
                        0f, duration).SetEase(Ease.OutBack, _overshootAmount));
                    slot.KickTween = recoil;
                }
                if (started || turned || (refresh && slot.LiftTween != null && slot.LiftTween.IsActive()
                    && !slot.LiftTween.IsComplete()))
                {
                    slot.LiftTween?.Kill();
                    //float amplitude = _impulseStrength * duration * 0.25f * (1f + index * 0.15f);
                    float settle = Mathf.Clamp(3f / Mathf.Max(0.01f, _impulseDamping), 0.05f, 3f);
                    Sequence impulse = DOTween.Sequence();
                    impulse.AppendInterval(_impulsePropagationDelay * index);
                    float amplitude =
                        _impulseStrength *
                        duration *
                        0.25f;
                    impulse.Append(DOTween.To(() => slot.Lift, value => slot.Lift = value,
                        0f, settle * 0.5f).SetEase(Ease.OutSine));
                    slot.LiftTween = impulse;
                }
                if (slot.Point == null) continue;
                Vector3 position = slot.Rest;
                position.x += slot.X + slot.Kick;
                position.y += slot.Bob + slot.Lift + slot.Compression;
                slot.Point.localPosition = position;
            }
            if (moving) _lastMoveDirection = velocity.normalized;
            if (changedSpeed) _decelerating = decelerating;
            if (direction == 0) _decelerating = false;
            _previousVelocity = velocity;
            _horizontalDirection = direction;
            _moving = moving;
        }

        public void PlayCatchLanding(CarryableObject item)
        {
            if (!isActiveAndEnabled || item == null || item.CarryVisual == null) return;
            Landing landing = new Landing
            {
                Item = item,
                Visual = item.CarryVisual,
                RestScale = item.CarryVisualRestScale
            };
            landing.StateChanged = () => StopLanding(landing);
            item.StateChanged += landing.StateChanged;
            _landings.Add(landing);
            Vector3 squash = landing.RestScale;
            squash.x *= _catchSquashWidth;
            squash.y *= _catchSquashRatio;
            Sequence sequence = DOTween.Sequence();
            sequence.Append(landing.Visual.DOScale(squash, 0.05f).SetEase(Ease.OutQuad));
            sequence.Append(landing.Visual.DOScale(landing.RestScale,
                _catchStackRecoveryDuration).SetEase(Ease.OutBack));
            sequence.OnComplete(() => StopLanding(landing));
            landing.Tween = sequence;

            int top = _carryController.HeldCount - 1;
            for (int index = top; index >= 0; index--)
            {
                if (index >= _slots.Count) continue;
                Slot slot = _slots[index];
                slot.CompressionTween?.Kill();
                int depth = top - index;
                float amount = _catchStackCompression / (depth + 1f);
                Sequence compression = DOTween.Sequence();
                compression.AppendInterval(depth * _catchPropagationDelay);
                compression.Append(DOTween.To(() => slot.Compression, value => slot.Compression = value,
                    -amount, 0.05f).SetEase(Ease.OutQuad));
                compression.Append(DOTween.To(() => slot.Compression, value => slot.Compression = value,
                    0f, _catchStackRecoveryDuration).SetEase(Ease.OutBack));
                slot.CompressionTween = compression;
            }
        }

        private void StopLanding(Landing landing)
        {
            landing.Tween?.Kill();
            if (landing.Item != null) landing.Item.StateChanged -= landing.StateChanged;
            if (landing.Visual != null) landing.Visual.localScale = landing.RestScale;
            _landings.Remove(landing);
        }
        private void SetBobbing(Slot slot, int index)
        {
            slot.BobTween?.Kill();
            if (_bobFrequency <= 0f || _bobAmplitude <= 0f)
            {
                slot.BobTween = DOTween.To(() => slot.Bob, value => slot.Bob = value,
                    0f, Mathf.Max(0.01f, _returnDuration)).SetEase(Ease.OutSine);
                return;
            }
            float quarter = 0.25f / _bobFrequency;
            Sequence bob = DOTween.Sequence();
            bob.Append(DOTween.To(() => slot.Bob, value => slot.Bob = value,
                _bobAmplitude, quarter).SetEase(Ease.OutSine));
            bob.Append(DOTween.To(() => slot.Bob, value => slot.Bob = value,
                -_bobAmplitude, quarter * 2f).SetEase(Ease.InOutSine));
            bob.Append(DOTween.To(() => slot.Bob, value => slot.Bob = value,
                0f, quarter).SetEase(Ease.InSine));
            // Delay only the initial start, not every cycle.
            bob.SetDelay(index * _bobPhaseDelay, false).SetLoops(-1);
            slot.BobTween = bob;
        }

        private void OnDisable()
        {
            while (_landings.Count > 0) StopLanding(_landings[_landings.Count - 1]);
            foreach (Slot slot in _slots)
            {
                slot.CompressionTween?.Kill();
                slot.CompressionTween = null;
                slot.Compression = 0f;
                slot.SwayTween?.Kill();
                slot.KickTween?.Kill();
                slot.BobTween?.Kill();
                slot.LiftTween?.Kill();
                slot.SwayTween = slot.KickTween = slot.BobTween = slot.LiftTween = null;
                slot.X = slot.Kick = slot.Bob = slot.Lift = 0f;
                if (slot.Point != null) slot.Point.localPosition = slot.Rest;
            }
            _previousVelocity = _lastMoveDirection = Vector2.zero;
            _horizontalDirection = 0;
            _moving = _decelerating = false;
        }
    }
}
