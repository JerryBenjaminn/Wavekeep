namespace Wavekeep.Data
{
    /// <summary>
    /// Mutually-exclusive specialisation branch an <see cref="UpgradeDefinitionSO"/> can belong to
    /// (Task 19 §4/§5). Hero-exclusive upgrades are tagged <see cref="Mage"/> or <see cref="Defender"/>;
    /// picking from one branch permanently locks the other out of that hero's draw pool for the run.
    /// <see cref="Neutral"/> upgrades (the default, and every generic-pool upgrade) belong to no branch
    /// and are always drawable. The exclusivity logic is driven entirely by this field, never by a
    /// specific hero identity, so any future hero with branch-tagged upgrades works unmodified.
    /// </summary>
    public enum UpgradeBranch
    {
        Neutral,
        Mage,
        Defender
    }
}
