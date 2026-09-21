using System.Collections.Generic;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatLoadoutRuntime
    {
        private readonly List<RatLoadoutEntry> _configuredEntries =
            new List<RatLoadoutEntry>();
        private readonly List<RatDefinition> _drawPile =
            new List<RatDefinition>();
        private readonly List<RatDefinition> _discardPile =
            new List<RatDefinition>();

        public bool UsingFallback { get; private set; }
        public bool HasCards { get { return _configuredEntries.Count > 0; } }

        public void Configure(RatLoadoutDefinition definition, RatDefinition fallback)
        {
            List<RatLoadoutEntry> validEntries = new List<RatLoadoutEntry>();
            int invalidCount = definition == null
                ? 0
                : definition.CopyValidEntries(validEntries);

            if (invalidCount > 0)
            {
                Debug.LogWarning(
                    "[RatLoadout] Invalid entries were ignored. Only unique Rank1 definitions with positive Copies are allowed.",
                    definition);
            }

            ReplaceEntries(validEntries, fallback);
        }

        public void ReplaceEntries(IList<RatLoadoutEntry> entries, RatDefinition fallback)
        {
            _configuredEntries.Clear();
            UsingFallback = false;

            HashSet<RatDefinition> definitions = new HashSet<RatDefinition>();
            if (entries != null)
            {
                foreach (RatLoadoutEntry entry in entries)
                {
                    if (entry.Definition == null
                        || entry.Definition.Rank != RatRank.Rank1
                        || entry.Copies < 1
                        || !definitions.Add(entry.Definition))
                    {
                        continue;
                    }

                    _configuredEntries.Add(entry);
                }
            }

            if (_configuredEntries.Count == 0)
            {
                if (IsBasicRank1(fallback))
                {
                    _configuredEntries.Add(new RatLoadoutEntry
                    {
                        Definition = fallback,
                        Copies = 1
                    });
                    UsingFallback = true;
                }
            }

            Reset();
        }

        public bool TryPeekNext(out RatDefinition definition)
        {
            RecycleDiscardIfNeeded();
            if (_drawPile.Count == 0)
            {
                definition = null;
                return false;
            }

            definition = _drawPile[_drawPile.Count - 1];
            return true;
        }

        public void ConsumeNext()
        {
            if (_drawPile.Count == 0) return;

            int lastIndex = _drawPile.Count - 1;
            RatDefinition definition = _drawPile[lastIndex];
            _drawPile.RemoveAt(lastIndex);
            _discardPile.Add(definition);
        }

        public void Reset()
        {
            _drawPile.Clear();
            _discardPile.Clear();

            foreach (RatLoadoutEntry entry in _configuredEntries)
            {
                for (int copy = 0; copy < entry.Copies; copy++)
                {
                    _drawPile.Add(entry.Definition);
                }
            }

            Shuffle(_drawPile);
        }

        private void RecycleDiscardIfNeeded()
        {
            if (_drawPile.Count > 0 || _discardPile.Count == 0) return;

            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_drawPile);
        }

        private void Shuffle(List<RatDefinition> cards)
        {
            for (int index = cards.Count - 1; index > 0; index--)
            {
                int swapIndex = Random.Range(0, index + 1);
                RatDefinition temporary = cards[index];
                cards[index] = cards[swapIndex];
                cards[swapIndex] = temporary;
            }
        }

        private bool IsBasicRank1(RatDefinition definition)
        {
            return definition != null
                && definition.Type == RatType.Basic
                && definition.Rank == RatRank.Rank1;
        }
    }
}
