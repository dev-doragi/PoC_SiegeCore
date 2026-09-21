using SiegeCore.Cannon;
using SiegeCore.Summon;
using UnityEngine;

namespace SiegeCore.Rat
{
    [CreateAssetMenu(menuName = "SiegeCore/Abilities/Tank Projectile")]
    public sealed class TankProjectileAbility : ProjectileAbility
    {
        [SerializeField] private Barricade _barricadePrefab;
        [SerializeField, Min(1f)] private float _rank1Health = 20f;
        [SerializeField, Min(0f)] private float _healthPerRank = 15f;

        public override void OnCancelled(ProjectileContext context)
        {
            if (_barricadePrefab == null)
            {
                Debug.LogWarning("[TankProjectileAbility] Barricade prefab is not assigned.");
                return;
            }

            Barricade barricade = Object.Instantiate(
                _barricadePrefab,
                context.Position,
                Quaternion.identity);
            int rank = context.Definition == null
                ? 1
                : (int)context.Definition.Rank;
            float health = _rank1Health
                + Mathf.Max(0, rank - 1) * _healthPerRank;
            barricade.Initialize(
                context.Side,
                health,
                context.Factory == null ? null : context.Factory.Battlefield);
        }
    }
}
