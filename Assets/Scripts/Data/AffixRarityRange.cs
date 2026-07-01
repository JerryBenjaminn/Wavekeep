namespace Wavekeep.Data
{
    /// <summary>
    /// Task 76: one affix's inclusive roll range for a single rarity tier — a designer-tunable serialized sub-field
    /// of <see cref="AffixDefinitionSO"/> (`_rarityRanges[Common..Legendary]`). Adjacent tiers must NOT overlap so a
    /// higher rarity is always strictly better; that invariant is authored + validated by the Task 76 editor setup.
    /// Unique is exempt (its affixes are hand-authored fixed values, not rolled from ranges).
    /// </summary>
    [System.Serializable]
    public struct AffixRarityRange
    {
        public float min;
        public float max;

        public AffixRarityRange(float min, float max) { this.min = min; this.max = max; }
    }
}
