using System.Collections.Generic;
using Wavekeep.Data;
using Wavekeep.Economy;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// A runtime ability instance (CLAUDE.md §3.2). Wraps an <see cref="AbilityDefinitionSO"/> with
    /// mutable per-run state (level, cooldown). Implemented by <c>AbilityRuntime</c>.
    /// </summary>
    public interface IAbility
    {
        /// <summary>Counts down the cooldown (and, for auto-abilities, may drive auto-execution).</summary>
        void Tick(float deltaTime);

        /// <summary>Performs the effect (damage/AoE) against targets resolved from the context.
        /// Implementations no-op if not <see cref="IsReady"/> or if no valid target exists.</summary>
        void Execute(AbilityExecutionContext context);

        /// <summary>True when the cooldown has elapsed and the ability may execute.</summary>
        bool IsReady { get; }

        /// <summary>Charge progress in [0,1] for UI (Task 21): 0 just after a cast, 1 when ready. Derived
        /// from the live cooldown state, so the UI never tracks cooldown independently.</summary>
        float CooldownProgress01 { get; }

        /// <summary>Current upgrade level of this instance (1-based).</summary>
        int CurrentLevel { get; }

        /// <summary>Read-only back-reference to the SO template.</summary>
        AbilityDefinitionSO Definition { get; }

        /// <summary>Raise the ability's level by one, clamped to the definition's max (CLAUDE.md §3.2).</summary>
        void Upgrade();

        /// <summary>Read-only snapshot of the ability's FINAL computed stats for UI (Task 22), produced
        /// by the SAME pipeline execution uses (so the panel can't show stale/approximate numbers).</summary>
        AbilityStats ResolveStats(UpgradeInventory upgrades, ConsumableInventory consumables,
            IReadOnlyList<StatModifier> equippedModifiers);
    }
}
