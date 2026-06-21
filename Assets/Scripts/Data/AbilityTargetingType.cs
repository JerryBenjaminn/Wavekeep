namespace Wavekeep.Data
{
    /// <summary>
    /// How an ability picks its victims at execution time. Kept minimal for Task 04 — just enough
    /// to drive the two execution paths in <c>AbilityRuntime</c>.
    /// </summary>
    public enum AbilityTargetingType
    {
        /// <summary>Hits the single nearest living enemy within range.</summary>
        SingleTarget,

        /// <summary>Hits every living enemy within radius of the caster.</summary>
        AreaOfEffect
    }
}
