namespace Wavekeep.Data
{
    /// <summary>
    /// The live, hero-affecting stats gear can implicitly boost or roll as an affix (Task 67). Deliberately
    /// limited to stats the game ALREADY uses at runtime: the four ability-output modifiers consumed by
    /// <c>AbilityRuntime.ComputeStats</c>, plus the non-combat Luck total. No hero HP/Armor/defensive stats —
    /// that layer does not exist (enemies attack the wall, not the hero — CLAUDE.md §2).
    ///
    /// The first four map 1:1 onto <see cref="AbilityModifierType"/> when producing a <see cref="StatModifier"/>;
    /// <see cref="Luck"/> is routed to the loadout's separate Luck total instead (it is not a StatModifier).
    /// </summary>
    public enum GearStatType
    {
        DamageMultiplier,
        DamageFlatBonus,
        CooldownMultiplier,
        RangeMultiplier,
        Luck
    }
}
