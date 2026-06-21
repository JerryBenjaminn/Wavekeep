using UnityEngine;

namespace Wavekeep.Input
{
    /// <summary>
    /// Platform-agnostic interaction input (CLAUDE.md §3.6 / §3.7). The game is 3D with a
    /// fixed 3/4 top-down camera, so a screen tap/click must be resolved to a world-space
    /// position via <c>Physics.Raycast</c> against the ground/placement layer — never used
    /// as a raw 2D screen coordinate by gameplay code.
    ///
    /// Gameplay-adjacent code consumes this interface only; it must not call
    /// <c>UnityEngine.Input.*</c> or touch APIs directly.
    /// </summary>
    public interface IInteractionInput
    {
        /// <summary>
        /// Resolves the current pointer/touch position to a world point on the ground layer.
        /// Returns false if there is no active pointer or the ray misses the ground.
        /// </summary>
        bool TryGetInteractionPoint(out Vector3 worldPosition);

        /// <summary>True on the frame the primary interaction (tap / left-click) began.</summary>
        bool InteractionTriggeredThisFrame { get; }

        /// <summary>Raw screen-space pointer position, exposed for UI hit-testing.</summary>
        Vector2 ScreenPosition { get; }
    }
}
