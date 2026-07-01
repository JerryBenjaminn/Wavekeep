using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One weighted slot/base entry in a <see cref="LootTableSO"/> (Task 68 — gear redesign part 2). Replaces the
    /// old "entry IS a finished item" meaning: an entry now references the <see cref="GearBaseSO"/> (i.e. the slot)
    /// that can drop, plus its relative weight in the slot pick. RARITY is no longer baked here — it is a separate
    /// Luck-weighted roll (see <see cref="LootTableSO.RarityWeights"/>) resolved on top of the chosen slot. Plain
    /// serializable data; read-only at runtime.
    /// </summary>
    [Serializable]
    public sealed class LootSlotEntry
    {
        [SerializeField] private GearBaseSO _base;
        [Tooltip("Relative weight within this table's slot pick (higher = more likely).")]
        [SerializeField, Min(0)] private int _weight = 1;

        public GearBaseSO Base => _base;
        public int Weight => _weight;
    }
}
