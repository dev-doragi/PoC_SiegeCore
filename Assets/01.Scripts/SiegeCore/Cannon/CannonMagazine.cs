using System.Collections.Generic;
using SiegeCore.Rat;

namespace SiegeCore.Cannon
{
    public sealed class CannonMagazine
    {
        private readonly int _capacity;
        private readonly List<CannonMagazineEntry> _items =
            new List<CannonMagazineEntry>();

        public CannonMagazine(int capacity)
        {
            if (capacity < 1)
            {
                _capacity = 1;
            }
            else
            {
                _capacity = capacity;
            }
        }
        public int Count { get { return _items.Count; } }
        public bool IsFull { get { return Count >= _capacity; } }

        public bool Enqueue(RatAgent rat)
        {
            if (rat == null || Contains(rat) || IsFull)
            {
                return false;
            }

            _items.Add(new CannonMagazineEntry(rat));
            rat.Released += Remove;
            rat.Died += Remove;
            rat.StateChanged += HandleStateChanged;
            return true;
        }

        public bool Contains(RatAgent rat)
        {
            foreach (CannonMagazineEntry entry in _items)
            {
                if (entry.Rat == rat)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryPeekReady(out RatAgent rat, out float readyTime)
        {
            rat = null;
            readyTime = float.PositiveInfinity;
            if (_items.Count == 0)
            {
                return false;
            }

            CannonMagazineEntry entry = _items[0];
            if (!entry.IsReady || entry.Rat == null)
            {
                return false;
            }

            rat = entry.Rat;
            readyTime = entry.ReadyTime;
            return true;
        }

        public void MarkReady(RatAgent rat, float readyTime)
        {
            foreach (CannonMagazineEntry entry in _items)
            {
                if (entry.Rat != rat)
                {
                    continue;
                }

                entry.MarkReady(readyTime);
                return;
            }
        }

        public bool ApplyIntruderDamage(
            VehicleSide cannonSide,
            float damageRatioPerSecond,
            float interval)
        {
            if (damageRatioPerSecond <= 0f || interval <= 0f)
            {
                return false;
            }

            bool hasIntruder = false;
            CannonMagazineEntry[] items = _items.ToArray();
            for (int index = 0; index < items.Length; index++)
            {
                RatAgent rat = items[index].Rat;
                if (rat == null
                    || rat.IsDead
                    || rat.State != RatState.Loaded
                    || rat.Faction == cannonSide)
                {
                    continue;
                }

                hasIntruder = true;
                if (rat.Definition == null)
                {
                    continue;
                }

                float maxHealth = rat.Definition.Ground.Health;
                float damage = maxHealth * damageRatioPerSecond * interval;
                rat.TakeDamage(damage);
            }

            return hasIntruder;
        }

        public void Remove(RatAgent rat)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                CannonMagazineEntry entry = _items[index];
                if (entry.Rat != rat)
                {
                    continue;
                }

                _items.RemoveAt(index);
                if (rat == null)
                {
                    return;
                }

                rat.Released -= Remove;
                rat.Died -= Remove;
                rat.StateChanged -= HandleStateChanged;
                return;
            }
        }

        private void HandleStateChanged(RatAgent rat, RatState previous, RatState next)
        {
            if (next == RatState.Loaded)
            {
                MarkReady(rat, UnityEngine.Time.time);
                return;
            }

            if (next != RatState.CannonLoading)
            {
                Remove(rat);
            }
        }

        public void CancelLoading()
        {
            CannonMagazineEntry[] items = _items.ToArray();
            foreach (CannonMagazineEntry entry in items)
            {
                if (entry.Rat != null && entry.Rat.State == RatState.CannonLoading)
                {
                    entry.Rat.CancelCannonLoading();
                }
            }
        }

        public void ReleaseAll()
        {
            CannonMagazineEntry[] items = _items.ToArray();
            foreach (CannonMagazineEntry entry in items)
            {
                RatAgent rat = entry.Rat;
                Remove(rat);
                if (rat != null && rat.IsSpawned) rat.Release();
            }
        }
    }
}
