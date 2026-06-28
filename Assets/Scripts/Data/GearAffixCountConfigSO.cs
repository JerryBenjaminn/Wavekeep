using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Read-only config (Task 67): how many random affixes a rolled item of each rarity gets, plus the shared
    /// affix pool generation draws from (a later task). Common→Legendary scale up; Unique is hand-authored with
    /// NO random rolls (its count is 0 here — Unique affixes are authored directly, not generated). Read-only
    /// at runtime (CLAUDE.md §3.5).
    /// </summary>
    [CreateAssetMenu(fileName = "GearAffixCountConfig", menuName = "Wavekeep/Gear/Affix Count Config")]
    public sealed class GearAffixCountConfigSO : ScriptableObject
    {
        [Tooltip("Affix count per rarity, indexed by Rarity (Common..Unique). Unique = 0 (hand-authored, no rolls).")]
        [SerializeField] private int[] _affixCountByRarity = { 0, 1, 2, 3, 4, 0 };

        [Tooltip("Shared pool of affixes generation draws from (used by a later task; referenced here now).")]
        [SerializeField] private List<AffixDefinitionSO> _affixPool = new List<AffixDefinitionSO>();

        public IReadOnlyList<AffixDefinitionSO> AffixPool => _affixPool;

        /// <summary>Number of random affixes a rolled item of <paramref name="rarity"/> receives.</summary>
        public int AffixCountFor(Rarity rarity)
        {
            if (_affixCountByRarity == null || _affixCountByRarity.Length == 0) return 0;
            int i = Mathf.Clamp((int)rarity, 0, _affixCountByRarity.Length - 1);
            return Mathf.Max(0, _affixCountByRarity[i]);
        }
    }
}
