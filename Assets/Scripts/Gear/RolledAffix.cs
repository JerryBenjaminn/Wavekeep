using Wavekeep.Data;

namespace Wavekeep.Gear
{
    /// <summary>
    /// One affix actually rolled onto a <see cref="GearInstance"/> (Task 67): a reference to its read-only
    /// <see cref="AffixDefinitionSO"/> template plus the magnitude rolled within that affix's range. The
    /// magnitude is immutable once rolled; mutating an item's affixes (reroll, a later task) REPLACES a
    /// RolledAffix rather than editing it — so existing affixes are never silently changed in place. The SO is
    /// never mutated (CLAUDE.md §3.5). Persisted as {affixId, value} via the gear save.
    /// </summary>
    public sealed class RolledAffix
    {
        public AffixDefinitionSO Definition { get; }
        public float Value { get; }

        public RolledAffix(AffixDefinitionSO definition, float value)
        {
            Definition = definition;
            Value = value;
        }
    }
}
