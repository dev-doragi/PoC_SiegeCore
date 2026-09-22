using System.Collections.Generic;
using SiegeCore.Cannon;
using SiegeCore.Combat;
using SiegeCore.Ground;
using SiegeCore.Rat;
using UnityEngine;

namespace SiegeCore.Summon
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class NeutralBomb : MonoBehaviour
    {
        private Rigidbody2D _rigidbody;
        private float _damage;
        private float _radius;
        private float _lifetime;
        private float _destroyAt;
        private LayerMask _groundLayers;
        private LayerMask _targetLayers;
        private VehicleSide _attackerSide;
        private GroundDropMotion _dropMotion;
        private bool _detonated;
        private bool _destroyWithoutDetonation;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _dropMotion = new GroundDropMotion(_rigidbody);
        }

        public void Initialize(
            VehicleSide attackerSide,
            float damage,
            float radius,
            float lifetime,
            LayerMask groundLayers,
            LayerMask targetLayers,
            RatBattlefield battlefield)
        {
            _attackerSide = attackerSide;
            _damage = Mathf.Max(0f, damage);
            _radius = Mathf.Max(0.1f, radius);
            _lifetime = Mathf.Max(0.1f, lifetime);
            _destroyAt = Time.time + _lifetime;
            _groundLayers = groundLayers;
            _targetLayers = targetLayers.value == 0 ? Physics2D.AllLayers : targetLayers;
            _detonated = false;
            _destroyWithoutDetonation = false;

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody2D>();
            }

            if (battlefield != null
                && battlefield.BattlefieldGround != null)
            {
                Vector3 landingPosition;
                if (battlefield.TryGetBattlefieldLandingPosition(
                    _rigidbody.position.x,
                    out landingPosition))
                {
                    _dropMotion.Begin(landingPosition, Detonate);
                }
                else
                {
                    _destroyWithoutDetonation = true;
                    landingPosition = battlefield.GetBattlefieldCenterAtX(
                        _rigidbody.position.x);
                    _dropMotion.Begin(landingPosition, DestroyWithoutDetonation);
                }
                return;
            }

            if (battlefield != null)
            {
                Debug.LogError(
                    "[NeutralBomb] BattlefieldGround is not assigned.",
                    this);
            }

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.linearVelocity = Vector2.zero;
        }

        private void FixedUpdate()
        {
            _dropMotion.Tick(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (!_detonated && Time.time >= _destroyAt)
            {
                if (_destroyWithoutDetonation)
                {
                    DestroyWithoutDetonation();
                }
                else
                {
                    Detonate();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_dropMotion.IsDropping)
            {
                return;
            }

            if (other.GetComponentInParent<RatAgent>() != null)
            {
                return;
            }

            if (IsGroundCollider(other))
            {
                Detonate();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_dropMotion.IsDropping)
            {
                return;
            }

            if (collision.collider.GetComponentInParent<RatAgent>() != null)
            {
                return;
            }

            if (IsGroundCollider(collision.collider))
            {
                Detonate();
            }
        }

        private bool IsGroundCollider(Collider2D other)
        {
            if (_groundLayers.value == 0)
            {
                return true;
            }

            return (_groundLayers.value & (1 << other.gameObject.layer)) != 0;
        }

        private void Detonate()
        {
            if (_detonated)
            {
                return;
            }

            _detonated = true;
            _dropMotion.Cancel();
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                _radius,
                _targetLayers);
            HashSet<RatAgent> rats = new HashSet<RatAgent>();

            for (int index = 0; index < hits.Length; index++)
            {
                RatAgent rat = hits[index].GetComponentInParent<RatAgent>();
                if (rat == null || !rats.Add(rat) || rat.IsDead)
                {
                    continue;
                }

                rat.TakeDamage(new DamageData
                {
                    AttackerSide = _attackerSide,
                    Damage = _damage,
                    HitPoint = transform.position,
                    IsNeutral = true
                });
            }

            Destroy(gameObject);
        }

        private void DestroyWithoutDetonation()
        {
            _detonated = true;
            _dropMotion.Cancel();
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
