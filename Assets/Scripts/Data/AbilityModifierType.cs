namespace Wavekeep.Data
{
    /// <summary>
    /// How a <see cref="TagInteractionRule"/> modifies a hero ability's computed output when the
    /// player holds an upgrade carrying the matching tag (CLAUDE.md §3.8). This is a GENERIC,
    /// data-driven set of modifier kinds — ability code switches on this enum, never on a specific
    /// ability identity, so tag interactions stay data-driven (a reviewer-blocking requirement).
    /// </summary>
    public enum AbilityModifierType
    {
        DamageMultiplier,
        DamageFlatBonus,
        CooldownMultiplier,
        RangeMultiplier
    }
}
