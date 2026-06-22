using System.Collections.Generic;
using Wavekeep.Data;

namespace Wavekeep.Gear
{
    /// <summary>
    /// The player's collection of OWNED-but-unequipped loot (Task 12). A non-static plain C# class
    /// owned (via <c>GearManager</c>) by <see cref="Wavekeep.Core.GameSession"/> — no static singleton.
    ///
    /// Items are identical-by-definition (no rolled stats this task), so ownership is just a per-item
    /// COUNT keyed by the <see cref="LootItemSO"/> template. Equipping moves a copy out of here into a
    /// loadout slot; unequipping/replacing returns one back — so this never holds equipped items.
    /// </summary>
    public sealed class GearInventory
    {
        private readonly Dictionary<LootItemSO, int> _counts = new Dictionary<LootItemSO, int>();

        /// <summary>Live read-only view of owned (unequipped) item counts.</summary>
        public IReadOnlyDictionary<LootItemSO, int> Owned => _counts;

        public int CountOf(LootItemSO item)
        {
            if (item == null) return 0;
            return _counts.TryGetValue(item, out int c) ? c : 0;
        }

        public void Add(LootItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return;
            _counts[item] = CountOf(item) + amount;
        }

        /// <summary>Remove <paramref name="amount"/> of an item. Returns false (changing nothing) if not
        /// enough are owned, so counts can never go negative.</summary>
        public bool Remove(LootItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            int current = CountOf(item);
            if (current < amount) return false;

            int remaining = current - amount;
            if (remaining <= 0) _counts.Remove(item);
            else _counts[item] = remaining;
            return true;
        }

        public void Clear() => _counts.Clear();
    }
}
