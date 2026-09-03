using UnityEngine;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(PlayerAimController))]
    public sealed class CarryController : MonoBehaviour
    {
        [SerializeField] private Transform _holdPoint;
        [SerializeField, Min(0.1f)] private float _pickupRadius = 1.5f;
        [SerializeField] private LayerMask _pickupLayers = ~0;
        [SerializeField] private LayerMask _obstacleLayers = ~0;

        private PlayerAimController _aimController;
        private Collider2D[] _playerColliders;
        private Vector3 _holdOffset;
        private CarryableObject _heldObject;

        public CarryableObject HeldObject => _heldObject;
        public bool HasHeldObject => _heldObject != null && _heldObject.IsCarried;

        private void Awake()
        {
            _aimController = GetComponent<PlayerAimController>();
            _playerColliders = GetComponentsInChildren<Collider2D>();
            if (_holdPoint == null)
            {
                Debug.LogError("[CarryController] Assign a child HoldPoint.", this);
                enabled = false;
                return;
            }
            _holdOffset = _holdPoint.localPosition;
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<PrimaryActionInputEvent>(HandlePrimaryAction);
            EventBus.Instance.Subscribe<SecondaryActionInputEvent>(HandleSecondaryAction);
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<PrimaryActionInputEvent>(HandlePrimaryAction);
            EventBus.Instance.Unsubscribe<SecondaryActionInputEvent>(HandleSecondaryAction);
            if (HasHeldObject) _heldObject.Drop(transform.position);
            _heldObject = null;
        }

        private void LateUpdate()
        {
            Vector3 offset = _holdOffset;
            if (_aimController == null || _holdPoint == null) return;
            offset.x = Mathf.Abs(offset.x) * _aimController.FacingSign;
            _holdPoint.localPosition = offset;
        }

        private void HandlePrimaryAction(PrimaryActionInputEvent inputEvent)
        {
            if (!inputEvent.IsPressed || Time.timeScale <= 0f) return;
            if (HasHeldObject) TryThrow();
            else TryPickUpNearest();
        }

        private void HandleSecondaryAction(SecondaryActionInputEvent inputEvent)
        {
            if (inputEvent.IsPressed && Time.timeScale > 0f) TryThrow();
        }

        public bool TryPickUpNearest()
        {
            if (HasHeldObject || _holdPoint == null) return false;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, _pickupRadius, _pickupLayers);
            CarryableObject nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider2D candidate in nearby)
            {
                CarryableObject item = candidate.GetComponentInParent<CarryableObject>();
                if (item == null || item.IsCarried || item.IsAirborne || !item.isActiveAndEnabled) continue;
                if (!CanReach(item)) continue;

                float distance = ((Vector2)item.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = item;
                nearestDistance = distance;
            }

            if (nearest == null || !nearest.TryPickUp(_holdPoint)) return false;
            _heldObject = nearest;
            return true;
        }

        private bool CanReach(CarryableObject item)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(transform.position, item.transform.position, _obstacleLayers);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider.isTrigger || hit.transform.IsChildOf(transform)
                    || hit.transform.IsChildOf(item.transform)) continue;
                return false;
            }
            return true;
        }

        public bool TryThrow()
        {
            if (!HasHeldObject || _aimController == null) return false;

            Vector3 groundPosition = _heldObject.transform.position;
            groundPosition.y = transform.position.y;
            groundPosition.z = transform.position.z;
            if (!_heldObject.TryThrow(_aimController.AimDirection, groundPosition, _playerColliders)) return false;
            _heldObject = null;
            return true;
        }

        // A cannon can accept the object at its loading point without enabling its physics.
        public bool TryTransferHeldObject(Transform destination, out CarryableObject item)
        {
            item = null;
            if (!HasHeldObject || !_heldObject.TransferTo(destination)) return false;
            item = _heldObject;
            _heldObject = null;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _pickupRadius);
        }
    }
}
