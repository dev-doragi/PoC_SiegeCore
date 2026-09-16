using SiegeCore.Cannon;
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
        [SerializeField, Min(0f)]
        private float _cancellationHeightDifference = 0.5f;

        private RatAgent _agent;
        private CarryableObject _carryable;

        private bool _isActive;
        private bool _resolved;

        public VehicleSide Side
        {
            get { return _agent.AttackSide; }
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
            _agent.StateChanged += HandleStateChanged;

            RefreshState(_agent.State);
        }

        private void OnDisable()
        {
            _agent.StateChanged -= HandleStateChanged;

            _isActive = false;
            _resolved = false;
        }

        private void Update()
        {
            if (!IsCannonFlight)
            {
                return;
            }

            /*
             * 실제 비행은 CarryableObject가 담당한다.
             * CarryableObject의 CannonFlight가 끝났다면
             * 목적지까지 정상 비행이 끝난 것이다.
             */
            if (!_carryable.IsCannonFlight)
            {
                Resolve();
            }
        }

        private void HandleStateChanged(
            RatAgent agent,
            RatState previousState,
            RatState nextState)
        {
            RefreshState(nextState);
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

            float heightDifference =
                Mathf.Abs(other.Height - Height);

            if (heightDifference > _cancellationHeightDifference)
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

            siege.TakeProjectileDamage(
                Side,
                _agent.Definition.ProjectileDamage,
                transform.position);

            Resolve();
        }

        public void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _isActive = false;

            /*
             * BBB라면 원래 Faction 기준 Basic Rat을 배출한다.
             * RatAgent 내부에서 중복 Burst는 방지한다.
             */
            _agent.TryBurstContents();

            _agent.Release();
        }
    }
}