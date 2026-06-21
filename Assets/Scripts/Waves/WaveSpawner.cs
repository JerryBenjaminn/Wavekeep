using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Pooling;
using Wavekeep.Runtime;

namespace Wavekeep.Waves
{
    /// <summary>
    /// Drives the run's wave sequence (CLAUDE.md §3.2). Reads the active <see cref="DifficultyTierSO"/>,
    /// spawns each <see cref="WaveConfigSO"/>'s enemies from the single far-side spawn line via the
    /// <see cref="EnemyPoolManager"/> (never <c>Instantiate</c>), advances all active
    /// <see cref="EnemyRuntime"/> movement/attack from a single <c>Update</c> tick (§3.4), and
    /// publishes wave/run lifecycle events through the session <see cref="EventBus"/>.
    ///
    /// Enemies approach from one direction only (CLAUDE.md §2), attack the <see cref="WallRuntime"/>
    /// on arrival, and are released to the pool only on death. The run ends in defeat when the wall
    /// is destroyed, or in victory if every wave is fully cleared (all enemies killed).
    ///
    /// Lives in Scripts/Waves (the task left the folder to my discretion). It pulls its
    /// dependencies (EventBus, EnemyPoolManager) from <see cref="GameSession"/> rather than any
    /// static access (§3.5).
    /// </summary>
    [AddComponentMenu("Wavekeep/Waves/Wave Spawner")]
    public sealed class WaveSpawner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private DifficultyTierSO _difficultyTier;

        [Header("Arena")]
        [Tooltip("Far-side points enemies spawn from (a single approach direction). Selected round-robin.")]
        [SerializeField] private Transform[] _spawnMarkers;
        [Tooltip("The defended wall enemies advance toward and attack.")]
        [SerializeField] private WallRuntime _wall;
        [Tooltip("Distance from the wall at which an enemy stops and begins attacking.")]
        [SerializeField, Min(0f)] private float _arrivalThreshold = 1.5f;
        [Tooltip("Seconds between each enemy attack against the wall (placeholder; tune later).")]
        [SerializeField, Min(0.05f)] private float _attackInterval = 1f;

        [Header("Lifecycle")]
        [SerializeField] private bool _autoStartOnPlay = true;

        [Header("Debug")]
        [Tooltip("When enabled, the kill key damages the most-recently-spawned active enemy to verify the death path.")]
        [SerializeField] private bool _enableDebugKillKey = true;
        [SerializeField] private Key _debugKillKey = Key.K;

        private EventBus _events;
        private EnemyPoolManager _pool;

