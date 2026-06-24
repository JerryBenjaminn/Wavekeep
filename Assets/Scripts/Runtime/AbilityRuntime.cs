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
    /// layering: base stats → per-level multipliers → tag-interaction modifiers → role-targeted held
    /// upgrade modifiers → consumable → equipped-gear modifiers.
    ///
    /// Tag interactions and upgrade modifiers are resolved data-drively: the runtime walks the
    /// definition's <see cref="TagInteractionRule"/> list and the held upgrades' generic
    /// <see cref="UpgradeStatModifier"/>s, switching on GENERIC enums, never on a specific ability or
    /// upgrade identity (a reviewer-blocking requirement, CLAUDE.md §3.8).
    ///
    /// Task 19 adds two optional, data-flagged on-hit payloads: a Frost STACKING effect (basic) and an
    /// arena Slow+DoT ZONE payload (ultimate), plus the held-upgrade-driven Chain Frost spread and
    /// Ultimate Freeze behaviours — all keyed off SO/upgrade data, not the Frost Warden specifically.
    /// </summary>
    public sealed class AbilityRuntime : IAbility
    {
        private float _cooldownTimer;
        private float _cooldownDuration; // full cooldown last applied; drives the charge-bar progress (Task 21)
        private readonly AbilityRole _role;

        // Reusable buffer so AoE target collection doesn't allocate, and (crucially) so we never
        // mutate the live active-enemy list while enumerating it — kills remove enemies from it.
        private readonly List<EnemyRuntime> _aoeBuffer = new List<EnemyRuntime>();

        public AbilityDefinitionSO Definition { get; }
        public int CurrentLevel { get; private set; } = 1;
        public bool IsReady => _cooldownTimer <= 0f;

        /// <summary>Charge progress in [0,1] for UI (Task 21): 0 right after a cast, climbing to 1 when
        /// the ability is ready again. Derived purely from the live cooldown state — no parallel timer.
        /// Reads 1 before the first cast (no cooldown has been applied yet → ready).</summary>
        public float CooldownProgress01 =>
            _cooldownDuration <= 0f ? 1f : Mathf.Clamp01(1f - _cooldownTimer / _cooldownDuration);

        public AbilityRuntime(AbilityDefinitionSO definition, AbilityRole role = AbilityRole.Basic)
        {
            Definition = definition;
            _role = role;
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
        /// tag interactions × held-upgrade modifiers × consumable × equipped-gear modifiers). Handy for
        /// debug logs/tooltips; does not mutate state.</summary>
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

            // Task 31 (Permafrost Eruption): an ability can deal a FRACTION of the caster's current basic
            // damage instead of its own base. context.BasicDamage already includes the basic's modifiers.
            if (Definition.DamageScalesWithBasicFraction > 0f)
            {
                damage = context.BasicDamage * Definition.DamageScalesWithBasicFraction;
            }

            int maxTargets = ResolveMaxTargets(context.Upgrades); // Task 31 (Wider Burst); 0 = unlimited

            // Task 31 (Pass 2): a zone-payload ability (Frost Zone) places a PERSISTENT zone on cast that
            // handles slow / Zone Pulse / Absolute Zero over its lifetime — instead of an instant AoE. Falls
            // through to the legacy cast-time behaviour only when no zone system is wired (older scenes).
            if (Definition.AppliesZonePayload && context.Zones != null)
            {
                SpawnFrostZone(context);
                _cooldownTimer = cooldown;
                _cooldownDuration = cooldown;
                return;
            }

            // Task 23: crit is the FINAL multiplicative step in the damage pipeline — a per-cast roll on
            // the fully-modified damage. Rolled here (at execution), NOT in ComputeStats, so deterministic
            // previews (GetEffectiveDamage / ResolveStats) never randomly crit. Defaults to no-op (0% chance).
            damage = RollCrit(damage, context.Consumables);

            bool hitSomething;
            switch (Definition.TargetingType)
            {
                case AbilityTargetingType.AreaOfEffect:
                    hitSomething = ExecuteAreaOfEffect(context, range, damage, maxTargets);
                    break;
                case AbilityTargetingType.TargetedAreaOfEffect:
                    // Task 20: `range` here is the resolved BLAST radius (ComputeStats bases it on
                    // AoeRadius for this mode, so all radius modifiers/tag rules hit the blast). The cast
                    // distance to find a target is the raw SO Range field.
                    hitSomething = ExecuteTargetedAreaOfEffect(context, Definition.Range, range, damage, maxTargets);
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
                _cooldownDuration = cooldown; // remember the full duration so the UI can show fill progress
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
            OnHit(nearest, context, damage);

            // Task 08: fire the visual from the SAME resolved target used for damage (not a separate
            // targeting pass), so the indicator can never disagree with where damage actually landed.
            context.Feedback?.OnSingleTargetHit(context.CasterPosition, nearest.Transform.position);
            return true;
        }

        private bool ExecuteAreaOfEffect(AbilityExecutionContext context, float radius, float damage, int maxTargets)
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
            LimitTargets(context.CasterPosition, maxTargets); // Task 31 (Wider Burst) — nearest N when capped

            // Task 08: show the radius indicator with the ACTUAL radius used for the overlap test, at
            // the caster centre — only when the AoE actually connects (mirrors single-target firing on
            // a hit), so an auto-firing AoE basic with no targets in range doesn't spam the ring.
            context.Feedback?.OnAreaOfEffect(context.CasterPosition, radius);

            for (int i = 0; i < _aoeBuffer.Count; i++)
            {
                OnHit(_aoeBuffer[i], context, damage);
            }
            _aoeBuffer.Clear();
            return true;
        }

        // Task 20: ranged impact-AoE. Resolve the nearest enemy within castDistance (same selection as
        // SingleTarget), then apply the effect as an AoE of blastRadius centred on THAT enemy's position
        // — a bolt that explodes on impact, so everything clustered around the aim point is caught, not a
        // blast around the caster. A general mode any future ability can use, not a Frost-Warden special.
        private bool ExecuteTargetedAreaOfEffect(AbilityExecutionContext context, float castDistance,
            float blastRadius, float damage, int maxTargets)
        {
            EnemyRuntime nearest = null;
            float bestSqr = castDistance * castDistance;

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

            // The impact point is the aim target's position; the blast is centred there, NOT on the caster.
            Vector3 impact = nearest.Transform.position;

            // Task 31 (Pass 2): Frozen Ground — a basic hit leaves a slowing ice patch at the impact point
            // (once per cast, not per target). Pure CC: slow only, no pulse/growth.
            if (_role == AbilityRole.Basic && context.Zones != null && context.Upgrades != null &&
                context.Upgrades.TryGetFrozenGround(out float fgRadius, out float fgDuration, out float fgSlow))
            {
                // Frozen Ground stays a circular patch at the impact (Task 33 only changed Frost Zone's shape).
                context.Zones.Spawn(GroundZone.Circle(impact, fgRadius, fgDuration, fgSlow, 0.5f));
            }

            // Snapshot in-range targets first; applying damage can remove enemies from the live list.
            _aoeBuffer.Clear();
            float blastSqr = blastRadius * blastRadius;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if ((enemy.Transform.position - impact).sqrMagnitude <= blastSqr)
                {
                    _aoeBuffer.Add(enemy);
                }
            }

            LimitTargets(impact, maxTargets); // Task 31 (Wider Burst) — keep nearest N to the impact when capped

            // Visual: a bolt from the caster to the impact, then the blast ring at the impact point using
            // the ACTUAL radius used for the overlap test (so feedback matches where damage really landed).
            context.Feedback?.OnSingleTargetHit(context.CasterPosition, impact);
            context.Feedback?.OnAreaOfEffect(impact, blastRadius);

            for (int i = 0; i < _aoeBuffer.Count; i++)
            {
                OnHit(_aoeBuffer[i], context, damage);
            }
            _aoeBuffer.Clear();
            return true;
        }

        // Unified per-target resolution: direct damage, then the data-flagged on-hit payloads. Each
        // payload no-ops if the target was just killed (the underlying EnemyRuntime calls guard a
        // resolved enemy), so ordering damage first is safe.
        private void OnHit(EnemyRuntime target, AbilityExecutionContext context, float damage)
        {
            if (damage > 0f)
            {
                // Task 31 (Shattering Impact): bonus damage against a target ALREADY impaired by Slow/Freeze/
                // Frost — applied to THIS hit, never as a separate tick. Checked before frost from this hit is
                // applied, so it keys off the target's pre-existing CC.
                float finalDamage = damage;
                if (context.Upgrades != null && target.IsImpaired)
                {
                    float bonus = context.Upgrades.BonusDamageVsImpaired();
                    if (bonus > 0f) finalDamage *= 1f + bonus;
                }
                target.TakeDamage(finalDamage);
            }

            ApplyZonePayload(target, context);          // Task 19: ultimate Slow zone (DoT removed in Task 31)
            ApplyFrostStack(target, context);           // Task 19: basic Frost stacking
            ApplyHeldStatusEffects(target, context.Upgrades); // Task 11: held status-upgrade payloads
            ApplyBaselineStatus(target);                // Task 31: ability's own status (apex freeze)
            ApplyHardFreeze(target, context);           // Task 31: basic chance-to-hard-freeze
        }

        // Task 31 (Wider Burst): when an AoE is capped at maxTargets, keep only the nearest N to the blast
        // centre. 0 = unlimited. Operates on the already-collected _aoeBuffer.
        private void LimitTargets(Vector3 center, int maxTargets)
        {
            if (maxTargets <= 0 || _aoeBuffer.Count <= maxTargets) return;
            _aoeBuffer.Sort((a, b) =>
                (a.Transform.position - center).sqrMagnitude.CompareTo(
                (b.Transform.position - center).sqrMagnitude));
            _aoeBuffer.RemoveRange(maxTargets, _aoeBuffer.Count - maxTargets);
        }

        // Task 33: place the persistent Frost Zone as a FULL-WIDTH band in front of the wall — independent
        // of the caster's position. Slow + duration come from ResolveZonePayload (Deepening Frost sets slow;
        // Lingering Chill adds duration); Zone Pulse + Absolute Zero (duration extension) come from held
        // upgrades. The band depth is the ability's AoeRadius; X is unconstrained (full arena width).
        private void SpawnFrostZone(AbilityExecutionContext context)
        {
            ResolveZonePayload(context.Upgrades, context.Consumables, out float slow, out float duration, out _);
            float depth = Definition.AoeRadius > 0f ? Definition.AoeRadius : 6f;

            // Band of `depth` extending from the defended line (wall) toward the spawn side.
            float wallZ = context.DefendedLineZ;
            float sign = context.ApproachDirectionZ >= 0f ? 1f : -1f;
            float minZ = Mathf.Min(wallZ, wallZ + sign * depth);
            float maxZ = Mathf.Max(wallZ, wallZ + sign * depth);

            float pulseInterval = 0f, pulseFraction = 0f;
            float extendPerDeath = 0f, capBonus = 0f;
            if (context.Upgrades != null)
            {
                context.Upgrades.TryGetZonePulse(out pulseInterval, out pulseFraction);
                context.Upgrades.TryGetZoneDurationExtend(out extendPerDeath, out capBonus);
            }
            // Absolute Zero cap: remaining duration may extend only up to the cast duration + cap headroom.
            float maxDuration = extendPerDeath > 0f ? duration + capBonus : duration;

            context.Zones.Spawn(GroundZone.Box(
                minZ, maxZ, duration, maxDuration, slow, 0.5f, pulseInterval, pulseFraction, extendPerDeath));

            Debug.Log($"[AbilityRuntime] {Definition.AbilityName}: Frost Zone (full-width band " +
                      $"z=[{minZ:0.#},{maxZ:0.#}], slow={slow:0.##}, dur={duration:0.#}s, capDur={maxDuration:0.#}s).");
        }

        // Task 31: base AoE target cap + the basic-role Wider Burst modifier. 0 = unlimited.
        private int ResolveMaxTargets(UpgradeInventory upgrades)
        {
            float cap = Definition.MaxTargets;
            if (upgrades != null && _role == AbilityRole.Basic)
                cap = upgrades.ResolveModifier(UpgradeModifierTarget.BasicMaxTargets, cap);
            int rounded = Mathf.RoundToInt(cap);
            return rounded < 0 ? 0 : rounded;
        }

        // Task 31: the ability's OWN status on hit (no held upgrade) — used by apex abilities such as
        // Remorseless Winter's freeze. No-op on a target the damage just killed (ApplyStatusEffect guards it).
        private void ApplyBaselineStatus(EnemyRuntime target)
        {
            if (!Definition.AppliesBaselineStatus || Definition.BaselineStatusDuration <= 0f || target == null) return;
            target.ApplyStatusEffect(
                Definition.BaselineStatusType, Definition.BaselineStatusMagnitude, Definition.BaselineStatusDuration);
        }

        // Task 31 (Hard Freeze): a basic hit has a held-upgrade chance to fully freeze (hard stun) the target.
        private void ApplyHardFreeze(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (_role != AbilityRole.Basic || context.Upgrades == null || target == null) return;
            if (!context.Upgrades.TryGetHardFreeze(out float chance, out float duration)) return;
            if (duration > 0f && Random.value < chance)
            {
                target.ApplyStatusEffect(StatusEffectType.Freeze, 0f, duration);
            }
        }

        // Task 19: apply/refresh the Frost stack on a hit, resolving the effective config from the SO
        // base + held upgrade modifiers. If the hit pushes the enemy to max (Freeze trigger) AND the
        // player holds a Chain-Frost upgrade, spread stacks to nearby enemies.
        private void ApplyFrostStack(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (!Definition.AppliesFrostStack || target == null) return;

            ResolveFrostConfig(context.Upgrades, context.Consumables, out float perStackSlow,
                out int maxStacks, out float decay, out float freezeDuration);
            int stacksPerHit = Definition.FrostStacksPerHit;

            bool triggered = target.ApplyStack(StackingEffectType.Frost, stacksPerHit, perStackSlow,
                maxStacks, decay, freezeDuration);

            if (triggered && context.Upgrades != null &&
                context.Upgrades.TryGetChainSpread(out int chainStacks, out float chainRadius))
            {
                SpreadFrost(target, context, chainStacks, chainRadius, perStackSlow, maxStacks, decay, freezeDuration);
            }
        }

        // Chain Frost (Defender): when a max-stack Freeze triggers, push extra stacks to other enemies
        // within radius of the triggering enemy. One level only (spread hits don't themselves re-spread).
        private void SpreadFrost(EnemyRuntime origin, AbilityExecutionContext context, int chainStacks,
            float chainRadius, float perStackSlow, int maxStacks, float decay, float freezeDuration)
        {
            if (chainStacks <= 0 || chainRadius <= 0f) return;
            float radiusSqr = chainRadius * chainRadius;
            var originPos = origin.Transform.position;
            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e == origin || !e.IsAlive) continue;
                if ((e.Transform.position - originPos).sqrMagnitude > radiusSqr) continue;
                e.ApplyStack(StackingEffectType.Frost, chainStacks, perStackSlow, maxStacks, decay, freezeDuration);
            }
        }

        private void ResolveFrostConfig(UpgradeInventory upgrades, ConsumableInventory consumables,
            out float perStackSlow, out int maxStacks, out float decay, out float freezeDuration)
        {
            perStackSlow = Definition.FrostPerStackSlow;
            if (consumables != null) perStackSlow += consumables.FrostPerStackSlowBonus(); // Task 23: Frost Potion
            decay = Definition.FrostDecayInterval;
            float baseMax = Definition.FrostMaxStacks;
            float baseFreeze = Definition.FrostTriggerFreezeDuration;

            if (upgrades != null)
            {
                baseMax = upgrades.ResolveModifier(UpgradeModifierTarget.FrostMaxStacks, baseMax);
                baseFreeze = upgrades.ResolveModifier(UpgradeModifierTarget.FrostFreezeDuration, baseFreeze);
            }

            maxStacks = Mathf.Max(1, Mathf.RoundToInt(baseMax));
            freezeDuration = Mathf.Max(0f, baseFreeze);
        }

        // Task 19: the zone ultimate's baseline Slow + DoT payload, applied to each enemy in range for
        // the (upgrade-modified) duration — independent of any held status-upgrades. Slow magnitude
        // layers held UltimateSlowMagnitude modifiers AND the Slow-tag TagInteractionRule (generic).
        private void ApplyZonePayload(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (!Definition.AppliesZonePayload || target == null) return;

            ResolveZonePayload(context.Upgrades, context.Consumables, out float slow, out float duration, out float dotPerSecond);

            if (slow > 0f && duration > 0f)
                target.ApplyStatusEffect(StatusEffectType.Slow, slow, duration);

            // DoT delivered as Burn (the generic DoT), converting damage-per-second to per-tick.
            if (dotPerSecond > 0f && duration > 0f)
            {
                float perTick = dotPerSecond * EnemyRuntime.BurnTickInterval;
                target.ApplyStatusEffect(StatusEffectType.Burn, perTick, duration);
            }

            // Ultimate Freeze (Mage): heavily-frosted enemies are frozen on cast. "Per tick" in the
            // design is simplified to on-cast here, since the DoT ticks inside EnemyRuntime with no
            // per-tick ability callback (documented decision, Task 19).
            if (context.Upgrades != null &&
                context.Upgrades.TryGetUltimateFreeze(out int threshold, out float freezeDur) &&
                target.GetStackCount(StackingEffectType.Frost) >= threshold && freezeDur > 0f)
            {
                target.ApplyStatusEffect(StatusEffectType.Freeze, 0f, freezeDur);
            }
        }

        // Resolve the zone payload's effective Slow magnitude, duration and DoT/s (Task 19) from the SO
        // base + held upgrade modifiers + Slow-tag interactions. Shared by the live cast (ApplyZonePayload)
        // and the read-only stat snapshot (ResolveStats), so the panel and the real cast never disagree.
        private void ResolveZonePayload(UpgradeInventory upgrades, ConsumableInventory consumables,
            out float slow, out float duration, out float dotPerSecond)
        {
            duration = Definition.ZoneDuration;
            slow = Definition.ZoneSlowMagnitude;
            if (upgrades != null)
            {
                duration = upgrades.ResolveModifier(UpgradeModifierTarget.UltimateDuration, duration);
                slow = upgrades.ResolveModifier(UpgradeModifierTarget.UltimateSlowMagnitude, slow);
            }
            if (consumables != null) duration += consumables.UltimateDurationBonus(); // Task 23: Ultimate Duration Potion
            slow = ApplySlowTagInteractions(slow, upgrades);
            slow = Mathf.Clamp01(slow);
            duration = Mathf.Max(0f, duration);
            dotPerSecond = Definition.ZoneDotDamagePerSecond;
        }

        // Task 23: roll the final crit step. Reads crit chance/damage from the consumable aggregates (the
        // only crit source for now; upgrades/gear could feed the same getters later). No-op at 0% chance.
        private float RollCrit(float damage, ConsumableInventory consumables)
        {
            if (consumables == null || damage <= 0f) return damage;

            float chance = consumables.TotalCritChance();
            if (chance <= 0f) return damage;

            if (Random.value < chance)
            {
                float critted = damage * (1f + consumables.TotalCritDamageBonus());
                Debug.Log($"[AbilityRuntime] {Definition.AbilityName} CRIT! {damage:0.#} → {critted:0.#}");
                return critted;
            }
            return damage;
        }

        /// <summary>Task 22: a read-only snapshot of this ability's FINAL stats, computed through the
        /// SAME helpers the live cast uses (ComputeStats / ResolveZonePayload / ResolveFrostConfig). No
        /// re-derivation, so the stat panel always matches actual execution.</summary>
        public AbilityStats ResolveStats(UpgradeInventory upgrades, ConsumableInventory consumables,
            IReadOnlyList<StatModifier> equippedModifiers)
        {
            ComputeStats(upgrades, consumables, equippedModifiers,
                out float damage, out float cooldown, out float range);

            bool targeted = Definition.TargetingType == AbilityTargetingType.TargetedAreaOfEffect;
            float castDistance = targeted ? Definition.Range : range;

            bool hasZone = Definition.AppliesZonePayload;
            float slow = 0f, duration = 0f, dotPerSecond = 0f;
            if (hasZone) ResolveZonePayload(upgrades, consumables, out slow, out duration, out dotPerSecond);

            bool hasFrost = Definition.AppliesFrostStack;
            float frostPerStackSlow = 0f, frostFreeze = 0f;
            int frostMax = 0;
            if (hasFrost) ResolveFrostConfig(upgrades, consumables, out frostPerStackSlow, out frostMax, out _, out frostFreeze);

            return new AbilityStats(
                Definition, Definition.TargetingType,
                damage, cooldown, range, castDistance,
                IsReady, CooldownProgress01,
                hasZone, slow, duration, dotPerSecond,
                hasFrost, frostMax, frostFreeze, frostPerStackSlow);
        }

        // Walk the ability's TagInteractionRules for SlowMagnitudeMultiplier entries whose tag the
        // player holds (generic; the Frost Warden ultimate authors a Slow-tag → ×1.1 rule).
        private float ApplySlowTagInteractions(float slow, UpgradeInventory upgrades)
        {
            var rules = Definition.TagInteractionRules;
            if (rules == null || upgrades == null) return slow;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || rule.ModifierType != AbilityModifierType.SlowMagnitudeMultiplier) continue;
                if (!upgrades.HasTag(rule.MatchTag)) continue;
                slow *= rule.ModifierValue;
            }
            return slow;
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
        // → role-targeted held-upgrade modifiers → consumable modifiers → equipped gear/artifact
        // modifiers. Nothing written back to the SO; each source is just another layer feeding the SAME
        // AbilityModifierType switch.
        private void ComputeStats(UpgradeInventory upgrades, ConsumableInventory consumables,
            IReadOnlyList<StatModifier> equippedModifiers,
            out float damage, out float cooldown, out float range)
        {
            var level = CurrentLevelEntry();
            damage = Definition.BaseDamage * level.DamageMultiplier;
            cooldown = Definition.BaseCooldown * level.CooldownMultiplier;

            // `range` is the mode's primary AoE/acquisition radius, and is what every radius modifier
            // (level RangeMultiplier, AoE-tag TagInteractionRule, BasicRadius upgrade, gear) scales. For
            // TargetedAreaOfEffect (Task 20) that primary radius is the impact BLAST (AoeRadius); the cast
            // distance is read raw from Definition.Range in Execute. So the AoE-tag / Wider Bolt modifiers
            // correctly grow the blast, not the reach.
            float radiusBase = Definition.TargetingType == AbilityTargetingType.TargetedAreaOfEffect
                ? Definition.AoeRadius
                : Definition.Range;
            range = radiusBase * level.RangeMultiplier;

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

            // Task 19: role-targeted held-upgrade modifiers (e.g. basic damage/cooldown/radius). Resolved
            // only for the matching role so an upgrade can't leak across the basic/ultimate boundary.
            if (upgrades != null && _role == AbilityRole.Basic)
            {
                damage = upgrades.ResolveModifier(UpgradeModifierTarget.BasicDamage, damage);
                cooldown = upgrades.ResolveModifier(UpgradeModifierTarget.BasicCooldown, cooldown);
                range = upgrades.ResolveModifier(UpgradeModifierTarget.BasicRadius, range);
            }

            // Consumable shop bonuses (Task 06): the same pipeline, just another modifier source —
            // not a parallel damage path. Flat damage adds; cooldown reduction multiplies.
            if (consumables != null)
            {
                damage += consumables.TotalFlatDamageBonus();
                damage += consumables.TotalElementalLightningBonus(); // Task 23: Lightning placeholder (all abilities)
                if (_role == AbilityRole.Basic) damage += consumables.BasicDamageBonus(); // Task 23: basic-only
                cooldown *= consumables.CooldownMultiplier();
                range += consumables.AoeRadiusBonus(); // Task 30: migrated generic AoE-radius upgrade (flat metres)
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
        // every modifier source resolves identically (CLAUDE.md §3.8 generic modifier types). Modifier
        // kinds that don't affect damage/cooldown/range (e.g. SlowMagnitudeMultiplier) are resolved in
        // their own dedicated path and ignored here.
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
