namespace Wavekeep.Data
{
    /// <summary>
    /// The kind of run-bonus a <see cref="ConsumableDefinitionSO"/> grants when purchased (CLAUDE.md
    /// §3.1 shop items). Deliberately a small, generic, data-driven set that maps onto systems that
    /// ALREADY exist (Task 06 §2): the two ability modifiers feed the existing
    /// <c>AbilityRuntime</c> modifier pipeline (same one tag interactions use), and the wall effect
    /// calls the existing <c>WallRuntime</c> — no parallel damage/health path. Adding more effect
    /// types later is purely a new enum case + routing in <c>ShopController</c>.
    /// </summary>
    public enum ConsumableEffectType
    {
        /// <summary>Flat additive bonus to every hero ability's damage (AbilityRuntime pipeline).</summary>
        FlatDamageBoost,

        /// <summary>Multiplies every hero ability's cooldown (value &lt; 1 = faster). AbilityRuntime pipeline.</summary>
        CooldownReduction,

        /// <summary>Instantly restores wall HP via <c>WallRuntime.Heal</c> (clamped to max). Instant, not ongoing.</summary>
        HealWall,

        /// <summary>Grants reroll points (Task 09) by amount = effect value, routed to <c>RerollManager.Add</c>.
        /// Applied through the normal <c>ShopController.TryPurchase</c> path like any other consumable.</summary>
        GainRerollPoints,

        // --- Task 23 new ongoing ability modifiers (all read by AbilityRuntime's existing pipeline) ---

        /// <summary>Adds flat crit CHANCE (value is a fraction [0..1]). Aggregated by ConsumableInventory and
        /// rolled as the final multiplicative step in AbilityRuntime's damage pipeline.</summary>
        CritChanceBoost,

        /// <summary>Adds flat crit DAMAGE bonus (value is a fraction; a crit deals ×(1+sum)). Aggregated and
        /// applied only when a crit chance roll succeeds.</summary>
        CritDamageBoost,

        /// <summary>Frost Potion: adds to the per-stack slow of a frost-applying ability (value [0..1]). A
        /// no-op for heroes whose abilities don't apply Frost stacks (Task 19/23).</summary>
        FrostPotency,

        /// <summary>Lightning Potion (PLACEHOLDER, Task 23): a generic flat damage bonus to ALL abilities for
        /// now, kept as its own effect type so a future Lightning hero/kit can repurpose it without re-authoring
        /// the shop assets.</summary>
        ElementalLightning,

        /// <summary>Adds seconds to the hero's ultimate zone duration (value in seconds), through the SAME
        /// duration pipeline as Task 19's Extended Zone. A no-op for ultimates without a zone duration.</summary>
        UltimateDurationBoost,

        /// <summary>Flat damage bonus to the BASIC ability only (value), via the role-aware damage pipeline.</summary>
        BasicDamageBoost,

        // --- Task 24 ---

        /// <summary>Luck Potion: adds a flat, runtime-only Luck bonus (value) for the rest of the run, routed
        /// to <c>LuckState.AddPotionBonus</c> through the normal <c>ShopController.TryPurchase</c> path. Luck is
        /// strictly non-combat — it reweights shop offer tiers (and, more weakly, loot drop tiers), never damage.
        /// Resets to zero at run end like other per-run state (CLAUDE.md §6).</summary>
        LuckBoost
    }
}
