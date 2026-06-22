using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Abilities;
using Wavekeep.Data;
using Wavekeep.Economy;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Mutable runtime instance of an ability (CLAUDE.md §3.2 / §3.5). Wraps an
    /// <see cref="AbilityDefinitionSO"/> with the only mutable state — current level and cooldown
    /// timer — and NEVER writes back to the SO. Final output is computed locally each execution by
    /// layering: base stats → per-level multipliers → tag-interaction modifiers.
    ///
    /// Tag interactions are resolved data-drively: the runtime walks the definition's
    /// <see cref="TagInteractionRule"/> list and switches on the GENERIC
    /// <see cref="AbilityModifierType"/>, never on a specific ability identity.
    /// </summary>
    public sealed class AbilityRuntime : IAbility
    {
        private float _cooldownTimer;

        // Reusable buffer so AoE target collection doesn't allocate, and (crucially) so we never
        // mutate the live active-enemy list while enumerating it — kills remove enemies from it.
        private readonly List<EnemyRuntime> _aoeBuffer = new List<EnemyRuntime>();

        public AbilityDefinitionSO Definition { get; }
        public int CurrentLevel { get; private set; } = 1;
        public bool IsReady => _cooldownTimer <= 0f;

        public AbilityRuntime(AbilityDefinitionSO definition)
        {
            Definition = definition;
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= deltaTime;
            }
        }

        public void Upgrade()
        {
            if (CurrentLevel < Definition.MaxLevel) CurrentLevel++;
        }

        /// <summary>Read-only preview of the damage this ability would deal right now (base × level ×
        /// tag interactions × consumable × equipped-gear modifiers). Handy for debug logs/tooltips;
        /// does not mutate state.</summary>
        public float GetEffectiveDamage(UpgradeInventory upgrades, ConsumableInventory consumables,
            IReadOnlyList<StatModifier> equippedModifiers)
        {
            ComputeStats(upgrades, consumables, equippedModifiers, out float damage, out _, out _);
            return damage;
        }

        public void Execute(AbilityExecutionContext context)
        {
            if (!IsReady || context.Enemies == null) return;

            ComputeStats(context.Upgrades, context.Consumables, context.EquippedModifiers,
                out float damage, out float cooldown, out float range);

            bool hitSomething;
            switch (Definition.TargetingType)
            {
                case AbilityTargetingType.AreaOfEffect:
                    hitSomething = ExecuteAreaOfEffect(context, range, damage);
                    break;
                default: // SingleTarget
                    hitSomething = ExecuteSingleTarget(context, range, damage);
                    break;
            }

            // Only consume the cooldown when the ability actually connected, so an auto-firing basic
            // ability keeps retrying each frame until a target is in range rather than wasting shots.
            if (hitSomething)
            {
                _cooldownTimer = cooldown;
            }
        }

        private bool ExecuteSingleTarget(AbilityExecutionContext context, float range, float damage)
        {
            EnemyRuntime nearest = null;
            float bestSqr = range * range;

            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;

                float sqr = (enemy.Transform.position - context.CasterPosition).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    nearest = enemy;
                }
            }

            if (nearest == null) return false;
            nearest.TakeDamage(damage);
            ApplyHeldStatusEffects(nearest, context.Upgrades); // Task 11

            // Task 08: fire the visual from the SAME resolved target used for damage (not a separate
            // targeting pass), so the indicator can never disagree with where damage actually landed.
            context.Feedback?.OnSingleTargetHit(context.CasterPosition, nearest.Transform.position);
            return true;
        }

        private bool ExecuteAreaOfEffect(AbilityExecutionContext context, float radius, float damage)
        {
            // Snapshot in-range targets first; applying damage can remove enemies from the live list.
            _aoeBuffer.Clear();
            float radiusSqr = radius * radius;

            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;

                if ((enemy.Transform.position - context.CasterPosition).sqrMagnitude <= radiusSqr)
                {
                    _aoeBuffer.Add(enemy);
                }
            }

            if (_aoeBuffer.Count == 0) return false;

            // Task 08: show the radius indicator with the ACTUAL radius used for the overlap test, at
            // the caster centre — only when the AoE actually connects (mirrors single-target firing on
            // a hit), so an auto-firing AoE basic with no targets in range doesn't spam the ring.
            context.Feedback?.OnAreaOfEffect(context.CasterPosition, radius);

            for (int i = 0; i < _aoeBuffer.Count; i++)
            {
                _aoeBuffer[i].TakeDamage(damage);
                ApplyHeldStatusEffects(_aoeBuffer[i], context.Upgrades); // Task 11
            }
            _aoeBuffer.Clear();
            return true;
        }

        // Task 11: if THIS ability is flagged to deliver status effects, apply every held status-upgrade's
        // effect to the hit target. Fully data-driven — which ability delivers (AbilityDefinitionSO flag)
        // and what it applies (UpgradeDefinitionSO data) — never hardcoded per hero. No-op on a target the
        // damage just killed (ApplyStatusEffect guards a resolved enemy).
        private void ApplyHeldStatusEffects(EnemyRuntime target, UpgradeInventory upgrades)
        {
            if (!Definition.AppliesStatusEffects || upgrades == null || target == null) return;

            var held = upgrades.Upgrades;
            for (int i = 0; i < held.Count; i++)
            {
                var upgrade = held[i];
                if (upgrade == null || !upgrade.AppliesStatusEffect) continue;
                target.ApplyStatusEffect(upgrade.StatusEffectType, upgrade.StatusMagnitude, upgrade.StatusDuration);
            }
        }

        // Modifier stack (documented order): base → per-level multipliers → tag-interaction modifiers
        // → consumable modifiers → equipped gear/artifact modifiers. Nothing written back to the SO;
        // each source is just another layer feeding the SAME AbilityModifierType switch.
        private void ComputeStats(UpgradeInventory upgrades, ConsumableInventory consumables,
            IReadOnlyList<StatModifier> equippedModifiers,
            out float damage, out float cooldown, out float range)
        {
            var level = CurrentLevelEntry();
            damage = Definition.BaseDamage * level.DamageMultiplier;
            cooldown = Definition.BaseCooldown * level.CooldownMultiplier;
            range = Definition.Range * level.RangeMultiplier;

            var rules = Definition.TagInteractionRules;
            if (rules != null && upgrades != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule == null || !upgrades.HasTag(rule.MatchTag)) continue;
                    ApplyModifier(rule.ModifierType, rule.ModifierValue, ref damage, ref cooldown, ref range);
                }
            }

            // Consumable shop bonuses (Task 06): the same pipeline, just another modifier source —
            // not a parallel damage path. Flat damage adds; cooldown reduction multiplies.
            if (consumables != null)
            {
                damage += consumables.TotalFlatDamageBonus();
                cooldown *= consumables.CooldownMultiplier();
            }

            // Equipped gear/artifact bonuses (Task 12): the active hero loadout's aggregated modifiers,
            // applied last through the SAME switch — extends the pipeline, never a parallel calculation.
            if (equippedModifiers != null)
            {
                for (int i = 0; i < equippedModifiers.Count; i++)
                {
                    ApplyModifier(equippedModifiers[i].ModifierType, equippedModifiers[i].Value,
                        ref damage, ref cooldown, ref range);
                }
            }

            damage = Mathf.Max(0f, damage);
            cooldown = Mathf.Max(0.01f, cooldown);
            range = Mathf.Max(0f, range);
        }

        // The single, shared modifier-application switch used by tag interactions AND equipped gear, so
        // every modifier source resolves identically (CLAUDE.md §3.8 generic modifier types).
        private static void ApplyModifier(AbilityModifierType type, float value,
            ref float damage, ref float cooldown, ref float range)
        {
            switch (type)
            {
                case AbilityModifierType.DamageMultiplier: damage *= value; break;
                case AbilityModifierType.DamageFlatBonus: damage += value; break;
                case AbilityModifierType.CooldownMultiplier: cooldown *= value; break;
                case AbilityModifierType.RangeMultiplier: range *= value; break;
            }
        }

        private AbilityUpgradeLevel CurrentLevelEntry()
        {
            var levels = Definition.UpgradeLevels;
            if (levels == null || levels.Count == 0) return AbilityUpgradeLevel.Identity;

            int index = Mathf.Clamp(CurrentLevel - 1, 0, levels.Count - 1);
            return levels[index] ?? AbilityUpgradeLevel.Identity;
        }
    }
}
