using System;
using System.Collections.Generic;
using SiegeCore.Cannon;
using SiegeCore.Rat;
using UnityEngine;

namespace SiegeCore.Player
{
    /// <summary>
    /// 차지 입력과 부채꼴 타격을 담당한다. 판정 범위와 발사 방향을 분리해
    /// 여러 Rat을 맞혀도 플레이어가 조준한 방향을 유지한다.
    /// </summary>
    [RequireComponent(typeof(PlayerAimController))]
    public sealed class PlayerAttackController : MonoBehaviour
    {
        [Serializable]
        private sealed class BatFlightProfile
        {
            [SerializeField, Min(0f), Tooltip("조준 방향으로 적용할 수평 속도입니다.")]
            private float _horizontalSpeed = 2.5f;

            [SerializeField, Min(0f), Tooltip("가상 Height에 적용할 초기 상승 속도입니다.")]
            private float _verticalSpeed = 6f;

            [SerializeField, Min(0f), Tooltip("발사 후 머리로 받을 수 없게 막는 시간입니다.")]
            private float _catchLockDuration = 0.2f;

            [SerializeField, Min(0f), Tooltip("연쇄 합성을 계속할 수 있는 최소 수평 속도입니다. 일반 공격에서는 사용하지 않습니다.")]
            private float _collisionFusionMinimumSpeed = 3f;

            public float HorizontalSpeed { get { return Mathf.Max(0f, _horizontalSpeed); } }
            public float VerticalSpeed { get { return Mathf.Max(0f, _verticalSpeed); } }
            public float CatchLockDuration { get { return Mathf.Max(0f, _catchLockDuration); } }
            public float CollisionFusionMinimumSpeed
            {
                get { return Mathf.Max(0f, _collisionFusionMinimumSpeed); }
            }

            public BatFlightProfile(
                float horizontalSpeed,
                float verticalSpeed,
                float catchLockDuration,
                float collisionFusionMinimumSpeed)
            {
                _horizontalSpeed = horizontalSpeed;
                _verticalSpeed = verticalSpeed;
                _catchLockDuration = catchLockDuration;
                _collisionFusionMinimumSpeed = collisionFusionMinimumSpeed;
            }
        }

        [Header("Attack Cone")]
        [SerializeField, Min(0.1f), Tooltip("Rat을 타격할 수 있는 최대 거리입니다.")]
        private float _attackRadius = 1.8f;

        [SerializeField, Range(1f, 180f), Tooltip("부채꼴 공격 범위의 전체 각도입니다.")]
        private float _attackAngle = 90f;

        [SerializeField, Range(1f, 2f), Tooltip("풀차지에서 공격 반경에 곱하는 값입니다.")]
        private float _fullChargeRadiusMultiplier = 1.2f;

        [SerializeField, Range(1f, 2f), Tooltip("풀차지에서 공격 각도에 곱하는 값입니다.")]
        private float _fullChargeAngleMultiplier = 1.15f;

        [SerializeField, Tooltip("타격 가능한 Rat Collider가 포함된 Layer입니다.")]
        private LayerMask _targetLayers = ~0;

        [SerializeField, Tooltip("플레이어와 Rat 사이에서 공격을 가로막는 Layer입니다.")]
        private LayerMask _obstacleLayers = 0;

        [SerializeField, Min(0f), Tooltip("적 Rat에게만 적용되는 빠따 피해량입니다.")]
        private float _enemyDamage = 1f;

        [SerializeField, Min(0f), Tooltip("공격 후 다음 차지를 시작하기까지의 시간입니다.")]
        private float _cooldown = 0.25f;

        [SerializeField, Min(0f), Tooltip("빠따를 놓은 직후 이동이 잠기는 시간입니다.")]
        private float _swingMovementLockDuration = 0.15f;

