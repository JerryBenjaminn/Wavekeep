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
        GainRerollPoints
    }
}
