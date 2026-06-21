using UnityEngine;

namespace Wavekeep.Input
{
    /// <summary>
    /// Shared base for interaction-input implementations. Holds the identical 3D resolution
    /// logic (screen position → world position via <c>Physics.Raycast</c> against the ground
    /// layer). Subclasses only supply how the screen position and trigger are read, so touch
    /// and mouse differ only in their input source, not in world resolution (CLAUDE.md §3.7).
    /// </summary>
    public abstract class InteractionInputBase : IInteractionInput
    {
        private const float MaxRayDistance = 1000f;

        private readonly Camera _camera;
        private readonly LayerMask _groundMask;

        protected InteractionInputBase(Camera camera, LayerMask groundMask)
        {
            _camera = camera;
            _groundMask = groundMask;
        }

        /// <summary>Source-specific screen position read (mouse cursor vs. primary touch).</summary>
        protected abstract bool TryGetScreenPosition(out Vector2 screenPosition);

        /// <inheritdoc />
        public abstract bool InteractionTriggeredThisFrame { get; }

        /// <inheritdoc />
        public Vector2 ScreenPosition => TryGetScreenPosition(out var sp) ? sp : Vector2.zero;

        /// <inheritdoc />
        public bool TryGetInteractionPoint(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (_camera == null) return false;
            if (!TryGetScreenPosition(out var screenPosition)) return false;

            var ray = _camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, MaxRayDistance, _groundMask))
            {
                worldPosition = hit.point;
                return true;
            }

            return false;
        }
    }
}
