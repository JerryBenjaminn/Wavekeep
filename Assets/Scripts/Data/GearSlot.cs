namespace Wavekeep.Data
{
    /// <summary>
    /// The six equip slots a hero has (CLAUDE.md §6 locked sub-decision, Task 12): five gear slots plus
    /// one artifact slot. The enum order is used as the loadout array index, so values must stay stable
    /// (appending new slots is safe; reordering would break saved loadouts).
    /// </summary>
    public enum GearSlot
    {
        Helmet,
        Body,
        Hands,
        Legs,
        Feet,
        Artifact
    }
}
