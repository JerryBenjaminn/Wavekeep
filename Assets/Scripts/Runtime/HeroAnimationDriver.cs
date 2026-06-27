using System;
using UnityEngine;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 56 — generic, hero-agnostic bridge between a hero's <see cref="Animator"/> and its
    /// <see cref="HeroRuntime"/>. Lives on the Animator's GameObject so it can be the Animation-Event target
    /// (Unity routes events to components on the Animator's own GameObject) and triggers the Attack clip.
    ///
    /// It deliberately holds NO hero-specific logic: it only fires the <c>Attack</c> trigger and forwards the
    /// clip's impact-frame event (<see cref="OnBasicAttackImpactFrame"/>) to a callback the hero binds. The
    /// actual ability effect (Frost Warden's ice-burst, later Pyromancer's fireball, …) stays in that hero's
    /// ability code reacting to the callback — so the SAME clip/event reuses across heroes with no shared-
    /// plumbing changes (Task 56 §2.3 / acceptance).
    /// </summary>
    [AddComponentMenu("Wavekeep/Heroes/Hero Animation Driver")]
    [DisallowMultipleComponent]
    public sealed class HeroAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        // "Attack" is used for BOTH the trigger parameter and the state name; the hashes are equal, but kept
        // as separate named consts for clarity at the call sites.
        private static readonly int AttackStateHash = Animator.StringToHash("Attack");

        private Action _onBasicAttackImpact;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        }

        /// <summary>
        /// True only when the Animator can actually play an attack: a controller is assigned, it exposes an
        /// <c>Attack</c> Trigger parameter, AND it has an <c>Attack</c> state to land in. While this is false
        /// (e.g. the controller is still empty or unassigned), the hero keeps its original immediate auto-fire,
        /// so animation gating can never silently break the basic attack. Evaluated once by HeroRuntime (the
        /// controller doesn't change at runtime), not per frame.
        /// </summary>
        public bool CanDriveAttack
        {
            get
            {
                if (_animator == null || _animator.runtimeAnimatorController == null) return false;
                if (!_animator.HasState(0, AttackStateHash)) return false;
                return HasAttackTrigger();
            }
        }

        /// <summary>Bind the callback invoked from the Attack clip's impact-frame Animation Event. The hero
        /// points this at its deferred basic-ability application.</summary>
        public void BindBasicAttackImpact(Action onImpact) => _onBasicAttackImpact = onImpact;

        /// <summary>Fire the Attack trigger once (called by the hero when it decides to attack). The
        /// controller's Attack→Idle exit-time transition returns to Idle on its own afterwards.</summary>
        public void TriggerAttack()
        {
            if (_animator != null) _animator.SetTrigger(AttackTrigger);
        }

        /// <summary>Animation-Event target on the Attack clip (Task 56 §2.3). Generic name so any hero reusing
        /// this clip wires the same event; the per-hero reaction is whatever was bound via
        /// <see cref="BindBasicAttackImpact"/>.</summary>
        // ReSharper disable once UnusedMember.Global — invoked by Unity's Animation Event system.
        public void OnBasicAttackImpactFrame() => _onBasicAttackImpact?.Invoke();

        private bool HasAttackTrigger()
        {
            var parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                    parameters[i].nameHash == AttackTrigger)
                    return true;
            }
            return false;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: let a setup script assign the Animator reference on the prefab.</summary>
        public void EditorSetAnimator(Animator animator) => _animator = animator;
#endif
    }
}
