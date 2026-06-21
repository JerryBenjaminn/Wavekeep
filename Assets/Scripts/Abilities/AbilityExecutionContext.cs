using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Runtime;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// Everything an ability needs at execution time (CLAUDE.md §3.2). Deliberately minimal for
    /// Task 04 — extend as later abilities need more (target point, caster stats, etc.).
    ///
    /// <see cref="Enemies"/> is the spawner's live active-enemy list (read-only); abilities resolve
    /// targets from it. <see cref="Upgrades"/> is the per-run inventory used to resolve tag
    /// interactions. The context is rebuilt cheaply each frame by the hero controller.
    /// </summary>
    public readonly struct AbilityExecutionContext
    {
        public readonly Vector3 CasterPosition;
        public readonly IReadOnlyList<EnemyRuntime> Enemies;
        public readonly UpgradeInventory Upgrades;

        public AbilityExecutionContext(
            Vector3 casterPosition,
            IReadOnlyList<EnemyRuntime> enemies,
            UpgradeInventory upgrades)
        {
            CasterPosition = casterPosition;
            Enemies = enemies;
            Upgrades = upgrades;
        }
    }
}
