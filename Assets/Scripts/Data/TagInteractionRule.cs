using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// A hero ability's data-driven reaction to a class of upgrades (CLAUDE.md §3.8): "when the
    /// player holds any upgrade carrying <see cref="MatchTag"/>, modify my output by
    /// <see cref="ModifierValue"/> via <see cref="ModifierType"/>". Authored on the
    /// <see cref="AbilityDefinitionSO"/>; resolved by <c>AbilityRuntime</c> against the
    /// <c>UpgradeInventory</c>. Tags (not per-upgrade IDs) so new upgrades scale automatically.
    /// All magnitudes are SO fields, never hardcoded.
    /// </summary>
    [Serializable]
    public sealed class TagInteractionRule
    {
        [SerializeField] private UpgradeTag _matchTag;
        [SerializeField] private AbilityModifierType _modifierType = AbilityModifierType.DamageMultiplier;
        [SerializeField] private float _modifierValue = 1f;

        public UpgradeTag MatchTag => _matchTag;
        public AbilityModifierType ModifierType => _modifierType;
        public float ModifierValue => _modifierValue;
    }
}
