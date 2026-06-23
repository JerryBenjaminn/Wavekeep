using Wavekeep.Data;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// A read-only snapshot of an ability's FINAL computed stats (Task 22), produced by the same
    /// <c>AbilityRuntime</c> code path that execution uses — never a re-derivation. Consumed by the
    /// in-game stat panel so the displayed numbers can't drift from what abilities actually use.
    ///
    /// Fields are mode-dependent: <see cref="Range"/> is the resolved primary radius (acquisition for
    /// SingleTarget, caster blast for AreaOfEffect, impact blast for TargetedAreaOfEffect). Zone fields
    /// are only meaningful when <see cref="AppliesZonePayload"/>; frost fields only when
    /// <see cref="AppliesFrostStack"/>.
    /// </summary>
    public readonly struct AbilityStats
    {
        public readonly AbilityDefinitionSO Definition;
        public readonly AbilityTargetingType TargetingType;

        public readonly float Damage;
        public readonly float Cooldown;
        public readonly float Range;         // resolved primary radius (meaning depends on TargetingType)
        public readonly float CastDistance;  // TargetedAreaOfEffect: max reach to find a target (else == Range)

        public readonly bool IsReady;
        public readonly float CooldownProgress01;

        public readonly bool AppliesZonePayload;
        public readonly float ZoneSlowMagnitude;   // [0..1]
        public readonly float ZoneDuration;        // seconds
        public readonly float ZoneDotPerSecond;

        public readonly bool AppliesFrostStack;
        public readonly int FrostMaxStacks;
        public readonly float FrostFreezeDuration;
        public readonly float FrostPerStackSlow;    // [0..1] per stack

        public AbilityStats(
            AbilityDefinitionSO definition, AbilityTargetingType targetingType,
            float damage, float cooldown, float range, float castDistance,
            bool isReady, float cooldownProgress01,
            bool appliesZonePayload, float zoneSlowMagnitude, float zoneDuration, float zoneDotPerSecond,
            bool appliesFrostStack, int frostMaxStacks, float frostFreezeDuration, float frostPerStackSlow)
        {
            Definition = definition;
            TargetingType = targetingType;
            Damage = damage;
            Cooldown = cooldown;
            Range = range;
            CastDistance = castDistance;
            IsReady = isReady;
            CooldownProgress01 = cooldownProgress01;
            AppliesZonePayload = appliesZonePayload;
            ZoneSlowMagnitude = zoneSlowMagnitude;
            ZoneDuration = zoneDuration;
            ZoneDotPerSecond = zoneDotPerSecond;
            AppliesFrostStack = appliesFrostStack;
            FrostMaxStacks = frostMaxStacks;
            FrostFreezeDuration = frostFreezeDuration;
            FrostPerStackSlow = frostPerStackSlow;
        }
    }
}
