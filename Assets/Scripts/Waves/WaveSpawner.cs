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
        [Tooltip("Task 17: the between-wave shop intermission only opens every N wave-completions " +
                 "(5 = after waves 5, 10, 15...). Other waves flow straight into the next with no pause. " +
                 "0 disables the shop intermission entirely. Configurable — not a hardcoded inline check.")]
        [SerializeField, Min(0)] private int _shopIntervalWaves = 5;

        [Header("Debug")]
        [Tooltip("When enabled, the kill key damages the most-recently-spawned active enemy to verify the death path.")]
        [SerializeField] private bool _enableDebugKillKey = true;
        [SerializeField] private Key _debugKillKey = Key.K;

        private EventBus _events;
        private EnemyPoolManager _pool;
        private PauseState _pause;
        private ComboApexState _comboApex; // Task 50 (Frostburn): handed to each spawned EnemyRuntime

        private readonly List<EnemyRuntime> _activeEnemies = new List<EnemyRuntime>();
        private int _spawnMarkerCursor;
        private bool _runStarted;
        private bool _runEnded;
        private bool _awaitingContinue;
        private int _currentWaveNumber;

        /// <summary>1-based number of the wave currently in progress (or last reached). 0 before the run
        /// starts. Surfaced for the Task 08 end screen's minimal stats.</summary>
        public int CurrentWaveNumber => _currentWaveNumber;

        /// <summary>Live, read-only view of the currently active enemies (Task 04: ability target
        /// acquisition reads this). Do not cache across frames — entries are added/removed as enemies
        /// spawn and resolve.</summary>
        public IReadOnlyList<EnemyRuntime> ActiveEnemies => _activeEnemies;

        /// <summary>Task 33: the defended line's Z (the wall's position along the approach axis). Frost Zone
        /// places its full-width band relative to this.</summary>
        public float DefendedLineZ => _wall != null ? _wall.transform.position.z : 0f;

        /// <summary>Task 33: sign (+1/−1) pointing from the wall toward the spawn side along Z, so a band can
        /// be placed "in front of the wall" regardless of arena orientation.</summary>
        public float ApproachDirectionZ
        {
            get
            {
                if (_wall == null || _spawnMarkers == null || _spawnMarkers.Length == 0 || _spawnMarkers[0] == null)
                    return 1f;
                float delta = _spawnMarkers[0].position.z - _wall.transform.position.z;
                return delta >= 0f ? 1f : -1f;
            }
        }

        /// <summary>Task 53: the enemy spawn edge's Z (far side of the arena), averaged across the spawn markers.
        /// Lets a full-width zone (Firewall) center itself at mid-arena depth between the wall and this edge.
        /// Falls back to <see cref="DefendedLineZ"/> when no markers are wired (so callers degrade to a near-wall
        /// band rather than placing the zone off-arena).</summary>
        public float SpawnLineZ
        {
            get
            {
                if (_spawnMarkers == null || _spawnMarkers.Length == 0) return DefendedLineZ;
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < _spawnMarkers.Length; i++)
                {
                    if (_spawnMarkers[i] == null) continue;
                    sum += _spawnMarkers[i].position.z;
                    count++;
                }
                return count > 0 ? sum / count : DefendedLineZ;
            }
        }

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
            _pause = _bootstrap.Session.PauseState;
            _comboApex = _bootstrap.Session.ComboApex; // Task 50 (Frostburn): per-tick Burn amp under Frost CC
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

            // Task 07: freeze the whole active-enemy simulation (movement + wall attacks + debug kill)
            // while the level-up card picker is up. Spawning is frozen separately in SpawnWaveRoutine.
            if (_pause != null && _pause.IsPaused) return;

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

        /// <summary>Release the between-wave pause gate so the next wave begins (Task 06). Called by the
        /// shop UI's Continue button. A general resume hook — no shop/wave-specific branching here.</summary>
        public void ContinueAfterIntermission()
        {
            _awaitingContinue = false;
        }

        /// <summary>Task 17: true when a 1-based <paramref name="waveNumber"/> should open the between-wave
        /// shop intermission (every <see cref="_shopIntervalWaves"/> waves). Interval &lt;= 0 disables it.
        /// Pure read-only check — the configurable interval, not a hardcoded inline modulo.</summary>
        private bool IsShopIntermissionWave(int waveNumber) =>
            _shopIntervalWaves > 0 && waveNumber % _shopIntervalWaves == 0;

        private IEnumerator RunRoutine()
        {
            var waves = _difficultyTier.Waves;
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                var wave = waves[waveIndex];
                if (wave == null) continue;

                _currentWaveNumber = waveIndex + 1;
                _events.Publish(new WaveStartedEvent(waveIndex));
                Debug.Log($"[WaveSpawner] Wave {waveIndex} started ('{_difficultyTier.TierName}').");

                yield return StartCoroutine(SpawnWaveRoutine(wave, _currentWaveNumber));

                // Wave completes only once every spawned enemy has resolved (i.e. been killed —
                // reaching the wall no longer resolves an enemy). If the wall falls first,
                // HandleWallDestroyed stops this coroutine and ends the run in defeat.
                yield return new WaitUntil(() => _activeEnemies.Count == 0);

                // Task 07: if clearing the last enemy also triggered a level-up, let the player resolve
                // the card pick before the wave-complete / shop-intermission sequence runs, so the
                // level-up card and the shop screen never stack on top of each other.
                yield return new WaitWhile(() => _pause != null && _pause.IsPaused);

                _events.Publish(new WaveCompletedEvent(waveIndex));
                Debug.Log($"[WaveSpawner] Wave {waveIndex} completed.");

                // Task 06: pause between waves so the player can visit the shop, but only when another
                // wave follows (never after the final wave — that goes straight to victory). This is a
                // general pause/resume gate; the spawner stays ignorant of the shop. The shop UI opens
                // on IntermissionStartedEvent and releases the gate via ContinueAfterIntermission().
                //
                // Task 17: only open the intermission every _shopIntervalWaves completions (waves 5, 10,
                // ...). Other waves flow straight into the next with no pause. This block sits AFTER the
                // "all enemies resolved" wait + WaveCompletedEvent above, so on a boss wave (every 10th,
                // a multiple of 5) the shop can only appear once the boss is actually dead — no race.
                if (waveIndex < waves.Count - 1 && IsShopIntermissionWave(_currentWaveNumber))
                {
                    _awaitingContinue = true;

                    // Task 28: the between-wave shop intermission halts wave progression, so raise the SHARED
                    // PauseState — the SAME signal the level-up card picker already uses. Previously the
                    // intermission only halted the spawner locally (via _awaitingContinue), leaving time-based
                    // systems that key off PauseState (notably ultimate charge accrual in HeroRuntime) running
                    // through the shop. Pausing here makes every paused system freeze uniformly while the shop
                    // is open, then resume when the player continues. PauseState is reference-counted, so this
                    // nests safely with any other pause source.
                    _pause?.Pause();
                    _events.Publish(new IntermissionStartedEvent(waveIndex, waveIndex + 1));
                    yield return new WaitUntil(() => !_awaitingContinue || _runEnded);
                    _pause?.Resume();
                    if (_runEnded) yield break;
                }
            }

            EndRun(RunOutcome.WavesCleared);
        }

        private IEnumerator SpawnWaveRoutine(WaveConfigSO wave, int waveNumber)
        {
            // Task 10 stacking order (multiplicative): per-wave × tier × milestone. Milestone is the
            // generalized every-Nth-wave step computed by the tier from its own params (no per-wave
            // hardcoding). All three feed the SAME EnemyRuntime stat scaling as Task 02 — the SO is
            // never mutated; the multiplier is applied to the runtime copy at spawn.
            float milestone = _difficultyTier.GetMilestoneMultiplier(waveNumber);
            float multiplier = _difficultyTier.GlobalStatMultiplier * wave.StatMultiplier * milestone;
            bool bossWave = _difficultyTier.IsBossWave(waveNumber);

            Debug.Log($"[WaveSpawner] Wave {waveNumber} scaling: perWave={wave.StatMultiplier:0.00} × " +
                      $"tier={_difficultyTier.GlobalStatMultiplier:0.00} × milestone={milestone:0.00} " +
                      $"→ final={multiplier:0.00}{(bossWave ? "  [BOSS WAVE]" : "")}");

            // Boss spawns ALONGSIDE (not instead of) the normal composition, at the same scaled
            // multiplier, through the SAME pool/SpawnEnemy path — it's just a tougher EnemyDefinitionSO,
            // so it lands in _activeEnemies and the existing "all enemies resolved" wait covers it.
            if (bossWave)
            {
                yield return StartCoroutine(SpawnBosses(multiplier, wave));
            }

            foreach (var entry in wave.SpawnEntries)
            {
                if (entry == null || entry.EnemyType == null || entry.EnemyType.Prefab == null)
                {
                    Debug.LogWarning("[WaveSpawner] Skipping spawn entry with missing enemy type/prefab.", this);
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    // Task 07: hold spawning while paused (level-up picker) so a card pick doesn't let
                    // the wave keep streaming enemies in behind the frozen ones.
                    while (_pause != null && _pause.IsPaused) yield return null;

                    // Task 13: regular enemies roll their own EnemyDefinitionSO loot table (or none).
                    SpawnEnemy(entry.EnemyType, multiplier, entry.EnemyType.LootTable);
                    if (entry.SpawnInterval > 0f)
                    {
                        yield return new WaitForSeconds(entry.SpawnInterval);
                    }
                }
            }
        }

        private IEnumerator SpawnBosses(float multiplier, WaveConfigSO wave)
        {
            var bossDef = _difficultyTier.BossDefinition;
            if (bossDef == null || bossDef.Prefab == null)
            {
                Debug.LogWarning("[WaveSpawner] Boss wave but boss definition/prefab missing; skipping boss.", this);
                yield break;
            }

            // Task 13: boss loot tier is determined by the WAVE (this WaveConfigSO's boss table), not by
            // the shared boss definition — so later boss waves can drop higher tiers with no new boss type.
            var bossLootTable = wave.BossLootTable;

            int bossCount = Mathf.Max(1, _difficultyTier.BossCount);
            for (int b = 0; b < bossCount; b++)
            {
                while (_pause != null && _pause.IsPaused) yield return null;
                SpawnEnemy(bossDef, multiplier, bossLootTable);
                Debug.Log($"[WaveSpawner] BOSS spawned: '{bossDef.EnemyName}' at mult={multiplier:0.00} " +
                          $"(loot: {(bossLootTable != null ? bossLootTable.name : "none")}).");
            }
        }

        private void SpawnEnemy(EnemyDefinitionSO definition, float multiplier, LootTableSO lootTable)
        {
            var marker = _spawnMarkers[_spawnMarkerCursor];
            _spawnMarkerCursor = (_spawnMarkerCursor + 1) % _spawnMarkers.Length;

            var instance = _pool.Get(definition.Prefab);
            instance.transform.position = marker.position;

            var enemy = new EnemyRuntime();
            enemy.Initialize(definition, instance, multiplier, _wall, _events, _arrivalThreshold, _attackInterval,
                OnEnemyResolved, lootTable, _comboApex);
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
