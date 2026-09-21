using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    public readonly struct ProjectileContext
    {
        public RatAgent Owner { get; }
        public RatDefinition Definition { get; }
        public VehicleSide Side { get; }
        public Vector3 Position { get; }
        public RatFactory Factory { get; }

        public ProjectileContext(
            RatAgent owner,
            RatDefinition definition,
            VehicleSide side,
            Vector3 position,
            RatFactory factory)
        {
            Owner = owner;
            Definition = definition;
            Side = side;
            Position = position;
            Factory = factory;
        }
    }

    public abstract class ProjectileAbility : ScriptableObject
    {
        public virtual void OnLaunched(ProjectileContext context) { }

        public virtual void OnCancelled(ProjectileContext context) { }

        public virtual void OnSiegeHit(ProjectileContext context) { }

        public virtual void OnExpired(ProjectileContext context) { }
    }
}
