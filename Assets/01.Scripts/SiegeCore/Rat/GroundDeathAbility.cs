using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    public readonly struct GroundDeathAbilityContext
    {
        public readonly RatFactory Factory;
        public readonly VehicleSide Faction;
        public readonly Vector3 Position;
        public readonly bool Infiltrated;

        public GroundDeathAbilityContext(RatFactory factory, VehicleSide faction, Vector3 position, bool infiltrated)
        {
            Factory = factory;
            Faction = faction;
            Position = position;
            Infiltrated = infiltrated;
        }
    }

    public abstract class GroundDeathAbility : ScriptableObject
    {
        public virtual void OnGroundDeath(GroundDeathAbilityContext context) { }
    }
}
