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

        // Task 35: reusable buffer for Chain Lightning jump-target collection (same no-alloc rationale).
        private readonly List<EnemyRuntime> _chainBuffer = new List<EnemyRuntime>();

        // Task 46: per-hit proc flags, written by the payload helpers during a single OnHit and read by the
        // lightning-VFX call right after, so a strike's flash reflects what ACTUALLY procced on that hit
        // (Overcharge spike / Execute) without changing OnHit's signature or adding a parallel path.
        private bool _lastHitSpike;
        private bool _lastHitExecute;

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
            damage = RollCrit(damage, context.Consumables, context.Upgrades, out bool critted);

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
                    hitSomething = ExecuteSingleTarget(context, range, damage, critted);
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

        private bool ExecuteSingleTarget(AbilityExecutionContext context, float range, float damage, bool critted)
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

            // Task 47: an apex talent gets ONE high-impact effect per cast (and the shared weight treatment),
            // not the per-hit Basic/Ultimate visuals — so suppress the basic-tier beam/lightning here.
            // Task 46: otherwise route to the lightning presenter when the data flag says so (Bolt Striker);
            // keep the diagnostic beam for anything else. No ability identity is hardcoded.
            bool apex = _role == AbilityRole.Apex;
            bool lightning = !apex && Definition.VfxStyle == AbilityVfxStyle.Lightning;

            // Task 35: a single-target cast can strike its target multiple times (Multi-Strike upgrade on the
            // ultimate, or an ability-baked _hitCount like Thunderstorm). Each strike is a full OnHit so the
            // per-hit payloads (Execute, Overload, etc.) resolve per strike. 1 hit = the pre-Task-35 behaviour.
            ResolveMultiHit(context.Upgrades, out int hits, out float hitFraction);
            float perHitDamage = damage * hitFraction;
            for (int i = 0; i < hits; i++)
            {
                OnHit(nearest, context, perHitDamage);

                // Task 46: one lightning flash PER actual strike (so Multi-Strike's hit count is visible),
                // styled by what really procced on this hit (crit/spike/execute) and the role (ultimate =
                // heavier). Fired inside the loop so a target that dies mid-burst doesn't draw phantom hits.
                if (lightning)
                    context.Feedback?.OnLightningStrike(context.CasterPosition, nearest.Transform.position,
                        BuildLightningFlags(critted));

                if (!nearest.IsAlive) break; // target died mid-burst — stop wasting strikes on it
            }

            // Task 47: one high-impact apex effect at the resolved target (clearly exceeding Basic/Ultimate).
            // Task 08: otherwise fire the visual from the SAME resolved target used for damage (not a separate
            // targeting pass), so the indicator can never disagree with where damage actually landed.
            if (apex)
                context.Feedback?.OnApexImpact(nearest.Transform.position, ApexVisualRadius(), ResolveApexStyle());
            else if (!lightning)
                context.Feedback?.OnSingleTargetHit(context.CasterPosition, nearest.Transform.position);

            // Task 35 (Chain Lightning / Thunderstorm): the bolt jumps to nearby OTHER enemies for a fraction
            // of the per-hit damage. Single-/double-jump only (nearest N) — NOT an AoE; it is pure damage, so
            // it never re-applies the primary's on-hit payloads (e.g. Static Charge, which is target-specific).
            ResolveChain(context.Upgrades, out int jumps, out float chainFraction);
            if (jumps > 0 && chainFraction > 0f)
                ChainJumps(nearest, context, perHitDamage * chainFraction, jumps);

            return true;
        }

        // Task 46: assemble the lightning-strike style for the strike just resolved, from state the runtime
        // already computed: role (ultimate = heavier), the per-cast crit roll, and this hit's Overcharge-spike
        // / Execute procs. Presentation-free — the presenter maps these flags to thickness/colour/scale.
        private LightningStrikeFlags BuildLightningFlags(bool critted)
        {
            var flags = LightningStrikeFlags.None;
            if (_role == AbilityRole.Ultimate) flags |= LightningStrikeFlags.Ultimate;
            if (critted) flags |= LightningStrikeFlags.Crit;
            if (_lastHitSpike) flags |= LightningStrikeFlags.Spike;
            if (_lastHitExecute) flags |= LightningStrikeFlags.Execute;
            return flags;
        }

        // Task 47: visual spread radius for a single-target apex effect (nova/storm/execute). The gameplay is
        // single-target; this only sizes the VFX bigger than a Basic/Ultimate burst.
        private float ApexVisualRadius() => Mathf.Max(3.5f, Definition.AoeRadius);

        // Task 47: pick the apex VFX from the apex's existing data — palette from AbilityVfxStyle, shape from
        // targeting + the finisher (ConsumesStaticCharge) flag. No ability identity is hardcoded.
        private ApexVfxStyle ResolveApexStyle()
        {
            bool frost = Definition.VfxStyle == AbilityVfxStyle.Frost;
            if (Definition.TargetingType == AbilityTargetingType.AreaOfEffect)
                return frost ? ApexVfxStyle.FrostShockwave : ApexVfxStyle.LightningStorm;
            if (Definition.ConsumesStaticCharge)
                return ApexVfxStyle.LightningExecute; // Lethal Surge — the finisher
            return frost ? ApexVfxStyle.FrostNova : ApexVfxStyle.LightningStorm;
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

            // Task 47: an AoE apex (Permafrost Eruption) shows a radius-filling shockwave so its area reads at a
            // glance; otherwise (Task 08) show the diagnostic ring. Both use the ACTUAL radius from the overlap
            // test, only when the AoE actually connects.
            if (_role == AbilityRole.Apex)
                context.Feedback?.OnApexImpact(context.CasterPosition, radius, ResolveApexStyle());
            else
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
                // Task 45: a persistent ice-patch decal for the patch's actual radius/duration (distinct from
                // the one-shot impact burst below).
                context.Feedback?.OnGroundPatch(impact, fgRadius, fgDuration);
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

            // Visual: the frost basic (data-flagged AppliesFrostStack) shows a travelling ice projectile +
            // crystallization burst sized to the ACTUAL blast radius (so Wider Burst grows it); any other
            // TargetedAoE keeps the generic diagnostic beam + ring. Both use the real impact + radius, so
            // feedback can never disagree with where damage landed.
            if (Definition.AppliesFrostStack)
            {
                context.Feedback?.OnRangedImpactBurst(context.CasterPosition, impact, blastRadius);
            }
            else
            {
                context.Feedback?.OnSingleTargetHit(context.CasterPosition, impact);
                context.Feedback?.OnAreaOfEffect(impact, blastRadius);
            }

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
            // Task 46: clear the per-hit proc flags so they reflect ONLY what happens during this hit
            // (read by the lightning-VFX call right after OnHit returns).
            _lastHitSpike = false;
            _lastHitExecute = false;

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

                // Task 35 — Bolt Striker per-hit damage modifiers. Each is guarded by role + held data, so
                // it is a no-op for any other hero/ability (generic, never keyed on hero identity).
                finalDamage = ApplyStaticCharge(target, context, finalDamage);        // Basic: consecutive-hit stacks
                finalDamage = ApplyOverchargeSpike(context, finalDamage);             // Basic: independent spike roll
                finalDamage = ApplyExecuteBonus(target, context, finalDamage);        // Ultimate: vs low-HP targets
                finalDamage = ApplyApexFinisherBonuses(target, context, finalDamage); // Apex: Lethal Surge
                finalDamage = ApplyComboConsume(target, context, finalDamage);        // Task 38: Frozen Lightning amp

                DealDamage(target, context, finalDamage);
            }

            ApplyZonePayload(target, context);          // Task 19: ultimate Slow zone (DoT removed in Task 31)
            ApplyFrostStack(target, context);           // Task 19: basic Frost stacking
            ApplyHeldStatusEffects(target, context.Upgrades); // Task 11: held status-upgrade payloads
            ApplyBaselineStatus(target);                // Task 31: ability's own status (apex freeze)
            ApplyComboPrime(target, context);           // Task 38: Frozen Lightning prime (after the freeze lands)
            ApplyHardFreeze(target, context);           // Task 31: basic chance-to-hard-freeze
            ApplyPiercingBolt(target, context);         // Task 35: basic temporary Armor reduction (Task 34 mechanism)
            ApplyOverload(target, context);             // Task 35: ultimate generic vulnerability
        }

        // Task 34/35: the actual damage application — diminishing-returns mitigation (Task 34) then
        // EnemyRuntime.TakeDamage (which applies the Task 35 Overload vulnerability as its final stage).
        // Chain jumps call this directly so they take mitigation/vulnerability but NONE of the on-hit payloads.
        private void DealDamage(EnemyRuntime target, AbilityExecutionContext context, float rawDamage)
        {
            if (target == null || rawDamage <= 0f) return;
            target.TakeDamage(MitigateDamage(target, rawDamage));
        }

        // Task 35: resolve repeated single-target strikes. Default 1 hit at full damage; the ability can bake
        // a multi-hit (Thunderstorm), and the Ultimate's Multi-Strike upgrade overrides both at runtime.
        private void ResolveMultiHit(UpgradeInventory upgrades, out int hits, out float fraction)
        {
            hits = Mathf.Max(1, Definition.HitCount);
            fraction = Definition.HitDamageFraction <= 0f ? 1f : Definition.HitDamageFraction;
            if (_role == AbilityRole.Ultimate && upgrades != null &&
                upgrades.TryGetMultiStrike(out int n, out float f))
            {
                hits = Mathf.Max(1, n);
                fraction = f;
            }
        }

        // Task 35: resolve chain-jump config. A baked chain (Thunderstorm) is the ability's own behaviour;
        // otherwise the Basic reads its held Chain Lightning upgrade. No chain for anything else.
        private void ResolveChain(UpgradeInventory upgrades, out int jumps, out float fraction)
        {
            if (Definition.ChainJumps > 0 && Definition.ChainDamageFraction > 0f)
            {
                jumps = Definition.ChainJumps;
                fraction = Definition.ChainDamageFraction;
                return;
            }
            if (_role == AbilityRole.Basic && upgrades != null &&
                upgrades.TryGetChainLightning(out int j, out float f))
            {
                jumps = j;
                fraction = f;
                return;
            }
            jumps = 0;
            fraction = 0f;
        }

        // Task 35: deal chainDamage to the nearest `jumps` alive enemies within ChainRange of the origin
        // (excluding the origin). Single/double jump only — explicitly NOT an AoE.
        private void ChainJumps(EnemyRuntime origin, AbilityExecutionContext context, float chainDamage, int jumps)
        {
            if (chainDamage <= 0f || jumps <= 0) return;

            float chainRange = Definition.ChainRange > 0f ? Definition.ChainRange : 8f;
            float rangeSqr = chainRange * chainRange;
            var originPos = origin.Transform.position;

            _chainBuffer.Clear();
            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e == origin || !e.IsAlive) continue;
                if ((e.Transform.position - originPos).sqrMagnitude <= rangeSqr) _chainBuffer.Add(e);
            }
            if (_chainBuffer.Count == 0) return;

            if (_chainBuffer.Count > jumps)
            {
                _chainBuffer.Sort((a, b) =>
                    (a.Transform.position - originPos).sqrMagnitude.CompareTo(
                    (b.Transform.position - originPos).sqrMagnitude));
                _chainBuffer.RemoveRange(jumps, _chainBuffer.Count - jumps);
            }

            // Task 47: an apex (Thunderstorm) renders its own storm in OnApexImpact, so don't draw per-jump
            // basic-tier bolts here. Task 46: the basic's Chain Lightning draws a distinct bolt per actual jump.
            bool apex = _role == AbilityRole.Apex;
            bool lightning = Definition.VfxStyle == AbilityVfxStyle.Lightning;
            for (int i = 0; i < _chainBuffer.Count; i++)
            {
                var jump = _chainBuffer[i];
                DealDamage(jump, context, chainDamage); // pure damage — no Static Charge/spike/piercing re-trigger
                if (apex) continue; // storm visual handles apex chain feedback
                if (lightning) context.Feedback?.OnChainJump(originPos, jump.Transform.position);
                else context.Feedback?.OnSingleTargetHit(originPos, jump.Transform.position);
            }
            _chainBuffer.Clear();
        }

        // Task 35 (Static Charge — Basic): consecutive hits on the same target stack a damage bonus; switching
        // targets resets the combo (tracked in the shared HeroCombatState so Lethal Surge can consume it).
        private float ApplyStaticCharge(EnemyRuntime target, AbilityExecutionContext context, float damage)
        {
            if (_role != AbilityRole.Basic || context.Upgrades == null || context.CombatState == null) return damage;
            if (!context.Upgrades.TryGetStaticCharge(out float perStack, out int maxStacks)) return damage;

            int bonusStacks = context.CombatState.RegisterBasicHit(target, maxStacks);
            if (bonusStacks > 0 && perStack > 0f) damage *= 1f + perStack * bonusStacks;
            return damage;
        }

        // Task 35 (Overcharge — Basic): a separate, independently-rolled chance for a one-time damage spike on
        // top of the (already crit-resolved) hit. Distinct from the crit-chance bonus, which feeds RollCrit.
        private float ApplyOverchargeSpike(AbilityExecutionContext context, float damage)
        {
            if (_role != AbilityRole.Basic || context.Upgrades == null) return damage;
            if (!context.Upgrades.TryGetOverchargeSpike(out float chance, out float bonus)) return damage;

            if (Random.value < chance)
            {
                Debug.Log($"[AbilityRuntime] {Definition.AbilityName} OVERCHARGE SPIKE +{bonus * 100f:0}%!");
                damage *= 1f + bonus;
                _lastHitSpike = true; // Task 46: flag this hit for the more-intense spike flash
            }
            return damage;
        }

        // Task 35 (Execute — Ultimate): bonus damage against a target below the held Execute upgrade's HP%.
        private float ApplyExecuteBonus(EnemyRuntime target, AbilityExecutionContext context, float damage)
        {
            if (_role != AbilityRole.Ultimate || context.Upgrades == null || target == null) return damage;
            if (!context.Upgrades.TryGetExecute(out float threshold, out float bonus)) return damage;
            if (target.MaxHealth > 0f && target.CurrentHealth / target.MaxHealth < threshold)
            {
                damage *= 1f + bonus;
                _lastHitExecute = true; // Task 46: flag this hit for the distinct Execute flash
            }
            return damage;
        }

        // Task 35 (Lethal Surge apex): +bonus per CONSUMED Static Charge stack, and an extra bonus if the
        // target is below the held Execute upgrade's threshold. Both are ability-baked (apex data), reading
        // the same shared combat state / held Execute the basic + ultimate use.
        private float ApplyApexFinisherBonuses(EnemyRuntime target, AbilityExecutionContext context, float damage)
        {
            if (Definition.ConsumesStaticCharge && context.CombatState != null &&
                Definition.StaticChargeConsumeBonusPerStack > 0f)
            {
                int stacks = context.CombatState.ConsumeStaticCharge();
                if (stacks > 0) damage *= 1f + Definition.StaticChargeConsumeBonusPerStack * stacks;
            }

            if (Definition.LowHpExecuteBonus > 0f && target != null && context.Upgrades != null &&
                context.Upgrades.TryGetExecute(out float threshold, out _) &&
                target.MaxHealth > 0f && target.CurrentHealth / target.MaxHealth < threshold)
            {
                damage *= 1f + Definition.LowHpExecuteBonus;
            }
            return damage;
        }

        // Task 38 (Frozen Lightning — passive combo CONSUME): if THIS ability is the consuming apex of an
        // unlocked passive combo (Lethal Surge) AND the target is currently primed, multiply this hit's damage
        // by the combo's multiplier and consume the prime. Applied AFTER the ability's own finisher bonuses
        // (Static Charge / Execute), so it amplifies the fully-resolved damage exactly as specified. Generic —
        // keyed off the run's ComboApexState by AbilityDefinitionSO, never on the Bolt Striker specifically.
        private float ApplyComboConsume(EnemyRuntime target, AbilityExecutionContext context, float damage)
        {
            if (target == null || context.ComboApex == null) return damage;
            if (!context.ComboApex.TryGetConsumeMultiplier(Definition, out float multiplier)) return damage;
            if (multiplier <= 1f || !target.IsPrimed) return damage;
            if (!target.ConsumePrime()) return damage; // someone else consumed it this frame — no double-dip
            Debug.Log($"[AbilityRuntime] FROZEN LIGHTNING! {Definition.AbilityName} consumes a primed target ×{multiplier:0.##}.");
            // Task 47: fire the combo VFX at the EXACT consume moment (frost-blue freeze → gold strike), not a timer.
            context.Feedback?.OnComboFrozenLightning(target.Transform.position);
            return damage * multiplier;
        }

        // Task 38 (Frozen Lightning — passive combo PRIME): if THIS ability is the priming apex of an unlocked
        // passive combo (Remorseless Winter), mark the just-hit target primed for the combo's window. Called in
        // the on-hit payload phase right after the baseline freeze lands, so a freeze and its prime always go
        // together. No-op on a just-killed target (ApplyPrime guards a resolved enemy).
        private void ApplyComboPrime(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (target == null || context.ComboApex == null) return;
            if (context.ComboApex.TryGetPrimeWindow(Definition, out float window))
                target.ApplyPrime(window);
        }

        // Task 35 (Piercing Bolt — Basic): apply Task 34's generic temporary Armor reduction to the target,
        // so its Physical damage taken from ALL sources rises for the duration. No-op on a just-killed target.
        private void ApplyPiercingBolt(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (_role != AbilityRole.Basic || context.Upgrades == null || target == null) return;
            if (context.Upgrades.TryGetPiercingBolt(out float amount, out float duration))
            {
                target.ApplyArmorReduction(amount, duration);
                // Task 46: gold fracture-line debuff indicator on the target for the ACTUAL debuff duration.
                context.Feedback?.OnArmorBreak(target.Transform, duration);
            }
        }

        // Task 35 (Overload — Ultimate): apply the generic incoming-damage vulnerability (separate from the
        // Armor-reduction mechanism) so the target takes more from all sources for the duration.
        private void ApplyOverload(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (_role != AbilityRole.Ultimate || context.Upgrades == null || target == null) return;
            if (context.Upgrades.TryGetOverload(out float bonus, out float duration))
            {
                target.ApplyVulnerability(bonus, duration);
                // Task 46: pulsing gold ring debuff indicator — distinct from Piercing Bolt's fracture lines.
                context.Feedback?.OnVulnerability(target.Transform, duration);
            }
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

            // Task 45: persistent band visual (activation flash + ambient mist + pulse flashes). The handle is
            // owned by the zone, so the band's lifetime matches the real zone (including Absolute Zero
            // extension) and pulses flash on the zone's ACTUAL pulse cadence — no parallel timer.
            var zoneVisual = context.Feedback?.BeginZone(minZ, maxZ);

            context.Zones.Spawn(GroundZone.Box(
                minZ, maxZ, duration, maxDuration, slow, 0.5f, pulseInterval, pulseFraction, extendPerDeath, zoneVisual));

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

        // Task 34: diminishing-returns mitigation — the final pipeline step. Picks the enemy defence stat
        // matching THIS ability's DamageType (Armor for Physical, MagicResist for Magical) and applies
        // damageTaken = raw × 100 / (100 + defence). A defence of 0 yields ×1 (no mitigation), so enemies
        // without authored Armor/MagicResist take unchanged damage. Reads EffectiveArmor so any active
        // temporary armor debuff (Task 34) reduces Physical damage from every source while live.
        private float MitigateDamage(EnemyRuntime target, float rawDamage)
        {
            if (target == null || rawDamage <= 0f) return rawDamage;

            float defense = Definition.DamageType == DamageType.Physical
                ? target.EffectiveArmor
                : target.MagicResist;

            if (defense <= 0f) return rawDamage; // no mitigation — preserves pre-Task-34 behaviour
            return rawDamage * (100f / (100f + defense));
        }

        // Task 23: roll the final crit step. Reads crit chance/damage from the consumable aggregates and
        // (Task 35) the held-upgrade crit-chance bonus (Overcharge) — both feed the SAME single roll, never
        // a parallel crit path. No-op at 0% chance.
        private float RollCrit(float damage, ConsumableInventory consumables, UpgradeInventory upgrades, out bool didCrit)
        {
            didCrit = false;
            if (damage <= 0f) return damage;

            float chance = consumables != null ? consumables.TotalCritChance() : 0f;
            if (upgrades != null) chance += upgrades.CritChanceBonus(); // Task 35 (Overcharge)
            chance = Mathf.Clamp01(chance);
            if (chance <= 0f) return damage;

            if (Random.value < chance)
            {
                float critDamageBonus = consumables != null ? consumables.TotalCritDamageBonus() : 0f;
                float critted = damage * (1f + critDamageBonus);
                Debug.Log($"[AbilityRuntime] {Definition.AbilityName} CRIT! {damage:0.#} → {critted:0.#}");
                didCrit = true;
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
            else if (upgrades != null && _role == AbilityRole.Ultimate)
            {
                // Task 35 (Charged Finisher): scales the ultimate's base damage only. Multi-Strike's
                // per-hit fraction is applied later (per hit), so this composes cleanly with it.
                damage = upgrades.ResolveModifier(UpgradeModifierTarget.UltimateDamage, damage);
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
