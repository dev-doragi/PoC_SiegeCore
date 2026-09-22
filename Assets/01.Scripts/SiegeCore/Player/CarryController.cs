using System.Collections.Generic;
using SiegeCore.Rat;
using UnityEngine;

namespace SiegeCore.Player
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(PlayerAimController))]
    public sealed class CarryController : MonoBehaviour
    {
        [Header("Carry Slots")]
        [SerializeField, Tooltip("단일 슬롯 구성을 위한 이전 HoldPoint입니다. Hold Points가 비어 있을 때만 사용합니다.")]
        private Transform _holdPoint;

        [SerializeField, Min(1), Tooltip("최대 운반 수입니다. 실제 용량은 HoldPoint 개수를 넘지 않습니다.")]
        private int _maxCarryCount = 3;

        [SerializeField, Tooltip("아래에서 위 순서로 사용할 머리 위 운반 위치입니다.")]
        private Transform[] _holdPoints = new Transform[0];

        [SerializeField] private CarryStackAnimator _stackAnimator;

        [SerializeField, Tooltip("공중 Catch 범위의 중심입니다. 플레이어 발 위치에 둡니다.")]
        private Transform _catchOrigin;

        [Header("Airborne Catch - Eligibility")]
        [SerializeField, Min(0f), Tooltip("Peak 이후 Catch 판정을 시작하기 전에 Rat이 실제로 낙하해야 하는 높이입니다.")]
        private float _minimumCatchFallDistance = 0.4f;

        [Header("Airborne Catch - Area")]
        [SerializeField, Min(0.1f), Tooltip("Catch Area의 좌우 반경입니다.")]
        private float _catchHorizontalRadius = 0.85f;

        [SerializeField, Min(0.1f), Tooltip("Catch Area의 위아래 반경입니다.")]
        private float _catchVerticalRadius = 0.55f;

        [SerializeField, Tooltip("CatchOrigin을 기준으로 Catch Area 중심을 이동합니다.")]
        private Vector2 _catchAreaOffset = Vector2.zero;

        [Header("Airborne Catch - Response")]
        [SerializeField, Min(0.01f), Tooltip("Catch된 Rat이 현재 공중 위치에서 HoldPoint까지 이동하는 시간입니다.")]
        private float _catchTweenDuration = 0.12f;

        [SerializeField, Range(0f, 0.3f), Tooltip("Catch 순간 플레이어와 운반 스택이 눌리는 강도입니다.")]
        private float _catchSquashStrength = 0.08f;

        private const float CatchMovementLockDuration = 0.1f;

        [Header("Interaction")]
        [SerializeField, Min(0.01f)] private float _throwMovementLockDuration = 0.2f;

        [Header("Enemy Recovery")]
        [SerializeField, Min(0f), Tooltip("운반 중 적이 깨어났을 때 플레이어가 밀려나는 속도입니다.")]
        private float _recoveryKnockbackSpeed = 6f;

        [SerializeField, Min(0f), Tooltip("적이 깨어난 뒤 이동 입력을 막는 시간입니다.")]
        private float _recoveryKnockbackDuration = 0.25f;

        private PlayerAimController _aimController;
        private PlayerController _player;
        private PlayerAttackController _attackController;
        private RatStacking _ratStacking;
        private Collider2D[] _playerColliders;
        private Vector3 _holdOffset;
        private readonly List<ICarryable> _heldObjects = new List<ICarryable>();
        private readonly Dictionary<ICarryable, CarrySorting> _carrySorting = new Dictionary<ICarryable, CarrySorting>();
        private int _carriedLayerId;
        private bool _handlingEnemyRecovery;

        private sealed class CarrySorting
        {
            public Renderer[] Renderers;
            public int[] Layers;
            public int[] Orders;
            public int MinimumOrder;
            public int OrderSpan;
        }

        public int HeldCount { get { PruneHeldObjects(); return _heldObjects.Count; } }
        public ICarryable HeldObject => HeldCount > 0 ? _heldObjects[_heldObjects.Count - 1] : null;
        public bool HasHeldObject => HeldCount > 0;
        public int HoldPointCount
        {
            get
            {
                if (_holdPoints != null && _holdPoints.Length > 0) return _holdPoints.Length;
                return _holdPoint != null ? 1 : 0;
            }
        }
        public PlayerController Player => _player;
        public bool InteractionLocked { get; set; }
        public ICarryable GetHeld(int index) => index >= 0 && index < HeldCount ? _heldObjects[index] : null;

        public bool ReplaceTopPair(SiegeCore.Rat.RatAgent result)
        {
            int count = HeldCount;
            if (count < 2 || result == null
                || !result.TryAttachToCarrySlot(GetHoldPoint(count - 2))) return false;
            for (int index = count - 1; index >= count - 2; index--)
            {
                ICarryable item = _heldObjects[index];
                RestoreCarrySorting(item);
                _heldObjects.RemoveAt(index);
                SiegeCore.Rat.RatAgent rat = ((Component)item).GetComponent<SiegeCore.Rat.RatAgent>();
                rat.Release();
            }
            _heldObjects.Add(result.Carryable);
            CaptureCarrySorting(result.Carryable);
            RefreshCarrySorting();
            return true;
        }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _attackController = GetComponent<PlayerAttackController>();
            _ratStacking = GetComponent<RatStacking>();
            if (_stackAnimator == null) _stackAnimator = GetComponentInChildren<CarryStackAnimator>();
            _carriedLayerId = SortingLayer.NameToID("Carried");
            _aimController = GetComponent<PlayerAimController>();
            _playerColliders = GetComponentsInChildren<Collider2D>();
            if (GetHoldPoint(0) == null)
            {
                Debug.LogError("[CarryController] Assign a child HoldPoint.", this);
                enabled = false;
                return;
            }
            if (_holdPoint != null) _holdOffset = _holdPoint.localPosition;
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<SecondaryActionInputEvent>(HandleSecondaryAction);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<SecondaryActionInputEvent>(HandleSecondaryAction);
            }
            DropAll();
            // Also restore objects still in their initial airborne arc.
            List<ICarryable> pending = new List<ICarryable>(_carrySorting.Keys);
            foreach (ICarryable item in pending) RestoreCarrySorting(item);
        }

        private void LateUpdate()
        {
            PruneHeldObjects();

            Vector3 offset = _holdOffset;
            if (_aimController == null || _holdPoint == null || (_holdPoints != null && _holdPoints.Length > 0)) return;
            offset.x = Mathf.Abs(offset.x) * _aimController.FacingSign;
            _holdPoint.localPosition = offset;
        }

        private void HandleSecondaryAction(SecondaryActionInputEvent inputEvent)
        {
            if (!inputEvent.IsPressed || Time.timeScale <= 0f)
            {
                return;
            }

            if (_attackController != null)
            {
                _attackController.CancelCharge();
            }

            TryThrow();
        }

        private void FixedUpdate()
        {
            if (Time.timeScale <= 0f) return;
            int count = HeldCount;
            Transform point = GetHoldPoint(count);
            if (InteractionLocked || count >= GetCarryCapacity() || point == null
                || (count > 0 && _heldObjects[0] is SiegeCore.Cannon.Cannon))
            {
                return;
            }

            CarryableObject best = null;
            float bestDistance = float.PositiveInfinity;
            Vector2 catchAreaCenter = GetCatchAreaCenter();
            foreach (RatAgent rat in RatAgent.Active)
            {
                if (rat == null) continue;
                CarryableObject item = rat.Carryable;
                Vector2 visualPosition = item.CatchVisualPosition;
                float distance = Vector2.Distance(visualPosition, catchAreaCenter);
                if (!rat.CanBeCaught
                    || item.FallDistanceFromPeak < _minimumCatchFallDistance) continue;
                if (!IsInsideCatchArea(visualPosition)
                    || distance >= bestDistance) continue;
                best = item;
                bestDistance = distance;
            }
            if (best == null) return;
            TryCompleteCatch(best, point);
        }

        private Vector2 GetCatchAreaCenter()
        {
            Vector2 origin = _catchOrigin != null ? _catchOrigin.position : transform.position;
            return origin + _catchAreaOffset;
        }

        private bool IsInsideCatchArea(Vector2 visualPosition)
        {
            Vector2 offset = visualPosition - GetCatchAreaCenter();
            float horizontalRadius = Mathf.Max(0.1f, _catchHorizontalRadius);
            float verticalRadius = Mathf.Max(0.1f, _catchVerticalRadius);
            float normalizedX = offset.x / horizontalRadius;
            float normalizedY = offset.y / verticalRadius;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
        }

        private bool TryCompleteCatch(CarryableObject item, Transform point)
        {
            if (item == null || point == null || !IsInsideCatchArea(item.CatchVisualPosition)
                || !item.Agent.TryCatch(point, _catchTweenDuration)) return false;
            _heldObjects.Add(item);
            CaptureCarrySorting(item);
            RefreshCarrySorting();
            if (_stackAnimator != null) _stackAnimator.PlayCatchLanding(item, _catchSquashStrength);
            if (_player != null) _player.StopMovementFor(CatchMovementLockDuration);
            return true;
        }

        private int GetCarryCapacity()
        {
            return Mathf.Min(Mathf.Max(1, _maxCarryCount), HoldPointCount);
        }

        /// <summary>
        /// 운반 중인 적이 회복했을 때 한 번만 전체 Drop과 플레이어 반동을 발생시킨다.
        /// </summary>
        public void HandleCarriedEnemyRecovered(RatAgent recoveredEnemy)
        {
            if (_handlingEnemyRecovery || recoveredEnemy == null)
            {
                return;
            }

            _handlingEnemyRecovery = true;

            if (_ratStacking != null)
            {
                _ratStacking.Cancel();
            }

            if (_attackController != null)
            {
                _attackController.CancelCharge();
            }

            DropAll();

            if (_player != null)
            {
                int facing = 1;
                if (_aimController != null)
                {
                    facing = _aimController.FacingSign;
                }

                Vector2 knockbackDirection = Vector2.left * facing;
                _player.ApplyKnockback(
                    knockbackDirection,
                    _recoveryKnockbackSpeed,
                    _recoveryKnockbackDuration);
            }

            _handlingEnemyRecovery = false;
        }

        public void DropAll()
        {
            for (int index = _heldObjects.Count - 1; index >= 0; index--)
            {
                ICarryable item = _heldObjects[index];
                RestoreCarrySorting(item);

                Component component = item as Component;
                RatAgent rat = null;
                if (component != null)
                {
                    rat = component.GetComponent<RatAgent>();
                }
                if (rat != null)
                {
                    rat.DropFromCarry(transform.position);
                }
                else if (item.IsCarried)
                {
                    item.Drop(transform.position);
                }
            }

            _heldObjects.Clear();
        }

        public bool TryThrow()
        {
            if (InteractionLocked) { return false; }
            if (!HasHeldObject) return false;
            ICarryable heldObject = HeldObject;

            Vector3 groundPosition = heldObject.CarryTransform.position;
            groundPosition.y = transform.position.y;
            groundPosition.z = transform.position.z;
            IThrowable throwable = heldObject as IThrowable;
            if (throwable != null)
            {
                if (_aimController == null
                    || !throwable.TryThrow(
                        _aimController.EightWayAimDirection,
                        groundPosition,
                        _playerColliders)) return false;
            }
            else heldObject.Drop(groundPosition);
            if (_player != null) _player.StopMovementFor(Mathf.Max(0.01f, _throwMovementLockDuration));
            if (throwable == null) RestoreCarrySorting(heldObject);
            _heldObjects.RemoveAt(_heldObjects.Count - 1);
            return true;
        }

        public Transform GetHoldPoint(int index)
        {
            if (index < 0) return null;
            if (_holdPoints != null && _holdPoints.Length > 0)
                return index < _holdPoints.Length ? _holdPoints[index] : null;
            return index == 0 ? _holdPoint : null;
        }

        private void PruneHeldObjects()
        {
            bool changed = false;
            for (int index = _heldObjects.Count - 1; index >= 0; index--)
            {
                ICarryable item = _heldObjects[index];
                if (item is Component component && component != null && component.gameObject.activeInHierarchy && item.IsCarried) continue;
                RestoreCarrySorting(item);
                _heldObjects.RemoveAt(index);
                changed = true;
            }
            if (!changed) return;
            ReflowHeldObjects();
        }

        private void ReflowHeldObjects()
        {
            for (int index = 0; index < _heldObjects.Count; index++)
            {
                Transform point = GetHoldPoint(index);
                if (point == null) continue;
                Transform carried = _heldObjects[index].CarryTransform;
                carried.SetParent(point, false);
                carried.localPosition = Vector3.zero;
            }
            RefreshCarrySorting();
        }

        private void CaptureCarrySorting(ICarryable item)
        {
            // Throw 직후 다시 받은 Rat은 기존 정렬 캐시와 이벤트 구독을 그대로 사용한다.
            if (_carrySorting.ContainsKey(item))
            {
                return;
            }

            // Cache once on pickup, including temporarily hidden visuals and shadows.
            Renderer[] renderers = item.CarryTransform.GetComponentsInChildren<Renderer>(true);
            CarrySorting sorting = new CarrySorting
            {
                Renderers = renderers,
                Layers = new int[renderers.Length],
                Orders = new int[renderers.Length],
                MinimumOrder = int.MaxValue
            };
            int maximumOrder = int.MinValue;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                sorting.Layers[index] = renderer.sortingLayerID;
                sorting.Orders[index] = renderer.sortingOrder;
                sorting.MinimumOrder = Mathf.Min(sorting.MinimumOrder, renderer.sortingOrder);
                maximumOrder = Mathf.Max(maximumOrder, renderer.sortingOrder);
            }
            sorting.OrderSpan = renderers.Length > 0 ? maximumOrder - sorting.MinimumOrder + 1 : 1;
            _carrySorting.Add(item, sorting);
            if (item is IThrowable throwable) throwable.GroundSortingRequested += HandleGroundSortingRequested;
            if (item is CarryableObject ratItem) ratItem.Agent.Released += HandleRatReleased;
        }

        private void HandleRatReleased(RatAgent rat)
        {
            RestoreCarrySorting(rat.Carryable);
            if (_heldObjects.Remove(rat.Carryable)) ReflowHeldObjects();
        }

        private void HandleGroundSortingRequested(IThrowable item)
        {
            RestoreCarrySorting(item);
            Component component = item as Component;
            if (component == null || !component.gameObject.activeInHierarchy || !item.IsCarried)
            {
                if (_heldObjects.Remove(item)) ReflowHeldObjects();
            }
        }

        private void RefreshCarrySorting()
        {
            int baseOrder = 0;
            foreach (ICarryable item in _heldObjects)
            {
                if (!_carrySorting.TryGetValue(item, out CarrySorting sorting)) continue;
                for (int index = 0; index < sorting.Renderers.Length; index++)
                {
                    Renderer renderer = sorting.Renderers[index];
                    if (renderer == null) continue;
                    renderer.sortingLayerID = _carriedLayerId;
                    renderer.sortingOrder = baseOrder + sorting.Orders[index] - sorting.MinimumOrder;
                }
                // Preserve each object's internal ordering while keeping the next slot above it.
                baseOrder += sorting.OrderSpan;
            }
        }

        private void RestoreCarrySorting(ICarryable item)
        {
            if (!_carrySorting.TryGetValue(item, out CarrySorting sorting)) return;
            if (item is IThrowable throwable) throwable.GroundSortingRequested -= HandleGroundSortingRequested;
            if (item is CarryableObject ratItem) ratItem.Agent.Released -= HandleRatReleased;
            for (int index = 0; index < sorting.Renderers.Length; index++)
            {
                Renderer renderer = sorting.Renderers[index];
                if (renderer == null) continue;
                renderer.sortingLayerID = sorting.Layers[index];
                renderer.sortingOrder = sorting.Orders[index];
            }
            _carrySorting.Remove(item);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = GetCatchAreaCenter();
            Gizmos.color = Color.cyan;
            DrawCatchEllipse(center);

        }

        private void DrawCatchEllipse(Vector3 center)
        {
            const int segmentCount = 32;
            float horizontalRadius = Mathf.Max(0.1f, _catchHorizontalRadius);
            float verticalRadius = Mathf.Max(0.1f, _catchVerticalRadius);
            Vector3 previous = center + Vector3.right * horizontalRadius;
            for (int index = 1; index <= segmentCount; index++)
            {
                float angle = index * Mathf.PI * 2f / segmentCount;
                Vector3 current = center + new Vector3(
                    Mathf.Cos(angle) * horizontalRadius,
                    Mathf.Sin(angle) * verticalRadius,
                    0f);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }
}
