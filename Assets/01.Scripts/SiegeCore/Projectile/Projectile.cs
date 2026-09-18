using SiegeCore.Cannon;
using SiegeCore.Combat;
using SiegeCore.Player;
using SiegeCore.Rat;
using UnityEngine;

namespace SiegeCore.Projectile
{
    [RequireComponent(typeof(RatAgent))]
    [RequireComponent(typeof(CarryableObject))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        private RatAgent _agent;
        private CarryableObject _carryable;

        private bool _isActive;
        private bool _resolved;

        public VehicleSide Side
        {
            get { return _agent.AttackSide; }
        }

        public CannonSlot SourceSlot
        {
            get { return _agent.ProjectileSourceSlot; }
        }

        public float Height
        {
            get { return _carryable.Height; }
        }

        public bool IsCannonFlight
        {
            get
            {
                return _isActive
                    && !_resolved
                    && _agent.State == RatState.CannonFlight;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
            _carryable = GetComponent<CarryableObject>();

            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        private void OnEnable()
        {
            ResetForPool();
            _agent.StateChanged += HandleStateChanged;
            _carryable.StateChanged += HandleCarryableStateChanged;
        }

        private void OnDisable()
        {
            _agent.StateChanged -= HandleStateChanged;
            _carryable.StateChanged -= HandleCarryableStateChanged;

            ResetForPool();
        }

        internal void ResetForPool()
        {
            // Pool activation is not a new cannon launch.
            _isActive = false;
            _resolved = false;
        }

        private void HandleStateChanged(
            RatAgent agent,
            RatState previousState,
            RatState nextState)
        {
            RefreshState(nextState);
        }

        private void HandleCarryableStateChanged()
        {
            if (_isActive && !_carryable.IsCannonFlight)
            {
                Resolve();
            }
        }

        private void RefreshState(RatState state)
        {
            if (state == RatState.CannonFlight)
            {
                _isActive = true;
                _resolved = false;
                return;
            }

            _isActive = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsCannonFlight)
            {
                return;
            }

            Projectile otherProjectile =
                other.GetComponentInParent<Projectile>();

            if (otherProjectile != null
                && otherProjectile != this)
            {
                TryCancel(otherProjectile);
                return;
            }

            SiegeHealth siege =
                other.GetComponentInParent<SiegeHealth>();

            if (siege != null)
            {
                TryHitSiege(siege);
            }
        }

        private void TryCancel(Projectile other)
        {
            if (!other.IsCannonFlight)
            {
                return;
            }

            if (other.Side == Side)
            {
                return;
            }

            if (SourceSlot == null
                || other.SourceSlot == null
                || SourceSlot.TargetSlot != other.SourceSlot
                || other.SourceSlot.TargetSlot != SourceSlot)
            {
                return;
            }

            Resolve();
            other.Resolve();
        }

        private void TryHitSiege(SiegeHealth siege)
        {
            if (siege.Side == Side)
            {
                return;
            }

            siege.TakeDamage(new DamageData
            {
                AttackerSide = Side,
                Damage = _agent.Definition.ProjectileDamage,
                HitPoint = transform.position
            });

            Resolve();
        }

        public void Resolve()
        {
            if (!_isActive || _resolved)
            {
                return;
            }

            _resolved = true;
            _isActive = false;

            /*
             * BBB��� ���� Faction ���� Basic Rat�� �����Ѵ�.
             * RatAgent ���ο��� �ߺ� Burst�� �����Ѵ�.
             */
            _agent.TryBurstContents();

            _agent.Release();
        }
    }
}
