using System.Collections.Generic;
using SiegeCore.Rat;

namespace SiegeCore.Cannon
{
    public sealed class CannonMagazine
    {
        private readonly int _capacity;
        private readonly List<RatAgent> _items = new List<RatAgent>();

        public CannonMagazine(int capacity) { _capacity = capacity < 1 ? 1 : capacity; }
        public int Count { get { return _items.Count; } }
        public bool IsFull { get { return Count >= _capacity; } }

        internal void Enqueue(RatAgent rat)
        {
            _items.Add(rat);
            rat.Released += Remove;
            rat.Died += Remove;
            rat.StateChanged += HandleStateChanged;
        }

        public RatAgent Peek() { return _items.Count > 0 ? _items[0] : null; }

        public void Remove(RatAgent rat)
        {
            if (!_items.Remove(rat)) return;
            rat.Released -= Remove;
            rat.Died -= Remove;
            rat.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(RatAgent rat, RatState previous, RatState next)
        {
            if (next != RatState.Loaded) Remove(rat);
        }

        public void ReleaseAll()
        {
            RatAgent[] items = _items.ToArray();
            foreach (RatAgent rat in items)
            {
                Remove(rat);
                if (rat != null && rat.IsSpawned) rat.Release();
            }
        }
    }
}
