using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Core;

namespace Wavekeep.Pooling
{
    /// <summary>
    /// TEMPORARY Task 01 verification component (acceptance criteria: prove prewarm/recycle
    /// works). Prewarms a placeholder 3D prefab through the session's
    /// <see cref="EnemyPoolManager"/>, spawns a few in a row, then releases them — logging each
    /// step. Remove or replace once Task 02 introduces real enemy spawning.
    /// </summary>
    [AddComponentMenu("Wavekeep/Debug/Enemy Pool Smoke Test")]
    public sealed class EnemyPoolSmokeTest : MonoBehaviour
    {
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private GameObject _placeholderPrefab;
        [SerializeField] private int _prewarmCount = 5;
        [SerializeField] private float _spacing = 2f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogWarning("[EnemyPoolSmokeTest] No GameSessionBootstrap/Session assigned; skipping.");
                return;
            }

            if (_placeholderPrefab == null)
            {
                Debug.LogWarning("[EnemyPoolSmokeTest] No placeholder prefab assigned; skipping.");
                return;
            }

            var pool = _bootstrap.Session.EnemyPool;

            pool.Prewarm(_placeholderPrefab, _prewarmCount);
            Debug.Log($"[EnemyPoolSmokeTest] Prewarmed {_prewarmCount} instances.");

            for (int i = 0; i < _prewarmCount; i++)
            {
                var instance = pool.Get(_placeholderPrefab);
                instance.transform.position = new Vector3(i * _spacing, 0.5f, 0f);
                _spawned.Add(instance);
            }
            Debug.Log($"[EnemyPoolSmokeTest] Got {_spawned.Count} instances from the pool.");
        }

        // Releases everything spawned, proving instances return to the pool for reuse.
        [ContextMenu("Release All")]
        private void ReleaseAll()
        {
            if (_bootstrap == null || _bootstrap.Session == null) return;

            var pool = _bootstrap.Session.EnemyPool;
            foreach (var instance in _spawned)
            {
                pool.Release(instance);
            }

            Debug.Log($"[EnemyPoolSmokeTest] Released {_spawned.Count} instances back to the pool.");
            _spawned.Clear();
        }
    }
}
