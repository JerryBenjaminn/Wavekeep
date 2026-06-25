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
        [Tooltip("Task 34: damage school. Physical is mitigated by enemy Armor, Magical by Magic Resistance. " +
                 "Set explicitly on every ability — Frost Warden's and Bolt Striker's abilities are Magical.")]
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [Tooltip("Blast radius at the impact point for TargetedAreaOfEffect (Task 20). Unused by the " +
                 "other targeting types. This is the radius the AoE-tag / radius modifiers actually scale " +
                 "for a targeted impact-AoE.")]
        [SerializeField, Min(0f)] private float _aoeRadius = 2.5f;

        [Tooltip("Task 31: max enemies an AoE/TargetedAoE hit can affect. 0 = unlimited. Wider Burst raises " +
                 "this via the BasicMaxTargets modifier. Ignored by SingleTarget.")]
        [SerializeField, Min(0)] private int _maxTargets;

        [Header("Apex / Baseline Status on Hit (Task 31 — the ability itself applies a status, no held upgrade)")]
        [Tooltip("If true, every hit applies the status below directly (used by apex abilities like " +
                 "Remorseless Winter's freeze — independent of held status-upgrades).")]
        [SerializeField] private bool _appliesBaselineStatus;
        [SerializeField] private StatusEffectType _baselineStatusType = StatusEffectType.Freeze;
        [SerializeField, Min(0f)] private float _baselineStatusMagnitude;
        [SerializeField, Min(0f)] private float _baselineStatusDuration;

        [Tooltip("Task 31: if > 0, this ability's damage is this FRACTION of the caster's current BASIC " +
                 "ability damage (e.g. 0.5 = 50%), instead of BaseDamage. Used by Permafrost Eruption.")]
        [SerializeField, Min(0f)] private float _damageScalesWithBasicFraction;

        [Header("Multi-Hit / Chain (Task 35 — single-target repeated strikes + bolt jumps)")]
        [Tooltip("Times this ability strikes its single target per cast (Thunderstorm bakes 2). The Ultimate's " +
                 "Multi-Strike upgrade overrides this at runtime. Min 1.")]
        [SerializeField, Min(1)] private int _hitCount = 1;
        [Tooltip("Each strike deals this fraction [0..1] of the resolved damage (1 = full). Multi-Strike's " +
                 "upgrade overrides this for the Ultimate.")]
        [SerializeField, Min(0f)] private float _hitDamageFraction = 1f;
        [Tooltip("Bolt jumps baked into THIS ability (Thunderstorm). The basic's jumps come from the held " +
                 "Chain Lightning upgrade instead, not here. 0 = no baked chain.")]
        [SerializeField, Min(0)] private int _chainJumps;
        [Tooltip("Baked jump damage as a fraction [0..1] of the primary hit's damage.")]
        [SerializeField, Min(0f)] private float _chainDamageFraction;
        [Tooltip("Max distance (m) from the primary target to find chain-jump targets. Used by both the " +
                 "baked chain and the basic's Chain Lightning upgrade.")]
        [SerializeField, Min(0f)] private float _chainRange = 8f;

        [Header("Apex Finisher Bonuses (Task 35 — Lethal Surge reads run state)")]
        [Tooltip("If true, this ability adds a damage bonus per CURRENT Static Charge stack and consumes them " +
                 "(Lethal Surge).")]
        [SerializeField] private bool _consumesStaticCharge;
        [Tooltip("Bonus fraction [0..1] added per consumed Static Charge stack.")]
        [SerializeField, Min(0f)] private float _staticChargeConsumeBonusPerStack;
        [Tooltip("Additional bonus fraction [0..1] applied if the target is below the held Execute upgrade's " +
                 "current HP% threshold (Lethal Surge). 0 = none.")]
        [SerializeField, Min(0f)] private float _lowHpExecuteBonus;

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

        /// <summary>Task 34: damage school selecting which enemy defence stat mitigates this ability's hits.</summary>
        public DamageType DamageType => _damageType;

        // --- Task 35: multi-hit / chain / apex-finisher config ---
        public int HitCount => _hitCount;
        public float HitDamageFraction => _hitDamageFraction;
        public int ChainJumps => _chainJumps;
        public float ChainDamageFraction => _chainDamageFraction;
        public float ChainRange => _chainRange;
        public bool ConsumesStaticCharge => _consumesStaticCharge;
        public float StaticChargeConsumeBonusPerStack => _staticChargeConsumeBonusPerStack;
        public float LowHpExecuteBonus => _lowHpExecuteBonus;

        /// <summary>Impact blast radius for <see cref="AbilityTargetingType.TargetedAreaOfEffect"/> (Task 20).</summary>
        public float AoeRadius => _aoeRadius;

        /// <summary>Task 31: base cap on enemies an AoE hit affects (0 = unlimited). Raised by Wider Burst.</summary>
        public int MaxTargets => _maxTargets;

        // Task 31 baseline status-on-hit (apex abilities apply a status directly, no held upgrade needed).
        public bool AppliesBaselineStatus => _appliesBaselineStatus;
        public StatusEffectType BaselineStatusType => _baselineStatusType;
        public float BaselineStatusMagnitude => _baselineStatusMagnitude;
        public float BaselineStatusDuration => _baselineStatusDuration;

        /// <summary>Task 31: if &gt; 0, damage = this fraction of the caster's current basic damage.</summary>
        public float DamageScalesWithBasicFraction => _damageScalesWithBasicFraction;
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
