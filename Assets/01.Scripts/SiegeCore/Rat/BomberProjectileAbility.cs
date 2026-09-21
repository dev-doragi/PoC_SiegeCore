using SiegeCore.Cannon;
using SiegeCore.Summon;
using UnityEngine;

namespace SiegeCore.Rat
{
    [CreateAssetMenu(menuName = "SiegeCore/Abilities/Bomber Projectile")]
    public sealed class BomberProjectileAbility : ProjectileAbility
    {
        [SerializeField] private NeutralBomb _bombPrefab;
        [SerializeField, Min(0f)] private float _rank1Damage = 12f;
        [SerializeField, Min(0f)] private float _damagePerRank = 6f;
        [SerializeField, Min(0.1f)] private float _blastRadius = 1.5f;
        [SerializeField, Min(0.1f)] private float _lifetime = 5f;
        [SerializeField] private LayerMask _groundLayers;
        [SerializeField] private LayerMask _targetLayers = ~0;

        public override void OnCancelled(ProjectileContext context)
        {
            if (_bombPrefab == null)
            {
                Debug.LogWarning("[BomberProjectileAbility] Bomb prefab is not assigned.");
                return;
            }

            NeutralBomb bomb = Object.Instantiate(
                _bombPrefab,
                context.Position,
                Quaternion.identity);
            int rank = context.Definition == null
                ? 1
                : (int)context.Definition.Rank;
            float damage = _rank1Damage
                + Mathf.Max(0, rank - 1) * _damagePerRank;
            bomb.Initialize(
                context.Side,
                damage,
                _blastRadius,
                _lifetime,
                _groundLayers,
                _targetLayers,
                context.Factory == null ? null : context.Factory.Battlefield);
        }
    }
}
