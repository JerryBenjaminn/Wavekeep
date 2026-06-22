using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored single-wave configuration (CLAUDE.md §3.1). Read-only at runtime.
    /// Difficulty progression is data-driven via these assets, not magic numbers in spawner code.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Wavekeep/Wave Config")]
    public sealed class WaveConfigSO : ScriptableObject
    {
        [Tooltip("Display/ordering index for this wave.")]
        [SerializeField] private int _waveNumber = 1;

        [SerializeField] private List<EnemySpawnEntry> _spawnEntries = new List<EnemySpawnEntry>();

        [Header("Scaling")]
        [Tooltip("Per-wave stat multiplier, layered on top of the DifficultyTier's global multiplier.")]
        [SerializeField, Min(0f)] private float _statMultiplier = 1f;

        [Header("Boss Loot (Task 13)")]
        [Tooltip("Loot table the boss uses ON THIS WAVE (only meaningful on boss waves). Keyed by wave, " +
                 "not boss type, so later boss waves can drop higher tiers without new boss definitions. " +
                 "Null = boss drops nothing. A future tier (wave 50) is just a new WaveConfigSO + table.")]
        [SerializeField] private LootTableSO _bossLootTable;

        public int WaveNumber => _waveNumber;
        public IReadOnlyList<EnemySpawnEntry> SpawnEntries => _spawnEntries;
        public float StatMultiplier => _statMultiplier;
        public LootTableSO BossLootTable => _bossLootTable;
    }
}
