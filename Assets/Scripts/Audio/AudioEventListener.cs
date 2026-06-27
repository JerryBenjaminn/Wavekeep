using System;
using Wavekeep.Core;
using Wavekeep.Core.Events;

namespace Wavekeep.Audio
{
    /// <summary>
    /// Task 59: the "what plays when" policy for audio — subscribes to the existing EventBus signals (CLAUDE.md
    /// §3.3) and asks <see cref="AudioManager"/> to play the matching placeholder cue from <see cref="AudioConfigSO"/>.
    /// Owned and disposed by AudioManager (which is owned by GameSession), so its subscriptions follow the same
    /// run/scene lifecycle as every other manager (§3.5). Kept separate from the playback engine so the engine
    /// stays reusable and this policy file is the one place that maps events → cues.
    ///
    /// Deliberately does NOT subscribe to <see cref="CurrencyChangedEvent"/> — see Task 59 §2.4: it fires per
    /// kill/spend and would be noisy; the correct hook is a shop-purchase action, which has no event yet.
    /// </summary>
    public sealed class AudioEventListener : IDisposable
    {
        private readonly EventBus _events;
        private readonly AudioManager _audio;
        private readonly AudioConfigSO _config;

        public AudioEventListener(EventBus events, AudioManager audio, AudioConfigSO config)
        {
            _events = events;
            _audio = audio;
            _config = config;
            if (_events == null) return;

            _events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            _events.Subscribe<WaveStartedEvent>(OnWaveStarted);
            _events.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            _events.Subscribe<XPLevelUpEvent>(OnLevelUp);
            _events.Subscribe<RunEndedEvent>(OnRunEnded);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt) => _audio?.PlayCue(_config?.EnemyKilledCue);
        private void OnWaveStarted(WaveStartedEvent evt) => _audio?.PlayCue(_config?.WaveStartedCue);
        private void OnWaveCompleted(WaveCompletedEvent evt) => _audio?.PlayCue(_config?.WaveCompletedCue);
        private void OnLevelUp(XPLevelUpEvent evt) => _audio?.PlayCue(_config?.LevelUpCue);

        private void OnRunEnded(RunEndedEvent evt)
        {
            // Branch on the run result: victory (waves cleared) vs defeat → different music/cue (§2.4).
            var cue = evt.Result.Outcome == RunOutcome.Defeated ? _config?.DefeatCue : _config?.VictoryCue;
            _audio?.PlayMusicTrack(cue, crossfade: true);
        }

        public void Dispose()
        {
            if (_events == null) return;
            _events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            _events.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            _events.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            _events.Unsubscribe<XPLevelUpEvent>(OnLevelUp);
            _events.Unsubscribe<RunEndedEvent>(OnRunEnded);
        }
    }
}
