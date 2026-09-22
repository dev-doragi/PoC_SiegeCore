using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(RatAgent), typeof(RatFlightMotion))]
    public sealed class RatCollisionFusion : MonoBehaviour
    {
        [SerializeField] private RatMergeResolver _mergeResolver;
        [Header("Collision Fusion")]
        private RatAgent _rat;
        private RatFlightMotion _motion;
        private RatCollisionFusion _fusionPartner;
        public bool IsLocked { get; private set; }

        private void Awake()
        {
            _rat = GetComponent<RatAgent>();
            _motion = GetComponent<RatFlightMotion>();
        }

        public void ResetFusion()
        {
            IsLocked = false;
            if (_fusionPartner != null)
            {
                _fusionPartner.IsLocked = false;
                _fusionPartner._fusionPartner = null;
                _fusionPartner = null;
            }
        }

        private void OnDisable() { ResetFusion(); }

        public bool TryResolveOverlap()
        {
            if (!_motion.IsAirborne
                || _motion.SwingId == 0
                || !_motion.IsFullChargeRoute
                || !_motion.CanCollisionFuse
                || _motion.IsHitStopped
                || IsLocked)
            {
                return false;
            }

            bool hasFusionSpeed = _motion.Body.linearVelocity.magnitude
                >= _motion.FusionMinimumSpeed;

            foreach (RatAgent candidate in RatAgent.Active)
            {
                if (candidate == null || candidate.IsDead) continue;
                RatCollisionFusion other = candidate.GetComponent<RatCollisionFusion>();
                if (other == null
                    || !other.isActiveAndEnabled
                    || other == this
                    || other._rat == null
                    || other._motion.Collider == null
                    || other.IsLocked)
                {
                    continue;
                }

                ColliderDistance2D distance = _motion.Collider.Distance(other._motion.Collider);
                if (!distance.isOverlapped)
                {
                    continue;
                }

                if (!hasFusionSpeed)
                {
                    continue;
                }

                if (TryBeginCollisionFusion(other))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryBeginCollisionFusion(RatCollisionFusion other)
        {
            if (other == null
                || _motion == null
                || other._motion == null
                || _motion.Collider == null
                || other._motion.Collider == null
                || !_motion.Collider.Distance(other._motion.Collider).isOverlapped)
            {
                return false;
            }

            string rejectionReason = GetCollisionFusionRejectionReason(other);
            if (rejectionReason != null)
            {
                return false;
            }

            RatAgent otherRat = other._rat;
            Vector3 midpoint = (transform.position + other.transform.position) * 0.5f;
            if (!_mergeResolver.TryResolve(_rat.Definition, otherRat.Definition, out RatDefinition resultDefinition))
                return false;

            IsLocked = true;
            other.IsLocked = true;
            _fusionPartner = other;
            other._fusionPartner = this;

            return CompleteCollisionFusion(other, resultDefinition, midpoint);
        }

        private string GetCollisionFusionRejectionReason(RatCollisionFusion other)
        {
            if (_rat == null || other == null || other._rat == null)
            {
                return "Rat 참조 없음";
            }

            RatAgent otherRat = other._rat;
            if (_rat.Faction != VehicleSide.Ally
                || otherRat.Faction != VehicleSide.Ally)
            {
                return "아군 조합이 아님";
            }

            if (_rat.Factory == null || _rat.Factory != otherRat.Factory)
            {
                return "Factory가 다르거나 없음";
            }

            if (_rat.Definition == null || otherRat.Definition == null)
            {
                return "Rat Definition 없음";
            }

            Vector3 midpoint = (transform.position + other.transform.position) * 0.5f;
            if (!_rat.Factory.Battlefield.IsSafe(midpoint))
            {
                return "Safe Zone 밖";
            }

            if (_mergeResolver == null
                || !_mergeResolver.TryResolve(_rat.Definition, otherRat.Definition, out RatDefinition resultDefinition))
            {
                return "합성 규칙 없음";
            }

            return null;
        }

        private bool CompleteCollisionFusion(
            RatCollisionFusion other,
            RatDefinition resultDefinition,
            Vector3 position)
        {
            if (other == null || _rat == null || _rat.Factory == null)
            {
                IsLocked = false;
                _fusionPartner = null;

                return false;
            }

            RatFactory factory = _rat.Factory;
            RatAgent first = _rat;
            RatAgent second = other._rat;
            // Rat끼리의 물리 충돌은 제외되어 있으므로 현재 속도가 합성 직전 운동량이다.
            // 이전 FixedUpdate 캐시는 Hit Stop이나 풀링 전환 때문에 오래된 값일 수 있다.
            Vector2 preservedVelocity = _motion.Body.linearVelocity;
            float preservedHeight = _motion.Height;
            float preservedVerticalSpeed = _motion.VerticalSpeed;
            int preservedSwingId = _motion.SwingId;
            float preservedFusionMinimumSpeed =
                _motion.FusionMinimumSpeed;
            Transform preservedReturnTarget = _motion.ReturnTarget;
            RatAgent result = factory.Spawn(resultDefinition, VehicleSide.Ally, position);

            if (result == null)
            {
                IsLocked = false;
                other.IsLocked = false;
                _fusionPartner = null;
                other._fusionPartner = null;

                return false;
            }

            Vector2 safePosition = result.Motion.FindNearestValidGroundPosition(
                factory.Battlefield.Ground,
                position);

            result.ContinueAfterCollisionFusion(
                safePosition,
                preservedHeight,
                preservedVerticalSpeed,
                preservedVelocity,
                preservedSwingId,
                preservedFusionMinimumSpeed,
                preservedReturnTarget);

            second.Release();
            first.Release();
            return true;
        }
    }
}
