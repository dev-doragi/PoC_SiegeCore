using SiegeCore.Cannon;
using SiegeCore.Player;
using UnityEngine;

namespace SiegeCore.Projectile
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private Transform _visual;

        private ProjectilePool _owner;
        private Rigidbody2D _rigidbody;
        private Vector3 _visualRestPosition;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;
        private float _flightDuration;
        private float _flightTime;
        private float _arcHeight;
        private float _damage;

        public VehicleSide Side { get; private set; }
        public CannonTrajectoryType TrajectoryType { get; private set; }
        public float Height { get; private set; }
        public bool IsCannonFlight { get; private set; }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            if (_visual == null || _visual == transform)
            {
                Debug.LogError("[Projectile] Assign a separate Visual child.", this);
                enabled = false;
                return;
            }
            _visualRestPosition = _visual.localPosition;
            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        public void Launch(ProjectilePool owner, Vector3 startPosition, Vector3 targetPosition, VehicleSide side,
            CannonTrajectoryType trajectoryType, float flightDuration, float arcHeight, float damage)
        {
            // This prefab may also be used as pickup ammunition in the scene.
            // Only pooled flight hands presentation and physics over to Projectile.
            CarryableObject ammunition = GetComponent<CarryableObject>();
            if (ammunition != null) ammunition.enabled = false;

            _owner = owner;
            Side = side;
            TrajectoryType = trajectoryType;
            _startPosition = startPosition;
            _targetPosition = targetPosition;
            _flightDuration = Mathf.Max(0.01f, flightDuration);
            _flightTime = 0f;
            _arcHeight = Mathf.Max(0f, arcHeight);
            _damage = Mathf.Max(0f, damage);
            Height = 0f;
            IsCannonFlight = true;
            _rigidbody.position = _startPosition;
            transform.position = _startPosition;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.simulated = true;
            GetComponent<Collider2D>().isTrigger = true;
            _visual.gameObject.SetActive(true);
            UpdateVisual();
        }

        private void FixedUpdate()
        {
            if (!IsCannonFlight) return;

            _flightTime += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(_flightTime / _flightDuration);
            Vector2 position = Vector2.Lerp(_startPosition, _targetPosition, progress);
            _rigidbody.MovePosition(position);
            Height = TrajectoryType == CannonTrajectoryType.Arc
                ? 4f * _arcHeight * progress * (1f - progress)
                : 0f;

            if (progress >= 1f) ReturnToPool();
        }

        private void LateUpdate()
        {
            if (IsCannonFlight) UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_visual == null) return;
            _visual.position = transform.TransformPoint(_visualRestPosition) + Vector3.up * Height;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsCannonFlight) return;
            SiegeHealth siege = other.GetComponentInParent<SiegeHealth>();
            if (siege == null || siege.Side == Side) return;

            siege.TakeProjectileDamage(Side, _damage, transform.position);
            ReturnToPool();
        }

        public void ReturnToPool()
        {
            if (!IsCannonFlight) return;
            IsCannonFlight = false;
            Height = 0f;
            _visual.localPosition = _visualRestPosition;
            _owner?.Return(this);
        }
    }
}
