namespace Wavekeep.Data
{
    /// <summary>
    /// The damage school an ability deals (Task 34). Chooses which enemy defensive stat mitigates the
    /// hit: <see cref="Physical"/> is reduced by the target's Armor, <see cref="Magical"/> by its Magic
    /// Resistance. Every <c>AbilityDefinitionSO</c> tags exactly one type; no silent default — each
    /// existing ability sets this explicitly (CLAUDE.md §2: behaviour reads SO data, never hardcoded).
    ///
    /// Deliberately a closed two-value enum, not an open elemental system — Elemental flavour (Fire,
    /// Frost, Lightning) lives in <c>UpgradeTag</c>; this is purely the mitigation channel.
    /// </summary>
    public enum DamageType
    {
        Physical,
        Magical
    }
}
