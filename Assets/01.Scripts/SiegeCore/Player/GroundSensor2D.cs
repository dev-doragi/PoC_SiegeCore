using UnityEngine;

namespace SiegeCore.Player
{
    public sealed class GroundSensor2D : MonoBehaviour
    {
        [Header("Ground Check")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private Vector2 _boxSize = new Vector2(0.55f, 0.08f);
        [SerializeField] private Vector2 _offset = new Vector2(0f, -0.55f);

        public bool IsGrounded { get; private set; }

        private void FixedUpdate()
        {
            UpdateGroundedState();
        }

        private void UpdateGroundedState()
        {
            Vector2 origin = (Vector2)transform.position + _offset;
            Collider2D hit = Physics2D.OverlapBox(origin, _boxSize, 0f, _groundLayer);

            IsGrounded = hit != null;
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 origin = (Vector2)transform.position + _offset;

            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(origin, _boxSize);
        }
    }
}
