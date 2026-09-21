using System;
using UnityEngine;

namespace SiegeCore.Rat
{
    [Serializable]
    public struct RatCatalogEntry
    {
        public RatDefinition Definition;
        public PoolDefinition Pool;
    }

    [CreateAssetMenu(menuName = "SiegeCore/Rat Catalog")]
    public sealed class RatCatalog : ScriptableObject
    {
        [SerializeField] private RatCatalogEntry[] _entries = new RatCatalogEntry[0];
        [SerializeField] private RatDefinition _fallbackDefinition;

        public RatCatalogEntry[] Entries
        {
            get { return _entries; }
        }

        public RatDefinition FallbackDefinition
        {
            get { return _fallbackDefinition; }
        }

        public bool HasValidFallback
        {
            get
            {
                return _fallbackDefinition != null
                    && _fallbackDefinition.Type == RatType.Basic
                    && _fallbackDefinition.Rank == RatRank.Rank1;
            }
        }
    }
}
