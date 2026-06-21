namespace Wavekeep.Data
{
    /// <summary>
    /// Tags carried by <see cref="UpgradeDefinitionSO"/> entries and matched by a hero ability's
    /// <see cref="TagInteractionRule"/> list (CLAUDE.md §3.8). A lightweight enum to start — new
    /// tags can be added freely without touching unrelated code. Not a locked list.
    /// </summary>
    public enum UpgradeTag
    {
        AoE,
        SingleTarget,
        DoT,
        Slow,
        Elemental_Fire,
        Elemental_Dark
    }
}
