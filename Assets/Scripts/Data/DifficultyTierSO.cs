using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored difficulty tier (CLAUDE.md §3.1) — wraps an ordered sequence of
    /// <see cref="WaveConfigSO"/> plus a global stat multiplier applied to every enemy spawned
    /// under this tier (multiplied with each wave's own override). Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyTier", menuName = "Wavekeep/Difficulty Tier")]
    public sealed class DifficultyTierSO : ScriptableObject
    {
        [SerializeField] private string _tierName = "Normal";
        [SerializeField] private List<WaveConfigSO> _waves = new List<WaveConfigSO>();

        [Header("Scaling")]
        [Tooltip("Applied to all enemies in this tier, multiplied with each wave's StatMultiplier.")]
        [SerializeField, Min(0f)] private float _globalStatMultiplier = 1f;

        [Header("Milestone Scaling (Task 10)")]
        [Tooltip("A milestone step is applied every N waves (5 = waves 5, 10, 15...). 0 disables milestones.")]
        [SerializeField, Min(0)] private int _milestoneWaveInterval = 5;
        [Tooltip("Extra multiplier added per milestone reached. e.g. 0.5 → wave 5 ×1.5, wave 10 ×2.0, ...")]
        [SerializeField, Min(0f)] private float _milestoneStep = 0.5f;

        [Header("Boss Waves (Task 10)")]
        [Tooltip("A boss spawns every N waves (10 = waves 10, 20, 30...). 0 disables boss waves.")]
        [SerializeField, Min(0)] private int _bossWaveInterval = 10;
        [Tooltip("Enemy spawned on boss waves — just a tougher EnemyDefinitionSO (no special boss type). Nullable.")]
        [SerializeField] private EnemyDefinitionSO _bossDefinition;
        [Tooltip("How many bosses spawn on a boss wave (default 1).")]
        [SerializeField, Min(1)] private int _bossCount = 1;

        public string TierName => _tierName;
        public IReadOnlyList<WaveConfigSO> Waves => _waves;
        public float GlobalStatMultiplier => _globalStatMultiplier;

        public int MilestoneWaveInterval => _milestoneWaveInterval;
        public float MilestoneStep => _milestoneStep;
        public int BossWaveInterval => _bossWaveInterval;
        public EnemyDefinitionSO BossDefinition => _bossDefinition;
        public int BossCount => _bossCount;

        /// <summary>
        /// Generalized milestone multiplier for a 1-based <paramref name="waveNumber"/> (Task 10):
        /// <c>1 + floor(waveNumber / interval) * step</c>. A pure, read-only computation over this
        /// tier's own serialized parameters — it never mutates the SO and never hardcodes per-wave
        /// values, so it scales to wave 50/100 with no extra authoring. Returns 1 when milestones are
        /// disabled (interval &lt;= 0).
        /// </summary>
        public float GetMilestoneMultiplier(int waveNumber)
        {
            if (_milestoneWaveInterval <= 0) return 1f;
            int milestonesReached = waveNumber / _milestoneWaveInterval; // integer division = floor
            return 1f + milestonesReached * _milestoneStep;
        }

        /// <summary>True when a 1-based <paramref name="waveNumber"/> is a boss wave (every
        /// <see cref="BossWaveInterval"/> waves) and a boss is configured. Pure read-only.</summary>
        public bool IsBossWave(int waveNumber)
        {
            return _bossWaveInterval > 0
                   && _bossDefinition != null
                   && waveNumber % _bossWaveInterval == 0;
        }
    }
}
