using System;
using System.Collections.Generic;

namespace Wavekeep.Progression
{
    /// <summary>
    /// Plain serializable DTO for the talent-discovery save file (Task 43). Same shape/discipline as the
    /// Task 12 gear save and Task 42 hero-slot save: a tiny versioned wrapper for Unity's <c>JsonUtility</c>
    /// with <see cref="saveVersion"/> FIRST so future migrations can branch on it. Its own file alongside the
    /// others, not folded in.
    ///
    /// Discovered talents are stored as a flat list of stable string ids (the talent SO asset names), covering
    /// BOTH <c>ApexTalentDefinitionSO</c> and <c>ComboApexTalentDefinitionSO</c> in one set — they never
    /// collide because Codex lookups always pair an id with the SO it came from.
    /// </summary>
    [Serializable]
    public sealed class TalentDiscoverySaveData
    {
        public int saveVersion;
        public List<string> discoveredTalentIds = new List<string>();
    }
}
