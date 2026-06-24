using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Economy;
using Wavekeep.Runtime;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// Everything an ability needs at execution time (CLAUDE.md §3.2). Deliberately minimal for
    /// Task 04 — extend as later abilities need more (target point, caster stats, etc.).
    ///
    /// <see cref="Enemies"/> is the spawner's live active-enemy list (read-only); abilities resolve
    /// targets from it. <see cref="Upgrades"/> is the per-run inventory used to resolve tag
    /// interactions. <see cref="Consumables"/> (Task 06) is the parallel inventory of purchased shop
    /// effects, read as just another modifier source. <see cref="EquippedModifiers"/> (Task 12) is the
    /// active hero loadout's aggregated gear/artifact stat modifiers, layered into the same pipeline.
    /// <see cref="Feedback"/> (Task 08) is an optional visual sink the runtime notifies at the
    /// resolution point. The context is rebuilt cheaply each frame by the hero controller.
    /// </summary>
    public readonly struct AbilityExecutionContext
    {
        public readonly Vector3 CasterPosition;
        public readonly IReadOnlyList<EnemyRuntime> Enemies;
        public readonly UpgradeInventory Upgrades;
        public readonly ConsumableInventory Consumables;
        public readonly IReadOnlyList<StatModifier> EquippedModifiers;
        public readonly IAbilityFeedback Feedback;

        /// <summary>Task 31: the caster's CURRENT effective basic-ability damage, so abilities that scale
        /// off it (e.g. Permafrost Eruption = 50% of basic) read one consistent value. 0 when unknown.</summary>
        public readonly float BasicDamage;

        /// <summary>Task 31 (Pass 2): sink for persistent ground/zone effects (Frost Zone, Frozen Ground).
        /// Abilities spawn zones into it; HeroRuntime owns + ticks it. Null when no zone system is wired.</summary>
        public readonly GroundZoneManager Zones;

        /// <summary>Task 33: the defended line's Z (wall position on the approach axis) and the sign pointing
        /// from it toward the spawn side, so a full-width zone can be placed in front of the wall.</summary>
        public readonly float DefendedLineZ;
        public readonly float ApproachDirectionZ;

        public AbilityExecutionContext(
            Vector3 casterPosition,
            IReadOnlyList<EnemyRuntime> enemies,
            UpgradeInventory upgrades,
            ConsumableInventory consumables,
            IReadOnlyList<StatModifier> equippedModifiers = null,
            IAbilityFeedback feedback = null,
            float basicDamage = 0f,
            GroundZoneManager zones = null,
            float defendedLineZ = 0f,
            float approachDirectionZ = 1f)
        {
            CasterPosition = casterPosition;
            Enemies = enemies;
            Upgrades = upgrades;
            Consumables = consumables;
            EquippedModifiers = equippedModifiers;
            Feedback = feedback;
            BasicDamage = basicDamage;
            Zones = zones;
            DefendedLineZ = defendedLineZ;
            ApproachDirectionZ = approachDirectionZ;
        }
    }
}
