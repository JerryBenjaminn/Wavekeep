using UnityEngine;

namespace Wavekeep.Input
{
    /// <summary>How <see cref="InteractionInputProvider"/> should pick an implementation.</summary>
    public enum InteractionInputMode
    {
        /// <summary>Touch on mobile platforms, mouse otherwise.</summary>
        Auto,
        /// <summary>Force the mouse implementation (useful for editor testing).</summary>
        Mouse,
        /// <summary>Force the touch implementation (useful for editor testing).</summary>
        Touch
    }

    /// <summary>
    /// Factory that selects the correct <see cref="IInteractionInput"/> implementation for the
    /// platform (CLAUDE.md §3.6). The chosen instance is exposed through
    /// <see cref="Wavekeep.Core.GameSession"/>; the override makes the switch provable in-editor.
    /// </summary>
    public static class InteractionInputProvider
    {
        public static IInteractionInput Create(InteractionInputMode mode, Camera camera, LayerMask groundMask)
        {
            bool useTouch = mode switch
            {
                InteractionInputMode.Touch => true,
                InteractionInputMode.Mouse => false,
                _ => Application.isMobilePlatform
            };

            return useTouch
                ? new TouchInteractionInput(camera, groundMask)
                : (IInteractionInput)new MouseInteractionInput(camera, groundMask);
        }
    }
}
