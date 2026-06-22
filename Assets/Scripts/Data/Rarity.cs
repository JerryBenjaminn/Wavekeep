namespace Wavekeep.Data
{
    /// <summary>
    /// Loot rarity tiers in ascending power (CLAUDE.md §6 locked sub-decision, Task 12). Loot tables
    /// (Task 13) decide which tiers drop where (e.g. higher tiers boss-exclusive); this task only needs
    /// the enum to tag sample items. Order is meaningful (ascending) — keep it stable.
    /// </summary>
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Unique
    }
}
