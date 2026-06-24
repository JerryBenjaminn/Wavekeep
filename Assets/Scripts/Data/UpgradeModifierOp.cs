namespace Wavekeep.Data
{
    /// <summary>
    /// How an <see cref="UpgradeStatModifier"/> combines its value with the base parameter (Task 19).
    /// <see cref="Add"/> sums (use a signed delta, e.g. -1 max stacks, +0.5s freeze); <see cref="Multiply"/>
    /// scales (e.g. ×1.4 damage, ×0.667 cooldown). Multiple held modifiers on the same target compose.
    /// </summary>
    public enum UpgradeModifierOp
    {
        Add,
        Multiply,

        /// <summary>Task 31: OVERRIDE the parameter with the value (the last held Set wins). Used where a
        /// tier replaces a base value rather than stacking — e.g. Deepening Frost setting the zone slow %.</summary>
        Set
    }
}
