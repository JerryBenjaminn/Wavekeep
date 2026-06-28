using System;
using Wavekeep.Core;
using Wavekeep.Core.Events;

namespace Wavekeep.Economy
{
    /// <summary>
    /// Single source of truth for the run's XP and hero level (CLAUDE.md §3.2). A non-static plain
    /// C# class owned by <see cref="GameSession"/> — no static singleton.
    ///
    /// Subscribes to <see cref="EnemyKilledEvent"/> and adds the dead enemy's <c>XpReward</c>
    /// (read from the <see cref="Wavekeep.Data.EnemyDefinitionSO"/>, never written). The per-level
    /// threshold follows a configurable quadratic curve (Task 63):
    /// <c>threshold(level) = baseXP + level*increment + quadratic*level²</c>.
    /// <c>baseXP</c>/<c>increment</c>/<c>quadratic</c> are injected (serialized on the bootstrap) —
    /// not hardcoded magic numbers — so they're easy to tune. The quadratic term keeps early levels
    /// quick while preventing the player from blowing through 6+ levels in a single early wave
    /// (the linear curve front-loaded progression — Task 61 audit). Setting quadratic to 0 reduces
    /// it back to the original linear curve.
    ///
    /// A single XP gain may cross several thresholds at once: the level-up loop runs until the
    /// remaining XP is below the current threshold, carrying the remainder over (never discarded)
    /// and publishing one <see cref="XPLevelUpEvent"/> per level gained.
    /// </summary>
    public sealed class XPManager : IDisposable
    {
        private readonly EventBus _events;
        private readonly int _baseXP;
        private readonly int _increment;
        private readonly int _quadratic;

        public int CurrentLevel { get; private set; } = 1;

        /// <summary>XP accumulated within the current level (resets toward 0 on each level-up, carrying remainder).</summary>
        public int CurrentXP { get; private set; }

        /// <summary>XP required to advance from the current level to the next.</summary>
        public int XPToNextLevel { get; private set; }

        public XPManager(EventBus events, int baseXP, int increment, int quadratic = 0)
        {
            _events = events;
            _baseXP = baseXP;
            _increment = increment;
            _quadratic = quadratic;
            XPToNextLevel = ComputeThreshold(CurrentLevel);
            _events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (evt.Definition == null) return;
            AddXP(evt.Definition.XpReward);
        }

        private void AddXP(int amount)
        {
            if (amount <= 0) return;
            CurrentXP += amount;

            // Loop so one large gain can cross multiple level thresholds in a single kill,
            // carrying over the leftover each time rather than discarding it.
            while (CurrentXP >= XPToNextLevel)
            {
                CurrentXP -= XPToNextLevel;
                CurrentLevel++;
                XPToNextLevel = ComputeThreshold(CurrentLevel);
                _events.Publish(new XPLevelUpEvent(CurrentLevel));
            }
        }

        // Guarded to at least 1 so a misconfigured curve (e.g. 0/negative) can't cause an infinite loop.
        // Task 63: quadratic term (quadratic*level²) flattens early front-loading without slowing the
        // first couple of levels noticeably. quadratic=0 → original linear curve.
        private int ComputeThreshold(int level) =>
            Math.Max(1, _baseXP + level * _increment + _quadratic * level * level);

        public void Dispose()
        {
            _events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }
    }
}
