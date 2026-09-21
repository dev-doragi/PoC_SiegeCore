using UnityEngine;

namespace SiegeCore.Player
{
    public sealed class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private Camera _aimCamera;

        private Vector2 _screenPosition;
        private Vector2 _aimDirection = Vector2.right;
        private bool _hasScreenPosition;
        private bool _inputLocked;

        public Vector2 AimDirection
        {
            get
            {
                RefreshAim();
                return _aimDirection;
            }
        }

        public int FacingSign => AimDirection.x >= 0f ? 1 : -1;
        public bool IsFacingLeft => FacingSign < 0;
        public Vector2 EightWayAimDirection => SnapToEightDirections(AimDirection);

        public static Vector2 SnapToEightDirections(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector2.right;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle));
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<LookInputEvent>(HandleLookInput);
            EventBus.Instance.Subscribe<BattlefieldViewChangedEvent>(HandleBattlefieldViewChanged);
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<LookInputEvent>(HandleLookInput);
            EventBus.Instance.Unsubscribe<BattlefieldViewChangedEvent>(HandleBattlefieldViewChanged);
            _hasScreenPosition = false;
            _inputLocked = false;
        }

        private void HandleLookInput(LookInputEvent inputEvent)
        {
            if (_inputLocked)
            {
                return;
            }

            _screenPosition = inputEvent.Value;
            _hasScreenPosition = true;
        }

        private void HandleBattlefieldViewChanged(BattlefieldViewChangedEvent eventMessage)
        {
            _inputLocked = eventMessage.IsActive;
        }

        private void RefreshAim()
        {
            if (_inputLocked || !_hasScreenPosition) return;
            if (_aimCamera == null) _aimCamera = Camera.main;
            if (_aimCamera == null) return;

            // Reproject the cached screen position even when the mouse is stationary:
            // both the player and the Cinemachine-driven camera can still move.
            Ray ray = _aimCamera.ScreenPointToRay(_screenPosition);
            Plane playerPlane = new Plane(Vector3.forward, transform.position);
            if (!playerPlane.Raycast(ray, out float distance)) return;

            Vector2 offset = (Vector2)(ray.GetPoint(distance) - transform.position);
            if (offset.sqrMagnitude > 0.0001f) _aimDirection = offset.normalized;
        }
    }
}
