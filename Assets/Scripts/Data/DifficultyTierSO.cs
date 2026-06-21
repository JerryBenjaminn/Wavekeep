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

        public string TierName => _tierName;
        public IReadOnlyList<WaveConfigSO> Waves => _waves;
        public float GlobalStatMultiplier => _globalStatMultiplier;
    }
}
