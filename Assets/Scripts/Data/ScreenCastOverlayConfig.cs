using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Task 57 (Part B) — designer-tunable settings for one hero's brief, full-screen "cast effect" overlay,
    /// shown on Ultimate cast. Purely data (no behaviour): the generic <c>ScreenCastOverlayController</c> reads
    /// these to fade a full-screen image in/hold/out. Kept small and serializable so it can live directly on
    /// <see cref="HeroDefinitionSO"/> (one per hero) — adding a new hero's overlay is just new data + a trigger
    /// call, never new controller code.
    ///
    /// Design intent (Task 57 §0): every hero's Ultimate is cast often, so each overlay must be brief and subtle
    /// (low <see cref="MaxOpacity"/>, short durations) to avoid clutter when several overlap.
    /// </summary>
    [Serializable]
    public sealed class ScreenCastOverlayConfig
    {
        [Tooltip("Full-screen sprite (e.g. a window-frost frame). Optional — leave null for a plain tinted flash " +
                 "using Tint only.")]
        [SerializeField] private Sprite _sprite;

        [Tooltip("Multiplies the sprite's colour, or IS the colour when no sprite is set. Alpha here is ignored — " +
                 "opacity is driven by the fade animation up to MaxOpacity.")]
        [SerializeField] private Color _tint = new Color(0.6f, 0.85f, 1f, 1f);

        [Tooltip("Seconds to fade from 0 → MaxOpacity.")]
        [SerializeField, Min(0f)] private float _fadeInDuration = 0.1f;

        [Tooltip("Seconds held at MaxOpacity between fade-in and fade-out.")]
        [SerializeField, Min(0f)] private float _holdDuration = 0.3f;

        [Tooltip("Seconds to fade from MaxOpacity → 0.")]
        [SerializeField, Min(0f)] private float _fadeOutDuration = 0.1f;

        [Tooltip("Peak opacity [0..1]. Keep low (≈0.3–0.4) so the arena stays visible and overlapping overlays " +
                 "don't obscure the screen. Defaults to 0 (INACTIVE) so heroes without an authored overlay never " +
                 "flash — set it above 0 to enable this hero's overlay.")]
        [SerializeField, Range(0f, 1f)] private float _maxOpacity;

        public Sprite Sprite => _sprite;
        public Color Tint => _tint;
        public float FadeInDuration => _fadeInDuration;
        public float HoldDuration => _holdDuration;
        public float FadeOutDuration => _fadeOutDuration;
        public float MaxOpacity => _maxOpacity;

        /// <summary>True if this config would actually show anything (some opacity + some duration). Lets the
        /// trigger path no-op cleanly for heroes that haven't authored an overlay yet.</summary>
        public bool IsActive =>
            _maxOpacity > 0f && (_fadeInDuration + _holdDuration + _fadeOutDuration) > 0f;
    }
}
