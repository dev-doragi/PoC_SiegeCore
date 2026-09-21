using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeCore.Rat
{
    [Serializable]
    public struct RatLoadoutEntry
    {
        public RatDefinition Definition;
        [Min(1)] public int Copies;
    }

    [CreateAssetMenu(menuName = "SiegeCore/Rat Loadout")]
    public sealed class RatLoadoutDefinition : ScriptableObject
    {
        [SerializeField] private RatLoadoutEntry[] _entries = new RatLoadoutEntry[0];

        public RatLoadoutEntry[] Entries
        {
            get { return _entries; }
        }

        public int CopyValidEntries(List<RatLoadoutEntry> validEntries)
        {
            validEntries.Clear();
            if (_entries == null) return 0;

            HashSet<RatDefinition> definitions = new HashSet<RatDefinition>();
            int invalidCount = 0;

            foreach (RatLoadoutEntry entry in _entries)
            {
                if (entry.Definition == null
                    || entry.Definition.Rank != RatRank.Rank1
                    || entry.Copies < 1
                    || !definitions.Add(entry.Definition))
                {
                    invalidCount++;
                    continue;
                }

                validEntries.Add(entry);
            }

            return invalidCount;
        }
    }
}