        [Header("Charge")]
        [SerializeField, Min(0f), Tooltip("이 시간보다 짧게 누르고 놓으면 공격, 쿨다운, 이동 잠금이 모두 발생하지 않습니다.")]
        private float _minimumChargeDuration = 0.2f;

        [SerializeField, Min(0.05f), Tooltip("풀차지까지 필요한 시간입니다.")]
        private float _fullChargeDuration = 0.8f;

        [SerializeField, Range(0.8f, 1f), Tooltip("풀차지 충돌 기능이 활성화되는 차지 비율입니다.")]
        private float _fullChargeThreshold = 1f;

        [Header("Normal Attack Flight")]
        [SerializeField, Tooltip("최소 차지 이상, 풀차지 미만 공격에 사용하는 고정 비행 설정입니다.")]
        private BatFlightProfile _normalAttackProfile =
            new BatFlightProfile(0.6f, 12f, 0.2f, 0f);

        [Header("Full Charge Flight")]
        [SerializeField, Tooltip("풀차지 공격의 장거리 비행과 연쇄 합성 설정입니다.")]
        private BatFlightProfile _fullChargeProfile =
            new BatFlightProfile(20f, 1.5f, 0.8f, 3f);

        [SerializeField, Range(0.1f, 1f), Tooltip("풀차지 중 유지되는 플레이어 이동 속도 비율입니다.")]
        private float _fullChargeMovementMultiplier = 0.25f;

        [SerializeField, Range(0f, 20f), Tooltip("여러 Rat을 맞힐 때 가장자리에 적용할 최대 퍼짐 각도입니다.")]
        private float _maximumSpreadAngle = 7f;

        [Header("Charge Debug")]
        [SerializeField, Tooltip("차지 진행도와 발사 결과를 Console에 표시합니다.")]
        private bool _showChargeLogs = true;

        [SerializeField, Range(0.1f, 0.5f), Tooltip("차지 중 로그를 남기는 진행 간격입니다. 0.25는 25% 간격입니다.")]
        private float _chargeLogStep = 0.25f;

        [Header("Optional Presentation")]
        [SerializeField, Tooltip("차지 중 표시할 선택형 부채꼴 오브젝트입니다.")]
        private GameObject _chargePreview = null;

        [SerializeField, Tooltip("차지 거리를 표시할 선택형 LineRenderer입니다.")]
        private LineRenderer _rangePreview = null;

        private readonly List<RatAgent> _targets = new List<RatAgent>();
        private readonly HashSet<RatAgent> _uniqueTargets = new HashSet<RatAgent>();
        private PlayerAimController _aimController = null;
        private PlayerController _playerController = null;
        private RatStacking _stacking = null;
        private bool _isCharging = false;
        private float _chargeStartedAt = 0f;
        private float _nextAttackTime = 0f;
        private int _nextSwingId = 1;
        private int _lastLoggedChargeStep = -1;

        public bool IsCharging { get { return _isCharging; } }

        public float ChargeRatio
        {
            get
            {
                if (!_isCharging)
                {
                    return 0f;
                }

                float minimumDuration = GetMinimumChargeDuration();
                float fullDuration = Mathf.Max(minimumDuration + 0.01f, _fullChargeDuration);
                float attackChargeDuration = fullDuration - minimumDuration;
                float elapsedAfterMinimum = Time.time - _chargeStartedAt - minimumDuration;
                return Mathf.Clamp01(elapsedAfterMinimum / attackChargeDuration);
            }
        }

        private void Awake()
        {
            _aimController = GetComponent<PlayerAimController>();
            _playerController = GetComponent<PlayerController>();
            _stacking = GetComponent<RatStacking>();
            SetPreviewVisible(false);
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<PrimaryActionInputEvent>(HandlePrimaryAction);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<PrimaryActionInputEvent>(HandlePrimaryAction);
            }

            CancelCharge();
        }

