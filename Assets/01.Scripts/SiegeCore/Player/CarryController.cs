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

        [Header("Airborne Catch")]
        [SerializeField, Min(0.1f), Tooltip("지상 Rat을 직접 줍는 반경입니다.")]
        private float _pickupRadius = 0.55f;

        [SerializeField, Min(0.1f), Tooltip("다음 슬롯 높이를 통과할 때 허용하는 지상 평면 오차입니다.")]
        private float _airborneCatchRadius = 0.5f;

        [SerializeField, Min(0f), Tooltip("공중 Rat을 머리에 받는 순간 플레이어 이동을 잠그는 시간입니다.")]
        private float _catchMovementLockDuration = 0.1f;

        [SerializeField, Min(0.1f)] private float _catchAssistRadius = 0.85f;
        [SerializeField, Min(0.1f)] private float _catchAssistHeight = 1.2f;
        [SerializeField, Min(0.01f)] private float _catchAssistDuration = 0.2f;
        [SerializeField, Min(0f)] private float _catchAssistSpeed = 4f;
        [SerializeField, Min(0f)] private float _catchAssistAcceleration = 40f;
        [SerializeField] private LayerMask _pickupLayers = ~0;
        [SerializeField] private LayerMask _obstacleLayers = ~0;
        [SerializeField, Min(0.01f)] private float _throwMovementLockDuration = 0.2f;

        [Header("Enemy Recovery")]
        [SerializeField, Min(0f), Tooltip("운반 중 적이 깨어났을 때 플레이어가 밀려나는 속도입니다.")]
        private float _recoveryKnockbackSpeed = 6f;

        [SerializeField, Min(0f), Tooltip("적이 깨어난 뒤 이동 입력을 막는 시간입니다.")]
        private float _recoveryKnockbackDuration = 0.25f;

        private PlayerAimController _aimController;
        private PlayerController _player;
        private Collider2D[] _playerColliders;
        private Vector3 _holdOffset;
        private readonly List<ICarryable> _heldObjects = new List<ICarryable>();
        private readonly Dictionary<ICarryable, CarrySorting> _carrySorting = new Dictionary<ICarryable, CarrySorting>();
        private CarryableObject _catchCandidate;
        private Transform _reservedPoint;
        private int _reservedCount;
        private readonly HashSet<CarryableObject> _catchBlockedUntilExit = new HashSet<CarryableObject>();
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
            if (count < 2 || result == null || !result.Carryable.TryPickUp(GetHoldPoint(count - 2))) { return false; }
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
            _catchBlockedUntilExit.Clear();
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

            PlayerAttackController attack = GetComponent<PlayerAttackController>();
            if (attack != null)
            {
                attack.CancelCharge();
            }

            TryThrow();
        }

        private void FixedUpdate()
        {
            _catchBlockedUntilExit.RemoveWhere(item => item == null || !item.IsAirborne);
            if (Time.timeScale <= 0f) return;
            int count = HeldCount;
            Transform point = GetHoldPoint(count);
            if (InteractionLocked || count >= GetCarryCapacity() || point == null
                || (count > 0 && _heldObjects[0] is SiegeCore.Cannon.Cannon))
            {
                CancelCatchReservation();
                return;
            }

            if (_catchCandidate != null)
            {
                if (IsCatchReservationValid(_catchCandidate, _reservedPoint)
                    && _catchCandidate.HasCatchAssist(this)) return;
                CancelCatchReservation();
            }

            CarryableObject best = null;
            float bestDistance = float.PositiveInfinity;
            Vector2 groundTarget = GetCatchGroundPosition(point);
            foreach (RatAgent rat in RatAgent.Active)
            {
                if (rat == null) continue;
                CarryableObject item = rat.Carryable;
                float distance = Vector2.Distance(item.PhysicsPosition, groundTarget);
                if (_catchBlockedUntilExit.Contains(item))
                {
                    if (distance > Mathf.Max(_catchAssistRadius, _airborneCatchRadius)
                        || item.PhysicsPosition.y + item.Height > point.position.y + _catchAssistHeight)
                        _catchBlockedUntilExit.Remove(item);
                    continue;
                }
                if (!rat.CanBeCaught || item.IsCatchAssisted) continue;
                float gap = item.PhysicsPosition.y + item.Height - point.position.y;
                if (gap < 0f || gap > _catchAssistHeight
                    || item.GetTimeToCatchPoint(point) > _catchAssistDuration) continue;
                float radius = Mathf.Lerp(_airborneCatchRadius,
                    Mathf.Max(_airborneCatchRadius, _catchAssistRadius), gap / _catchAssistHeight);
                if (distance > radius || distance >= bestDistance) continue;
                best = item;
                bestDistance = distance;
            }
            if (best == null) return;
            _catchCandidate = best;
            _reservedPoint = point;
            _reservedCount = count;
            best.BeginCatchAssist(this, point, _catchAssistSpeed, _catchAssistAcceleration);
        }

        internal Vector2 GetCatchGroundPosition(Transform point)
        {
            return new Vector2(point.position.x, transform.position.y);
        }

        internal bool IsCatchReservationValid(CarryableObject item, Transform point)
        {
            return isActiveAndEnabled && !InteractionLocked && item != null && item.isActiveAndEnabled
                && item == _catchCandidate && point != null && point == _reservedPoint
                && HeldCount == _reservedCount && HeldCount < GetCarryCapacity()
                && GetHoldPoint(HeldCount) == point
                && Vector2.Distance(item.PhysicsPosition, GetCatchGroundPosition(point))
                    <= Mathf.Max(_airborneCatchRadius, _catchAssistRadius);
        }

        internal bool TryCompleteCatch(CarryableObject item, Transform point, Vector2 contactGroundPosition)
        {
            if (!IsCatchReservationValid(item, point)
                || Vector2.Distance(contactGroundPosition, GetCatchGroundPosition(point)) > _airborneCatchRadius
                || !item.TryCatch(point)) return false;
            _catchCandidate = null;
            _reservedPoint = null;
            _heldObjects.Add(item);
            CaptureCarrySorting(item);
            RefreshCarrySorting();
            if (_stackAnimator != null) _stackAnimator.PlayCatchLanding(item);
            if (_player != null && _catchMovementLockDuration > 0f)
                _player.StopMovementFor(_catchMovementLockDuration);
            return true;
        }

        private void CancelCatchReservation()
        {
            if (_catchCandidate != null) _catchCandidate.CancelCatchAssist(this);
            _catchCandidate = null;
            _reservedPoint = null;
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

            RatStacking stacking = GetComponent<RatStacking>();
            if (stacking != null)
            {
                stacking.Cancel();
            }

            PlayerAttackController attack = GetComponent<PlayerAttackController>();
            if (attack != null)
            {
                attack.CancelCharge();
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
            CancelCatchReservation();
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

        public bool TryPickUpNearest()
        {
            if (InteractionLocked) { return false; }
            int count = HeldCount;
            if (count >= GetCarryCapacity() || GetHoldPoint(count) == null) return false;
            if (count > 0 && _heldObjects[0] is SiegeCore.Cannon.Cannon) return false;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, _pickupRadius, _pickupLayers);
            ICarryable nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider2D candidate in nearby)
            {
                ICarryable item = GetCarryable(candidate);
                if (item == null || !item.CanBePickedUp) continue;
                if (count > 0 && item is SiegeCore.Cannon.Cannon) continue;
                if (!CanReach(item)) continue;

                float distance = ((Vector2)item.CarryTransform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = item;
                nearestDistance = distance;
            }

            if (nearest == null || !nearest.TryPickUp(GetHoldPoint(count))) return false;
            _heldObjects.Add(nearest);
            CaptureCarrySorting(nearest);
            RefreshCarrySorting();
            return true;
        }

        private static ICarryable GetCarryable(Component component)
        {
            MonoBehaviour[] behaviours = component.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                ICarryable carryable = behaviour as ICarryable;
                if (carryable != null) return carryable;
            }
            return null;
        }

        private bool CanReach(ICarryable item)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(transform.position, item.CarryTransform.position, _obstacleLayers);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider.isTrigger || hit.transform.IsChildOf(transform)
                    || hit.transform.IsChildOf(item.CarryTransform)) continue;
                return false;
            }
            return true;
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
                if (_aimController == null || !throwable.TryThrow(_aimController.AimDirection, groundPosition, _playerColliders)) return false;
                CarryableObject thrownCarryable = throwable as CarryableObject;
                if (thrownCarryable != null)
                {
                    _catchBlockedUntilExit.Add(thrownCarryable);
                }
            }
            else heldObject.Drop(groundPosition);
            if (_player != null) _player.StopMovementFor(Mathf.Max(0.01f, _throwMovementLockDuration));
            if (throwable == null) RestoreCarrySorting(heldObject);
            _heldObjects.RemoveAt(_heldObjects.Count - 1);
            return true;
        }

        // A cannon can accept the object at its loading point without enabling its physics.
        public bool TryTransferHeldObject(Transform destination, out CarryableObject item)
        {
            item = null;
            CarryableObject carryableObject = HeldObject as CarryableObject;
            if (!HasHeldObject || carryableObject == null || !carryableObject.TransferTo(destination)) return false;
            item = carryableObject;
            RestoreCarrySorting(carryableObject);
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
        }

        private void HandleGroundSortingRequested(IThrowable item)
        {
            RestoreCarrySorting(item);
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
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _pickupRadius);

            Transform catchPoint = GetHoldPoint(Application.isPlaying ? HeldCount : 0);
            if (catchPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(
                    catchPoint.position,
                    Mathf.Max(0.1f, _airborneCatchRadius));
                Vector3 funnelTop = catchPoint.position + Vector3.up * _catchAssistHeight;
                float assistRadius = Mathf.Max(_airborneCatchRadius, _catchAssistRadius);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(funnelTop, assistRadius);
                Gizmos.DrawLine(catchPoint.position + Vector3.left * _airborneCatchRadius,
                    funnelTop + Vector3.left * assistRadius);
                Gizmos.DrawLine(catchPoint.position + Vector3.right * _airborneCatchRadius,
                    funnelTop + Vector3.right * assistRadius);
            }
        }
    }
}
