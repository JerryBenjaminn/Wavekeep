using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// A single equipped-item stat modifier (Task 12). DELIBERATELY reuses the existing
    /// <see cref="AbilityModifierType"/> vocabulary (Damage mult/flat, Cooldown mult, Range mult) that
    /// <c>AbilityRuntime.ComputeStats</c> already understands from Task 04 — so gear/artifact modifiers
    /// feed the SAME switch as tag-interaction and consumable modifiers, not a parallel stat path
    /// (a reviewer-blocking requirement). A small serializable struct authored on gear/artifact SOs.
    ///
    /// Examples: +10 flat damage = {DamageFlatBonus, 10}; +20% damage = {DamageMultiplier, 1.2};
    /// -15% cooldown = {CooldownMultiplier, 0.85}; +25% range = {RangeMultiplier, 1.25}.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        [SerializeField] private AbilityModifierType _modifierType;
        [SerializeField] private float _value;

        public AbilityModifierType ModifierType => _modifierType;
        public float Value => _value;

        public StatModifier(AbilityModifierType modifierType, float value)
        {
            _modifierType = modifierType;
            _value = value;
        }
    }
}
