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

        /// <summary>Read-only view of one live effect for UI (Task 22). Mirrors the private
        /// <see cref="ActiveEffect"/> without exposing the mutable storage.</summary>
        public readonly struct ActiveEffectView
        {
            public readonly ConsumableEffectType Type;
            public readonly float Value;
            public readonly float RemainingSeconds; // only meaningful when !Permanent
            public readonly bool Permanent;

            public ActiveEffectView(ConsumableEffectType type, float value, float remainingSeconds, bool permanent)
            {
                Type = type;
                Value = value;
                RemainingSeconds = remainingSeconds;
                Permanent = permanent;
            }
        }

        private readonly List<ConsumableDefinitionSO> _owned = new List<ConsumableDefinitionSO>();
        private readonly List<ActiveEffect> _activeEffects = new List<ActiveEffect>();

        public IReadOnlyList<ConsumableDefinitionSO> Owned => _owned;

        /// <summary>Number of live ongoing effects (Task 22 stat panel). Indexed by <see cref="GetActiveEffect"/>.</summary>
        public int ActiveEffectCount => _activeEffects.Count;

        /// <summary>A read-only snapshot of the live effect at <paramref name="index"/> (Task 22). Indexed
        /// (not a list property) so the panel can iterate each frame without allocating.</summary>
        public ActiveEffectView GetActiveEffect(int index)
        {
            var e = _activeEffects[index];
            return new ActiveEffectView(e.Type, e.Value, e.RemainingSeconds, e.Permanent);
        }

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

        // --- Task 23 aggregates: each sums the active effects of one new type, mirroring the pattern
        // above. AbilityRuntime reads these as additional modifier SOURCES — never a parallel damage path.

        private float SumOf(ConsumableEffectType type)
        {
            float total = 0f;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Type == type) total += _activeEffects[i].Value;
            }
            return total;
        }

        /// <summary>Total crit CHANCE [0..1] from all active Crit Chance effects (clamped).</summary>
        public float TotalCritChance() => Mathf.Clamp01(SumOf(ConsumableEffectType.CritChanceBoost));

        /// <summary>Total crit DAMAGE bonus fraction from all active Crit Damage effects (a crit deals ×(1+this)).</summary>
        public float TotalCritDamageBonus() => SumOf(ConsumableEffectType.CritDamageBoost);

        /// <summary>Total per-stack frost slow bonus [0..1] from Frost Potions (Task 23).</summary>
        public float FrostPerStackSlowBonus() => SumOf(ConsumableEffectType.FrostPotency);

        /// <summary>Total flat damage from Lightning Potions — generic placeholder applied to ALL abilities.</summary>
        public float TotalElementalLightningBonus() => SumOf(ConsumableEffectType.ElementalLightning);

        /// <summary>Total seconds added to the ultimate's zone duration by Ultimate Duration Potions.</summary>
        public float UltimateDurationBonus() => SumOf(ConsumableEffectType.UltimateDurationBoost);

        /// <summary>Total flat damage added to the BASIC ability only by Basic Attack Damage Potions.</summary>
        public float BasicDamageBonus() => SumOf(ConsumableEffectType.BasicDamageBoost);

        /// <summary>Total flat AoE/blast-radius bonus (metres) from AoE Radius potions (Task 30 — migrated
        /// generic "AoE radius" upgrade). Added to the resolved ability radius in AbilityRuntime.</summary>
        public float AoeRadiusBonus() => SumOf(ConsumableEffectType.AoeRadiusBoost);

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