        private void Update()
        {
            if (!_isCharging)
            {
                return;
            }

            if (Time.timeScale <= 0f
                || (_playerController != null && _playerController.IsKnockedBack)
                || (_stacking != null && _stacking.IsStacking))
            {
                CancelCharge();
                return;
            }

            UpdatePreview();
            UpdateChargeMovementSpeed();
            LogChargeProgress();
        }

        private void HandlePrimaryAction(PrimaryActionInputEvent inputEvent)
        {
            if (inputEvent.IsPressed)
            {
                BeginCharge();
                return;
            }

            ReleaseCharge();
        }

        private void BeginCharge()
        {
            if (_isCharging
                || Time.timeScale <= 0f
                || Time.time < _nextAttackTime
                || (_playerController != null && _playerController.IsKnockedBack)
                || (_stacking != null && _stacking.IsStacking))
            {
                return;
            }

            _isCharging = true;
            _chargeStartedAt = Time.time;
            _lastLoggedChargeStep = -1;
            SetPreviewVisible(true);
            UpdatePreview();
            UpdateChargeMovementSpeed();
            LogChargeProgress();
        }

        private void ReleaseCharge()
        {
            if (!_isCharging)
            {
                return;
            }

            float chargeDuration = Time.time - _chargeStartedAt;
            float chargeRatio = ChargeRatio;
            _isCharging = false;
            ReleaseMovementLock();
            SetPreviewVisible(false);

            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (chargeDuration < GetMinimumChargeDuration())
            {
                if (_showChargeLogs)
                {
                    Debug.Log(
                        "[Bat Charge] Cancelled before minimum charge ("
                        + chargeDuration.ToString("0.00")
                        + "s / "
                        + GetMinimumChargeDuration().ToString("0.00")
                        + "s)",
                        this);
                }

                return;
            }

            _nextAttackTime = Time.time + Mathf.Max(0f, _cooldown);
            if (_playerController != null)
            {
                _playerController.StopMovementFor(_swingMovementLockDuration);
            }
            PerformSwing(chargeRatio);
        }

        public void CancelCharge()
        {
            _isCharging = false;
            ReleaseMovementLock();
            SetPreviewVisible(false);
        }

        private void PerformSwing(float chargeRatio)
        {
            Vector2 aimDirection = _aimController.AimDirection;
            CollectTargets(aimDirection, chargeRatio);

            bool isFullCharge = chargeRatio >= _fullChargeThreshold;
            BatFlightProfile flightProfile = GetFlightProfile(isFullCharge);
            float speed = flightProfile.HorizontalSpeed;
            int swingId = _nextSwingId;
            _nextSwingId++;

            if (_showChargeLogs)
            {
                Debug.Log(
                    "[Bat Charge] Release "
                    + Mathf.RoundToInt(chargeRatio * 100f)
                    + "% | Speed "
                    + speed.ToString("0.0")
                    + " | Targets "
                    + _targets.Count
                    + " | Aim "
                    + aimDirection,
                    this);
            }

            for (int index = 0; index < _targets.Count; index++)
            {
                RatAgent rat = _targets[index];
                if (rat.Faction == VehicleSide.Enemy)
                {
                    rat.TakeDamage(_enemyDamage);
                    if (rat.State == RatState.Dead)
                    {
                        continue;
                    }
                }

                Vector2 launchDirection = GetLaunchDirection(rat, aimDirection, chargeRatio);
                rat.LaunchFromBat(
                    launchDirection,
                    speed,
                    flightProfile.VerticalSpeed,
                    flightProfile.CatchLockDuration,
                    flightProfile.CollisionFusionMinimumSpeed,
                    swingId,
                    isFullCharge,
                    transform);
            }
        }

        private void CollectTargets(Vector2 aimDirection, float chargeRatio)
        {
            _targets.Clear();
            _uniqueTargets.Clear();
            float attackRadius = GetAttackRadius(chargeRatio);
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                attackRadius,
                _targetLayers);

