using SiegeCore.Cannon;
using SiegeCore.Combat;
using SiegeCore.Ground;
using SiegeCore.Rat;
using UnityEngine;

namespace SiegeCore.Summon
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class Barricade : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float _defaultHealth = 20f;

        public VehicleSide Owner { get; private set; }
        public VehicleSide Side { get { return Owner; } }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsDead { get { return CurrentHealth <= 0f; } }

        private SpriteRenderer _visual;
        private Rigidbody2D _rigidbody;
        private GroundDropMotion _dropMotion;

        private void Awake()
        {
            _visual = GetComponentInChildren<SpriteRenderer>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _dropMotion = new GroundDropMotion(_rigidbody);
            MaxHealth = Mathf.Max(1f, _defaultHealth);
            CurrentHealth = MaxHealth;
        }

        public void Initialize(
            VehicleSide owner,
            float maxHealth,
            RatBattlefield battlefield)
        {
            Owner = owner;
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            if (_visual != null)
            {
                _visual.color = Color.white;
            }

            if (battlefield != null
                && battlefield.BattlefieldGround != null)
            {
                Vector3 landingPosition =
                    battlefield.GetBattlefieldCenterAtX(
                        _rigidbody.position.x);
                _dropMotion.Begin(landingPosition, null);
            }
            else if (battlefield != null)
            {
                Debug.LogError(
                    "[Barricade] BattlefieldGround is not assigned.",
                    this);
            }
        }

        private void FixedUpdate()
        {
            _dropMotion.Tick(Time.fixedDeltaTime);
        }

        public void TakeDamage(DamageData damageData)
        {
            if (IsDead
                || damageData.Damage <= 0f
                || (!damageData.IsNeutral && damageData.AttackerSide == Owner))
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damageData.Damage);
            if (IsDead)
            {
                Destroy(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, Vector3.one);
        }
    }
}
