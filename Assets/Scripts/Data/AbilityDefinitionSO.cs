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
        [Tooltip("SingleTarget: acquisition range. AreaOfEffect: caster-centred blast radius. " +
                 "TargetedAreaOfEffect: max cast distance to find a target (the blast size is AoeRadius).")]
        [SerializeField, Min(0f)] private float _range = 10f;
        [SerializeField] private AbilityTargetingType _targetingType = AbilityTargetingType.SingleTarget;
        [Tooltip("Blast radius at the impact point for TargetedAreaOfEffect (Task 20). Unused by the " +
                 "other targeting types. This is the radius the AoE-tag / radius modifiers actually scale " +
                 "for a targeted impact-AoE.")]
        [SerializeField, Min(0f)] private float _aoeRadius = 2.5f;

        [Header("Upgrade Levels (ordered; level 1 = index 0; values are multipliers on base)")]
        [SerializeField] private List<AbilityUpgradeLevel> _upgradeLevels = new List<AbilityUpgradeLevel>();

        [Header("Tag Interactions (§3.8 — reacts to held upgrades' tags)")]
        [SerializeField] private List<TagInteractionRule> _tagInteractionRules = new List<TagInteractionRule>();

        [Header("Status Effects (Task 11)")]
        [Tooltip("If true, this ability's hits apply the status effects of any held status-upgrades. " +
                 "Flag the deliberate 'payload' ability (typically the ultimate), NOT a rapid auto-basic, " +
                 "so status effects aren't spammed every frame.")]
        [SerializeField] private bool _appliesStatusEffects;

        [Header("Frost / Stacking Effect (Task 19 — optional; set on a frost-applier basic)")]
        [Tooltip("If true, each hit applies/refreshes a Frost stack (the generic stacking effect on EnemyRuntime).")]
        [SerializeField] private bool _appliesFrostStack;
        [SerializeField, Min(0)] private int _frostStacksPerHit = 1;
        [Tooltip("Movement-speed reduction per stack [0..1].")]
        [SerializeField, Min(0f)] private float _frostPerStackSlow = 0.08f;
        [Tooltip("Stacks needed to trigger the Freeze payload (then stacks reset to 0).")]
        [SerializeField, Min(1)] private int _frostMaxStacks = 5;
        [Tooltip("Seconds between automatic -1 stack decay when not refreshed by a new hit.")]
        [SerializeField, Min(0.01f)] private float _frostDecayInterval = 4f;
        [Tooltip("Freeze duration applied when stacks reach max.")]
        [SerializeField, Min(0f)] private float _frostTriggerFreezeDuration = 1.5f;

        [Header("Zone Payload (Task 19 — optional; set on a damage-over-time + slow zone ultimate)")]
        [Tooltip("If true, on cast this ability applies a baseline Slow + DoT (Burn) to every enemy in " +
                 "range for the zone duration — independent of any held status-upgrades.")]
        [SerializeField] private bool _appliesZonePayload;
        [Tooltip("Baseline Slow fraction [0..1] the zone applies for its duration.")]
        [SerializeField, Min(0f)] private float _zoneSlowMagnitude;
        [Tooltip("Zone DoT in damage-per-second; delivered as a Burn over the duration.")]
        [SerializeField, Min(0f)] private float _zoneDotDamagePerSecond;
        [Tooltip("Seconds the zone's Slow + DoT last on affected enemies.")]
        [SerializeField, Min(0f)] private float _zoneDuration;

        public string AbilityName => _abilityName;
        public Sprite Icon => _icon;
        public float BaseDamage => _baseDamage;
        public float BaseCooldown => _baseCooldown;
        public float Range => _range;
        public AbilityTargetingType TargetingType => _targetingType;

        /// <summary>Impact blast radius for <see cref="AbilityTargetingType.TargetedAreaOfEffect"/> (Task 20).</summary>
        public float AoeRadius => _aoeRadius;
        public IReadOnlyList<AbilityUpgradeLevel> UpgradeLevels => _upgradeLevels;
        public IReadOnlyList<TagInteractionRule> TagInteractionRules => _tagInteractionRules;

        /// <summary>True if this ability delivers held status-upgrades' effects on hit (Task 11).</summary>
        public bool AppliesStatusEffects => _appliesStatusEffects;

        // Frost / stacking config (Task 19) — base values; held upgrades adjust them at apply time.
        public bool AppliesFrostStack => _appliesFrostStack;
        public int FrostStacksPerHit => _frostStacksPerHit;
        public float FrostPerStackSlow => _frostPerStackSlow;
        public int FrostMaxStacks => _frostMaxStacks;
        public float FrostDecayInterval => _frostDecayInterval;
        public float FrostTriggerFreezeDuration => _frostTriggerFreezeDuration;

        // Zone payload config (Task 19) — base values; held upgrades adjust them at cast time.
        public bool AppliesZonePayload => _appliesZonePayload;
        public float ZoneSlowMagnitude => _zoneSlowMagnitude;
        public float ZoneDotDamagePerSecond => _zoneDotDamagePerSecond;
        public float ZoneDuration => _zoneDuration;

        /// <summary>Highest level this ability defines (at least 1, even with no explicit entries).</summary>
        public int MaxLevel => Mathf.Max(1, _upgradeLevels.Count);
    }
}
