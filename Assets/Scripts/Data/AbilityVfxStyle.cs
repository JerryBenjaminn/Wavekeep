namespace Wavekeep.Data
{
    /// <summary>
    /// Task 46: selects the VISUAL treatment an ability uses when it resolves, so the VFX layer can style
    /// a hit without the logic layer knowing a specific hero/ability (CLAUDE.md §3.8 — data-driven, not
    /// identity-keyed). Frost effects gate on their existing payload flags (AppliesFrostStack / Zone), so
    /// this enum currently only needs to distinguish Bolt Striker's electrical look from the generic
    /// diagnostic beam/ring. <see cref="Default"/> keeps the Task 08 beam/ring; <see cref="Lightning"/>
    /// routes single-target hits through the gold electrical presenter. Add values here as new visual
    /// languages appear, never branch on the ability asset itself.
    /// </summary>
    public enum AbilityVfxStyle
    {
        Default,
        Lightning,

        /// <summary>Task 47: marks an ability as using the frost (blue/white) palette for APEX VFX selection.
        /// Frost Basic/Ultimate visuals still gate on their payload flags (AppliesFrostStack / zone) — this is
        /// only read to pick an apex's palette (e.g. Remorseless Winter, Permafrost Eruption).</summary>
        Frost
    }
}
