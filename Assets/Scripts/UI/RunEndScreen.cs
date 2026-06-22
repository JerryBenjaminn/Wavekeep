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
    /// Restart approach (documented decision): "Play Again" reloads the active scene. That is the most
    /// leak-proof reset available and the one CLAUDE.md §3.5 is built around — <c>GameSessionBootstrap</c>
    /// reassembles a brand-new <see cref="GameSession"/> (fresh Currency/XP/Upgrade/Consumable/Pause),
    /// <c>WallRuntime.Awake</c> restores full HP, the <c>WaveSpawner</c> resets to wave 1, and hero
    /// select reappears — so no per-run state can survive into the next run. See the Task 08 report for
    /// the per-system audit. Chosen over hand-resetting each system precisely because piecemeal resets
    /// are the documented "most common bug" here.
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
        [SerializeField] private Button _playAgainButton;

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

            if (_playAgainButton != null) _playAgainButton.onClick.AddListener(OnPlayAgain);

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

        private void OnPlayAgain()
        {
            // Full scene reload = guaranteed-fresh run (see class summary). No manual per-system reset,
            // so nothing can leak. PauseState is part of the discarded session, so no resume is needed.
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }
    }
}
