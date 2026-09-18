using SiegeCore.Cannon;

namespace SiegeCore.Combat
{
    public interface IDamageable
    {
        VehicleSide Side { get; }
        bool IsDead { get; }
        void TakeDamage(DamageData damageData);
    }
}
