using UnityEngine;

namespace SiegeCore.Player
{
    public sealed class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private Camera _aimCamera;

        private Vector2 _screenPosition;
        private Vector2 _aimDirection = Vector2.right;
        private bool _hasScreenPosition;

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

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<LookInputEvent>(HandleLookInput);
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<LookInputEvent>(HandleLookInput);
            _hasScreenPosition = false;
        }

        private void HandleLookInput(LookInputEvent inputEvent)
        {
            _screenPosition = inputEvent.Value;
            _hasScreenPosition = true;
        }

        private void RefreshAim()
        {
            if (!_hasScreenPosition) return;
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
