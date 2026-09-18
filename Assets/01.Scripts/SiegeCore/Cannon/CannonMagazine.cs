using System.Collections.Generic;
using SiegeCore.Player;

namespace SiegeCore.Cannon
{
    public sealed class CannonMagazine
    {
        private readonly int _capacity;
        private readonly Queue<CarryableObject> _items = new Queue<CarryableObject>();

        public CannonMagazine(int capacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public int Count
        {
            get
            {
                RemoveMissingObjects();
                return _items.Count;
            }
        }

        public bool IsFull { get { return Count >= _capacity; } }

        public bool TryEnqueue(CarryableObject item)
        {
            if (item == null || IsFull) return false;
            _items.Enqueue(item);
            return true;
        }

        public CarryableObject Peek()
        {
            RemoveMissingObjects();
            return _items.Count > 0 ? _items.Peek() : null;
        }

        public CarryableObject Dequeue()
        {
            RemoveMissingObjects();
            return _items.Count > 0 ? _items.Dequeue() : null;
        }

        private void RemoveMissingObjects()
        {
            int count = _items.Count;
            for (int index = 0; index < count; index++)
            {
                CarryableObject item = _items.Dequeue();
                if (item != null && item.IsLoaded) _items.Enqueue(item);
            }
        }
    }
}
