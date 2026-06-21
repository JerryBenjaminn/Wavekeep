using UnityEngine;
using UnityEngine.InputSystem;

namespace Wavekeep.Input
{
    /// <summary>
    /// PC interaction input. Reads the mouse via the new Input System package (never the
    /// legacy <c>UnityEngine.Input</c> class). World resolution is inherited from
    /// <see cref="InteractionInputBase"/>.
    /// </summary>
    public sealed class MouseInteractionInput : InteractionInputBase
    {
        public MouseInteractionInput(Camera camera, LayerMask groundMask)
            : base(camera, groundMask)
        {
        }

        protected override bool TryGetScreenPosition(out Vector2 screenPosition)
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                screenPosition = Vector2.zero;
                return false;
            }

            screenPosition = mouse.position.ReadValue();
            return true;
        }

        public override bool InteractionTriggeredThisFrame =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }
}
