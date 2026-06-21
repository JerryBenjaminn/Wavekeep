using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One entry in an <see cref="AbilityDefinitionSO"/>'s ordered upgrade-level list. Values are
    /// multipliers applied on top of the ability's base stats at that level (level 1 = list index 0).
    /// A neutral entry is all-1.0. Plain serializable data — read-only at runtime.
    /// </summary>
    [Serializable]
    public sealed class AbilityUpgradeLevel
    {
        [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float _cooldownMultiplier = 1f;
        [SerializeField, Min(0f)] private float _rangeMultiplier = 1f;

        public float DamageMultiplier => _damageMultiplier;
        public float CooldownMultiplier => _cooldownMultiplier;
        public float RangeMultiplier => _rangeMultiplier;

        /// <summary>Neutral (×1) modifiers, used when an ability defines no upgrade levels.</summary>
        public static AbilityUpgradeLevel Identity { get; } = new AbilityUpgradeLevel();
    }
}
