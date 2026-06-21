using Wavekeep.Data;

namespace Wavekeep.Core.Events
{
    // Event payloads for the signals listed in CLAUDE.md §3.3.
    // These are grouped in one file as they are tiny related marker types; behaviour
    // lives elsewhere, so this does not violate the "one class per file" convention
    // for actual systems.

    /// <summary>Published when an enemy dies (health reaches zero). Carries the dead enemy's
    /// <see cref="EnemyDefinitionSO"/> so reward consumers (CurrencyManager/XPManager) can read
    /// its currency/xp yields. The SO is read-only — consumers must never write to it (CLAUDE.md §3.5).
    /// A richer KillContext (killer, position, …) can be added here later if needed.</summary>
    public readonly struct EnemyKilledEvent
    {
        public readonly EnemyDefinitionSO Definition;
        public EnemyKilledEvent(EnemyDefinitionSO definition) => Definition = definition;
    }

    // (Removed) EnemyReachedDefendedPointEvent — superseded by attack-the-wall behavior.
    // Enemies no longer resolve on arrival; they stop and attack WallRuntime on an interval and
    // are only released to the pool on death. Wall destruction ends the run via RunEndedEvent.

    /// <summary>Published when a wave begins.</summary>
    public readonly struct WaveStartedEvent
    {
        public readonly int WaveIndex;
        public WaveStartedEvent(int waveIndex) => WaveIndex = waveIndex;
    }

    /// <summary>Published when a wave is fully cleared.</summary>
    public readonly struct WaveCompletedEvent
    {
        public readonly int WaveIndex;
        public WaveCompletedEvent(int waveIndex) => WaveIndex = waveIndex;
    }

    /// <summary>Published when the active hero gains an XP level.</summary>
    public readonly struct XPLevelUpEvent
    {
        public readonly int NewLevel;
        public XPLevelUpEvent(int newLevel) => NewLevel = newLevel;
    }

    /// <summary>Published when the player's currency total changes.</summary>
    public readonly struct CurrencyChangedEvent
    {
        public readonly int NewTotal;
        public CurrencyChangedEvent(int newTotal) => NewTotal = newTotal;
    }

    /// <summary>Published when a run ends. Carries a minimal <see cref="RunResult"/>
    /// (see <c>RunResult.cs</c>). Task 02 fires this with <see cref="RunOutcome.WavesCleared"/>
    /// after the final wave resolves; defeat/game-over paths come in a later task.</summary>
    public readonly struct RunEndedEvent
    {
        public readonly RunResult Result;
        public RunEndedEvent(RunResult result) => Result = result;
    }
}
