using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One entry in the shared, hero-agnostic upgrade pool (CLAUDE.md §3.8). The level-up card picker
    /// (Task 07) draws from these; for Task 04 they are granted via debug keys. Carries one or more
    /// <see cref="UpgradeTag"/>s that hero abilities react to through their <see cref="TagInteractionRule"/>s.
    /// Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Wavekeep/Upgrade Definition")]
    public sealed class UpgradeDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _upgradeName;
        [SerializeField] private Sprite _icon;

        [Tooltip("Tags this upgrade carries; hero abilities react to these via TagInteractionRule.")]
        [SerializeField] private List<UpgradeTag> _tags = new List<UpgradeTag>();

        [Header("Generic Effect (data for later tasks; tag interaction is what drives Task 04)")]
        [SerializeField] private UpgradeEffectType _effectType = UpgradeEffectType.FlatDamageBonus;
        [SerializeField] private float _effectValue;

        [Header("Status Effect on Hit (Task 11 — applied by abilities flagged AppliesStatusEffects)")]
        [Tooltip("If true, holding this upgrade makes status-delivering ability hits apply the effect below.")]
        [SerializeField] private bool _appliesStatusEffect;
        [SerializeField] private StatusEffectType _statusEffectType = StatusEffectType.Freeze;
        [Tooltip("Freeze: unused. Slow: fraction reduced [0..1]. Burn: damage per tick. (See StatusEffectType.)")]
        [SerializeField, Min(0f)] private float _statusMagnitude;
        [Tooltip("Seconds the status lasts on a hit enemy.")]
        [SerializeField, Min(0f)] private float _statusDuration;

        public string UpgradeName => _upgradeName;
        public Sprite Icon => _icon;
        public IReadOnlyList<UpgradeTag> Tags => _tags;
        public UpgradeEffectType EffectType => _effectType;
        public float EffectValue => _effectValue;

        public bool AppliesStatusEffect => _appliesStatusEffect;
        public StatusEffectType StatusEffectType => _statusEffectType;
        public float StatusMagnitude => _statusMagnitude;
        public float StatusDuration => _statusDuration;

        public bool HasTag(UpgradeTag tag) => _tags.Contains(tag);
    }
}
