using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One weighted entry in a <see cref="LootTableSO"/> (Task 13): an item plus its relative weight in
    /// the weighted-random pick. Rarity is NOT stored here — it is read from the item itself
    /// (<see cref="LootItemSO.Rarity"/>), so a table's rarity range is simply a function of which items
    /// it lists. That is how the "regular enemies never roll Legendary/Unique" rule is enforced at the
    /// data level (the regular table just doesn't contain those items) — no code-level rarity filter.
    /// Plain serializable data, authored inside <see cref="LootTableSO"/>; read-only at runtime.
    /// </summary>
    [Serializable]
    public sealed class LootEntry
    {
        [SerializeField] private LootItemSO _item;
        [Tooltip("Relative weight within this table's weighted-random pick (higher = more likely).")]
        [SerializeField, Min(0)] private int _weight = 1;

        public LootItemSO Item => _item;
        public int Weight => _weight;
    }
}
