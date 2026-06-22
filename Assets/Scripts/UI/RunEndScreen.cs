using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Waves;

namespace Wavekeep.UI
{
    /// <summary>
    /// The first consumer of <see cref="RunEndedEvent"/> (publishing since Task 02). On run end it
    /// pauses gameplay using the SAME <see cref="PauseState"/> mechanism as Task 07's card picker —
    /// not a second pause system — and shows a placeholder Victory/Defeat Canvas screen with minimal
    /// stats and a "Play Again" button.
    ///
    /// Restart approach (Task 14 update): the end-screen button now returns to the HUB scene rather than
    /// reloading the gameplay scene. Loading a different scene still discards the gameplay
    /// <see cref="GameSession"/> entirely — so per-run Currency/XP/Upgrade/Consumable/Pause/Wall/Wave
    /// state is fully reset when the next run starts (its bootstrap builds a fresh session), exactly the
    /// leak-free guarantee Task 08 relied on — while persistent gear survives via disk. The Hub is now
    /// the between-run home (manage gear, then launch again).
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Run End Screen")]
    public sealed class RunEndScreen : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private WaveSpawner _waveSpawner;

        [Header("UI")]
        [Tooltip("Root object shown when the run ends, hidden during play.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _statsText;
        [Tooltip("Button that returns to the Hub (Task 14). Was 'Play Again' in Task 08.")]
        [SerializeField] private Button _playAgainButton;

        [Tooltip("Scene loaded when the player leaves the run (Task 14).")]
        [SerializeField] private string _hubSceneName = "Hub";

        private EventBus _events;
        private PauseState _pause;
        private bool _shown;

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogError("[RunEndScreen] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }

            _events = _bootstrap.Session.Events;
            _pause = _bootstrap.Session.PauseState;

            if (_playAgainButton != null) _playAgainButton.onClick.AddListener(OnReturnToHub);

            _events.Subscribe<RunEndedEvent>(OnRunEnded);
            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_events != null) _events.Unsubscribe<RunEndedEvent>(OnRunEnded);
        }

        private void OnRunEnded(RunEndedEvent evt)
        {
            if (_shown) return; // RunEndedEvent is single-shot per run, but guard against re-entry.
            _shown = true;

            // Same pause path as Task 07 — freezes the hero's ability ticking while this UI is up; the
            // flag doesn't touch the EventSystem, so the Play Again button stays interactive.
            _pause.Pause();

            bool victory = evt.Result.Outcome == RunOutcome.WavesCleared;
            if (_titleText != null) _titleText.text = victory ? "Victory!" : "Defeat";
            if (_statsText != null) _statsText.text = BuildStats();

            SetPanelVisible(true);
        }

        private string BuildStats()
        {
            var session = _bootstrap.Session;
            int wave = _waveSpawner != null ? _waveSpawner.CurrentWaveNumber : 0;
            int currency = session.CurrencyManager.CurrentCurrency;
            int level = session.XPManager.CurrentLevel;
            return $"Wave reached: {wave}\nCurrency: {currency}\nLevel: {level}";
        }

        private void OnReturnToHub()
        {
            // Task 14: leave the run for the Hub. Loading another scene discards the gameplay session,
            // so per-run state can't leak; persistent gear is already saved to disk. The next run is
            // launched fresh from the Hub.
            SceneManager.LoadScene(_hubSceneName);
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }
    }
}
