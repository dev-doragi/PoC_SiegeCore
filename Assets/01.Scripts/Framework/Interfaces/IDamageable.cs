using UnityEngine;

/// <summary>
/// 대상에게 전달할 피해 정보를 담는 데이터입니다.
/// </summary>
public struct DamageData
{
    public float Damage;
    public TeamType AttackerTeam;
    public Vector2 HitPoint;
    public Vector2 KnockbackForce;
    public bool IsPiercing; // 무기가 관통 상태(Thrust)인지 여부
}

public interface IDamageable
{
    /// <summary>
    /// 대상의 소속 팀입니다.
    /// </summary>
    TeamType Team { get; }

    /// <summary>
    /// 대상의 사망 여부입니다.
    /// </summary>
    bool IsDead { get; }

    /// <summary>
    /// 피해를 적용합니다.
    /// </summary>
    void TakeDamage(DamageData damageData);
}