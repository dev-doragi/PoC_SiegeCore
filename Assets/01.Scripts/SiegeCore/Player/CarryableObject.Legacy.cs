#if UNITY_EDITOR
#pragma warning disable 0169, 0414
using SiegeCore.Rat;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace SiegeCore.Player
{
    public sealed partial class CarryableObject
    {
        // Read only by the one-time Editor migration; never used by gameplay.
        [HideInInspector]
        [SerializeField] private RatMergeResolver _mergeResolver;
        [HideInInspector]
        [SerializeField] private Tilemap _groundTilemap;
        [HideInInspector]
        [SerializeField, Min(0f)] private float _snapSteering = 12f;
        [HideInInspector]
        [SerializeField, Min(0.01f)] private float _snapFinishDuration = 0.12f;
        [HideInInspector]
        [Header("Presentation")]
        [SerializeField] private Transform _visual;
        [HideInInspector]
        [Header("Throw")]
        [SerializeField, Min(0f)] private float _throwSpeed = 5.5f;
        [HideInInspector]
        [SerializeField, Min(0f)] private float _upwardSpeed = 4f;
        [HideInInspector]
        [SerializeField, Min(1), Tooltip("머리 위에서 던질 때 이동할 8방향 타일 수입니다.")]
        private int _throwCellDistance = 2;
        [HideInInspector]
        [SerializeField, Min(0.01f)] private float _heightGravity = 16f;
        [HideInInspector]
        [SerializeField, Range(0f, 0.9f)] private float _bounceRestitution = 0.45f;
        [HideInInspector]
        [SerializeField, Range(0f, 1f)] private float _groundSpeedRetention = 0.65f;
        [HideInInspector]
        [SerializeField, Min(0.01f)] private float _minimumBounceSpeed = 0.8f;
        [HideInInspector]
        [SerializeField, Range(0f, 1f)] private float _wallRestitution = 0.5f;
        [HideInInspector]
        [Header("Full Charge Impact")]
        [SerializeField, Min(0f), Tooltip("풀차지 타격과 고속 충돌에서 해당 Rat만 멈추는 시간입니다.")]
        private float _impactStopDuration = 0.06f;
        [HideInInspector]
        [SerializeField, Min(0f), Tooltip("스쿼시된 외형이 원래 크기로 돌아오는 시간입니다.")]
        private float _squashRecoveryDuration = 0.08f;
        [HideInInspector]
        [SerializeField, Min(0f), Tooltip("벽 충돌 히트스톱이 발생하는 최소 수평 속도입니다.")]
        private float _impactStopSpeedThreshold = 3f;
        [HideInInspector]
        [Header("Flight Weight")]
        [SerializeField, Min(0f), Tooltip("등급 한 단계마다 비행 중 추가되는 초당 수평 감속입니다. B는 추가 감속이 없습니다.")]
        private float _flightDecelerationPerRank = 1.5f;
        [HideInInspector]
        [Header("Collision Fusion")]
        [SerializeField, Min(0f), Tooltip("두 Rat이 충돌 합성될 수 있는 최대 가상 높이 차이입니다.")]
        private float _fusionHeightTolerance = 0.45f;
        [HideInInspector]
        [Header("Wall Popup")]
        [SerializeField, Min(0f), Tooltip("벽 충돌 직전 수평 속력을 팝업 상승 속도로 바꾸는 배율입니다.")]
        private float _popupVerticalSpeedMultiplier = 0.6f;
        [HideInInspector]
        [SerializeField, Min(0f), Tooltip("벽 팝업의 최소 상승 속도입니다.")]
        private float _minimumPopupVerticalSpeed = 11f;
        [HideInInspector]
        [SerializeField, Min(0f), Tooltip("벽 팝업의 최대 상승 속도입니다.")]
        private float _maximumPopupVerticalSpeed = 12f;
        [HideInInspector]
        [SerializeField, Range(0f, 1f), Tooltip("예상 낙하지점을 현재 플레이어 위치 쪽으로 보정하는 비율입니다.")]
        private float _popupPlayerBlend = 0.35f;
        [HideInInspector]
        [SerializeField, Min(0f), Tooltip("팝업 후 플레이어 쪽으로 이동할 수 있는 최대 수평 속도입니다.")]
        private float _maximumPopupReturnSpeed = 8f;
        [HideInInspector]
        [Header("Cannon")]
        [SerializeField, Min(0.01f)] private float _defaultCannonFlightDuration = 3f;
        [HideInInspector]
        [SerializeField, Min(0f)] private float _defaultCannonArcHeight = 2f;
    }
}
#endif
