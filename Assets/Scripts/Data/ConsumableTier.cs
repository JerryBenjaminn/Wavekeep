namespace Wavekeep.Data
{
    /// <summary>
    /// Power tier of a <see cref="ConsumableDefinitionSO"/> (Task 09). T1 weakest → T3 strongest.
    /// A lightweight enum (like <see cref="UpgradeTag"/> / <see cref="ConsumableEffectType"/>) rather
    /// than a bare int, so shop/offer code reads by name and adding a tier later is a one-line change.
    /// Drives the shop's "[T2]" label and the Reroll Potion variants; tier-weighted offer probability
    /// is a documented follow-up (it ties into the separate wave-scaling task).
    /// </summary>
    public enum ConsumableTier
    {
        Tier1,
        Tier2,
        Tier3
    }
}
