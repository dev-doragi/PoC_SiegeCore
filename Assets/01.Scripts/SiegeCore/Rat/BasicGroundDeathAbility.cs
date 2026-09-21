using UnityEngine;

namespace SiegeCore.Rat
{
    [CreateAssetMenu(menuName = "SiegeCore/Abilities/Basic Ground Death")]
    public sealed class BasicGroundDeathAbility : GroundDeathAbility
    {
        [SerializeField] private RatDefinition _spawnDefinition;

        public override void OnGroundDeath(GroundDeathAbilityContext context)
        {
            BasicRatSplit.Spawn(_spawnDefinition, context.Factory, context.Faction, context.Position, true, context.Infiltrated);
        }
    }
}
