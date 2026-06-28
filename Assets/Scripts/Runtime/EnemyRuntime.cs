using System;
using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Runtime wrapper around an <see cref="EnemyDefinitionSO"/> (CLAUDE.md §3.5). Holds all
    /// mutable per-enemy state — current health, working stats (after difficulty multipliers),
    /// and the pooled <see cref="GameObject"/> it drives. The SO is only ever read; multipliers
    /// are baked into this instance's working stats, never written back to the asset.
    ///
    /// Behaviour (CLAUDE.md §2): the enemy advances from its far-side spawn toward the defended
    /// <see cref="WallRuntime"/>, stops when it arrives, then attacks the wall on a repeating
    /// interval (dealing <see cref="ContactDamage"/>) until it dies. Reaching the wall does NOT
    /// resolve/despawn the enemy — only death (<see cref="TakeDamage"/> → 0 HP) releases it to the
    /// pool, signalled via the resolution callback so the owner stays in charge of pool/bookkeeping.
    ///
    /// Plain C# object, not a MonoBehaviour: movement/attack are advanced by a single external tick
    /// (the <c>WaveSpawner</c>) rather than a per-enemy <c>Update</c> (CLAUDE.md §3.4).
    /// </summary>
    public sealed class EnemyRuntime
    {
        private EventBus _events;
        private WallRuntime _wall;
        // Task 50 (Frostburn): the run's combo resolver, so Burn ticks can ask whether a Frostburn combo is
        // unlocked and amplify the tick while this enemy is under Frost CC. Injected (no static singleton);
        // null in older callers/tests → Burn behaves exactly as before.
        private ComboApexState _comboApex;
        private Action<EnemyRuntime> _onResolved;
        private float _arrivalThreshold;
        private float _attackInterval;
        private float _attackTimer;
        private bool _isAttacking;
        private bool _isResolved;
        // Task 54: true from the moment death starts until the death animation finishes and the enemy is
        // resolved/released. Distinct from _isResolved so a dying enemy stays in the spawner's active list
        // (and out of the pool) while its Death clip plays out (§2.7), but no longer moves/attacks/takes hits.
        private bool _isDying;

        // Task 13: the loot table resolved for THIS spawn (regular = its definition's; boss = the wave's
        // boss table). Carried only so Die() can hand it to the kill event — EnemyRuntime never rolls/
        // grants loot itself (that's LootService), keeping the death path additive.
        private LootTableSO _lootTable;

        // Task 11: generic status-effect state — ONE list + ONE tick loop handles the whole fixed
        // StatusEffectType set, rather than per-effect booleans/timers (CLAUDE.md §3.8).
        private struct ActiveStatusEffect
        {
            public StatusEffectType Type;
            public float RemainingDuration;
            public float Magnitude;
        }

        /// <summary>DoT granularity (seconds per tick); per-tick damage comes from the SO. Public so an
        /// ability authoring a damage-per-SECOND zone can convert it to per-tick (Task 19).</summary>
        public const float BurnTickInterval = 0.5f;
        private readonly List<ActiveStatusEffect> _statusEffects = new List<ActiveStatusEffect>();

        // Task 48: Burn is promoted out of the generic status list into its own richer model so the Pyromancer's
        // mechanics work: a single Burn that can STACK (Stacking Embers raises its per-tick), tracks its base
        // potency + duration (so Spreading Flame can copy it on death), and exposes a clean "is burning" query
        // (Combustion / Cataclysm). It is still ONE generic burn — any applier (Fireball, Firewall DoT, a Frost
        // zone's DoT, a held Burn status-upgrade) routes through ApplyBurn; only stacking parameters differ.
        private struct BurnState
        {
            public bool Active;
            public float BasePerTick;    // per-tick of the base instance (before stack bonus)
            public int Stacks;           // extra stacks beyond the base (0..MaxStacks)
            public float MaxStacks;      // stacking cap (0 = no stacking, just refresh)
            public float PerStackBonus;  // extra per-tick fraction added per stack [0..1]
            public float Duration;       // configured full duration (copied by Spreading Flame)
            public float Remaining;
            public float TickTimer;
        }

        private BurnState _burn;

        // Task 19: generic STACKING-effect state — a counter per StackingEffectType that decays over time
        // and fires a one-shot payload at max. Same pattern as _statusEffects (one list + one tick loop),
        // parameterised entirely by the applier so it is NOT Frost-Warden-specific (CLAUDE.md §3.8).
        private struct ActiveStackingEffect
        {
            public StackingEffectType Type;
            public int Stacks;
            public float PerStackSlow;       // movement-speed reduction contributed per stack
            public int MaxStacks;            // reaching this triggers the payload, then resets to 0
            public float DecayInterval;      // seconds to lose one stack when not refreshed
            public float DecayTimer;         // counts toward the next decay
            public float TriggerFreezeDuration; // Freeze applied (via the Task 11 status API) at max
        }

        private readonly List<ActiveStackingEffect> _stackingEffects = new List<ActiveStackingEffect>();

        // Task 34: temporary Armor-reduction debuffs. Same generic state-machine pattern as the status /
        // stacking lists (one list + one tick loop), NOT a one-off special case (CLAUDE.md §3.8). Each
        // entry lowers effective Armor by Amount until its duration lapses; entries are stackable-by-design
        // (the list sums them), affecting Physical damage from ALL sources while active. MagicResist is not
        // reducible yet — only Piercing Bolt (a future task) consumes this, and it targets Physical defence.
        private struct ActiveArmorReduction
        {
            public float Amount;
            public float RemainingDuration;
        }

        private readonly List<ActiveArmorReduction> _armorReductions = new List<ActiveArmorReduction>();

        // Task 49 (Armor Shredder): a STACKING effective-Armor reduction, deliberately DISTINCT from the flat
        // single-application reductions above (Bolt Striker's Piercing Bolt). Each Marksman Basic hit adds one
        // stack (capped) and refreshes a single shared timer; the reduction is PerStack × Stacks while active,
        // dropping all stacks together when the timer lapses. The live stack count is queryable so an apex
        // (Executioner's Volley) can scale off it. Same per-entry-expiry state-machine spirit as the others.
        private struct ArmorShredState
        {
            public bool Active;
            public int Stacks;
            public float PerStack;
            public int MaxStacks;
            public float Remaining; // refreshed on each hit; clears all stacks on expiry
        }

        private ArmorShredState _armorShred;

        // Task 35 (Overload): generic incoming-damage VULNERABILITY debuffs. Deliberately DISTINCT from the
        // Task 34 Armor reduction above — they operate at different pipeline stages: Armor reduction lowers
        // the diminishing-returns mitigation FACTOR (computed in AbilityRuntime before TakeDamage), whereas a
        // vulnerability is a flat multiplier applied to the final damage INSIDE TakeDamage, so it amplifies
        // damage from ALL sources (any hero's abilities, Burn ticks, …) after mitigation. Same generic,
        // stackable-by-design state-machine pattern (sum the bonuses) — not a one-off special case.
        private struct ActiveVulnerability
        {
            public float BonusFraction;
            public float RemainingDuration;
        }

        private readonly List<ActiveVulnerability> _vulnerabilities = new List<ActiveVulnerability>();

        // Task 38 (Frozen Lightning): generic cross-hero "combo prime" marker — a single timed window set by a
        // PASSIVE combo apex's priming hit (Remorseless Winter's freeze) and read+cleared by the consuming apex
        // (Lethal Surge) for an amplified hit. Kept as one generic timer (same per-entry-expiry rationale as the
        // status/armor/vulnerability machines above), NOT a Frozen-Lightning-specific flag, so any future
        // passive combo reuses it without further EnemyRuntime changes. 0 = not primed.
        private float _primedRemaining;

        // Task 44: per-enemy Frost VFX driver (Slow vs. Freeze overlay shader). Lives on the pooled
        // GameObject so it persists across reuse; EnemyRuntime only reads its own status state and pushes
        // the resulting visual tier each tick. Null only if the GameObject can't host one (never in practice).
        private FrostVfxController _frostVfx;

        // Task 51: per-enemy Burn VFX driver (ember/fire overlay shader), the fire counterpart to _frostVfx.
        // Same lifecycle — lives on the pooled GameObject, reset on reuse — and is fed a single intensity each
        // tick scaled by the current Burn stack count so more stacks read as a hotter fire (not just longer).
        private FireVfxController _fireVfx;

        // Task 54: drives this enemy's Animator (Run/Attack/Death) and is the Animation-Event target for the
        // attack impact frame. Resolved from the pooled GameObject in Initialize; null for enemies that have no
        // Animator (placeholder capsules, Goblin), which keep the original timer-driven attack / immediate
        // release. A driver WITHOUT a usable Animator is treated as null so those paths stay unchanged.
        private EnemyAnimationDriver _animDriver;

        public EnemyDefinitionSO Definition { get; private set; }
        public GameObject GameObject { get; private set; }
        public Transform Transform { get; private set; }

        // Working stats — copied from the SO and scaled by the difficulty multiplier on Initialize.
        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MoveSpeed { get; private set; }
        public float ContactDamage { get; private set; }

        // Task 34: defensive stats. Armor mitigates Physical damage, MagicResist mitigates Magical damage
        // (see EffectiveArmor / the mitigation step in AbilityRuntime). 0 = no mitigation (full damage).
        // Task 64 follow-up: these are taken at BASE value and NOT scaled by the wave multiplier — in the
        // 100/(100+def) model, scaling defence double-dips against the player's growing damage (the same
        // compounding class Task 64 fixed for contact damage), which made EvilGod ~54 MagicResist at wave 5,
        // a hidden ~35% tax on every Magical hero. Flat defence keeps a CONSTANT mitigation %, so a boss's
        // "tank" identity comes from its (still-scaling) HP, not from progressively hard-countering a damage type.
        public float Armor { get; private set; }
        public float MagicResist { get; private set; }

        public bool IsAlive => !_isResolved && CurrentHealth > 0f;

        /// <summary>
        /// (Re)initialise this runtime for a freshly pooled GameObject. <paramref name="statMultiplier"/>
        /// is the combined tier×wave multiplier; it scales health and contact damage (movement speed
        /// is left unscaled so pacing stays readable). Nothing is written back to the SO.
        /// </summary>
        public void Initialize(
            EnemyDefinitionSO definition,
            GameObject pooledInstance,
            float statMultiplier,
            WallRuntime wall,
            EventBus events,
            float arrivalThreshold,
            float attackInterval,
            Action<EnemyRuntime> onResolved,
            LootTableSO lootTable = null,
            ComboApexState comboApex = null)
        {
            Definition = definition;
            GameObject = pooledInstance;
            Transform = pooledInstance.transform;
            _wall = wall;
            _events = events;
            _comboApex = comboApex; // Task 50 (Frostburn): run combo resolver for per-tick Burn amp under Frost CC
            _arrivalThreshold = arrivalThreshold;
            _attackInterval = attackInterval;
            _attackTimer = 0f;
            _isAttacking = false;
            _onResolved = onResolved;
            _isResolved = false;
            _isDying = false; // Task 54: reset the death-animation gate on pooled reuse
            _lootTable = lootTable; // Task 13: resolved per spawn (regular def table / wave boss table)
            _statusEffects.Clear(); // reset per-run status state on pooled reuse (Task 11)
            _stackingEffects.Clear(); // reset stacking-effect state on pooled reuse (Task 19)
            _armorReductions.Clear(); // reset temporary armor debuffs on pooled reuse (Task 34)
            _vulnerabilities.Clear(); // reset vulnerability debuffs on pooled reuse (Task 35)
            _primedRemaining = 0f;    // reset combo prime on pooled reuse (Task 38)
            _burn = default;          // reset Burn DoT on pooled reuse (Task 48)
            _armorShred = default;    // reset stacking Armor Shredder on pooled reuse (Task 49)

            MaxHealth = definition.MaxHealth * statMultiplier;
            CurrentHealth = MaxHealth;
            ContactDamage = definition.ContactDamage * statMultiplier;
            MoveSpeed = definition.MoveSpeed;
            Armor = definition.Armor;             // Task 64 follow-up: base value, NOT wave-scaled (see Armor docs)
            MagicResist = definition.MagicResist; // → constant mitigation %, no double-dip vs growing player damage

            // Task 44: ensure a Frost VFX driver exists on this pooled GameObject (added once, reused after),
            // and reset it to a clean state so a previously-frozen enemy doesn't spawn back showing frost.
            if (_frostVfx == null)
            {
                _frostVfx = pooledInstance.GetComponent<FrostVfxController>();
                if (_frostVfx == null) _frostVfx = pooledInstance.AddComponent<FrostVfxController>();
            }
            _frostVfx.EnsureInitialized();
            _frostVfx.ResetImmediate();

            // Task 51: same pattern for the Burn (fire) overlay — ensure the driver exists once on this pooled
            // GameObject, then reset it so a previously-Burning enemy doesn't spawn back already on fire.
            if (_fireVfx == null)
            {
                _fireVfx = pooledInstance.GetComponent<FireVfxController>();
                if (_fireVfx == null) _fireVfx = pooledInstance.AddComponent<FireVfxController>();
            }
            _fireVfx.EnsureInitialized();
            _fireVfx.ResetImmediate();

            // Task 54: bind the Animator driver if this prefab has one with a usable Animator (Skeleton). A driver
            // without an Animator is ignored so unanimated enemies keep their original behaviour. Reset it here —
            // the same per-enemy reset path that just reset the Frost/Fire overlays (§2.6, no parallel reset path)
            // — so a recycled enemy snaps back to Run instead of flashing a leftover Death/Attack pose. Bind the
            // attack-impact callback so wall damage is applied from the clip's impact frame, not the trigger (§2.3).
            _animDriver = pooledInstance.GetComponent<EnemyAnimationDriver>();
            if (_animDriver != null && !_animDriver.HasAnimator) _animDriver = null;
            if (_animDriver != null)
            {
                _animDriver.ResetForPooling();
                _animDriver.BindAttackImpact(ApplyWallAttackDamage);
            }
        }

        /// <summary>Advance status effects, then movement or wall-attack by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (_isResolved) return;
            // Task 54: while the death animation plays the enemy is "dying" but not yet resolved — stop all
            // simulation (movement, wall attacks, DoTs) so a corpse never drifts or keeps hitting the wall.
            if (_isDying) return;

            // Task 48: Burn (its own DoT model) ticks first; like the old Burn it deals damage through the
            // normal TakeDamage path, which can resolve this enemy mid-tick — bail immediately if so.
            TickBurn(deltaTime);
            if (_isResolved) return;

            // Task 11: status effects (Freeze/Slow) tick next.
            TickStatusEffects(deltaTime);
            if (_isResolved || _wall == null) return;
            TickStackingEffects(deltaTime); // Task 19: stack decay (never lethal, so no resolve check needed)
            TickArmorReductions(deltaTime); // Task 34: expire temporary armor debuffs
            TickArmorShred(deltaTime);      // Task 49: expire the stacking Armor Shredder
            TickVulnerabilities(deltaTime); // Task 35: expire vulnerability debuffs
            TickPrime(deltaTime);           // Task 38: expire the combo prime window
            UpdateFrostVfx(deltaTime);      // Task 44: drive the Slow/Freeze overlay shader (also while attacking)
            UpdateFireVfx(deltaTime);       // Task 51: drive the Burn (ember/fire) overlay shader

            if (_isAttacking)
            {
                TickAttack(deltaTime);
                return;
            }

            // Approach the wall along the single approach axis (Z), preserving the enemy's lateral
            // (X) lane and height (Y) so enemies line up across the wall's width rather than funnel
            // to a single point. The arena is open width-wise with no obstacles (CLAUDE.md §2).
            var position = Transform.position;
            float targetZ = _wall.transform.position.z;
            var target = new Vector3(position.x, position.y, targetZ);
            // Task 11: speed reflects active Freeze/Slow (frozen → 0, so it stops then resumes when it lapses).
            Transform.position = Vector3.MoveTowards(position, target, EffectiveMoveSpeed * deltaTime);

            if (Mathf.Abs(Transform.position.z - targetZ) <= _arrivalThreshold)
            {
                // Task 54 (§2.4): movement halts the instant we enter the attack state — once _isAttacking is set,
                // Tick returns after TickAttack and never writes position again (this enemy never moves again per
                // §2.2), so there is no slide/drift during Attack/AttackRecovery.
                _isAttacking = true;
                _attackTimer = 0f;
                // Task 54 (§2.2): fire the Attack trigger ONCE; the Attack↔AttackRecovery loop then self-sustains
                // via the controller's exit-time transitions. Each Attack clip applies damage from its impact frame.
                _animDriver?.PlayAttack();
            }
        }

        /// <summary>Base <see cref="MoveSpeed"/> scaled by all active Freeze/Slow effects (Task 11).
        /// Combined MULTIPLICATIVELY: Freeze contributes ×0 (dominating any Slow while active); each
        /// Slow contributes ×(1 − magnitude). Burn does not affect speed. Wall-attack cadence is not
        /// affected (this task scopes Freeze/Slow to movement only).</summary>
        public float EffectiveMoveSpeed
        {
            get
            {
                float multiplier = 1f;
                for (int i = 0; i < _statusEffects.Count; i++)
                {
                    switch (_statusEffects[i].Type)
                    {
                        case StatusEffectType.Freeze:
                            multiplier *= 0f;
                            break;
                        case StatusEffectType.Slow:
                            multiplier *= Mathf.Clamp01(1f - _statusEffects[i].Magnitude);
                            break;
                    }
                }

                // Task 19: stacking effects (Frost) slow MULTIPLICATIVELY too — perStackSlow × stacks.
                for (int i = 0; i < _stackingEffects.Count; i++)
                {
                    var s = _stackingEffects[i];
                    multiplier *= Mathf.Clamp01(1f - s.PerStackSlow * s.Stacks);
                }

                return MoveSpeed * multiplier;
            }
        }

        /// <summary>
        /// Apply a status effect (Task 11), called by <c>AbilityRuntime</c> on a status-delivering hit.
        /// Generic over the fixed <see cref="StatusEffectType"/> set — no per-effect booleans. Stacking
        /// rule: re-applying the SAME type REFRESHES it (overwrites remaining duration + magnitude),
        /// it does not add a second instance; DIFFERENT types coexist (see <see cref="EffectiveMoveSpeed"/>).
        /// </summary>
        public void ApplyStatusEffect(StatusEffectType type, float magnitude, float duration)
        {
            if (_isResolved || duration <= 0f) return;

            // Task 48: Burn lives in its own model now. Route the generic Burn status (zone DoT, held Burn
            // upgrades, apex ignites) to ApplyBurn as a single, non-stacking instance — same refresh semantics.
            if (type == StatusEffectType.Burn)
            {
                ApplyBurn(magnitude, duration, maxStacks: 0, perStackBonus: 0f);
                return;
            }

            for (int i = 0; i < _statusEffects.Count; i++)
            {
                if (_statusEffects[i].Type != type) continue;

                var existing = _statusEffects[i];
                existing.RemainingDuration = duration; // refresh, don't stack
                existing.Magnitude = magnitude;
                _statusEffects[i] = existing;
                return;
            }

            _statusEffects.Add(new ActiveStatusEffect
            {
                Type = type,
                RemainingDuration = duration,
                Magnitude = magnitude
            });

            Debug.Log($"[EnemyRuntime] Status '{type}' applied (mag={magnitude:0.#}, dur={duration:0.#}s) to '{Definition.EnemyName}'.");
        }

        /// <summary>
        /// Task 48: apply (or refresh) the Burn DoT. <paramref name="perTick"/> is the base per-tick damage and
        /// <paramref name="duration"/> the burn length. With <paramref name="maxStacks"/> &gt; 0 (Stacking Embers),
        /// re-applying on a still-Burning target adds one stack (capped), each stack adding
        /// <paramref name="perStackBonus"/> of the base per-tick — stacks PERSIST on this specific enemy for the
        /// burn's remaining duration, independent of hits on other targets. Re-applying always refreshes the
        /// base potency + remaining duration. With maxStacks 0 it's a plain single-instance refresh (zone DoT etc.).
        /// </summary>
        public void ApplyBurn(float perTick, float duration, int maxStacks, float perStackBonus)
        {
            if (_isResolved || perTick <= 0f || duration <= 0f) return;

            if (_burn.Active)
            {
                // Stacking Embers: a repeat application on the SAME burning target adds a stack (up to the cap).
                if (maxStacks > 0 && _burn.Stacks < maxStacks) _burn.Stacks++;
            }
            else
            {
                _burn.Active = true;
                _burn.Stacks = 0;
                _burn.TickTimer = 0f;
            }

            _burn.BasePerTick = perTick;          // latest potency wins
            _burn.PerStackBonus = perStackBonus;
            _burn.MaxStacks = maxStacks;
            _burn.Duration = duration;
            _burn.Remaining = duration;           // refresh

            Debug.Log($"[EnemyRuntime] Burn applied ({BurnPerTick:0.#}/tick, dur={duration:0.#}s, stacks={_burn.Stacks}) to '{Definition.EnemyName}'.");
        }

        /// <summary>Task 48: true while this enemy has an active Burn (Combustion / Cataclysm / Spreading Flame
        /// all key off this).</summary>
        public bool IsBurning => _burn.Active;

        /// <summary>Task 48: the Burn's current EFFECTIVE per-tick damage (base × stack bonus), or 0 if not
        /// burning. Spreading Flame copies this as the spread instance's potency.</summary>
        public float BurnPerTick => _burn.Active ? _burn.BasePerTick * (1f + _burn.PerStackBonus * _burn.Stacks) : 0f;

        /// <summary>Task 48: the Burn's configured duration (copied by Spreading Flame), or 0 if not burning.</summary>
        public float BurnDuration => _burn.Active ? _burn.Duration : 0f;

        // Task 48: advance the Burn DoT. Damage goes through the normal TakeDamage path (same death/pool-release
        // flow as the old Burn — NOT a parallel system); the enemy can die mid-tick (handled by the caller's
        // resolve check). A burn that runs out of duration deactivates — the FireSubsystem detects that natural
        // expiry (was burning last tick, now alive but not burning) to roll Combustion.
        private void TickBurn(float deltaTime)
        {
            if (!_burn.Active) return;

            _burn.TickTimer += deltaTime;
            while (_burn.TickTimer >= BurnTickInterval)
            {
                _burn.TickTimer -= BurnTickInterval;
                // Task 50 (Frostburn): a Burn tick on a target currently under Frost CC (Slow/Freeze) is
                // multiplied while the Frostburn combo is unlocked — a CONTINUOUS per-tick check (re-evaluated
                // here each tick from the CURRENT CC + unlock state), never a one-time consumed prime.
                float tick = BurnPerTick;
                if (_comboApex != null && HasActiveSlowOrFreeze())
                    tick *= _comboApex.FrostburnBurnMultiplier();
                TakeDamage(tick);
                if (_isResolved) return; // burn was lethal; enemy already resolved/released
            }

            _burn.Remaining -= deltaTime;
            if (_burn.Remaining <= 0f) _burn = default; // natural expiry
        }

        // Task 50 (Frostburn): true if any Slow/Freeze STATUS is active (covers all the listed Frost Warden CC
        // sources — Frozen Ground / Deepening Frost zone Slow, Hard Freeze, Remorseless Winter — which all apply
        // a Slow/Freeze status). Frost-stack-only slow is intentionally not counted (it's not a status effect).
        private bool HasActiveSlowOrFreeze()
        {
            for (int i = 0; i < _statusEffects.Count; i++)
            {
                var t = _statusEffects[i].Type;
                if (t == StatusEffectType.Slow || t == StatusEffectType.Freeze) return true;
            }
            return false;
        }

        /// <summary>Task 50 (Chain Combustion): a Bolt Striker chain-jump that hits an already-Burning target
        /// extends the Burn by <paramref name="extendSeconds"/> and adds ONE Stacking-Embers-style stack (capped
        /// at <paramref name="maxStacks"/>, each worth <paramref name="perStackBonus"/>), WITHOUT a Fireball hit.
        /// No-op if not currently burning (the combo only refreshes/intensifies an existing Burn).</summary>
        public void AddBurnStackAndExtend(int maxStacks, float perStackBonus, float extendSeconds)
        {
            if (_isResolved || !_burn.Active) return;

            if (maxStacks > 0)
            {
                _burn.PerStackBonus = perStackBonus; // adopt the current Stacking Embers tier value
                _burn.MaxStacks = maxStacks;
                if (_burn.Stacks < maxStacks) _burn.Stacks++;
            }
            if (extendSeconds > 0f) _burn.Remaining += extendSeconds; // extend (not refresh-to-base)

            Debug.Log($"[EnemyRuntime] Chain Combustion: Burn +{extendSeconds:0.#}s, stacks={_burn.Stacks} on '{Definition.EnemyName}'.");
        }

        /// <summary>
        /// Apply (or refresh) a generic stacking effect (Task 19). Adds <paramref name="amount"/> stacks
        /// up to <paramref name="maxStacks"/>, refreshing the decay timer so a fresh hit extends rather
        /// than double-counting past the max. When the count REACHES the max it triggers a Freeze (via
        /// the existing Task 11 status API) and resets stacks to 0. All parameters come from the applier,
        /// so the same machine serves any future stacking effect. Returns true if this call hit max and
        /// fired the Freeze payload (so the caller can run on-trigger behaviour like Chain Frost spread).
        /// </summary>
        public bool ApplyStack(StackingEffectType type, int amount, float perStackSlow, int maxStacks,
            float decayInterval, float triggerFreezeDuration)
        {
            if (_isResolved || amount <= 0 || maxStacks < 1) return false;

            for (int i = 0; i < _stackingEffects.Count; i++)
            {
                if (_stackingEffects[i].Type != type) continue;

                var e = _stackingEffects[i];
                // Refresh tunables in case held upgrades changed them since the last hit, and reset decay.
                e.PerStackSlow = perStackSlow;
                e.MaxStacks = maxStacks;
                e.DecayInterval = decayInterval;
                e.TriggerFreezeDuration = triggerFreezeDuration;
                e.DecayTimer = 0f;
                e.Stacks = Mathf.Min(e.Stacks + amount, maxStacks); // clamp — never overshoot the max

                if (e.Stacks >= maxStacks)
                {
                    _stackingEffects.RemoveAt(i); // max reached → consume the stacks (reset to 0)
                    ApplyStatusEffect(StatusEffectType.Freeze, 0f, triggerFreezeDuration);
                    Debug.Log($"[EnemyRuntime] Frost reached max ({maxStacks}) on '{Definition.EnemyName}' → Freeze {triggerFreezeDuration:0.#}s, stacks reset.");
                    return true;
                }

                _stackingEffects[i] = e;
                Debug.Log($"[EnemyRuntime] {type} stacks → {e.Stacks}/{maxStacks} on '{Definition.EnemyName}'.");
                return false;
            }

            // First application of this type.
            int initial = Mathf.Min(amount, maxStacks);
            if (initial >= maxStacks)
            {
                ApplyStatusEffect(StatusEffectType.Freeze, 0f, triggerFreezeDuration);
                Debug.Log($"[EnemyRuntime] Frost reached max ({maxStacks}) on '{Definition.EnemyName}' → Freeze {triggerFreezeDuration:0.#}s, stacks reset.");
                return true;
            }

            _stackingEffects.Add(new ActiveStackingEffect
            {
                Type = type,
                Stacks = initial,
                PerStackSlow = perStackSlow,
                MaxStacks = maxStacks,
                DecayInterval = decayInterval,
                DecayTimer = 0f,
                TriggerFreezeDuration = triggerFreezeDuration
            });
            return false;
        }

        /// <summary>Task 31 (Shattering Impact): true if the enemy is currently affected by any Slow or
        /// Freeze status, OR carries any stacking (Frost) stacks — i.e. its movement is impaired by CC.</summary>
        public bool IsImpaired
        {
            get
            {
                for (int i = 0; i < _statusEffects.Count; i++)
                {
                    var t = _statusEffects[i].Type;
                    if (t == StatusEffectType.Slow || t == StatusEffectType.Freeze) return true;
                }
                for (int i = 0; i < _stackingEffects.Count; i++)
                {
                    if (_stackingEffects[i].Stacks > 0) return true;
                }
                return false;
            }
        }

        /// <summary>Current stack count of a stacking effect (Task 19), or 0 if none active.</summary>
        public int GetStackCount(StackingEffectType type)
        {
            for (int i = 0; i < _stackingEffects.Count; i++)
                if (_stackingEffects[i].Type == type) return _stackingEffects[i].Stacks;
            return 0;
        }

        /// <summary>Task 34: the enemy's Armor after any active temporary reductions, clamped at 0. This is
        /// what the Physical mitigation step reads, so an armor debuff lowers damage from EVERY Physical
        /// source while active, not just whoever applied it. MagicResist has no debuff path yet.</summary>
        public float EffectiveArmor
        {
            get
            {
                float reduction = 0f;
                for (int i = 0; i < _armorReductions.Count; i++)
                    reduction += _armorReductions[i].Amount;
                if (_armorShred.Active) reduction += _armorShred.PerStack * _armorShred.Stacks; // Task 49
                return Mathf.Max(0f, Armor - reduction);
            }
        }

        /// <summary>Task 49 (Armor Shredder): current stack count (0 if none active). Executioner's Volley
        /// reads this to pick the most-shredded target and scale its damage.</summary>
        public int ArmorShredStacks => _armorShred.Active ? _armorShred.Stacks : 0;

        /// <summary>Task 49: apply one Armor-Shredder stack (capped at <paramref name="maxStacks"/>) and refresh
        /// the shared timer to <paramref name="refresh"/>s. Reduction is <paramref name="perStack"/> × stacks of
        /// effective Armor while active; on expiry all stacks drop together. Distinct from the flat
        /// <see cref="ApplyArmorReduction"/> (Piercing Bolt) — this one stacks per hit.</summary>
        public void ApplyArmorShred(float perStack, int maxStacks, float refresh)
        {
            if (_isResolved || perStack <= 0f || maxStacks < 1 || refresh <= 0f) return;

            if (_armorShred.Active && _armorShred.Stacks < maxStacks) _armorShred.Stacks++;
            else if (!_armorShred.Active) _armorShred.Stacks = 1;

            _armorShred.Active = true;
            _armorShred.PerStack = perStack;   // latest tier's values win
            _armorShred.MaxStacks = maxStacks;
            _armorShred.Remaining = refresh;   // refresh the shared timer

            Debug.Log($"[EnemyRuntime] Armor Shredder → {_armorShred.Stacks}/{maxStacks} stacks " +
                      $"(-{_armorShred.PerStack * _armorShred.Stacks:0.#} Armor) on '{Definition.EnemyName}'.");
        }

        // Task 49: advance the Armor-Shredder timer; drop all stacks when it lapses (refreshed by new hits).
        private void TickArmorShred(float deltaTime)
        {
            if (!_armorShred.Active) return;
            _armorShred.Remaining -= deltaTime;
            if (_armorShred.Remaining <= 0f) _armorShred = default;
        }

        /// <summary>Task 34: apply a temporary, time-limited reduction to this enemy's effective Armor.
        /// Generic and stackable-by-design (each call adds an independent entry that the list sums), built
        /// on the same per-entry-duration pattern as the status/stacking machines rather than a special
        /// case. No ability grants this yet — it exists so a future Bolt Striker line (Piercing Bolt) can
        /// call it without further EnemyRuntime changes.</summary>
        public void ApplyArmorReduction(float amount, float duration)
        {
            if (_isResolved || amount <= 0f || duration <= 0f) return;

            _armorReductions.Add(new ActiveArmorReduction
            {
                Amount = amount,
                RemainingDuration = duration
            });

            Debug.Log($"[EnemyRuntime] Armor -{amount:0.#} for {duration:0.#}s on '{Definition.EnemyName}' (effective {EffectiveArmor:0.#}).");
        }

        // Task 34: advance each armor-reduction debuff's timer and drop the expired ones (reverting Armor).
        private void TickArmorReductions(float deltaTime)
        {
            for (int i = _armorReductions.Count - 1; i >= 0; i--)
            {
                var r = _armorReductions[i];
                r.RemainingDuration -= deltaTime;
                if (r.RemainingDuration <= 0f) _armorReductions.RemoveAt(i);
                else _armorReductions[i] = r;
            }
        }

        /// <summary>Task 35 (Overload): the multiplier applied to ALL incoming damage (1 + summed active
        /// vulnerability fractions). 1 when none active. Read inside <see cref="TakeDamage"/>, so it amplifies
        /// every source after mitigation — the documented distinction from Armor reduction (which lowers the
        /// mitigation factor instead). See <see cref="ApplyVulnerability"/>.</summary>
        public float IncomingDamageMultiplier
        {
            get
            {
                float bonus = 0f;
                for (int i = 0; i < _vulnerabilities.Count; i++)
                    bonus += _vulnerabilities[i].BonusFraction;
                return 1f + bonus;
            }
        }

        /// <summary>Task 35 (Overload): apply a temporary generic incoming-damage vulnerability — the target
        /// takes <paramref name="bonusFraction"/> extra damage from ALL sources for <paramref name="duration"/>
        /// seconds. Generic and stackable-by-design (the list sums), following the same per-entry pattern as
        /// Armor reduction, but distinct from it (a post-mitigation damage-taken multiplier, NOT an Armor edit).</summary>
        public void ApplyVulnerability(float bonusFraction, float duration)
        {
            if (_isResolved || bonusFraction <= 0f || duration <= 0f) return;

            _vulnerabilities.Add(new ActiveVulnerability
            {
                BonusFraction = bonusFraction,
                RemainingDuration = duration
            });

            Debug.Log($"[EnemyRuntime] Vulnerability +{bonusFraction * 100f:0.#}% dmg-taken for {duration:0.#}s on '{Definition.EnemyName}'.");
        }

        // Task 35: advance each vulnerability debuff's timer and drop the expired ones.
        private void TickVulnerabilities(float deltaTime)
        {
            for (int i = _vulnerabilities.Count - 1; i >= 0; i--)
            {
                var v = _vulnerabilities[i];
                v.RemainingDuration -= deltaTime;
                if (v.RemainingDuration <= 0f) _vulnerabilities.RemoveAt(i);
                else _vulnerabilities[i] = v;
            }
        }

        /// <summary>Task 38 (Frozen Lightning): true while this enemy is "primed" by a passive combo apex's
        /// priming hit (see <see cref="ApplyPrime"/>). The consuming apex reads this to decide whether to
        /// amplify, then clears it via <see cref="ConsumePrime"/>.</summary>
        public bool IsPrimed => _primedRemaining > 0f;

        /// <summary>Task 38 (Frozen Lightning): mark this enemy primed for <paramref name="duration"/> seconds.
        /// Set by a passive combo apex's priming hit (Remorseless Winter's freeze). Generic — not keyed to a
        /// specific combo. Extends (never shortens) an existing prime so a re-prime can't cut the window short.</summary>
        public void ApplyPrime(float duration)
        {
            if (_isResolved || duration <= 0f) return;
            if (duration > _primedRemaining) _primedRemaining = duration;
        }

        /// <summary>Task 38 (Frozen Lightning): consume the prime if active — returns true exactly once per
        /// prime, then clears it (so the consuming apex amplifies a primed target only once until it is
        /// re-primed). Returns false when not primed.</summary>
        public bool ConsumePrime()
        {
            if (_primedRemaining <= 0f) return false;
            _primedRemaining = 0f;
            return true;
        }

        // Task 38: count down the combo prime window; it simply lapses if the consuming apex never lands in time.
        private void TickPrime(float deltaTime)
        {
            if (_primedRemaining > 0f) _primedRemaining -= deltaTime;
        }

        // Task 44: map this enemy's current Frost status state to a discrete VFX tier and push it to the
        // overlay shader driver. Freeze (a hard movement stop) dominates and shows the strong tier; otherwise
        // any active Slow status OR lingering Frost stacks show the subtle mist tier, scaled by how impaired
        // the enemy is so a near-frozen enemy reads stronger than a single stack. Reading only this enemy's
        // own state keeps every enemy's VFX independent (no shared parameter).
        private void UpdateFrostVfx(float deltaTime)
        {
            if (_frostVfx == null) return;

            var tier = FrostVfxController.FrostTier.None;
            float intensity = 0f;

            bool frozen = false;
            float slowFraction = 0f; // strongest active Slow magnitude [0..1]
            for (int i = 0; i < _statusEffects.Count; i++)
            {
                if (_statusEffects[i].Type == StatusEffectType.Freeze) { frozen = true; break; }
                if (_statusEffects[i].Type == StatusEffectType.Slow)
                    slowFraction = Mathf.Max(slowFraction, _statusEffects[i].Magnitude);
            }

            if (frozen)
            {
                tier = FrostVfxController.FrostTier.Freeze;
                intensity = 1f;
            }
            else
            {
                // Normalise stacks against their own max so the mist deepens as the enemy nears a Freeze.
                float stackRatio = 0f;
                for (int i = 0; i < _stackingEffects.Count; i++)
                {
                    var s = _stackingEffects[i];
                    if (s.Stacks > 0 && s.MaxStacks > 0)
                        stackRatio = Mathf.Max(stackRatio, (float)s.Stacks / s.MaxStacks);
                }

                // A Slow status fraction (e.g. 0.25) is mapped onto the same 0..1 mist scale.
                float slowRatio = Mathf.Clamp01(slowFraction * 2f);
                float t = Mathf.Max(stackRatio, slowRatio);
                if (t > 0f)
                {
                    tier = FrostVfxController.FrostTier.Slow;
                    intensity = Mathf.Lerp(0.35f, 1f, t); // keep even one stack visible; shader keeps it subtle
                }
            }

            _frostVfx.SetTarget(tier, intensity);
            _frostVfx.TickVisual(deltaTime);
        }

        // Task 51: map this enemy's current Burn state to a single 0..1 intensity and push it to the ember/fire
        // overlay. A plain active Burn already shows clearly; each extra Stacking-Embers stack ramps the intensity
        // toward full, so more stacks read as a hotter fire rather than merely a longer one (the task's explicit
        // "intensity scales with stack count" requirement). Reads only this enemy's own Burn — no shared state.
        private void UpdateFireVfx(float deltaTime)
        {
            if (_fireVfx == null) return;

            float intensity = 0f;
            if (_burn.Active)
            {
                float stackRatio = _burn.MaxStacks > 0f
                    ? Mathf.Clamp01(_burn.Stacks / _burn.MaxStacks)
                    : 0f;
                intensity = Mathf.Lerp(0.45f, 1f, stackRatio); // a base burn is clearly visible; stacks intensify it
            }

            _fireVfx.SetTarget(intensity);
            _fireVfx.TickVisual(deltaTime);
        }

        // Task 19: decay each stacking effect by one stack per DecayInterval of NOT being refreshed.
        // Drop the effect entirely once it decays to zero.
        private void TickStackingEffects(float deltaTime)
        {
            for (int i = _stackingEffects.Count - 1; i >= 0; i--)
            {
                var e = _stackingEffects[i];
                e.DecayTimer += deltaTime;
                bool decayed = false;
                while (e.DecayTimer >= e.DecayInterval && e.Stacks > 0)
                {
                    e.DecayTimer -= e.DecayInterval;
                    e.Stacks--;
                    decayed = true;
                }

                if (decayed)
                    Debug.Log($"[EnemyRuntime] {e.Type} decayed → {e.Stacks} stack(s) on '{Definition.EnemyName}'.");

                if (e.Stacks <= 0) _stackingEffects.RemoveAt(i);
                else _stackingEffects[i] = e;
            }
        }

        // Advance Freeze/Slow durations and drop expired effects. Burn is handled separately in TickBurn
        // (Task 48 promoted it to its own model); only movement-affecting statuses live in this list now.
        private void TickStatusEffects(float deltaTime)
        {
            for (int i = _statusEffects.Count - 1; i >= 0; i--)
            {
                var effect = _statusEffects[i];

                effect.RemainingDuration -= deltaTime;
                if (effect.RemainingDuration <= 0f)
                {
                    _statusEffects.RemoveAt(i);
                }
                else
                {
                    _statusEffects[i] = effect;
                }
            }
        }

        private void TickAttack(float deltaTime)
        {
            if (_wall.IsDestroyed) return;

            // Task 54: animated enemies deal their wall damage from the Attack clip's impact frame
            // (ApplyWallAttackDamage via OnAttackImpactFrame), paced by the Attack/AttackRecovery animation loop —
            // NOT by this fixed interval timer. The timer path remains for unanimated enemies (no driver).
            if (_animDriver != null) return;

            _attackTimer += deltaTime;
            if (_attackTimer >= _attackInterval)
            {
                _attackTimer -= _attackInterval;
                _wall.TakeDamage(ContactDamage);
            }
        }

        // Task 54 (§2.3): the single wall-damage point for an animated attack, invoked from the Attack clip's
        // impact-frame Animation Event (OnAttackImpactFrame → driver → here). Decouples "attack started" (trigger)
        // from "weapon lands" (this). Guarded so a queued impact event can't damage the wall once the enemy has
        // started dying, has resolved, has left the attack state, or after the wall is already destroyed.
        private void ApplyWallAttackDamage()
        {
            if (_isResolved || _isDying || !_isAttacking) return;
            if (_wall == null || _wall.IsDestroyed) return;
            _wall.TakeDamage(ContactDamage);
        }

        /// <summary>Apply damage. Reaching zero health triggers <see cref="Die"/>. Works in any state
        /// (moving or attacking the wall). Nothing calls this in steady-state Task 02 gameplay except
        /// the manual debug trigger.
        ///
        /// Task 35: the final amount is scaled by <see cref="IncomingDamageMultiplier"/> (Overload
        /// vulnerability) here — the last stage, so it amplifies EVERY source after ability-side mitigation.</summary>
        public void TakeDamage(float amount)
        {
            // Task 54: ignore damage once the enemy has begun dying so a corpse can't be "killed" twice
            // (which would re-publish EnemyKilledEvent) during its Death animation.
            if (_isResolved || _isDying || amount <= 0f) return;

            amount *= IncomingDamageMultiplier; // Task 35: Overload vulnerability (1 when none active)
            CurrentHealth -= amount;
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0f;
                Die();
            }
        }

        private void Die()
        {
            if (_isResolved || _isDying) return;
            // Carry the definition (Task 03 currency/xp) AND the resolved loot table (Task 13 drops).
            // Loot rolling/granting happens in LootService listening to this same event — this death
            // path is unchanged otherwise (no new currency/xp/pool-release logic). The kill event is
            // published immediately at death; only the pool-RELEASE is deferred for the death animation.
            _events?.Publish(new EnemyKilledEvent(Definition, _lootTable));

            // Task 54 (§2.7): for animated enemies, play the Death clip and defer Resolve (pool-release +
            // active-list removal) until the clip finishes, so a corpse never pops out mid-animation. The enemy
            // stays in the spawner's active list but stops simulating (_isDying). Unanimated enemies (no driver)
            // resolve immediately, exactly as before.
            if (_animDriver != null)
            {
                _isDying = true;
                _animDriver.PlayDeath(Resolve);
            }
            else
            {
                Resolve();
            }
        }

        // Marks the enemy done and hands it back to its owner exactly once (owner releases to pool).
        // Death is the ONLY path that resolves an enemy — reaching the wall does not.
        private void Resolve()
        {
            _isResolved = true;
            var callback = _onResolved;
            _onResolved = null;
            callback?.Invoke(this);
        }
    }
}
