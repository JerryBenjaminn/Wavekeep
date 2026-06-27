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

        [Header("Burn On Hit (Task 48 — Pyromancer; this ability applies a Burn DoT on each hit)")]
        [Tooltip("If true, every hit applies a Burn DoT (the generic EnemyRuntime burn). The Basic Fireball " +
                 "sets this; an apex that ignites (Wildfire Apocalypse) sets it too with baked potency. Held " +
                 "Smoldering Wound / Stacking Embers modify the BASIC's burn at apply time (Basic role only).")]
        [SerializeField] private bool _appliesBurnOnHit;
        [Tooltip("Base Burn damage PER TICK (tick cadence = EnemyRuntime.BurnTickInterval).")]
        [SerializeField, Min(0f)] private float _burnDamagePerTick;
        [Tooltip("Base Burn duration in seconds.")]
        [SerializeField, Min(0f)] private float _burnDuration;

        [Header("Firewall Zone (Task 48 — Pyromancer ultimate; full-width sustained-DoT fire band)")]
        [Tooltip("If true, on cast this ability places a persistent full-width fire band (same geometry as " +
                 "Frost Zone) that deals sustained DoT to everything inside. The band depth is AoeRadius.")]
        [SerializeField] private bool _appliesFireWall;
        [Tooltip("Seconds between Firewall DoT ticks.")]
        [SerializeField, Min(0.05f)] private float _fireWallTickInterval = 0.5f;
        [Tooltip("Firewall DoT damage PER TICK (absolute; Raging Wall multiplies it via FirewallTickDamage).")]
        [SerializeField, Min(0f)] private float _fireWallTickDamage;
        [Tooltip("Firewall active duration in seconds (Lingering Embers adds via the UltimateDuration modifier).")]
        [SerializeField, Min(0f)] private float _fireWallDuration;
        [Tooltip("Task 53: Burn DoT PER TICK applied ONCE when an enemy first enters the Firewall band (a strong " +
                 "Fireball-tier Burn lingering as they cross), on top of the per-tick band DoT. 0 = no entry Burn.")]
        [SerializeField, Min(0f)] private float _fireWallEntryBurnPerTick = 4f;
        [Tooltip("Task 53: duration (s) of the on-entry Burn applied when an enemy first enters the Firewall band.")]
        [SerializeField, Min(0f)] private float _fireWallEntryBurnDuration = 3f;

        [Header("Bonus vs Burning (Task 48 — Cataclysm apex; extra damage to currently-Burning targets)")]
        [Tooltip("Extra fraction [0..1] of damage dealt to a target that is currently Burning. 0 = none.")]
        [SerializeField, Min(0f)] private float _bonusDamageVsBurningFraction;

        [Header("Pierce Corridor (Task 49 — Marksman; half-width of a PiercingLine/channel shot's hit lane)")]
        [Tooltip("Half-width (m) of the corridor a piercing shot sweeps along its line. Enemies within this " +
                 "perpendicular distance of the shot's ray are hit. Used by PiercingLine + the shot-burst channel.")]
        [SerializeField, Min(0f)] private float _shotCorridorHalfWidth = 1f;

        [Header("Shot-Burst Channel (Task 49 — Marksman Minigun / Bullet Storm apex)")]
        [Tooltip("If true, on cast this ability becomes ACTIVE for ChannelDuration, firing piercing shots on " +
                 "ChannelShotInterval (a sustained burst) instead of a single hit. Cooldown runs from cast.")]
        [SerializeField] private bool _appliesShotBurst;
        [Tooltip("Channel active duration in seconds (Minigun base 5s; Sustained Barrage adds via UltimateDuration).")]
        [SerializeField, Min(0f)] private float _channelDuration = 5f;
        [Tooltip("Base seconds between shots while channelling (Faster Spin-Up shortens it for the Ultimate role).")]
        [SerializeField, Min(0.02f)] private float _channelShotInterval = 0.15f;
        [Tooltip("Fan spread (degrees) of each channel trigger. >0 = a fixed arc fan (Bullet Storm); " +
                 "0 = sweep aim at a random enemy each shot (Minigun spray across the width).")]
        [SerializeField, Min(0f)] private float _channelSpreadAngle;

        [Header("Executioner's Volley (Task 49 — Marksman apex; one heavy shot at the most-shredded target)")]
        [Tooltip("If true, this ability fires a single shot at the enemy with the highest Armor-Shredder stack " +
                 "count, scaling damage by that count (see ShredStackDamageBonus).")]
        [SerializeField] private bool _targetsHighestShred;
        [Tooltip("Bonus damage fraction [0..1] added PER current Armor-Shredder stack on the chosen target.")]
        [SerializeField, Min(0f)] private float _shredStackDamageBonus;

        [Header("VFX (Task 46 — visual treatment only, no gameplay effect)")]
        [Tooltip("Selects which VFX presenter styles this ability's hits. Default = generic beam/ring; " +
                 "Lightning = gold electrical bolts/flashes (Bolt Striker). Frost uses its payload flags above.")]
        [SerializeField] private AbilityVfxStyle _vfxStyle = AbilityVfxStyle.Default;

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

        // Burn on hit (Task 48) — base values; held Smoldering Wound/Stacking Embers adjust the basic's burn.
        public bool AppliesBurnOnHit => _appliesBurnOnHit;
        public float BurnDamagePerTick => _burnDamagePerTick;
        public float BurnDuration => _burnDuration;

        // Firewall zone (Task 48) — base values; Raging Wall (tick) + Lingering Embers (duration) adjust them.
        public bool AppliesFireWall => _appliesFireWall;
        public float FireWallTickInterval => _fireWallTickInterval;
        public float FireWallTickDamage => _fireWallTickDamage;
        public float FireWallDuration => _fireWallDuration;
        public float FireWallEntryBurnPerTick => _fireWallEntryBurnPerTick; // Task 53: on-entry Burn application
        public float FireWallEntryBurnDuration => _fireWallEntryBurnDuration;

        /// <summary>Task 48 (Cataclysm): extra damage fraction vs currently-Burning targets (0 = none).</summary>
        public float BonusDamageVsBurningFraction => _bonusDamageVsBurningFraction;

        // Pierce corridor (Task 49) — shared by PiercingLine + the shot-burst channel.
        public float ShotCorridorHalfWidth => _shotCorridorHalfWidth > 0f ? _shotCorridorHalfWidth : 1f;

        // Shot-burst channel (Task 49) — Minigun + Bullet Storm.
        public bool AppliesShotBurst => _appliesShotBurst;
        public float ChannelDuration => _channelDuration;
        public float ChannelShotInterval => _channelShotInterval;
        public float ChannelSpreadAngle => _channelSpreadAngle;

        // Executioner's Volley (Task 49) — single shot at the most-shredded target, scaling with its stacks.
        public bool TargetsHighestShred => _targetsHighestShred;
        public float ShredStackDamageBonus => _shredStackDamageBonus;

        /// <summary>Task 46: visual treatment selector for the VFX layer (no gameplay effect).</summary>
        public AbilityVfxStyle VfxStyle => _vfxStyle;

        /// <summary>Highest level this ability defines (at least 1, even with no explicit entries).</summary>
        public int MaxLevel => Mathf.Max(1, _upgradeLevels.Count);
    }
}
