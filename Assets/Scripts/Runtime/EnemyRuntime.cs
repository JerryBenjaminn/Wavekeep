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
        private Action<EnemyRuntime> _onResolved;
        private float _arrivalThreshold;
        private float _attackInterval;
        private float _attackTimer;
        private bool _isAttacking;
        private bool _isResolved;

        // Task 11: generic status-effect state — ONE list + ONE tick loop handles the whole fixed
        // StatusEffectType set, rather than per-effect booleans/timers (CLAUDE.md §3.8).
        private struct ActiveStatusEffect
        {
            public StatusEffectType Type;
            public float RemainingDuration;
            public float Magnitude;
            public float BurnTimer; // only used by Burn
        }

        private const float BurnTickInterval = 0.5f; // DoT granularity; per-tick damage comes from the SO
        private readonly List<ActiveStatusEffect> _statusEffects = new List<ActiveStatusEffect>();

        public EnemyDefinitionSO Definition { get; private set; }
        public GameObject GameObject { get; private set; }
        public Transform Transform { get; private set; }

        // Working stats — copied from the SO and scaled by the difficulty multiplier on Initialize.
        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MoveSpeed { get; private set; }
        public float ContactDamage { get; private set; }

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
            Action<EnemyRuntime> onResolved)
        {
            Definition = definition;
            GameObject = pooledInstance;
            Transform = pooledInstance.transform;
            _wall = wall;
            _events = events;
            _arrivalThreshold = arrivalThreshold;
            _attackInterval = attackInterval;
            _attackTimer = 0f;
            _isAttacking = false;
            _onResolved = onResolved;
            _isResolved = false;
            _statusEffects.Clear(); // reset per-run status state on pooled reuse (Task 11)

            MaxHealth = definition.MaxHealth * statMultiplier;
            CurrentHealth = MaxHealth;
            ContactDamage = definition.ContactDamage * statMultiplier;
            MoveSpeed = definition.MoveSpeed;
        }

        /// <summary>Advance status effects, then movement or wall-attack by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (_isResolved) return;

            // Task 11: status effects tick first. Burn deals damage through the normal TakeDamage path,
            // which can resolve this enemy mid-tick — bail immediately if so (it's now pool-released).
            TickStatusEffects(deltaTime);
            if (_isResolved || _wall == null) return;

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
                _isAttacking = true;
                _attackTimer = 0f;
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
                Magnitude = magnitude,
                BurnTimer = 0f
            });

            Debug.Log($"[EnemyRuntime] Status '{type}' applied (mag={magnitude:0.#}, dur={duration:0.#}s) to '{Definition.EnemyName}'.");
        }

        // Advance durations, apply Burn ticks through the existing TakeDamage path (reusing the normal
        // death/pool-release flow — NOT a parallel DoT system), and drop expired effects.
        private void TickStatusEffects(float deltaTime)
        {
            for (int i = _statusEffects.Count - 1; i >= 0; i--)
            {
                var effect = _statusEffects[i];

                if (effect.Type == StatusEffectType.Burn)
                {
                    effect.BurnTimer += deltaTime;
                    while (effect.BurnTimer >= BurnTickInterval)
                    {
                        effect.BurnTimer -= BurnTickInterval;
                        TakeDamage(effect.Magnitude);
                        if (_isResolved) return; // burn was lethal; enemy already resolved/released
                    }
                }

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

            _attackTimer += deltaTime;
            if (_attackTimer >= _attackInterval)
            {
                _attackTimer -= _attackInterval;
                _wall.TakeDamage(ContactDamage);
            }
        }

        /// <summary>Apply damage. Reaching zero health triggers <see cref="Die"/>. Works in any state
        /// (moving or attacking the wall). Nothing calls this in steady-state Task 02 gameplay except
        /// the manual debug trigger.</summary>
        public void TakeDamage(float amount)
        {
            if (_isResolved || amount <= 0f) return;

            CurrentHealth -= amount;
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0f;
                Die();
            }
        }

        private void Die()
        {
            if (_isResolved) return;
            // Carry the definition so reward consumers (Task 03) can read currency/xp yields.
            _events?.Publish(new EnemyKilledEvent(Definition));
            Resolve();
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
