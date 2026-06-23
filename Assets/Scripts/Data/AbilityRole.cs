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
        Ultimate
    }
}
