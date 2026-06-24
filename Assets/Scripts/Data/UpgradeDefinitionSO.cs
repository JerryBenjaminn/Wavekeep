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

        [Header("Branch (Task 19 — Mage/Defender are mutually exclusive once picked; Neutral is always drawable)")]
        [SerializeField] private UpgradeBranch _branch = UpgradeBranch.Neutral;

        [Header("Stat Modifiers (Task 19 — parametric effects applied while held)")]
        [SerializeField] private List<UpgradeStatModifier> _statModifiers = new List<UpgradeStatModifier>();

        [Header("Chain Frost (Task 19 — Defender; spread stacks when a Frost max-stack triggers)")]
        [SerializeField] private bool _frostChainSpread;
        [SerializeField, Min(0)] private int _frostChainStacks = 2;
        [SerializeField, Min(0f)] private float _frostChainRadius = 2f;

        [Header("Ultimate Freeze (Task 19 — Mage; zone freezes heavily-frosted enemies)")]
        [SerializeField] private bool _ultimateFreezeOnStacks;
        [SerializeField, Min(1)] private int _ultimateFreezeStackThreshold = 3;
        [SerializeField, Min(0f)] private float _ultimateFreezeDuration = 2f;

        [Header("Generic Effect (data for later tasks; tag interaction is what drives Task 04)")]
        [SerializeField] private UpgradeEffectType _effectType = UpgradeEffectType.FlatDamageBonus;
        [SerializeField] private float _effectValue;

        [Header("Shattering Impact (Task 31 — bonus damage vs slowed/frozen targets, on the initial hit)")]
        [Tooltip("Extra fraction of damage dealt to a target already affected by Slow/Freeze (or Frost stacks). " +
                 "0 = none. Applied to the initial hit only — NOT a separate tick/DoT.")]
        [SerializeField, Min(0f)] private float _bonusDamageVsImpaired;

        [Header("Hard Freeze (Task 31 — chance to fully stun on basic hit)")]
        [Tooltip("Chance [0..1] that a hit fully freezes (hard stun) the target instead of only slowing it.")]
        [SerializeField, Range(0f, 1f)] private float _hardFreezeChance;
        [Tooltip("Stun (Freeze) duration in seconds when Hard Freeze procs.")]
        [SerializeField, Min(0f)] private float _hardFreezeDuration;

        [Header("Frozen Ground (Task 31 Pass 2 — ice patch spawned at a basic hit's impact)")]
        [Tooltip("Patch radius (m). 0 = this upgrade doesn't spawn a patch.")]
        [SerializeField, Min(0f)] private float _frozenGroundRadius;
        [SerializeField, Min(0f)] private float _frozenGroundDuration;
        [Tooltip("Slow fraction [0..1] applied to enemies standing in the patch.")]
        [SerializeField, Range(0f, 1f)] private float _frozenGroundSlow;

        [Header("Zone Pulse (Task 31 Pass 2 — Frost Zone area-tied damage pulse)")]
        [Tooltip("Seconds between pulses. 0 = no pulse.")]
        [SerializeField, Min(0f)] private float _zonePulseInterval;
        [Tooltip("Per-pulse damage as a fraction of the caster's current basic damage.")]
        [SerializeField, Min(0f)] private float _zonePulseBasicFraction;

        [Header("Absolute Zero (Task 33 — Frost Zone duration extends on a death inside it)")]
        [Tooltip("Seconds added to Frost Zone's remaining duration per enemy death inside it. 0 = no extension.")]
        [SerializeField, Min(0f)] private float _zoneDurationExtendPerDeath;
        [Tooltip("Hard-cap headroom: remaining duration can't exceed (zone's cast duration + this). Prevents " +
                 "runaway uptime. Placeholder 3s.")]
        [SerializeField, Min(0f)] private float _zoneDurationExtendCapBonus = 3f;

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

        /// <summary>Specialisation branch (Task 19). Drives mutual-exclusivity in the card picker.</summary>
        public UpgradeBranch Branch => _branch;

        /// <summary>Parametric ability/frost modifiers applied while this upgrade is held (Task 19).</summary>
        public IReadOnlyList<UpgradeStatModifier> StatModifiers => _statModifiers;

        // Chain Frost behaviour (Task 19 — Defender).
        public bool FrostChainSpread => _frostChainSpread;
        public int FrostChainStacks => _frostChainStacks;
        public float FrostChainRadius => _frostChainRadius;

        // Ultimate Freeze behaviour (Task 19 — Mage).
        public bool UltimateFreezeOnStacks => _ultimateFreezeOnStacks;
        public int UltimateFreezeStackThreshold => _ultimateFreezeStackThreshold;
        public float UltimateFreezeDuration => _ultimateFreezeDuration;

        public bool AppliesStatusEffect => _appliesStatusEffect;
        public StatusEffectType StatusEffectType => _statusEffectType;
        public float StatusMagnitude => _statusMagnitude;
        public float StatusDuration => _statusDuration;

        // Shattering Impact (Task 31): bonus fraction vs already-impaired targets, applied to the hit itself.
        public float BonusDamageVsImpaired => _bonusDamageVsImpaired;

        // Hard Freeze (Task 31): chance-based hard stun on hit.
        public float HardFreezeChance => _hardFreezeChance;
        public float HardFreezeDuration => _hardFreezeDuration;

        // Frozen Ground (Task 31 Pass 2): basic-hit ice patch.
        public float FrozenGroundRadius => _frozenGroundRadius;
        public float FrozenGroundDuration => _frozenGroundDuration;
        public float FrozenGroundSlow => _frozenGroundSlow;

        // Zone Pulse (Task 31 Pass 2): Frost Zone area pulse.
        public float ZonePulseInterval => _zonePulseInterval;
        public float ZonePulseBasicFraction => _zonePulseBasicFraction;

        // Absolute Zero (Task 33): Frost Zone duration extension on death inside.
        public float ZoneDurationExtendPerDeath => _zoneDurationExtendPerDeath;
        public float ZoneDurationExtendCapBonus => _zoneDurationExtendCapBonus;

        public bool HasTag(UpgradeTag tag) => _tags.Contains(tag);
    }
}
