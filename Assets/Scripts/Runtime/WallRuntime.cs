using System;
using UnityEngine;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// The defended wall (CLAUDE.md §2 locked decision). Sits between the approaching enemies and
    /// the player; enemies stop at it and attack it on an interval. Holds current/max HP; when HP
    /// reaches zero the run is lost.
    ///
    /// Max HP is a serialized placeholder here (not yet an SO field) — the task left the source to
    /// my discretion for Task 02; a per-level SO value can replace it later. Exposes an instance
    /// <see cref="OnWallDestroyed"/> callback (not a static event, per §3.5) that the WaveSpawner
    /// subscribes to in order to publish the defeat <c>RunEndedEvent</c> and stop the run.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Wall Runtime")]
    public sealed class WallRuntime : MonoBehaviour
    {
        [Tooltip("Max wall HP. Task 81 raised 300 → 1200 (with contact-damage damping) so the wall is an " +
                 "attrition gauge across the 60-wave curve; can move to a per-level SO later. NOTE: the " +
                 "scene's serialized value wins over this default — re-run 'Wavekeep/Setup Task 81'.")]
        [SerializeField, Min(1f)] private float _maxHP = 1200f;

        public float MaxHP { get; private set; }
        public float CurrentHP { get; private set; }
        public bool IsDestroyed => CurrentHP <= 0f;

        // Task 80 (Aegis Shield): a temporary absorb buffer that soaks incoming damage before wall HP, expiring
        // after its timer. And (Reinforced Barricade) a temporary incoming-damage multiplier. Both are applied by
        // the utility shop and counted down in real time here — see the flag in the Task 80 summary.
        private float _shield;
        private float _shieldRemaining;
        private float _damageReductionFraction; // [0..1]; incoming damage ×(1 − this) while active
        private float _reductionRemaining;

        /// <summary>Task 80: current absorb buffer remaining (0 when none). Read-only for HUD.</summary>
        public float Shield => _shield;

        /// <summary>Raised once, when HP first reaches zero.</summary>
        public event Action OnWallDestroyed;

        private void Awake()
        {
            MaxHP = _maxHP;
            CurrentHP = _maxHP;
        }

        private void Update()
        {
            // Task 80: expire the temporary shield / damage-reduction windows. Real-time countdown (see summary flag
            // re: the brief level-up pause). Timers only run while active, so this is a no-op in the common case.
            float dt = Time.deltaTime;
            if (_reductionRemaining > 0f)
            {
                _reductionRemaining -= dt;
                if (_reductionRemaining <= 0f) _damageReductionFraction = 0f;
            }
            if (_shieldRemaining > 0f)
            {
                _shieldRemaining -= dt;
                if (_shieldRemaining <= 0f) _shield = 0f;
            }
        }

        /// <summary>Apply <paramref name="amount"/> damage to the wall. Triggers destruction at zero HP.
        /// Task 80: incoming damage is first reduced by any active Barricade, then soaked by any active Aegis
        /// shield buffer, before it reaches wall HP.</summary>
        public void TakeDamage(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;

            // Barricade: reduce incoming damage first.
            if (_reductionRemaining > 0f && _damageReductionFraction > 0f)
                amount *= (1f - _damageReductionFraction);

            // Aegis: absorb from the shield buffer before touching HP.
            if (_shieldRemaining > 0f && _shield > 0f)
            {
                float absorbed = Mathf.Min(_shield, amount);
                _shield -= absorbed;
                amount -= absorbed;
            }

            if (amount <= 0f) return;

            CurrentHP -= amount;
            if (CurrentHP <= 0f)
            {
                CurrentHP = 0f;
                Debug.Log("[WallRuntime] Wall destroyed.");
                OnWallDestroyed?.Invoke();
            }
        }

        /// <summary>Task 80 (Aegis Shield): grant a temporary absorb buffer of <paramref name="amount"/> HP that
        /// soaks incoming damage before wall HP for <paramref name="duration"/> seconds. Refreshes to the larger of
        /// the current and new buffer/timer (picking it again shouldn't shrink an existing shield).</summary>
        public void AddShield(float amount, float duration)
        {
            if (amount <= 0f || duration <= 0f) return;
            _shield = Mathf.Max(_shield, amount);
            _shieldRemaining = Mathf.Max(_shieldRemaining, duration);
            Debug.Log($"[WallRuntime] Aegis shield {_shield:0.#} HP for {duration:0.#}s.");
        }

        /// <summary>Task 80 (Reinforced Barricade): reduce incoming wall damage by <paramref name="fraction"/> [0..1]
        /// for <paramref name="duration"/> seconds. Takes the stronger of any current window.</summary>
        public void SetDamageReduction(float fraction, float duration)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0f || duration <= 0f) return;
            _damageReductionFraction = Mathf.Max(_damageReductionFraction, fraction);
            _reductionRemaining = Mathf.Max(_reductionRemaining, duration);
            Debug.Log($"[WallRuntime] Barricade −{_damageReductionFraction * 100f:0}% damage for {duration:0.#}s.");
        }

        /// <summary>
        /// Restore <paramref name="amount"/> HP, clamped to <see cref="MaxHP"/> (Task 06: the HealWall
        /// consumable's single entry point into wall health — no parallel HP path). No-op once the wall
        /// is destroyed: a fallen wall has already ended the run.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;

            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            Debug.Log($"[WallRuntime] Healed {amount:0.#} → {CurrentHP:0.#}/{MaxHP:0.#} HP.");
        }
    }
}
