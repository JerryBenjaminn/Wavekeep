using System;
using Wavekeep.Core;
using Wavekeep.Core.Events;

namespace Wavekeep.Economy
{
    /// <summary>
    /// Single source of truth for the run's Currency total (CLAUDE.md §3.2). A non-static plain
    /// C# class owned by <see cref="GameSession"/> (same pattern as <c>EnemyPoolManager</c>/
    /// <c>EventBus</c>) — no static singleton.
    ///
    /// Subscribes to <see cref="EnemyKilledEvent"/> on the session <see cref="EventBus"/> and adds
    /// the dead enemy's <c>CurrencyReward</c> to the total, reading the value from the
    /// <see cref="Wavekeep.Data.EnemyDefinitionSO"/> (never writing to it). Publishes
    /// <see cref="CurrencyChangedEvent"/> whenever the total changes — from a kill or a future spend.
    /// Lives in Scripts/Economy (the task left Core vs Economy to my discretion).
    /// </summary>
    public sealed class CurrencyManager : IDisposable
    {
        private readonly EventBus _events;

        public int CurrentCurrency { get; private set; }

        public CurrencyManager(EventBus events)
        {
            _events = events;
            _events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (evt.Definition == null) return;
            Add(evt.Definition.CurrencyReward);
        }

        private void Add(int amount)
        {
            if (amount <= 0) return;
            CurrentCurrency += amount;
            _events.Publish(new CurrencyChangedEvent(CurrentCurrency));
        }

        /// <summary>
        /// Attempts to spend <paramref name="amount"/>. Validates funds before deducting; returns
        /// false (and changes nothing) on a non-positive amount or insufficient balance, so the
        /// total can never go negative. The shop will call this in a later task — nothing does yet.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0) return false;
            if (amount > CurrentCurrency) return false;

            CurrentCurrency -= amount;
            _events.Publish(new CurrencyChangedEvent(CurrentCurrency));
            return true;
        }

        public void Dispose()
        {
            _events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }
    }
}