        private readonly List<EnemyRuntime> _activeEnemies = new List<EnemyRuntime>();
        private int _spawnMarkerCursor;
        private bool _runStarted;
        private bool _runEnded;

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogError("[WaveSpawner] No GameSessionBootstrap/Session assigned; cannot run.", this);
                enabled = false;
                return;
            }

            if (_wall == null)
            {
                Debug.LogError("[WaveSpawner] No WallRuntime assigned; cannot run.", this);
                enabled = false;
                return;
            }

            _events = _bootstrap.Session.Events;
            _pool = _bootstrap.Session.EnemyPool;
            _wall.OnWallDestroyed += HandleWallDestroyed;

            if (_autoStartOnPlay)
            {
                StartRun();
            }
        }

        private void OnDestroy()
        {
            if (_wall != null) _wall.OnWallDestroyed -= HandleWallDestroyed;
        }

        private void Update()
        {
            if (_runEnded) return;

            // Single centralised tick for all active enemies (CLAUDE.md §3.4). Iterate backwards:
            // an enemy dying mid-tick removes itself from the list via the callback. An enemy's
            // attack can also destroy the wall, which ends the run and clears the list — break out
            // immediately in that case rather than indexing into an emptied list.
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                _activeEnemies[i].Tick(Time.deltaTime);
                if (_runEnded) return;
            }

            HandleDebugKill();
        }

        /// <summary>Begin processing the configured difficulty tier's waves. Safe to call once.</summary>
        public void StartRun()
        {
            if (_runStarted || _runEnded) return;

            if (_difficultyTier == null || _difficultyTier.Waves.Count == 0)
            {
                Debug.LogWarning("[WaveSpawner] No DifficultyTier or no waves configured; nothing to run.", this);
                return;
            }

            if (_spawnMarkers == null || _spawnMarkers.Length == 0)
            {
                Debug.LogError("[WaveSpawner] Spawn markers not assigned.", this);
                return;
            }

            _runStarted = true;
            StartCoroutine(RunRoutine());
        }

        private IEnumerator RunRoutine()
        {
            var waves = _difficultyTier.Waves;
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                var wave = waves[waveIndex];
                if (wave == null) continue;

                _events.Publish(new WaveStartedEvent(waveIndex));
                Debug.Log($"[WaveSpawner] Wave {waveIndex} started ('{_difficultyTier.TierName}').");

                yield return StartCoroutine(SpawnWaveRoutine(wave));

                // Wave completes only once every spawned enemy has resolved (i.e. been killed —
                // reaching the wall no longer resolves an enemy). If the wall falls first,
                // HandleWallDestroyed stops this coroutine and ends the run in defeat.
                yield return new WaitUntil(() => _activeEnemies.Count == 0);

                _events.Publish(new WaveCompletedEvent(waveIndex));
                Debug.Log($"[WaveSpawner] Wave {waveIndex} completed.");
            }

            EndRun(RunOutcome.WavesCleared);
        }

        private IEnumerator SpawnWaveRoutine(WaveConfigSO wave)
        {
            float multiplier = _difficultyTier.GlobalStatMultiplier * wave.StatMultiplier;

            foreach (var entry in wave.SpawnEntries)
            {
                if (entry == null || entry.EnemyType == null || entry.EnemyType.Prefab == null)
                {
                    Debug.LogWarning("[WaveSpawner] Skipping spawn entry with missing enemy type/prefab.", this);
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    SpawnEnemy(entry.EnemyType, multiplier);
                    if (entry.SpawnInterval > 0f)
                    {
                        yield return new WaitForSeconds(entry.SpawnInterval);
                    }
                }
            }
        }

        private void SpawnEnemy(EnemyDefinitionSO definition, float multiplier)
        {
            var marker = _spawnMarkers[_spawnMarkerCursor];
            _spawnMarkerCursor = (_spawnMarkerCursor + 1) % _spawnMarkers.Length;

            var instance = _pool.Get(definition.Prefab);
            instance.transform.position = marker.position;

            var enemy = new EnemyRuntime();
            enemy.Initialize(definition, instance, multiplier, _wall, _events, _arrivalThreshold, _attackInterval, OnEnemyResolved);
            _activeEnemies.Add(enemy);

            Debug.Log($"[WaveSpawner] Spawned '{definition.EnemyName}' mult={multiplier:0.00} maxHP={enemy.MaxHealth:0.0}");
        }

        // Called by an EnemyRuntime when it dies (the only resolution path). The spawner owns pool
        // release and active-count bookkeeping (CLAUDE.md §3.5 dependency ownership).
        private void OnEnemyResolved(EnemyRuntime enemy)
        {
            _activeEnemies.Remove(enemy);
            _pool.Release(enemy.GameObject);
        }

        private void HandleWallDestroyed()
        {
            EndRun(RunOutcome.Defeated);
        }

        // Single run-termination point. Guards against double-ending, halts spawning, returns any
        // still-active enemies to the pool, and publishes the result.
        private void EndRun(RunOutcome outcome)
        {
            if (_runEnded) return;
            _runEnded = true;

            StopAllCoroutines();
            ReleaseAllActiveEnemies();

            _events.Publish(new RunEndedEvent(new RunResult(outcome)));
            Debug.Log($"[WaveSpawner] Run ended: {outcome} — RunEndedEvent published.");
        }

        private void ReleaseAllActiveEnemies()
        {
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                _pool.Release(_activeEnemies[i].GameObject);
            }
            _activeEnemies.Clear();
        }

        private void HandleDebugKill()
        {
            if (!_enableDebugKillKey || _activeEnemies.Count == 0) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[_debugKillKey].wasPressedThisFrame) return;

            // Damage the most-recently-spawned active enemy by its full health to force Die().
            // Works whether the enemy is still moving or already attacking the wall.
            var target = _activeEnemies[_activeEnemies.Count - 1];
            target.TakeDamage(target.CurrentHealth);
            Debug.Log("[WaveSpawner] Debug kill: applied lethal damage to one active enemy.");
        }
    }
}
