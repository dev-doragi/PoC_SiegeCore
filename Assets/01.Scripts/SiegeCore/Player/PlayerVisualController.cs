using UnityEngine;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(PlayerAimController))]
    public sealed class PlayerVisualController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private bool _flipByHorizontalDirection = true;

        private PlayerAimController _aimController;

        private void Awake()
        {
            _aimController = GetComponent<PlayerAimController>();

            if (_bodyRenderer == null)
            {
                _bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void LateUpdate()
        {
            if (_aimController == null || _bodyRenderer == null)
            {
                return;
            }

            if (!_flipByHorizontalDirection)
            {
                return;
            }

            _bodyRenderer.flipX = _aimController.IsFacingLeft;
        }
    }
}
