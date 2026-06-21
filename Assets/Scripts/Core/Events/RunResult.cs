namespace Wavekeep.Core.Events
{
    /// <summary>How a run finished. Minimal for Task 02 (only the "all waves cleared" path is
    /// produced yet); defeat/game-over outcomes are added once defended-point health exists.</summary>
    public enum RunOutcome
    {
        WavesCleared,
        Defeated
    }

    /// <summary>
    /// Lightweight payload for <see cref="RunEndedEvent"/> (CLAUDE.md §3.3 <c>OnRunEnded(RunResult)</c>).
    /// Kept intentionally small for Task 02 — extended with stats (waves survived, time, etc.) later.
    /// </summary>
    public readonly struct RunResult
    {
        public readonly RunOutcome Outcome;

        public RunResult(RunOutcome outcome) => Outcome = outcome;
    }
}
