using System.Collections.Generic;
using UnityEngine;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(PlayerAimController))]
    public sealed class CarryController : MonoBehaviour
    {
        [SerializeField] private Transform _holdPoint;
        [SerializeField, Range(1, 3)] private int _maxCarryCount = 3;
        [SerializeField] private Transform[] _holdPoints;
        [SerializeField, Min(0.1f)] private float _pickupRadius = 1.5f;
        [SerializeField] private LayerMask _pickupLayers = ~0;
        [SerializeField] private LayerMask _obstacleLayers = ~0;
        [SerializeField, Min(0.01f)] private float _throwMovementLockDuration = 0.2f;

        private PlayerAimController _aimController;
        private PlayerController _player;
        private Collider2D[] _playerColliders;
        private Vector3 _holdOffset;
        private readonly List<ICarryable> _heldObjects = new List<ICarryable>();
        private readonly Dictionary<ICarryable, CarrySorting> _carrySorting = new Dictionary<ICarryable, CarrySorting>();
        private int _carriedLayerId;

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

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
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
            EventBus.Instance.Subscribe<PrimaryActionInputEvent>(HandlePrimaryAction);
            EventBus.Instance.Subscribe<SecondaryActionInputEvent>(HandleSecondaryAction);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<PrimaryActionInputEvent>(HandlePrimaryAction);
                EventBus.Instance.Unsubscribe<SecondaryActionInputEvent>(HandleSecondaryAction);
            }
            for (int index = _heldObjects.Count - 1; index >= 0; index--)
            {
                ICarryable item = _heldObjects[index];
                RestoreCarrySorting(item);
                if (item is Component component && component != null && item.IsCarried) item.Drop(transform.position);
            }
            _heldObjects.Clear();
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

        private void HandlePrimaryAction(PrimaryActionInputEvent inputEvent)
        {
            if (!inputEvent.IsPressed || Time.timeScale <= 0f) return;
            TryPickUpNearest();
        }

        private void HandleSecondaryAction(SecondaryActionInputEvent inputEvent)
        {
            if (inputEvent.IsPressed && Time.timeScale > 0f) TryThrow();
        }

        public bool TryPickUpNearest()
        {
            int count = HeldCount;
            if (count >= Mathf.Clamp(_maxCarryCount, 1, 3) || GetHoldPoint(count) == null) return false;
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
            if (!HasHeldObject) return false;
            ICarryable heldObject = HeldObject;

            Vector3 groundPosition = heldObject.CarryTransform.position;
            groundPosition.y = transform.position.y;
            groundPosition.z = transform.position.z;
            IThrowable throwable = heldObject as IThrowable;
            if (throwable != null)
            {
                if (_aimController == null || !throwable.TryThrow(_aimController.AimDirection, groundPosition, _playerColliders)) return false;
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
                if (item is Component component && component != null && item.IsCarried) continue;
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
        }
    }
}
