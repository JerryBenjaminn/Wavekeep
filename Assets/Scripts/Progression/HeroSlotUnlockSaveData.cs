using System;

namespace Wavekeep.Progression
{
    /// <summary>
    /// Plain serializable DTO for the hero-slot unlock save file (Task 42). Mirrors the Task 12 gear-save
    /// approach: a tiny versioned wrapper for Unity's <c>JsonUtility</c> (only fields, <see cref="saveVersion"/>
    /// FIRST so future migrations can branch on it). Stored in its OWN file alongside the gear save rather than
    /// folded into it, so the two persistence concerns evolve independently.
    ///
    /// Unlock state is modelled as a single integer ceiling (<see cref="maxUnlockedHeroSlots"/>, 1–4) rather
    /// than a per-milestone boolean set: the milestones are strictly ascending and permanent, so the highest
    /// reached slot count is a lossless, self-clamping representation that can only ever rise.
    /// </summary>
    [Serializable]
    public sealed class HeroSlotUnlockSaveData
    {
        public int saveVersion;
        public int maxUnlockedHeroSlots;
    }
}
