using System;
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

            MaxHealth = definition.MaxHealth * statMultiplier;
            CurrentHealth = MaxHealth;
            ContactDamage = definition.ContactDamage * statMultiplier;
            MoveSpeed = definition.MoveSpeed;
        }

        /// <summary>Advance movement or wall-attack by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
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
            Transform.position = Vector3.MoveTowards(position, target, MoveSpeed * deltaTime);

            if (Mathf.Abs(Transform.position.z - targetZ) <= _arrivalThreshold)
            {
                _isAttacking = true;
                _attackTimer = 0f;
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
