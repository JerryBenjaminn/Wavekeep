namespace Wavekeep.Core
{
    /// <summary>
    /// Run-scoped "is gameplay paused" flag (Task 07), owned by <see cref="GameSession"/> and injected
    /// into the systems that must freeze — currently <c>WaveSpawner</c> (enemy ticking + spawning) and
    /// <c>HeroRuntime</c> (ability ticking/auto-fire). A plain C# service, no static <c>Instance</c>.
    ///
    /// Deliberately NOT implemented via <c>Time.timeScale = 0</c>: a global timescale freeze can
    /// interfere with the very UI the level-up picker needs to stay interactive (and would also stall
    /// coroutine waits in unintended ways). A flag the gameplay loops opt into checking leaves the
    /// EventSystem / UI input path completely untouched, so the card buttons keep responding while the
    /// world is frozen.
    ///
    /// Reference-counted so independent pause sources nest safely: the world stays frozen until every
    /// <see cref="Pause"/> has been matched by a <see cref="Resume"/>.
    /// </summary>
    public sealed class PauseState
    {
        private int _pauseCount;

        /// <summary>True while at least one unmatched <see cref="Pause"/> is active.</summary>
        public bool IsPaused => _pauseCount > 0;

        /// <summary>Request a pause. Balanced by a later <see cref="Resume"/>.</summary>
        public void Pause() => _pauseCount++;

        /// <summary>Release one pause request. Clamped at zero so an extra call can't leave it negative.</summary>
        public void Resume()
        {
            if (_pauseCount > 0) _pauseCount--;
        }
    }
}
