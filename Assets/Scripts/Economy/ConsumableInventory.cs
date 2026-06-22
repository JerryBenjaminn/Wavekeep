using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.Economy
{
    /// <summary>
    /// The per-run set of purchased consumable effects (Task 06 §2), mirroring Task 04's
    /// <c>UpgradeInventory</c>: a non-static plain C# class owned by <c>GameSession</c>. The shop
    /// populates it via <c>ShopController</c>; the existing <c>AbilityRuntime</c> modifier pipeline
    /// queries the aggregate damage/cooldown getters each execution — consumables are just another
    /// modifier SOURCE the ability reads, NOT a separate damage path (a reviewer-blocking requirement).
    ///
    /// Two parallel records are kept: <c>_owned</c> (every purchased <see cref="ConsumableDefinitionSO"/>,
    /// used only for the non-stackable "already owned" check) and <c>_activeEffects</c> (the live
    /// ongoing ability modifiers, which may expire on a timer). Instant effects such as
    /// <see cref="ConsumableEffectType.HealWall"/> are applied immediately by the shop and are recorded
    /// in <c>_owned</c> but never added as an ongoing effect here.
    ///
    /// SO assets are read-only: effect values are COPIED out of the definition at purchase time; the
    /// definition is never mutated (CLAUDE.md §3.5).
    /// </summary>
    public sealed class ConsumableInventory
    {
        private struct ActiveEffect
        {
            public ConsumableEffectType Type;
            public float Value;
            public float RemainingSeconds; // only meaningful when !Permanent
            public bool Permanent;
        }

        private readonly List<ConsumableDefinitionSO> _owned = new List<ConsumableDefinitionSO>();
        private readonly List<ActiveEffect> _activeEffects = new List<ActiveEffect>();

        public IReadOnlyList<ConsumableDefinitionSO> Owned => _owned;

        /// <summary>True if this exact consumable has already been purchased this run (non-stackable gate).</summary>
        public bool Owns(ConsumableDefinitionSO definition)
        {
            return definition != null && _owned.Contains(definition);
        }

        /// <summary>Record a purchase for ownership/stackable bookkeeping. Effects are added separately.</summary>
        public void RegisterPurchase(ConsumableDefinitionSO definition)
        {
            if (definition != null) _owned.Add(definition);
        }

        /// <summary>
        /// Add an ongoing ability modifier (flat damage or cooldown) read from a consumable definition.
        /// Permanent when <paramref name="duration"/> &lt;= 0, otherwise expires after that many seconds.
        /// </summary>
        public void AddEffect(ConsumableEffectType type, float value, float duration)
        {
            _activeEffects.Add(new ActiveEffect
            {
                Type = type,
                Value = value,
                RemainingSeconds = duration,
                Permanent = duration <= 0f
            });
        }

        /// <summary>Total flat damage bonus from all active <see cref="ConsumableEffectType.FlatDamageBoost"/> effects.</summary>
        public float TotalFlatDamageBonus()
        {
            float total = 0f;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Type == ConsumableEffectType.FlatDamageBoost)
                {
                    total += _activeEffects[i].Value;
                }
            }
            return total;
        }

        /// <summary>Combined cooldown multiplier (product) from all active
        /// <see cref="ConsumableEffectType.CooldownReduction"/> effects. 1 when none are held.</summary>
        public float CooldownMultiplier()
        {
            float multiplier = 1f;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Type == ConsumableEffectType.CooldownReduction)
                {
                    multiplier *= _activeEffects[i].Value;
                }
            }
            return multiplier;
        }

        /// <summary>Advance timed effects and drop any that have expired. Permanent effects are untouched.
        /// Driven once per frame by the per-frame ability consumer (HeroRuntime).</summary>
        public void Tick(float deltaTime)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                if (effect.Permanent) continue;

                effect.RemainingSeconds -= deltaTime;
                if (effect.RemainingSeconds <= 0f)
                {
                    _activeEffects.RemoveAt(i);
                }
                else
                {
                    _activeEffects[i] = effect;
                }
            }
        }

        public void Clear()
        {
            _owned.Clear();
            _activeEffects.Clear();
        }
    }
}
