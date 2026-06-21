using UnityEngine;
using UnityEngine.InputSystem;

namespace Wavekeep.Input
{
    /// <summary>
    /// Mobile interaction input. Reads the primary touch via the new Input System package
    /// (never legacy touch APIs). World resolution is inherited from
    /// <see cref="InteractionInputBase"/>, identical to the mouse path.
    /// </summary>
    public sealed class TouchInteractionInput : InteractionInputBase
    {
        public TouchInteractionInput(Camera camera, LayerMask groundMask)
            : base(camera, groundMask)
        {
        }

        protected override bool TryGetScreenPosition(out Vector2 screenPosition)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                screenPosition = Vector2.zero;
                return false;
            }

            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }

        public override bool InteractionTriggeredThisFrame
        {
            get
            {
                var touchscreen = Touchscreen.current;
                return touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame;
            }
        }
    }
}
