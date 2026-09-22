using System;
using DG.Tweening;
using SiegeCore.Rat;
using UnityEngine;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(RatFlightMotion))]
    public sealed partial class CarryableObject : MonoBehaviour, IThrowable
    {
        [SerializeField, HideInInspector] private int _compositionVersion;
        private Transform _worldParent;
        private Tween _catchTween;
        private RatAgent _agent;
        private RatFlightMotion _motion;
        private RatPresenter _presentation;

        public event Action<IThrowable> GroundSortingRequested;
        public RatAgent Agent { get { return _agent; } }
        public Transform CarryTransform { get { return transform; } }
        public bool IsCarried { get { return _motion.IsCarried; } }
        public bool IsAirborne { get { return _motion.IsAirborne; } }
        public bool IsLoaded { get { return _motion.IsLoaded; } }
        public bool IsCannonLoading { get { return _motion.IsCannonLoading; } }
        public bool IsCannonFlight { get { return _motion.IsCannonFlight; } }
        public bool IsFusionLocked { get { return _motion.IsFusionLocked; } }
        public float Height { get { return _motion.Height; } }
        public float HeightGravity { get { return _motion.HeightGravity; } }
        public float FallDistanceFromPeak { get { return _motion.FallDistanceFromPeak; } }
        public Vector2 CatchGroundPosition { get { return _motion.CatchGroundPosition; } }
        public Vector2 CatchVisualPosition { get { return _motion.CatchVisualPosition; } }
        public Transform CarryVisual { get { return _presentation.Visual; } }
        public Vector3 CarryVisualRestScale { get { return _presentation.RestScale; } }
        public bool CanEnterCannon { get { return _agent != null && _agent.CanLoadIntoCannon; } }

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
            _motion = GetComponent<RatFlightMotion>();
            _presentation = GetComponent<RatPresenter>();
        }

        public bool TryThrow(Vector2 direction, Vector3 groundPosition, Collider2D[] throwerColliders)
        {
            return _agent.TryThrow(direction, groundPosition, throwerColliders);
        }

        public void Drop(Vector3 worldPosition)
        {
            _agent.DropFromCarry(worldPosition);
        }

        public void RememberWorldParent()
        {
            _worldParent = transform.parent;
        }

        public bool Attach(Transform holdPoint, bool catchInFlight, float duration = 0f)
        {
            if (!isActiveAndEnabled || holdPoint == null || holdPoint.IsChildOf(transform)) return false;
            if (catchInFlight
                ? !_motion.IsAirborne
                : (_motion.IsCarried
                    || _motion.IsAirborne
                    || _motion.IsCannonLoading
                    || _motion.IsLoaded)) return false;
            Vector3 displayedPosition = _presentation.Visual.position;
            RememberWorldParent();
            _motion.PrepareCarry();
            transform.SetParent(holdPoint, true);
            if (!catchInFlight)
            {
                transform.localPosition = Vector3.zero;
                return true;
            }
            transform.position += displayedPosition - _presentation.Visual.position;
            _catchTween = transform.DOLocalMove(Vector3.zero, Mathf.Max(0.01f, duration))
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    _catchTween = null;
                    transform.localPosition = Vector3.zero;
                });
            return true;
        }

        public void Detach()
        {
            CompleteCatchTween();
            transform.SetParent(_worldParent, true);
        }

        public void DropToGround(Vector3 worldPosition)
        {
            if (!IsCarried) return;
            Detach();
            transform.position = worldPosition;
            _motion.Body.position = worldPosition;
            _motion.CompleteSettle(false);
        }

        public void CompleteCatchTween()
        {
            if (_catchTween == null) return;
            _catchTween.Kill();
            _catchTween = null;
            if (IsCarried) transform.localPosition = Vector3.zero;
        }

        public void ResetCarry()
        {
            CompleteCatchTween();
            _worldParent = null;
        }

        public void NotifyGroundSorting()
        {
            GroundSortingRequested?.Invoke(this);
        }
    }
}
