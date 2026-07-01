using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One weighted rarity entry in a <see cref="LootTableSO"/> (Task 68). Rarity is rolled as its OWN step on top
    /// of the slot pick, reusing the same Luck-weighted approach the loot tables used before (the listed rarities
    /// here define the tier span; <c>LuckState.LootTierMultiplier</c> reshuffles odds within it). Which tiers a
    /// table lists is still how the boss-exclusive rarity lock is enforced (data, not code) — a regular table
    /// simply omits Legendary/Unique. Plain serializable data; read-only at runtime.
    /// </summary>
    [Serializable]
    public sealed class LootRarityWeight
    {
        [SerializeField] private Rarity _rarity = Rarity.Common;
        [Tooltip("Relative base weight for this rarity before Luck weighting (higher = more likely).")]
        [SerializeField, Min(0)] private int _weight = 1;

        public Rarity Rarity => _rarity;
        public int Weight => _weight;
    }
}
