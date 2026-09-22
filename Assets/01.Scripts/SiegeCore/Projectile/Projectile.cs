using SiegeCore.Cannon;
using SiegeCore.Combat;
using SiegeCore.Player;
using SiegeCore.Rat;
using UnityEngine;

namespace SiegeCore.Projectile
{
    public enum ProjectileEndReason
    {
        SiegeHit,
        Cancelled,
        Expired
    }

    [RequireComponent(typeof(RatAgent))]
    [RequireComponent(typeof(CarryableObject))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        private RatAgent _agent;
        private RatFlightMotion _motion;

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
            get { return _motion.Height; }
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
            _motion = GetComponent<RatFlightMotion>();

            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        private void OnEnable()
        {
            ResetForPool();
            _agent.StateChanged += HandleStateChanged;
            _motion.Settled += HandleCarryableStateChanged;
        }

        private void OnDisable()
        {
            _agent.StateChanged -= HandleStateChanged;
            _motion.Settled -= HandleCarryableStateChanged;

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
            if (_isActive && !_motion.IsCannonFlight)
            {
                Resolve();
            }
        }

        private void RefreshState(RatState state)
        {
            if (state == RatState.CannonFlight)
            {
                bool wasActive = _isActive && !_resolved;
                _isActive = true;
                _resolved = false;

                if (!wasActive)
                {
                    ProjectileAbility ability = _agent.Definition.ProjectileAbility;
                    if (ability != null)
                    {
                        ability.OnLaunched(CreateContext(transform.position));
                    }
                }

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

            Resolve(ProjectileEndReason.Cancelled);
            other.Resolve(ProjectileEndReason.Cancelled);
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
                Damage = _agent.Definition.Projectile.Damage,
                HitPoint = transform.position
            });

            Resolve(ProjectileEndReason.SiegeHit);
        }

        public void Resolve(ProjectileEndReason reason = ProjectileEndReason.Expired)
        {
            if (!_isActive || _resolved)
            {
                return;
            }

            _resolved = true;
            _isActive = false;

            ProjectileAbility ability = _agent.Definition.ProjectileAbility;
            if (ability != null)
            {
                ProjectileContext context = CreateContext(transform.position);
                if (reason == ProjectileEndReason.Cancelled)
                {
                    ability.OnCancelled(context);
                }
                else if (reason == ProjectileEndReason.SiegeHit)
                {
                    ability.OnSiegeHit(context);
                }
                else
                {
                    ability.OnExpired(context);
                }
            }

            _agent.Release();
        }

        private ProjectileContext CreateContext(Vector3 position)
        {
            return new ProjectileContext(
                _agent,
                _agent.Definition,
                _agent.Faction,
                position,
                _agent.Factory);
        }
    }
}
