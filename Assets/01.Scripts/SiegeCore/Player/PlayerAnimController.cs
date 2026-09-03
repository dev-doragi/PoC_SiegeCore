using UnityEngine;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(PlayerController), typeof(PlayerAimController))]
    public sealed class PlayerAnimController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [Header("Animator Parameters")]
        [SerializeField] private string _isMovingParameter = "IsMoving";
        [SerializeField] private string _isBackwardMoveParameter = "IsBackwardMove";

        private PlayerController _controller;
        private PlayerAimController _aimController;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _aimController = GetComponent<PlayerAimController>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            if (_animator == null || _controller == null || _aimController == null)
            {
                return;
            }

            // Side-view art: pure vertical movement uses Run; moving against the
            // mouse-facing horizontal direction uses Run_B.
            bool isMoving = _controller.IsMoving;
            bool isBackwardMove = isMoving && _controller.MoveInput.x * _aimController.FacingSign < -0.01f;
            _animator.SetBool(_isMovingParameter, isMoving);
            _animator.SetBool(_isBackwardMoveParameter, isBackwardMove);
        }
    }
}
