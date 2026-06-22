using UnityEngine;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// Sink for ability-execution visual feedback (Task 08 Part A). The pure-logic
    /// <c>AbilityRuntime</c> calls this at the EXACT point it resolves a target / radius and applies
    /// damage, passing the SAME values used for the real hit — so the visuals can never diverge from
    /// the actual targeting (a reviewer-blocking requirement). A MonoBehaviour presenter implements it;
    /// <c>AbilityRuntime</c> stays Unity-free and testable. A null feedback is a valid no-op (tests /
    /// headless).
    /// </summary>
    public interface IAbilityFeedback
    {
        /// <summary>A single-target hit: a beam from <paramref name="from"/> (caster) to the resolved
        /// target at <paramref name="to"/>.</summary>
        void OnSingleTargetHit(Vector3 from, Vector3 to);

        /// <summary>An AoE hit centered at <paramref name="center"/> (caster) using the ACTUAL
        /// <paramref name="radius"/> that was tested against enemies.</summary>
        void OnAreaOfEffect(Vector3 center, float radius);
    }
}
