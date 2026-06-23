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

        /// <summary>Hits every living enemy within radius of the CASTER (e.g. an arena-wide zone).</summary>
        AreaOfEffect,

        /// <summary>Ranged impact-AoE (Task 20): resolves the nearest living enemy within <c>Range</c>
        /// (the cast distance), then hits every living enemy within <c>AoeRadius</c> of THAT enemy's
        /// position — a bolt that travels out and explodes on impact, not a blast around the caster.</summary>
        TargetedAreaOfEffect
    }
}
