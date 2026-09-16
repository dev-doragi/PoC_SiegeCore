using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RatPlayerBoundary : MonoBehaviour
    {
        [SerializeField] private RatBattlefield _battlefield;
        private Rigidbody2D _body;
        private Vector2 _lastValid;
        private void Start()
        {
            _body = GetComponent<Rigidbody2D>();
            _lastValid = _body.position;
        }

        private void FixedUpdate()
        {
            Vector2 desired = _body.position + _body.linearVelocity * Time.fixedDeltaTime;
            if (_battlefield.IsWalkable(_battlefield.Ground.WorldToCell(desired), VehicleSide.Ally))
            {
                _lastValid = _body.position;
                return;
            }

            _body.position = _lastValid;
            _body.linearVelocity = Vector2.zero;
        }
    }
}
