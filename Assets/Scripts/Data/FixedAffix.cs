using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// A hand-authored, fixed-value affix entry on a <see cref="GearBaseSO"/> (Task 68). Unique-rarity gear does
    /// NOT roll random affixes (CLAUDE.md §6 / Task 68 locked decision); instead a generated Unique pulls this
    /// designer-authored set verbatim — a reference to the <see cref="AffixDefinitionSO"/> template plus the exact
    /// magnitude to apply (no <c>[min,max]</c> roll). Plain serializable data, authored inline on the base; the SO
    /// is never mutated at runtime (CLAUDE.md §3.5).
    /// </summary>
    [Serializable]
    public sealed class FixedAffix
    {
        [SerializeField] private AffixDefinitionSO _affix;
        [Tooltip("Exact magnitude applied for this affix on a Unique (no random roll).")]
        [SerializeField] private float _value;

        public AffixDefinitionSO Affix => _affix;
        public float Value => _value;
    }
}
