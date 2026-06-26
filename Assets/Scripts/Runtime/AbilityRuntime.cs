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

        // Task 49: reusable buffer for PiercingLine corridor collection (no-alloc; sorted by distance along the ray).
        private readonly List<EnemyRuntime> _pierceBuffer = new List<EnemyRuntime>();

        // Task 50 (Shatter): separate buffer for the detonation AoE so it never corrupts the _aoeBuffer/_pierceBuffer
        // iteration that OnHit may be running inside when the detonation fires.
        private readonly List<EnemyRuntime> _detonateBuffer = new List<EnemyRuntime>();

        // Task 49 (Minigun / Bullet Storm shot-burst channel): live channel state. While active the ability
        // fires piercing shots on its internal cadence; Tick advances the timers and accumulates how many shot
        // triggers are DUE this frame (fired by Execute, which has the context). Cooldown runs from cast.
        private bool _burstActive;
        private float _burstRemaining;
        private float _burstInterval;
        private float _burstShotTimer;
        private int _pendingTriggers;
        private int _burstShotIndex; // increments per trigger; drives the Bullet Storm arc fan

        // Task 46: per-hit proc flags, written by the payload helpers during a single OnHit and read by the
        // lightning-VFX call right after, so a strike's flash reflects what ACTUALLY procced on that hit
        // (Overcharge spike / Execute) without changing OnHit's signature or adding a parallel path.
        private bool _lastHitSpike;
        private bool _lastHitExecute;

        public AbilityDefinitionSO Definition { get; }
        public int CurrentLevel { get; private set; } = 1;

        // Task 49: a channelling shot-burst is "ready" the whole time it's active, so the hero keeps driving its
        // Execute each frame (to fire the due shots); otherwise the normal cooldown gate applies.
        public bool IsReady => _burstActive || _cooldownTimer <= 0f;

        /// <summary>Task 49: true while a Minigun-style shot-burst is mid-channel (see IAbility).</summary>
        public bool IsChanneling => _burstActive;

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
                _cooldownTimer -= deltaTime; // cooldown runs from cast, concurrently with any active channel
            }

            // Task 49: advance the shot-burst channel and queue the triggers that come due this frame (Execute
            // fires them, since it has the targeting context). The channel ends when its duration runs out.
            if (_burstActive)
            {
                _burstRemaining -= deltaTime;
                _burstShotTimer -= deltaTime;
                while (_burstShotTimer <= 0f)
                {
                    _pendingTriggers++;
                    _burstShotTimer += _burstInterval;
                }
                if (_burstRemaining <= 0f)
                {
                    _burstActive = false;
                    _pendingTriggers = 0; // drop any leftover queued triggers at channel end
                }
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

            // Task 48 (Firewall): a fire-wall ability places a PERSISTENT full-width fire band on cast (sustained
            // DoT + Inferno-Surge burst + Wildfire-Spread death-patches) instead of an instant hit — same pattern
            // as the Frost Zone above. Falls through to normal targeting only when no zone system is wired.
            if (Definition.AppliesFireWall && context.Zones != null)
            {
                SpawnFireWall(context);
                _cooldownTimer = cooldown;
                _cooldownDuration = cooldown;
                return;
            }

            // Task 49 (Minigun / Bullet Storm): a shot-burst ability channels — on cast it goes active and fires
            // piercing shots over its duration; while active it fires the triggers queued by Tick. `damage` is
            // the per-shot damage (Heavy Rounds via the ultimate-damage modifier, or the basic-fraction for the
            // apex). Manages its own cooldown (from cast), so it returns before the normal targeting switch.
            if (Definition.AppliesShotBurst)
            {
                ExecuteShotBurst(context, damage, cooldown);
                return;
            }

            // Task 49 (Executioner's Volley): a single heavy shot at the most-shredded target, scaling with its
            // Armor-Shredder stacks. Manages its own cooldown; returns before the normal targeting switch.
            if (Definition.TargetsHighestShred)
            {
                if (ExecuteExecutionersVolley(context, damage))
                {
                    _cooldownTimer = cooldown;
                    _cooldownDuration = cooldown;
                }
                return;
            }

            // Task 23: crit is the FINAL multiplicative step in the damage pipeline — a per-cast roll on
            // the fully-modified damage. Rolled here (at execution), NOT in ComputeStats, so deterministic
            // previews (GetEffectiveDamage / ResolveStats) never randomly crit. Defaults to no-op (0% chance).
            damage = RollCrit(damage, context.Consumables, context.Upgrades, out bool critted);

            bool hitSomething;
            switch (Definition.TargetingType)
            {
                case AbilityTargetingType.PiercingLine:
                    hitSomething = ExecutePiercingLine(context, range, damage);
                    break;
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
            // Task 51: a fire-styled single-target ability (Pyromancer's Fireball) draws a fireball projectile +
            // impact burst instead of the generic beam; gated on the data flag, never the ability identity.
            bool fire = !apex && Definition.VfxStyle == AbilityVfxStyle.Fire;

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
            else if (fire)
                // Single-target Fireball has no gameplay AoE, so the impact burst uses a small presentation size.
                context.Feedback?.OnFireballImpact(context.CasterPosition, nearest.Transform.position, 1.2f);
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
                finalDamage = ApplyBurningBonus(target, finalDamage);                 // Task 48: Cataclysm vs Burning

                DealDamage(target, context, finalDamage);
                ApplyShatter(target, context, finalDamage); // Task 50: Shatter detonation on a primed Physical hit
            }

            ApplyBurnOnHit(target, context);            // Task 48: Fireball/Wildfire-Apocalypse Burn DoT
            ApplyZonePayload(target, context);          // Task 19: ultimate Slow zone (DoT removed in Task 31)
            ApplyFrostStack(target, context);           // Task 19: basic Frost stacking
            ApplyHeldStatusEffects(target, context.Upgrades); // Task 11: held status-upgrade payloads
            ApplyBaselineStatus(target);                // Task 31: ability's own status (apex freeze)
            ApplyComboPrime(target, context);           // Task 38: Frozen Lightning prime (after the freeze lands)
            ApplyHardFreeze(target, context);           // Task 31: basic chance-to-hard-freeze
            ApplyPiercingBolt(target, context);         // Task 35: basic temporary Armor reduction (Task 34 mechanism)
            ApplyOverload(target, context);             // Task 35: ultimate generic vulnerability
            ApplyArmorShredder(target, context);        // Task 49: basic STACKING Armor Shredder
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
                ApplyChainCombustion(jump, context);    // Task 50: extend+stack Burn on an already-Burning jump target
                if (apex) continue; // storm visual handles apex chain feedback
                if (lightning) context.Feedback?.OnChainJump(originPos, jump.Transform.position);
                else context.Feedback?.OnSingleTargetHit(originPos, jump.Transform.position);
            }
            _chainBuffer.Clear();
        }

        // Task 50 (Chain Combustion): if a Chain Lightning jump hits an already-Burning target and the combo is
        // unlocked, extend that Burn and add one Stacking-Embers-style stack (current Pyromancer tier value), with
        // no Fireball required. Generic — keyed on the run's ComboApexState + the target's Burn state.
        private void ApplyChainCombustion(EnemyRuntime jump, AbilityExecutionContext context)
        {
            if (jump == null || context.ComboApex == null || !jump.IsBurning) return;
            if (!context.ComboApex.TryGetChainCombustion(out float extendSeconds)) return;

            int maxStacks = 0;
            float perStackBonus = 0f;
            context.Upgrades?.TryGetStackingEmbers(out perStackBonus, out maxStacks); // current Stacking Embers tier
            jump.AddBurnStackAndExtend(maxStacks, perStackBonus, extendSeconds);
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

        // Task 50 (Shatter): when a PHYSICAL hit (any Marksman shot) lands on a primed target and a Shatter combo
        // is unlocked, detonate an AoE burst = multiplier × this shot's damage to every enemy within the radius of
        // the primed target, then consume the prime. Generic — keyed on DamageType + the run's ComboApexState,
        // never on the Marksman/Bullet Storm specifically. The burst is unmitigated-free: it routes through
        // DealDamage so each enemy's Armor still mitigates the Physical burst. shotDamage is THIS hit's damage.
        private void ApplyShatter(EnemyRuntime target, AbilityExecutionContext context, float shotDamage)
        {
            if (target == null || shotDamage <= 0f || context.ComboApex == null) return;
            if (Definition.DamageType != DamageType.Physical || !target.IsPrimed) return;
            if (!context.ComboApex.TryGetShatterDetonation(out float radius, out float multiplier)) return;
            if (radius <= 0f || multiplier <= 0f) return;
            if (!target.ConsumePrime()) return; // someone else consumed it this frame — no double-detonate

            float burst = shotDamage * multiplier;
            var center = target.Transform.position;
            float radiusSqr = radius * radius;

            _detonateBuffer.Clear();
            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                if ((e.Transform.position - center).sqrMagnitude <= radiusSqr) _detonateBuffer.Add(e);
            }
            for (int i = 0; i < _detonateBuffer.Count; i++)
                DealDamage(_detonateBuffer[i], context, burst);

            context.Feedback?.OnAreaOfEffect(center, radius); // placeholder VFX (combo VFX is a future task)
            Debug.Log($"[AbilityRuntime] SHATTER! {Definition.AbilityName} detonates ×{multiplier:0.##} ({burst:0.#}) " +
                      $"in {radius:0.#}m on {_detonateBuffer.Count} enemy(ies).");
            _detonateBuffer.Clear();
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

        // Task 48 (Firewall): place the persistent full-width FIRE band in front of the wall — same geometry as
        // the Frost Zone (depth = AoeRadius, X unconstrained). Tick damage layers Raging Wall (FirewallTickDamage
        // multiplier); duration layers Lingering Embers (UltimateDuration add). Inferno Surge (burst) and Wildfire
        // Spread (death-patch) are resolved from held upgrades and baked into the zone. All keyed off data.
        private void SpawnFireWall(AbilityExecutionContext context)
        {
            float tickDamage = Definition.FireWallTickDamage;
            float duration = Definition.FireWallDuration;
            if (context.Upgrades != null)
            {
                tickDamage = context.Upgrades.ResolveModifier(UpgradeModifierTarget.FirewallTickDamage, tickDamage); // Raging Wall
                duration = context.Upgrades.ResolveModifier(UpgradeModifierTarget.UltimateDuration, duration);       // Lingering Embers
            }
            if (context.Consumables != null) duration += context.Consumables.UltimateDurationBonus();
            tickDamage = Mathf.Max(0f, tickDamage);
            duration = Mathf.Max(0f, duration);

            float tickInterval = Definition.FireWallTickInterval > 0f ? Definition.FireWallTickInterval : 0.5f;

            float burstInterval = 0f, burstFraction = 0f;
            float patchDuration = 0f, patchTickDamage = 0f;
            if (context.Upgrades != null)
            {
                context.Upgrades.TryGetInfernoSurge(out burstInterval, out burstFraction);          // Inferno Surge
                if (context.Upgrades.TryGetWildfireSpread(out float pd, out float tf))               // Wildfire Spread
                {
                    patchDuration = pd;
                    patchTickDamage = tickDamage * tf; // patch deals a fraction of Firewall's tick damage
                }
            }

            // Band of `depth` extending from the wall toward the spawn side (mirrors SpawnFrostZone).
            float depth = Definition.AoeRadius > 0f ? Definition.AoeRadius : 6f;
            float wallZ = context.DefendedLineZ;
            float sign = context.ApproachDirectionZ >= 0f ? 1f : -1f;
            float minZ = Mathf.Min(wallZ, wallZ + sign * depth);
            float maxZ = Mathf.Max(wallZ, wallZ + sign * depth);

            // Task 51: persistent wall-of-fire band visual — sweep-up activation flash + ambient flame for the
            // band's lifetime. Owned by the zone so Inferno Surge flares ride its REAL burst cadence (Pulse) and
            // Wildfire Spread cooling patches mirror its REAL death-patches (SpawnCoolingPatch), with no parallel
            // timer. Replaces Task 48's placeholder diagnostic ring.
            var wallVisual = context.Feedback?.BeginFireWall(minZ, maxZ);

            context.Zones.Spawn(GroundZone.FireBox(
                minZ, maxZ, duration, tickInterval, tickDamage,
                burstInterval, burstFraction, patchDuration, patchTickDamage, context.Zones.Spawn, wallVisual));

            Debug.Log($"[AbilityRuntime] {Definition.AbilityName}: Firewall (band z=[{minZ:0.#},{maxZ:0.#}], " +
                      $"tick={tickDamage:0.#}/{tickInterval:0.##}s, dur={duration:0.#}s, burst={burstFraction:0.##}×basic" +
                      $"@{burstInterval:0.##}s, patch={patchDuration:0.#}s).");
        }

        // Task 48 (Cataclysm): extra damage to a target that is currently Burning. Keyed off the ability's data
        // (BonusDamageVsBurningFraction), generic — a no-op for any ability that doesn't author it.
        private float ApplyBurningBonus(EnemyRuntime target, float damage)
        {
            if (target == null || Definition.BonusDamageVsBurningFraction <= 0f || !target.IsBurning) return damage;
            return damage * (1f + Definition.BonusDamageVsBurningFraction);
        }

        // Task 48 (Fireball / Wildfire Apocalypse): apply this ability's Burn DoT on hit. The Basic layers the
        // held Smoldering Wound (damage × / duration +) and Stacking Embers (per-target stacking) — Basic role
        // only, so an apex ignite (baked potency) doesn't pick up the basic's stacking. No-op on a just-killed
        // target (ApplyBurn guards a resolved enemy).
        private void ApplyBurnOnHit(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (!Definition.AppliesBurnOnHit || target == null) return;

            float perTick = Definition.BurnDamagePerTick;
            float duration = Definition.BurnDuration;
            int maxStacks = 0;
            float perStackBonus = 0f;

            if (_role == AbilityRole.Basic && context.Upgrades != null)
            {
                context.Upgrades.GetBurnAmplifiers(out float dmgMul, out float durBonus); // Smoldering Wound
                perTick *= dmgMul;
                duration += durBonus;
                if (context.Upgrades.TryGetStackingEmbers(out float pb, out int ms))       // Stacking Embers
                {
                    perStackBonus = pb;
                    maxStacks = ms;
                }
            }

            target.ApplyBurn(perTick, duration, maxStacks, perStackBonus);
        }

        // === Task 49: Marksman — piercing-line shots, the shot-burst channel, and Executioner's Volley. ===

        // Basic PiercingLine: fire Multishot shots in a fan toward the nearest enemy, each piercing up to the
        // Piercing Rounds limit (0 = unlimited; default 1 = single target). Returns true if any enemy was hit.
        private bool ExecutePiercingLine(AbilityExecutionContext context, float range, float damage)
        {
            if (!TryGetAimDirection(context, range, out Vector3 aimDir)) return false;

            int shotCount = 1;
            float spread = 0f;
            if (_role == AbilityRole.Basic && context.Upgrades != null)
                context.Upgrades.TryGetMultishot(out shotCount, out spread); // Multishot

            int pierceLimit = 1; // no Piercing Rounds → single target
            if (_role == AbilityRole.Basic && context.Upgrades != null &&
                context.Upgrades.TryGetPiercingRounds(out int limit))
                pierceLimit = limit; // 0 = unlimited

            // Basic has no Full Pierce bonus (that's the Minigun's line) — pierced targets all take 100%.
            return FireShotFan(context, aimDir, shotCount, spread, damage, pierceLimit, 0f, range);
        }

        // Task 49: drive the active Minigun / Bullet Storm channel. On the first call (cooldown ready) it starts
        // the channel and fires the first trigger; subsequent calls (while active) fire the triggers Tick queued.
        private void ExecuteShotBurst(AbilityExecutionContext context, float perShotDamage, float cooldown)
        {
            if (_burstActive)
            {
                FirePendingTriggers(context, perShotDamage);
                return;
            }
            if (_cooldownTimer > 0f) return; // not ready to start a new channel

            float duration = Definition.ChannelDuration;
            float interval = Definition.ChannelShotInterval;
            if (_role == AbilityRole.Ultimate && context.Upgrades != null)
            {
                duration = context.Upgrades.ResolveModifier(UpgradeModifierTarget.UltimateDuration, duration); // Sustained Barrage
                float fireRateBonus = context.Upgrades.MinigunFireRateBonus();                                  // Faster Spin-Up
                if (fireRateBonus > 0f) interval /= 1f + fireRateBonus;
            }
            if (_role == AbilityRole.Ultimate && context.Consumables != null)
                duration += context.Consumables.UltimateDurationBonus();

            _burstActive = true;
            _burstRemaining = Mathf.Max(0f, duration);
            _burstInterval = Mathf.Max(0.02f, interval);
            _burstShotTimer = _burstInterval; // first trigger fires now (below); next after one interval
            _burstShotIndex = 0;
            _pendingTriggers = 1;
            _cooldownTimer = cooldown;        // cooldown runs from cast
            _cooldownDuration = cooldown;

            // Task 52: the Minigun's spin-up moment (a ramp-up flash + heat-shimmer prime) right before sustained
            // fire begins, sized by the current damage tier. Gated on the kinetic style so apex channels skip it.
            if (Definition.VfxStyle == AbilityVfxStyle.Kinetic)
            {
                float spinIntensity = Definition.BaseDamage > 0f
                    ? Mathf.Clamp(perShotDamage / Definition.BaseDamage, 0.6f, 2.5f) : 1f;
                context.Feedback?.OnMinigunSpinUp(context.CasterPosition, spinIntensity);
            }

            Debug.Log($"[AbilityRuntime] {Definition.AbilityName}: shot-burst channel START (dur={_burstRemaining:0.#}s, " +
                      $"interval={_burstInterval:0.###}s).");
            FirePendingTriggers(context, perShotDamage);
        }

        // Fire the triggers Tick queued this frame. Each trigger is one shot (Minigun) fired in a sweep, or one
        // arc-stepped shot (Bullet Storm). Both pierce unlimited; the Minigun adds Full Pierce's beyond-first bonus.
        private void FirePendingTriggers(AbilityExecutionContext context, float perShotDamage)
        {
            int triggers = _pendingTriggers;
            _pendingTriggers = 0;
            if (triggers <= 0) return;

            float range = Definition.Range > 0f ? Definition.Range : 100f;
            float spread = Definition.ChannelSpreadAngle;
            float pierceBonus = (_role == AbilityRole.Ultimate && context.Upgrades != null)
                ? context.Upgrades.FullPierceBonus() : 0f; // Full Pierce (Minigun only)

            for (int t = 0; t < triggers; t++)
            {
                float dmg = RollCrit(perShotDamage, context.Consumables, context.Upgrades, out _);
                Vector3 dir;
                if (spread > 0f)
                {
                    // Bullet Storm: a wide arc fanned in front, stepping across the arc per shot.
                    if (!TryGetAimDirection(context, range, out Vector3 aim)) continue;
                    float frac = (_burstShotIndex % 12) / 11f; // 12-step fan across the arc
                    float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, frac);
                    dir = Quaternion.AngleAxis(angle, Vector3.up) * aim;
                }
                else
                {
                    // Minigun: spray — aim at a random alive enemy each trigger (sweeps the width over time).
                    if (!TryGetRandomAimDirection(context, range, out dir)) continue;
                }
                FireOneShot(context, dir, dmg, 0 /*unlimited pierce*/, pierceBonus, range);
                _burstShotIndex++;
            }
        }

        // Task 49 (Executioner's Volley): one heavy shot at the in-range enemy with the most Armor-Shredder
        // stacks (nearest breaks ties), scaling damage by that stack count. Returns true if it fired.
        private bool ExecuteExecutionersVolley(AbilityExecutionContext context, float baseDamage)
        {
            float range = Definition.Range > 0f ? Definition.Range : 100f;
            float rangeSqr = range * range;
            EnemyRuntime best = null;
            int bestStacks = -1;
            float bestSqr = float.MaxValue;

            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                float sqr = (e.Transform.position - context.CasterPosition).sqrMagnitude;
                if (sqr > rangeSqr) continue;
                int stacks = e.ArmorShredStacks;
                if (stacks > bestStacks || (stacks == bestStacks && sqr < bestSqr))
                {
                    best = e;
                    bestStacks = stacks;
                    bestSqr = sqr;
                }
            }
            if (best == null) return false;

            float damage = baseDamage * (1f + Definition.ShredStackDamageBonus * best.ArmorShredStacks);
            damage = RollCrit(damage, context.Consumables, context.Upgrades, out _);

            // One high-impact apex effect at the target (VFX polish out of scope — reuse the shared apex impact).
            context.Feedback?.OnApexImpact(best.Transform.position, ApexVisualRadius(), ResolveApexStyle());
            OnHit(best, context, damage);
            Debug.Log($"[AbilityRuntime] Executioner's Volley → {bestStacks} shred stack(s), {damage:0.#} dmg.");
            return true;
        }

        // Fire a fan of `shotCount` piercing shots around `aimDir` spanning `spreadDeg` total. Returns true if
        // any shot hit at least one enemy (so the basic consumes its cooldown only when it connects).
        private bool FireShotFan(AbilityExecutionContext context, Vector3 aimDir, int shotCount,
            float spreadDeg, float perShotDamage, int pierceLimit, float pierceBonus, float range)
        {
            shotCount = Mathf.Max(1, shotCount);
            bool hitAny = false;
            for (int i = 0; i < shotCount; i++)
            {
                Vector3 dir = aimDir;
                if (shotCount > 1 && spreadDeg > 0f)
                {
                    float frac = (float)i / (shotCount - 1); // 0..1 across the fan
                    float angle = Mathf.Lerp(-spreadDeg * 0.5f, spreadDeg * 0.5f, frac);
                    dir = Quaternion.AngleAxis(angle, Vector3.up) * aimDir;
                }
                if (FireOneShot(context, dir, perShotDamage, pierceLimit, pierceBonus, range)) hitAny = true;
            }
            return hitAny;
        }

        // Resolve a single shot along `dir` from the caster: collect every alive enemy within the corridor
        // (perpendicular distance ≤ the ability's corridor half-width) and within `range` AHEAD of the caster,
        // ordered nearest-first, then damage up to `pierceLimit` of them (0 = unlimited). The first hit takes
        // 100%; each pierced target beyond the first takes ×(1 + pierceBonus). Returns true if it hit anyone.
        private bool FireOneShot(AbilityExecutionContext context, Vector3 dir, float perShotDamage,
            int pierceLimit, float pierceBonus, float range)
        {
            if (perShotDamage <= 0f) return false;

            Vector3 dirN = new Vector3(dir.x, 0f, dir.z);
            if (dirN.sqrMagnitude < 0.0001f) return false;
            dirN.Normalize();

            float halfWidthSqr = Definition.ShotCorridorHalfWidth * Definition.ShotCorridorHalfWidth;
            var caster = context.CasterPosition;
            var enemies = context.Enemies;

            _pierceBuffer.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                Vector3 to = e.Transform.position - caster;
                to.y = 0f;
                float proj = Vector3.Dot(to, dirN);
                if (proj <= 0f || proj > range) continue;           // behind the caster or out of range
                Vector3 perp = to - dirN * proj;
                if (perp.sqrMagnitude > halfWidthSqr) continue;     // outside the shot's corridor
                _pierceBuffer.Add(e);
            }
            if (_pierceBuffer.Count == 0) return false;

            // Order nearest-first along the ray so "beyond the first" pierce bonus is well-defined.
            _pierceBuffer.Sort((a, b) =>
                Vector3.Dot(a.Transform.position - caster, dirN).CompareTo(
                Vector3.Dot(b.Transform.position - caster, dirN)));

            int limit = pierceLimit <= 0 ? _pierceBuffer.Count : Mathf.Min(pierceLimit, _pierceBuffer.Count);

            // Task 50 (Incendiary Rounds): when this Marksman shot pierces, every target BEYOND the first also
            // gets a Burn (at the held Smoldering Wound tier potency). Resolved once per shot, applied per pierced
            // target below. Keyed on the run's ComboApexState — generic, never on the Marksman/Pyromancer.
            float incBurnPerTick = 0f, incBurnDuration = 0f;
            bool incendiary = limit > 1 && context.ComboApex != null &&
                context.ComboApex.TryGetIncendiaryPierce(out incBurnPerTick, out incBurnDuration);
            if (incendiary && context.Upgrades != null)
            {
                context.Upgrades.GetBurnAmplifiers(out float burnMul, out float burnDurBonus); // Smoldering Wound tier
                incBurnPerTick *= burnMul;
                incBurnDuration += burnDurBonus;
            }

            // Task 52: a kinetic-styled shot (Marksman Basic / Minigun) draws an instant tracer + per-pierce
            // sparks instead of the generic beam; gated on the data style so apex shots (Bullet Storm) keep the
            // default path. Brightness scales with the current damage tier (Heavy Rounds); the tracer runs the
            // full straight line to the DEEPEST pierced target so it visibly continues through every pierce.
            bool kinetic = Definition.VfxStyle == AbilityVfxStyle.Kinetic;
            float tracerIntensity = (kinetic && Definition.BaseDamage > 0f)
                ? Mathf.Clamp(perShotDamage / Definition.BaseDamage, 0.6f, 2.5f) : 1f;
            Vector3 deepest = _pierceBuffer[limit - 1].Transform.position; // buffer is sorted nearest-first

            EnemyRuntime farthest = null;
            for (int j = 0; j < limit; j++)
            {
                var target = _pierceBuffer[j];
                Vector3 hitPos = target.Transform.position; // capture before OnHit may pool the enemy
                float dmg = perShotDamage * (j == 0 ? 1f : 1f + pierceBonus);
                OnHit(target, context, dmg);
                if (incendiary && j > 0) target.ApplyBurn(incBurnPerTick, incBurnDuration, 0, 0f); // beyond-first ignite
                if (kinetic) context.Feedback?.OnPierceImpact(hitPos); // ONE spark per actually-pierced enemy
                if (target.IsAlive) farthest = target; // farthest still-alive hit for the beam visual
            }

            if (kinetic)
            {
                // One tracer per shot; AppliesShotBurst (Minigun) marks it sustained → casing eject + heat shimmer.
                context.Feedback?.OnTracer(caster, deepest, tracerIntensity, Definition.AppliesShotBurst);
            }
            else
            {
                // Diagnostic beam from the caster to the deepest target the shot reached (placeholder VFX).
                var beamEnd = farthest != null ? farthest.Transform.position : caster + dirN * range;
                context.Feedback?.OnSingleTargetHit(caster, beamEnd);
            }

            _pierceBuffer.Clear();
            return true;
        }

        // Direction toward the nearest alive enemy within `range` (planar), or false if none.
        private bool TryGetAimDirection(AbilityExecutionContext context, float range, out Vector3 dir)
        {
            EnemyRuntime nearest = null;
            float bestSqr = range * range;
            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                float sqr = (e.Transform.position - context.CasterPosition).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; nearest = e; }
            }
            if (nearest == null) { dir = Vector3.forward; return false; }
            dir = PlanarDirection(context.CasterPosition, nearest.Transform.position);
            return true;
        }

        // Direction toward a RANDOM alive enemy within `range` (planar) — the Minigun's sweep aim, so its shots
        // spray across the arena width over the channel rather than tunnelling one lane. False if none in range.
        private bool TryGetRandomAimDirection(AbilityExecutionContext context, float range, out Vector3 dir)
        {
            _pierceBuffer.Clear();
            float rangeSqr = range * range;
            var enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                if ((e.Transform.position - context.CasterPosition).sqrMagnitude <= rangeSqr) _pierceBuffer.Add(e);
            }
            if (_pierceBuffer.Count == 0) { dir = Vector3.forward; return false; }
            var pick = _pierceBuffer[Random.Range(0, _pierceBuffer.Count)];
            dir = PlanarDirection(context.CasterPosition, pick.Transform.position);
            _pierceBuffer.Clear();
            return true;
        }

        private static Vector3 PlanarDirection(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? Vector3.forward : d.normalized;
        }

        // Task 49 (Armor Shredder — Basic): each hit adds a stacking effective-Armor reduction (capped, refreshed).
        // A DISTINCT mechanism from Piercing Bolt's flat reduction (EnemyRuntime tracks them separately).
        private void ApplyArmorShredder(EnemyRuntime target, AbilityExecutionContext context)
        {
            if (_role != AbilityRole.Basic || context.Upgrades == null || target == null) return;
            if (context.Upgrades.TryGetArmorShredder(out float perStack, out int maxStacks, out float refresh))
            {
                target.ApplyArmorShred(perStack, maxStacks, refresh);
                // Task 52: gray fracture indicator scaled to the CURRENT stack count, refreshed for the stack's
                // life so it fades per Task 49's stack-duration rules. Distinct from Piercing Bolt / Burn visuals.
                context.Feedback?.OnArmorShred(target.Transform, target.ArmorShredStacks, maxStacks, refresh);
            }
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
