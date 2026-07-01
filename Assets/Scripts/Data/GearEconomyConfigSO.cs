using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Read-only tuning for the gear economy (Task 71): the inventory capacity cap, the Salvage Dust yield per
    /// salvaged rarity, and the Artifact Forge Dust cost per crafted rarity. Keeps these economy numbers out of
    /// code (CLAUDE.md §4) so they're tunable without recompiling. Read-only at runtime (CLAUDE.md §3.5) — the
    /// behaviour lives in <c>GearManager</c>, which reads these values.
    ///
    /// Materials are a SINGLE shared Salvage Dust resource (Task 67/71 locked decision — no per-rarity shards).
    /// </summary>
    [CreateAssetMenu(fileName = "GearEconomyConfig", menuName = "Wavekeep/Gear/Economy Config")]
    public sealed class GearEconomyConfigSO : ScriptableObject
    {
        [Header("Inventory cap")]
        [Tooltip("Hard capacity for owned-unequipped gear. New DROPS beyond this go to an overflow buffer resolved " +
                 "at the Hub; deliberate player actions (forge, unequip-return) may exceed it. Tune for pressure.")]
        [SerializeField, Min(1)] private int _inventoryCapacity = 40;

        [Header("Salvage Dust yield (indexed by Rarity: Common..Unique)")]
        [Tooltip("Dust awarded when an item of each rarity is salvaged. Scales up with rarity.")]
        [SerializeField] private int[] _salvageDustByRarity = { 1, 2, 4, 8, 16, 32 };

        [Header("Artifact Forge cost (Dust, indexed by Rarity: Common..Unique)")]
        [Tooltip("Dust cost to forge an Artifact of each rarity. Meaningfully steeper at higher tiers; Unique is " +
                 "the premium sink. Deterministic — the player picks the rarity, no RNG.")]
        [SerializeField] private int[] _forgeCostByRarity = { 10, 25, 60, 140, 320, 700 };

        [Header("Reroll-affix cost (Dust, indexed by the ITEM's Rarity: Common..Unique) — Task 75")]
        [Tooltip("Dust to reroll ONE affix's value on an item of each rarity. Cheaper than a full rarity upgrade. " +
                 "Unique = 0 → not rerollable (Unique affixes are hand-authored).")]
        [SerializeField] private int[] _rerollAffixCostByRarity = { 3, 6, 15, 35, 80, 0 };

        [Header("Upgrade-rarity cost (Dust, indexed by the item's CURRENT Rarity: Common..Unique) — Task 75")]
        [Tooltip("Dust to raise an item ONE tier (Common→Uncommon … Epic→Legendary). Cheaper than forging fresh at " +
                 "the resulting tier. Legendary = 0 (cap) and Unique = 0 (Forge-only) → no upgrade available.")]
        [SerializeField] private int[] _upgradeRarityCostByRarity = { 15, 40, 90, 200, 0, 0 };

        public int InventoryCapacity => Mathf.Max(1, _inventoryCapacity);

        /// <summary>Dust awarded for salvaging the given rarity (clamped to the authored array; 0 if unauthored).</summary>
        public int SalvageYield(Rarity rarity) => Lookup(_salvageDustByRarity, rarity);

        /// <summary>Dust cost to forge an Artifact of the given rarity (clamped; 0 if unauthored).</summary>
        public int ForgeCost(Rarity rarity) => Lookup(_forgeCostByRarity, rarity);

        /// <summary>Task 75: Dust cost to reroll one affix's value on an item of the given rarity (0 = not
        /// rerollable, i.e. Unique).</summary>
        public int RerollAffixCost(Rarity rarity) => Lookup(_rerollAffixCostByRarity, rarity);

        /// <summary>Task 75: Dust cost to raise an item ONE tier from its current rarity (0 = no upgrade available,
        /// i.e. Legendary cap or Unique).</summary>
        public int UpgradeRarityCost(Rarity rarity) => Lookup(_upgradeRarityCostByRarity, rarity);

        private static int Lookup(int[] arr, Rarity rarity)
        {
            if (arr == null || arr.Length == 0) return 0;
            int i = Mathf.Clamp((int)rarity, 0, arr.Length - 1);
            return Mathf.Max(0, arr[i]);
        }
    }
}
