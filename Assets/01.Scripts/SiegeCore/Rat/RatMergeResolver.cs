using System;
using UnityEngine;

namespace SiegeCore.Rat
{
    public interface IRatMergeResolver
    {
        bool TryResolve(RatDefinition first, RatDefinition second, out RatDefinition result);
    }

    [Serializable]
    public struct RatMergeRule
    {
        public RatDefinition First;
        public RatDefinition Second;
        public RatDefinition Result;
    }

    [CreateAssetMenu(menuName = "SiegeCore/Rat Merge Resolver")]
    public sealed class RatMergeResolver : ScriptableObject, IRatMergeResolver
    {
        [SerializeField] private RatMergeRule[] _rules = new RatMergeRule[0];

        public bool TryResolve(RatDefinition first, RatDefinition second, out RatDefinition result)
        {
            result = null;
            if (first == null || second == null) return false;
            foreach (RatMergeRule rule in _rules)
            {
                if ((rule.First == first && rule.Second == second)
                    || (rule.First == second && rule.Second == first))
                {
                    result = rule.Result;
                    return result != null;
                }
            }
            return false;
        }
    }
}
