namespace Wavekeep.Data
{
    /// <summary>
    /// The specific ability/frost parameter an <see cref="UpgradeStatModifier"/> adjusts (Task 19).
    /// This is the generic, data-driven vocabulary the hero-exclusive upgrade effects are authored in —
    /// runtime code switches on this enum, never on a specific upgrade identity. Role-prefixed targets
    /// (Basic*/Ultimate*) are resolved only by the matching ability role so an upgrade can boost one
    /// ability without leaking into the other.
    /// </summary>
    public enum UpgradeModifierTarget
    {
        BasicDamage,
        BasicCooldown,
        BasicRadius,
        UltimateDuration,
        UltimateSlowMagnitude,
        FrostMaxStacks,
        FrostFreezeDuration,

        /// <summary>Task 31 (Wider Burst): added to the basic AoE's max-targets cap (Add op).</summary>
        BasicMaxTargets
    }
}
