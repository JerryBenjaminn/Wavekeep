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

        // === Task 35: Bolt Striker line effects (single-target DPS; NO AoE/DoT). Each is optional data
        // a tier authors; UpgradeInventory exposes generic getters and AbilityRuntime reads them by role,
        // never by hero identity — same pattern as the Frost Warden fields above. ===

        [Header("Chain Lightning (Task 35 — Basic; bolt jumps to nearby enemies for reduced damage)")]
        [Tooltip("Number of extra enemies the basic hit jumps to. 0 = no chain.")]
        [SerializeField, Min(0)] private int _chainLightningJumps;
        [Tooltip("Jump damage as a fraction [0..1] of the main hit's damage.")]
        [SerializeField, Min(0f)] private float _chainLightningFraction;

        [Header("Static Charge (Task 35 — Basic; consecutive hits on one target stack a damage bonus)")]
        [Tooltip("Damage bonus fraction added per stack [0..1]. 0 = no static charge.")]
        [SerializeField, Min(0f)] private float _staticChargePerStack;
        [Tooltip("Maximum stacks (the cap the bonus builds to).")]
        [SerializeField, Min(0)] private int _staticChargeMaxStacks;

        [Header("Overcharge (Task 35 — Basic; crit chance + an independent bonus-spike chance)")]
        [Tooltip("Flat crit CHANCE bonus [0..1] fed into the existing Task 23 crit pipeline.")]
        [SerializeField, Range(0f, 1f)] private float _critChanceBonus;
        [Tooltip("Chance [0..1] for a one-time bonus damage spike on a basic hit (rolled separately from crit).")]
        [SerializeField, Range(0f, 1f)] private float _overchargeSpikeChance;
        [Tooltip("Spike bonus damage as a fraction of the hit [0..1] (e.g. 0.5 = +50%).")]
        [SerializeField, Min(0f)] private float _overchargeSpikeBonus;

        [Header("Piercing Bolt (Task 35 — Basic; consumes Task 34's temporary Armor reduction)")]
        [Tooltip("Effective Armor reduction applied to the hit target for a duration. 0 = no reduction.")]
        [SerializeField, Min(0f)] private float _armorReductionAmount;
        [SerializeField, Min(0f)] private float _armorReductionDuration;

        [Header("Multi-Strike (Task 35 — Ultimate; the cast hits the same target multiple times)")]
        [Tooltip("Hits per ultimate cast. 0 = not a multi-strike upgrade (ability fires once).")]
        [SerializeField, Min(0)] private int _multiStrikeHits;
        [Tooltip("Each multi-strike hit deals this fraction [0..1] of the ultimate's resolved damage.")]
        [SerializeField, Min(0f)] private float _multiStrikeFraction;

        [Header("Execute (Task 35 — Ultimate; bonus damage vs low-HP targets)")]
        [Tooltip("Target HP fraction [0..1] below which the bonus applies. 0 = no execute.")]
        [SerializeField, Range(0f, 1f)] private float _executeThreshold;
        [Tooltip("Bonus damage fraction [0..1] vs targets under the threshold.")]
        [SerializeField, Min(0f)] private float _executeBonus;

        [Header("Overload (Task 35 — Ultimate; generic incoming-damage vulnerability, NOT Armor reduction)")]
        [Tooltip("Extra fraction of damage the target takes from ALL sources for the duration [0..1] " +
                 "(e.g. 0.10 = +10% taken). Distinct from Piercing Bolt's Armor reduction — this is a " +
                 "post-mitigation damage-taken multiplier on EnemyRuntime. 0 = no vulnerability.")]
        [SerializeField, Min(0f)] private float _vulnerabilityBonus;
        [SerializeField, Min(0f)] private float _vulnerabilityDuration;

        // === Task 48: Pyromancer line effects (DoT/AoE fire; held data read by AbilityRuntime/FireSubsystem
        // via UpgradeInventory getters, switching on data never on hero identity — same pattern as above). ===

        [Header("Smoldering Wound (Task 48 — Basic; increases Fireball's Burn damage and duration)")]
        [Tooltip("Multiplier on the Basic's base Burn per-tick damage (1 = no change).")]
        [SerializeField, Min(0f)] private float _burnDamageMultiplier = 1f;
        [Tooltip("Seconds added to the Basic's base Burn duration.")]
        [SerializeField, Min(0f)] private float _burnDurationBonus;

        [Header("Stacking Embers (Task 48 — Basic; repeated Fireball hits stack Burn damage on one target)")]
        [Tooltip("Extra Burn-damage fraction per stack [0..1]. 0 = no stacking (burn just refreshes).")]
        [SerializeField, Min(0f)] private float _burnStackPerStackBonus;
        [Tooltip("Maximum extra stacks the Burn builds to on a single target.")]
        [SerializeField, Min(0)] private int _burnMaxStacks;

        [Header("Spreading Flame (Task 48 — Basic; a Burning target's death spreads Burn to nearby enemies)")]
        [Tooltip("Number of new enemies the Burn spreads to on a Burning-target death. 0 = no spread.")]
        [SerializeField, Min(0)] private int _burnSpreadTargets;
        [Tooltip("Search radius (m) for spread targets around the dying enemy.")]
        [SerializeField, Min(0f)] private float _burnSpreadRange;
        [Tooltip("Fraction [0..1] of the original Burn's potency applied to each spread target (1 = 100%).")]
        [SerializeField, Min(0f)] private float _burnSpreadPotency = 1f;

        [Header("Combustion (Task 48 — Basic; a Burn that expires naturally may detonate in an AoE)")]
        [Tooltip("Chance [0..1] for a naturally-expiring Burn to detonate. 0 = no combustion.")]
        [SerializeField, Range(0f, 1f)] private float _combustionChance;
        [Tooltip("Detonation blast radius (m).")]
        [SerializeField, Min(0f)] private float _combustionRadius;
        [Tooltip("Detonation damage as a fraction [0..1] of the caster's current Basic damage.")]
        [SerializeField, Min(0f)] private float _combustionBasicFraction;

        [Header("Wildfire Spread (Task 48 — Ultimate; enemies dying in Firewall leave a lingering patch)")]
        [Tooltip("Seconds the smoldering patch persists AFTER Firewall ends. 0 = no patch.")]
        [SerializeField, Min(0f)] private float _wildfirePatchDuration;
        [Tooltip("Patch DoT as a fraction [0..1] of Firewall's per-tick damage.")]
        [SerializeField, Min(0f)] private float _wildfirePatchTickFraction;

        [Header("Inferno Surge (Task 48 — Ultimate; Firewall periodically bursts extra instant AoE)")]
        [Tooltip("Seconds between Firewall bursts. 0 = no burst.")]
        [SerializeField, Min(0f)] private float _infernoSurgeInterval;
        [Tooltip("Burst damage as a fraction [0..1] of the caster's current Basic damage.")]
        [SerializeField, Min(0f)] private float _infernoSurgeBasicFraction;

        // === Task 49: Marksman line effects (Physical pierce DPS; held data read by AbilityRuntime via
        // UpgradeInventory getters, switching on data never on hero identity). ===

        [Header("Piercing Rounds (Task 49 — Basic; shots pierce enemies in a line)")]
        [Tooltip("If true, the Basic's shots pierce. Limit = max enemies hit in line (0 = unlimited).")]
        [SerializeField] private bool _piercingRounds;
        [Tooltip("Max enemies a piercing shot hits (0 = unlimited). Ignored unless PiercingRounds is true.")]
        [SerializeField, Min(0)] private int _piercingRoundsLimit;

        [Header("Multishot (Task 49 — Basic; multiple shots per trigger in a narrow spread)")]
        [Tooltip("Shots fired per Basic trigger. 0 = not a multishot upgrade (one shot).")]
        [SerializeField, Min(0)] private int _multishotCount;
        [Tooltip("Total fan spread in degrees across the multishot shots.")]
        [SerializeField, Min(0f)] private float _multishotSpreadAngle;

        [Header("Armor Shredder (Task 49 — Basic; STACKING armor reduction per hit, distinct from Piercing Bolt)")]
        [Tooltip("Effective Armor removed PER stack. 0 = no shredder.")]
        [SerializeField, Min(0f)] private float _armorShredPerStack;
        [Tooltip("Maximum Armor-Shredder stacks on a single target.")]
        [SerializeField, Min(0)] private int _armorShredMaxStacks;
        [Tooltip("Seconds the shred lasts; refreshed on each new hit (drops all stacks if not refreshed in time).")]
        [SerializeField, Min(0f)] private float _armorShredRefresh;

        [Header("Faster Spin-Up (Task 49 — Ultimate; Minigun internal fire-rate bonus)")]
        [Tooltip("Fire-rate bonus fraction [0..1] during Minigun (shot interval ÷ (1+bonus)). 0 = none.")]
        [SerializeField, Min(0f)] private float _minigunFireRateBonus;

        [Header("Full Pierce (Task 49 — Ultimate; Minigun bonus damage to pierced targets beyond the first)")]
        [Tooltip("Bonus damage fraction [0..1] to each pierced target AFTER the first. Stacks with the base " +
                 "pierce (which deals 100% to each). 0 = none.")]
        [SerializeField, Min(0f)] private float _fullPierceBonus;

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

        // --- Task 35: Bolt Striker line effects ---

        // Chain Lightning (Basic).
        public int ChainLightningJumps => _chainLightningJumps;
        public float ChainLightningFraction => _chainLightningFraction;

        // Static Charge (Basic).
        public float StaticChargePerStack => _staticChargePerStack;
        public int StaticChargeMaxStacks => _staticChargeMaxStacks;

        // Overcharge (Basic).
        public float CritChanceBonus => _critChanceBonus;
        public float OverchargeSpikeChance => _overchargeSpikeChance;
        public float OverchargeSpikeBonus => _overchargeSpikeBonus;

        // Piercing Bolt (Basic) — feeds Task 34's EnemyRuntime.ApplyArmorReduction.
        public float ArmorReductionAmount => _armorReductionAmount;
        public float ArmorReductionDuration => _armorReductionDuration;

        // Multi-Strike (Ultimate).
        public int MultiStrikeHits => _multiStrikeHits;
        public float MultiStrikeFraction => _multiStrikeFraction;

        // Execute (Ultimate).
        public float ExecuteThreshold => _executeThreshold;
        public float ExecuteBonus => _executeBonus;

        // Overload (Ultimate) — generic incoming-damage vulnerability, distinct from Armor reduction.
        public float VulnerabilityBonus => _vulnerabilityBonus;
        public float VulnerabilityDuration => _vulnerabilityDuration;

        // --- Task 48: Pyromancer line effects ---

        // Smoldering Wound (Basic).
        public float BurnDamageMultiplier => _burnDamageMultiplier;
        public float BurnDurationBonus => _burnDurationBonus;

        // Stacking Embers (Basic).
        public float BurnStackPerStackBonus => _burnStackPerStackBonus;
        public int BurnMaxStacks => _burnMaxStacks;

        // Spreading Flame (Basic).
        public int BurnSpreadTargets => _burnSpreadTargets;
        public float BurnSpreadRange => _burnSpreadRange;
        public float BurnSpreadPotency => _burnSpreadPotency;

        // Combustion (Basic).
        public float CombustionChance => _combustionChance;
        public float CombustionRadius => _combustionRadius;
        public float CombustionBasicFraction => _combustionBasicFraction;

        // Wildfire Spread (Ultimate).
        public float WildfirePatchDuration => _wildfirePatchDuration;
        public float WildfirePatchTickFraction => _wildfirePatchTickFraction;

        // Inferno Surge (Ultimate).
        public float InfernoSurgeInterval => _infernoSurgeInterval;
        public float InfernoSurgeBasicFraction => _infernoSurgeBasicFraction;

        // --- Task 49: Marksman line effects ---

        // Piercing Rounds (Basic).
        public bool PiercingRounds => _piercingRounds;
        public int PiercingRoundsLimit => _piercingRoundsLimit;

        // Multishot (Basic).
        public int MultishotCount => _multishotCount;
        public float MultishotSpreadAngle => _multishotSpreadAngle;

        // Armor Shredder (Basic) — stacking, distinct from Bolt Striker's flat Piercing Bolt.
        public float ArmorShredPerStack => _armorShredPerStack;
        public int ArmorShredMaxStacks => _armorShredMaxStacks;
        public float ArmorShredRefresh => _armorShredRefresh;

        // Faster Spin-Up (Ultimate).
        public float MinigunFireRateBonus => _minigunFireRateBonus;

        // Full Pierce (Ultimate).
        public float FullPierceBonus => _fullPierceBonus;

        public bool HasTag(UpgradeTag tag) => _tags.Contains(tag);
    }
}
