using Wavekeep.Core;
using Wavekeep.Core.Events;

namespace Wavekeep.Economy
{
    /// <summary>
    /// Single source of truth for the run's reroll-point pool (Task 09). A non-static plain C# class
    /// owned by <see cref="GameSession"/> — the same pattern as <see cref="CurrencyManager"/>/
    /// <c>XPManager</c>, kept SEPARATE from currency so the two resources never conflate (a
    /// reviewer-blocking concern): rerolling never touches currency, and currency purchases never
    /// touch reroll points except via the explicit Reroll Potion effect.
    ///
    /// Lifecycle rule (Task 09 core): the pool starts at a configured value (3) and NEVER auto-resets
    /// between shop visits within a run — it only moves via <see cref="TrySpend"/> (a reroll, −1) and
    /// <see cref="Add"/> (a Reroll Potion, +tier). It returns to its starting value only because Task
    /// 08's "Play Again" reloads the scene, which reconstructs the whole <see cref="GameSession"/> (and
    /// thus a fresh manager) — there is no in-place reset to call, by design.
    ///
    /// Publishes <see cref="RerollPointsChangedEvent"/> on change so the shop UI stays decoupled. Holds
    /// no subscriptions, so it needs no Dispose.
    /// </summary>
    public sealed class RerollManager
    {
        private readonly EventBus _events;

        /// <summary>Current reroll points available this run.</summary>
        public int CurrentPoints { get; private set; }

        /// <summary>True while at least one reroll can be spent.</summary>
        public bool CanReroll => CurrentPoints > 0;

        public RerollManager(EventBus events, int startingPoints)
        {
            _events = events;
            CurrentPoints = startingPoints < 0 ? 0 : startingPoints;
        }

        /// <summary>Spend one reroll point. Returns false (changing nothing) when the pool is empty, so
        /// it can never go negative.</summary>
        public bool TrySpend()
        {
            if (CurrentPoints <= 0) return false;

            CurrentPoints--;
            _events.Publish(new RerollPointsChangedEvent(CurrentPoints));
            return true;
        }

        /// <summary>Add reroll points (e.g. a Reroll Potion's tier amount). Non-positive amounts no-op.</summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;

            CurrentPoints += amount;
            _events.Publish(new RerollPointsChangedEvent(CurrentPoints));
        }
    }
}
