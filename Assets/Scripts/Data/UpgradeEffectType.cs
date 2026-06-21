namespace Wavekeep.Data
{
    /// <summary>
    /// The generic effect an <see cref="UpgradeDefinitionSO"/> represents. Stored as data for Task 04
    /// (the shared upgrade pool + card picker in later tasks will act on it). In Task 04 itself,
    /// upgrades influence ability output via tag interactions (<see cref="TagInteractionRule"/>),
    /// not by this effect being applied globally yet. Kept small for MVP.
    /// </summary>
    public enum UpgradeEffectType
    {
        FlatDamageBonus,
        CooldownReductionPercent,
        AoeRadiusBonus
    }
}
