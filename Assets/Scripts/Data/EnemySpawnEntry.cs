using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One line of a wave's composition (CLAUDE.md §3.1): which enemy type, how many, and the
    /// delay between consecutive spawns of this entry. Authored inside <see cref="WaveConfigSO"/>.
    /// Plain serializable data — read-only at runtime.
    /// </summary>
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [SerializeField] private EnemyDefinitionSO _enemyType;
        [SerializeField, Min(0)] private int _count = 1;
        [Tooltip("Seconds between each spawn of this entry.")]
        [SerializeField, Min(0f)] private float _spawnInterval = 0.5f;

        public EnemyDefinitionSO EnemyType => _enemyType;
        public int Count => _count;
        public float SpawnInterval => _spawnInterval;
    }
}
