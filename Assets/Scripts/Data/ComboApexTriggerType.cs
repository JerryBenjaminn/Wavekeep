namespace Wavekeep.Data
{
    /// <summary>
    /// Task 38: how a cross-hero combo apex (<see cref="ComboApexTalentDefinitionSO"/>) takes effect once
    /// BOTH of its referenced single-hero apexes are unlocked in the same run. The field exists so future
    /// combo apexes can pick either style without a new SO type (CLAUDE.md §2 data-driven).
    /// </summary>
    public enum ComboApexTriggerType
    {
        /// <summary>No independent cooldown/ability. Defines a synergy RULE layered onto the two referenced
        /// apexes' EXISTING behaviour: one apex "primes" a hit target, the other "consumes" the primed state
        /// for an amplified effect. Frozen Lightning (this task's only combo apex) is Passive.</summary>
        Passive,

        /// <summary>Would behave like a third, independent automatic ability with its OWN cooldown (like a
        /// single-hero apex). Reserved for a FUTURE combo apex — no Active combo content is authored or
        /// implemented in Task 38 (out of scope). The value exists so adding one needs no schema change.</summary>
        Active
    }
}
