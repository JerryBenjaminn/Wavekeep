using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Core;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 36: the single owner of player ULTIMATE input for the active hero team. Two heroes are spawned
    /// from the same prefab, so per-hero ultimate keys can't live on <see cref="HeroRuntime"/> (they'd
    /// collide); this scene component reads input once and dispatches to the right hero via the session's
    /// <see cref="GameSession.Heroes"/> registry (no static singleton).
    ///
    /// Chosen interaction model (flagged for review):
    /// <list type="bullet">
    /// <item><b>Auto-ultimate is a GLOBAL toggle</b> (one key, default ON) applied to both heroes at once.
    ///   One global flag is the simplest to reason about and matches "both heroes act autonomously" as the
    ///   default; per-hero auto toggles were rejected as needless UI for Part 1.</item>
    /// <item>While auto is ON, each hero auto-casts its own ultimate (in <see cref="HeroRuntime"/>) — this
    ///   component does nothing but watch the toggle key.</item>
    /// <item>While auto is OFF, ultimates are <b>player-cast per hero</b> via one bound key each, by team
    ///   slot order (hero 1 / hero 2). Per-hero keys (over a single "next ready ultimate" key) keep control
    ///   explicit so the player always knows which hero fired — important once heroes diverge in role.</item>
    /// </list>
    /// Apex talents are untouched here — they always auto-fire on their own cooldowns (Task 29/31/35).
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Hero Team Controller")]
    public sealed class HeroTeamController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("Input")]
        [Tooltip("Toggles auto-ultimate for the WHOLE team on/off.")]
        [SerializeField] private Key _toggleAutoUltimateKey = Key.T;
        [Tooltip("Per-hero manual ultimate keys by team slot (index 0 = first hero, 1 = second). Used only " +
                 "while auto-ultimate is OFF.")]
        [SerializeField] private Key[] _manualUltimateKeys = { Key.U, Key.I };

        [Tooltip("Starting auto-ultimate state for the team (heroes default to this too).")]
        [SerializeField] private bool _autoUltimate = true;

        private GameSession _session;

        private void Start()
        {
            _session = _bootstrap != null ? _bootstrap.Session : null;
            if (_session == null)
            {
                Debug.LogWarning("[HeroTeamController] No GameSessionBootstrap/Session; disabling.", this);
                enabled = false;
                return;
            }
            ApplyAutoToHeroes(); // heroes spawn a frame later; re-applied on toggle anyway
        }

        private void Update()
        {
            if (_session == null) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Global auto-ultimate toggle — flips the flag and pushes it to every active hero.
            if (keyboard[_toggleAutoUltimateKey].wasPressedThisFrame)
            {
                _autoUltimate = !_autoUltimate;
                Debug.Log($"[HeroTeamController] Auto-ultimate {( _autoUltimate ? "ON" : "OFF (manual per-hero keys)")}.");
            }

            // Push the team's auto state to all heroes every frame — cheap, and robust to heroes spawning
            // a frame after Start (and to a designer's inspector default differing from the hero prefab's).
            ApplyAutoToHeroes();

            // Manual casting only matters while auto is off, and never while the run is paused (level-up).
            if (_autoUltimate) return;
            if (_session.PauseState != null && _session.PauseState.IsPaused) return;

            var heroes = _session.Heroes != null ? _session.Heroes.Heroes : null;
            if (heroes == null) return;

            int count = Mathf.Min(heroes.Count, _manualUltimateKeys.Length);
            for (int i = 0; i < count; i++)
            {
                if (keyboard[_manualUltimateKeys[i]].wasPressedThisFrame)
                {
                    heroes[i]?.TryCastUltimate();
                }
            }
        }

        private void ApplyAutoToHeroes()
        {
            var heroes = _session.Heroes != null ? _session.Heroes.Heroes : null;
            if (heroes == null) return;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != null) heroes[i].AutoUltimate = _autoUltimate;
            }
        }
    }
}
