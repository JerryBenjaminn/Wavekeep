namespace Wavekeep.Data
{
    /// <summary>
    /// Which slot a hero plugs an ability into (CLAUDE.md §3.8). Basic and Ultimate are both just
    /// <see cref="AbilityDefinitionSO"/> instances; this enum lets the runtime resolve role-targeted
    /// upgrade modifiers (Task 19 — e.g. "basic damage +40%" must not also boost the ultimate) without
    /// branching on a specific ability identity.
    /// </summary>
    public enum AbilityRole
    {
        Basic,
        Ultimate,

        /// <summary>Task 29: an Apex Talent's own independent ability. Gets no role-targeted Basic/Ultimate
        /// upgrade modifiers — it is a separate skill, not a buff on either of them.</summary>
        Apex
    }
}
