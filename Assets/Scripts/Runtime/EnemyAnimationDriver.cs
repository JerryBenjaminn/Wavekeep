using System;
using System.Collections;
using UnityEngine;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 54 — the per-enemy MonoBehaviour that drives a 3D enemy's <see cref="Animator"/> on behalf of its
    /// (plain-C#) <see cref="EnemyRuntime"/> (CLAUDE.md §3.4: EnemyRuntime has no Update of its own). It lives on
    /// the pooled prefab so it can hold editor-assigned bone references (§2.5) and be the Animation-Event target
    /// (§2.3) — Unity routes <see cref="OnAttackImpactFrame"/> to a component on the same GameObject as the Animator.
    ///
    /// Responsibilities:
    /// - Translate EnemyRuntime intent into Animator triggers: <see cref="PlayAttack"/> (fired ONCE on wall
    ///   arrival; the Attack↔AttackRecovery loop then self-sustains via exit-time transitions) and
    ///   <see cref="PlayDeath"/>.
    /// - Forward the Attack clip's impact-frame Animation Event back to EnemyRuntime (<see cref="OnAttackImpactFrame"/>),
    ///   so wall damage lands when the weapon visually connects, not when the Attack trigger fires (§2.3).
    /// - Delay pool-return until the Death clip has finished (§2.7).
    /// - Reset the Animator to <c>Run</c> on pooled reuse so a recycled enemy never flashes a leftover Death/Attack
    ///   pose (§2.6).
    ///
    /// Enemies without an Animator/controller (placeholder capsules, Goblin) simply never receive this component,
    /// and EnemyRuntime keeps its original timer-driven attack / immediate-release behaviour for them.
    /// </summary>
    [AddComponentMenu("Wavekeep/Enemies/Enemy Animation Driver")]
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Animator on this prefab driven by Skeleton_AnimatorController. Assigned by the Task 54 setup script.")]
        [SerializeField] private Animator _animator;

        [Header("VFX Anchors (Task 54 §2.5)")]
        [Tooltip("Bone where impacts/hit-reactions taken should originate (e.g. chest/upper-spine), instead of the " +
                 "capsule-era root/feet. Falls back to this transform when unassigned.")]
        [SerializeField] private Transform _hitVfxAnchor;
        [Tooltip("Bone where the death effect should originate (e.g. hips/root). Falls back to this transform when unassigned.")]
        [SerializeField] private Transform _deathVfxAnchor;

        [Header("Death Timing (Task 54 §2.7)")]
        [Tooltip("Length (seconds) of the Death clip — the pool-return is delayed by this long so the death " +
                 "animation completes first. Populated from the controller's Death state by the Task 54 setup script.")]
        [SerializeField, Min(0.05f)] private float _deathClipLength = 1.5f;

        // Trigger parameter names — must match the parameters the Task 54 setup script adds to the controller.
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int DieTrigger = Animator.StringToHash("Die");
        private const string RunStateName = "Run";

        // Set by EnemyRuntime each time it (re)binds to this pooled instance. The impact callback applies wall
        // damage; the death callback hands the enemy back for pool-release once the Death clip has played out.
        private Action _onAttackImpact;
        private Action _onDeathComplete;
        private Coroutine _deathRoutine;

        /// <summary>True only when a usable Animator is wired. EnemyRuntime treats "driver present" as
        /// "animation-driven"; a driver without an Animator is ignored so the timer-driven path still runs.</summary>
        public bool HasAnimator => _animator != null;

        /// <summary>Bone to anchor impact/hit-reaction VFX on the real model (§2.5); the root transform if unassigned.</summary>
        public Transform HitVfxAnchor => _hitVfxAnchor != null ? _hitVfxAnchor : transform;

        /// <summary>Bone to anchor the death VFX origin on the real model (§2.5); the root transform if unassigned.</summary>
        public Transform DeathVfxAnchor => _deathVfxAnchor != null ? _deathVfxAnchor : transform;

        /// <summary>Register the callback invoked from the Attack clip's impact-frame Animation Event (§2.3).
        /// EnemyRuntime points this at its wall-damage application.</summary>
        public void BindAttackImpact(Action onAttackImpact)
        {
            _onAttackImpact = onAttackImpact;
        }

        /// <summary>
        /// Reset for pooled reuse (§2.6): cancel any in-flight death wait, clear queued triggers, and snap the
        /// Animator back to the start of <c>Run</c> so a recycled enemy never shows a leftover Death/Attack frame.
        /// Called from <see cref="EnemyRuntime.Initialize"/> — the same per-enemy reset path that already resets
        /// the Frost/Fire overlays — rather than a second parallel reset path.
        /// </summary>
        public void ResetForPooling()
        {
            if (_deathRoutine != null)
            {
                StopCoroutine(_deathRoutine);
                _deathRoutine = null;
            }
            _onDeathComplete = null;

            if (_animator == null) return;
            _animator.speed = 1f; // Task 65 follow-up: clear any pause-freeze so a recycled enemy never spawns frozen
            _animator.ResetTrigger(AttackTrigger);
            _animator.ResetTrigger(DieTrigger);
            _animator.Play(RunStateName, 0, 0f);
            _animator.Update(0f); // apply immediately so the first visible frame is Run, not the recycled pose
        }

        /// <summary>Task 65 follow-up: freeze (or resume) the whole Animator for the shared gameplay pause, so an
        /// enemy at the wall visibly stops mid-swing and approaching enemies stop their walk cycle — instead of
        /// animating with no effect, which read to players as "the wall is still being attacked". Purely visual:
        /// wall damage is independently gated in <see cref="EnemyRuntime"/>. No-op without an Animator; speed is
        /// restored to 1 here on resume and also on pooled reuse (see <see cref="ResetForPooling"/>).</summary>
        public void SetPaused(bool paused)
        {
            if (_animator == null) return;
            _animator.speed = paused ? 0f : 1f;
        }

        /// <summary>Fire the Attack trigger ONCE on wall arrival (§2.2). The Attack→AttackRecovery→Attack loop is
        /// driven by the controller's exit-time transitions, so this is not called every attack.</summary>
        public void PlayAttack()
        {
            if (_animator == null) return;
            _animator.SetTrigger(AttackTrigger);
        }

        /// <summary>Play the Death animation, then invoke <paramref name="onComplete"/> after the clip length so the
        /// object only returns to the pool once the death is visually finished (§2.7). With no Animator the callback
        /// fires immediately (the enemy releases as it did pre-animation).</summary>
        public void PlayDeath(Action onComplete)
        {
            if (_animator == null)
            {
                onComplete?.Invoke();
                return;
            }

            _onDeathComplete = onComplete;
            _animator.ResetTrigger(AttackTrigger); // don't let a queued attack fight the death transition
            _animator.SetTrigger(DieTrigger);

            if (_deathRoutine != null) StopCoroutine(_deathRoutine);
            _deathRoutine = StartCoroutine(DeathReturnRoutine());
        }

        // Animation Event target on the Attack clip (§2.3): the single point where this attack's wall damage is
        // applied. Decoupled from the Attack trigger so damage lands when the weapon visually connects.
        // ReSharper disable once UnusedMember.Global — invoked by Unity's Animation Event system.
        public void OnAttackImpactFrame()
        {
            _onAttackImpact?.Invoke();
        }

        private IEnumerator DeathReturnRoutine()
        {
            // Plain scaled wait matching the Death clip length; the game pauses via PauseState (a gameplay gate),
            // not Time.timeScale, so an enemy is never mid-death while "paused".
            yield return new WaitForSeconds(_deathClipLength);
            _deathRoutine = null;
            var callback = _onDeathComplete;
            _onDeathComplete = null;
            callback?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only wiring entry point used by the Task 54 setup script to populate the serialized
        /// fields on the prefab (so they persist for inspector tuning), keeping the fields private at runtime.</summary>
        public void EditorConfigure(Animator animator, Transform hitAnchor, Transform deathAnchor, float deathClipLength)
        {
            _animator = animator;
            _hitVfxAnchor = hitAnchor;
            _deathVfxAnchor = deathAnchor;
            if (deathClipLength > 0f) _deathClipLength = deathClipLength;
        }
#endif
    }
}
