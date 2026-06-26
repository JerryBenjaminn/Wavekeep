namespace Wavekeep.Data
{
    /// <summary>
    /// Task 50: which RULE a Passive cross-hero combo apex (<see cref="ComboApexTalentDefinitionSO"/>) layers
    /// onto its two referenced apexes once both are unlocked. A small fixed enum (CLAUDE.md §3.8 status-effect
    /// spirit) handled once in the runtime — never branched on a specific hero/ability identity.
    ///
    /// SCHEMA-ADDITION JUSTIFICATION (the task asked to flag this): Frozen Lightning's original fields
    /// (prime window + a single consume multiplier) only describe ONE behaviour — "primer marks a target, a
    /// specific consuming apex's hit multiplies its own damage". The four new combos are fundamentally
    /// different mechanics that cannot be expressed by that pair of numbers, and the resolver cannot tell a
    /// "×1.75 Burn tick" combo from a "×2.5 consume" combo without a discriminator. This enum is the minimal,
    /// generic way to let the SAME SO type carry all five — proving the model generalises via a principled
    /// extension rather than a new SO type per combo.
    /// </summary>
    public enum ComboEffectType
    {
        /// <summary>Frozen Lightning (Task 38): primer marks a target; the consuming apex's own hit on a primed
        /// target is multiplied by the consume multiplier and the prime is consumed.</summary>
        AmplifyConsume,

        /// <summary>Shatter (Task 50): primer marks a target on freeze; ANY Physical hit on a primed target
        /// detonates an AoE burst (consumeMultiplier × the shot's damage within effectRadius) and consumes the
        /// prime — in addition to the shot's own damage.</summary>
        ShatterDetonate,

        /// <summary>Frostburn (Task 50): a CONTINUOUS passive — every Burn tick on a target that currently has an
        /// active Slow/Freeze is multiplied by the consume multiplier. No prime; re-evaluated per tick.</summary>
        FrostburnTick,

        /// <summary>Chain Combustion (Task 50): a Bolt Striker chain-jump that hits an already-Burning target
        /// extends that Burn by burnExtendSeconds and adds one Stacking-Embers-style stack — no Fireball needed.</summary>
        ChainCombustion,

        /// <summary>Incendiary Rounds (Task 50): every enemy a Marksman pierce shot hits BEYOND the first also
        /// receives a Burn instance (igniteBurn values, scaled by the held Smoldering Wound tier).</summary>
        IncendiaryPierce
    }
}
