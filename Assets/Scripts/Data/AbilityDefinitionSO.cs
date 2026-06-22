using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored ability template (CLAUDE.md §3.1 / §3.8). Abilities are shared/global assets,
    /// referenced by heroes rather than duplicated (§3.5). Read-only at runtime — live state
    /// (current level, cooldown timer) belongs in <c>AbilityRuntime</c>.
    ///
    /// A hero's Basic and Ultimate abilities are both just instances of THIS class assigned to two
    /// roles (§3.8) — there is no separate Basic/Ultimate subtype; the distinction is which slot a
    /// hero plugs them into and how the controller drives them (auto-fire vs. triggered).
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Wavekeep/Ability Definition")]
    public sealed class AbilityDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private Sprite _icon;

        [Header("Base Stats")]
        [SerializeField, Min(0f)] private float _baseDamage = 5f;
        [SerializeField, Min(0.01f)] private float _baseCooldown = 1f;
        [Tooltip("Single-target acquisition range, or AoE radius, depending on TargetingType.")]
        [SerializeField, Min(0f)] private float _range = 10f;
        [SerializeField] private AbilityTargetingType _targetingType = AbilityTargetingType.SingleTarget;

        [Header("Upgrade Levels (ordered; level 1 = index 0; values are multipliers on base)")]
        [SerializeField] private List<AbilityUpgradeLevel> _upgradeLevels = new List<AbilityUpgradeLevel>();

        [Header("Tag Interactions (§3.8 — reacts to held upgrades' tags)")]
        [SerializeField] private List<TagInteractionRule> _tagInteractionRules = new List<TagInteractionRule>();

        [Header("Status Effects (Task 11)")]
        [Tooltip("If true, this ability's hits apply the status effects of any held status-upgrades. " +
                 "Flag the deliberate 'payload' ability (typically the ultimate), NOT a rapid auto-basic, " +
                 "so status effects aren't spammed every frame.")]
        [SerializeField] private bool _appliesStatusEffects;

        public string AbilityName => _abilityName;
        public Sprite Icon => _icon;
        public float BaseDamage => _baseDamage;
        public float BaseCooldown => _baseCooldown;
        public float Range => _range;
        public AbilityTargetingType TargetingType => _targetingType;
        public IReadOnlyList<AbilityUpgradeLevel> UpgradeLevels => _upgradeLevels;
        public IReadOnlyList<TagInteractionRule> TagInteractionRules => _tagInteractionRules;

        /// <summary>True if this ability delivers held status-upgrades' effects on hit (Task 11).</summary>
        public bool AppliesStatusEffects => _appliesStatusEffects;

        /// <summary>Highest level this ability defines (at least 1, even with no explicit entries).</summary>
        public int MaxLevel => Mathf.Max(1, _upgradeLevels.Count);
    }
}
