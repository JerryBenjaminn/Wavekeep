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
        [Tooltip("Placeholder max HP for Task 02. Tune freely; can move to a per-level SO later.")]
        [SerializeField, Min(1f)] private float _maxHP = 300f;

        public float MaxHP { get; private set; }
        public float CurrentHP { get; private set; }
        public bool IsDestroyed => CurrentHP <= 0f;

        /// <summary>Raised once, when HP first reaches zero.</summary>
        public event Action OnWallDestroyed;

        private void Awake()
        {
            MaxHP = _maxHP;
            CurrentHP = _maxHP;
        }

        /// <summary>Apply <paramref name="amount"/> damage to the wall. Triggers destruction at zero HP.</summary>
        public void TakeDamage(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;

            CurrentHP -= amount;
            if (CurrentHP <= 0f)
            {
                CurrentHP = 0f;
                Debug.Log("[WallRuntime] Wall destroyed.");
                OnWallDestroyed?.Invoke();
            }
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
