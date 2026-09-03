using UnityEngine;

/// <summary>
/// 풀 생성에 필요한 프리팹/초기 수량/최대 수량 설정값입니다.
/// </summary>
[System.Serializable]
public struct PoolSetupData
{
    public GameObject Prefab;
    [Tooltip("초기 생성 개수 (Prewarm)")]
    public int InitialSize;
    [Tooltip("최대 생성 허용 개수")]
    public int MaxSize;
}