            for (int index = 0; index < hits.Length; index++)
            {
                RatAgent rat = hits[index].GetComponentInParent<RatAgent>();
                if (rat == null
                    || !_uniqueTargets.Add(rat)
                    || !CanHit(rat, aimDirection, chargeRatio))
                {
                    continue;
                }

                _targets.Add(rat);
            }
        }

        private bool CanHit(RatAgent rat, Vector2 aimDirection, float chargeRatio)
        {
            if (!rat.CanBeHitByBat)
            {
                return false;
            }

            Vector2 offset = (Vector2)rat.transform.position - (Vector2)transform.position;
            float attackAngle = GetAttackAngle(chargeRatio);
            if (offset.sqrMagnitude >= 0.0001f
                && Vector2.Angle(aimDirection, offset.normalized) > attackAngle * 0.5f)
            {
                return false;
            }

            RaycastHit2D hit = Physics2D.Linecast(
                transform.position,
                rat.transform.position,
                _obstacleLayers);
            return hit.collider == null;
        }

        private Vector2 GetLaunchDirection(
            RatAgent rat,
            Vector2 aimDirection,
            float chargeRatio)
        {
            if (_targets.Count <= 1 || _maximumSpreadAngle <= 0f)
            {
                return aimDirection;
            }

            Vector2 offset = (Vector2)rat.transform.position - (Vector2)transform.position;
            float signedAngle = Vector2.SignedAngle(aimDirection, offset.normalized);
            float halfAngle = Mathf.Max(0.5f, GetAttackAngle(chargeRatio) * 0.5f);
            float normalizedOffset = Mathf.Clamp(signedAngle / halfAngle, -1f, 1f);
            float spreadAngle = normalizedOffset * _maximumSpreadAngle;
            return Quaternion.Euler(0f, 0f, spreadAngle) * aimDirection;
        }

        private void UpdatePreview()
        {
            Vector2 aimDirection = _aimController.AimDirection;
            CollectTargets(aimDirection, ChargeRatio);
            if (_chargePreview != null)
            {
                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                _chargePreview.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (_rangePreview == null)
            {
                return;
            }

            bool isFullCharge = ChargeRatio >= _fullChargeThreshold;
            BatFlightProfile flightProfile = GetFlightProfile(isFullCharge);
            float speed = flightProfile.HorizontalSpeed;
            Vector3 start = transform.position;
            float flightTime = GetPreviewFlightTime(flightProfile.VerticalSpeed);
            if (_targets.Count > 0)
            {
                RatAgent representative = GetRepresentativeTarget(aimDirection);
                if (representative != null)
                {
                    start = representative.Carryable.PhysicsPosition;
                }
            }

            Vector3 end = start + (Vector3)(aimDirection * speed * flightTime);
            _rangePreview.positionCount = 2;
            _rangePreview.SetPosition(0, start);
            _rangePreview.SetPosition(1, end);
        }

        private float GetPreviewFlightTime(float verticalSpeed)
        {
            if (_targets.Count == 0)
            {
                return 0.5f;
            }

            CarryableObject carryable = _targets[0].Carryable;
            float gravity = Mathf.Max(0.01f, carryable.HeightGravity);
            return Mathf.Max(0.05f, 2f * Mathf.Max(0f, verticalSpeed) / gravity);
        }

        private BatFlightProfile GetFlightProfile(bool isFullCharge)
        {
            if (isFullCharge)
            {
                return _fullChargeProfile;
            }

            return _normalAttackProfile;
        }

        private RatAgent GetRepresentativeTarget(Vector2 aimDirection)
        {
            RatAgent representative = null;
            float smallestAngle = float.PositiveInfinity;

            for (int index = 0; index < _targets.Count; index++)
            {
                RatAgent rat = _targets[index];
                Vector2 offset = (Vector2)rat.transform.position - (Vector2)transform.position;
                float angle = Vector2.Angle(aimDirection, offset.normalized);
                if (angle >= smallestAngle)
                {
                    continue;
                }

                representative = rat;
                smallestAngle = angle;
            }

            return representative;
        }

        private void SetPreviewVisible(bool visible)
        {
            if (_chargePreview != null)
            {
                _chargePreview.SetActive(visible);
            }

            if (_rangePreview != null)
            {
                _rangePreview.enabled = visible;
            }
        }

        private void ReleaseMovementLock()
        {
            if (_playerController != null)
            {
                _playerController.SetActionMovementLocked(false);
                _playerController.SetActionMovementSpeedMultiplier(1f);
            }
        }

        private void UpdateChargeMovementSpeed()
        {
            if (_playerController == null)
            {
                return;
            }

            float multiplier = Mathf.Lerp(
                1f,
                _fullChargeMovementMultiplier,
                ChargeRatio);
            _playerController.SetActionMovementSpeedMultiplier(multiplier);
        }

        private float GetAttackRadius(float chargeRatio)
        {
            float multiplier = Mathf.Lerp(
                1f,
                _fullChargeRadiusMultiplier,
                Mathf.Clamp01(chargeRatio));
            return Mathf.Max(0.1f, _attackRadius * multiplier);
        }

        private float GetMinimumChargeDuration()
        {
            float fullDuration = Mathf.Max(0.05f, _fullChargeDuration);
            return Mathf.Clamp(_minimumChargeDuration, 0f, fullDuration - 0.01f);
        }

        private float GetAttackAngle(float chargeRatio)
        {
            float multiplier = Mathf.Lerp(
                1f,
                _fullChargeAngleMultiplier,
                Mathf.Clamp01(chargeRatio));
            return Mathf.Clamp(_attackAngle * multiplier, 1f, 180f);
        }

        private void LogChargeProgress()
        {
            if (!_showChargeLogs)
            {
                return;
            }

            float stepSize = Mathf.Max(0.1f, _chargeLogStep);
            int currentStep = Mathf.FloorToInt(ChargeRatio / stepSize);
            if (currentStep == _lastLoggedChargeStep)
            {
                return;
            }

            _lastLoggedChargeStep = currentStep;
            int percent = Mathf.RoundToInt(ChargeRatio * 100f);
            Debug.Log(
                "[Bat Charge] "
                + percent
                + "% | Move "
                + Mathf.RoundToInt(
                    Mathf.Lerp(1f, _fullChargeMovementMultiplier, ChargeRatio) * 100f)
                + "% | Radius "
                + GetAttackRadius(ChargeRatio).ToString("0.00")
                + " | Angle "
                + GetAttackAngle(ChargeRatio).ToString("0.0"),
                this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector2 direction = Vector2.right;
            PlayerAimController aimController = GetComponent<PlayerAimController>();
            if (Application.isPlaying && aimController != null)
            {
                direction = aimController.AimDirection;
            }

            float gizmoCharge = 0f;
            if (Application.isPlaying)
            {
                gizmoCharge = ChargeRatio;
            }

            float currentRadius = GetAttackRadius(gizmoCharge);
            float halfAngle = GetAttackAngle(gizmoCharge) * 0.5f;
            Vector2 leftEdge = Quaternion.Euler(0f, 0f, -halfAngle) * direction;
            Vector2 rightEdge = Quaternion.Euler(0f, 0f, halfAngle) * direction;
            Vector3 center = transform.position;
            Gizmos.DrawLine(center, center + (Vector3)(leftEdge * currentRadius));
            Gizmos.DrawLine(center, center + (Vector3)(rightEdge * currentRadius));

            const int segmentCount = 12;
            Vector3 previous = center + (Vector3)(leftEdge * currentRadius);
            for (int index = 1; index <= segmentCount; index++)
            {
                float progress = (float)index / segmentCount;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, progress);
                Vector2 arcDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                Vector3 current = center + (Vector3)(arcDirection * currentRadius);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }
}
