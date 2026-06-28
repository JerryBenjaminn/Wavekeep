using System.Collections.Generic;

namespace Wavekeep.Gear
{
    /// <summary>
    /// The player's collection of OWNED-but-unequipped gear (Task 67 redesign). Each item is now a UNIQUE,
    /// mutable <see cref="GearInstance"/> (no stacking — two "Rare Helmets" are distinct objects), so ownership
    /// is a flat list keyed by instance identity, not a per-template count. Equipping moves an instance out into
    /// a loadout slot; unequipping/replacing returns it — so this never holds equipped instances. No capacity
    /// cap yet (a later task). A non-static plain C# class owned (via <c>GearManager</c>) by GameSession.
    /// </summary>
    public sealed class GearInventory
    {
        private readonly List<GearInstance> _items = new List<GearInstance>();

        /// <summary>Live read-only view of owned (unequipped) instances.</summary>
        public IReadOnlyList<GearInstance> Items => _items;

        public int Count => _items.Count;

        public bool Contains(GearInstance item) => item != null && _items.Contains(item);

        public void Add(GearInstance item)
        {
            if (item == null || _items.Contains(item)) return;
            _items.Add(item);
        }

        public bool Remove(GearInstance item) => item != null && _items.Remove(item);

        /// <summary>Find an owned (unequipped) instance by its id, or null.</summary>
        public GearInstance FindById(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i] != null && _items[i].ItemId == instanceId) return _items[i];
            return null;
        }

        public void Clear() => _items.Clear();
    }
}
