using UnityEngine;

namespace SiegeCore.Rat
{
    [CreateAssetMenu(menuName = "SiegeCore/Abilities/Basic Projectile")]
    public sealed class BasicProjectileAbility : ProjectileAbility
    {
        [SerializeField] private RatDefinition _spawnDefinition;

        public override void OnCancelled(ProjectileContext context)
        {
            BasicRatSplit.Spawn(_spawnDefinition, context.Factory, context.Side, context.Position, false, false);
        }
    }
}
