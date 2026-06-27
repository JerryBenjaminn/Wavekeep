using Wavekeep.Data;

namespace Wavekeep.Core
{
    /// <summary>
    /// Task 57 (Part B) — abstraction for the screen-space cast-overlay system, exposed by
    /// <see cref="GameSession"/> so any hero can request a brief full-screen flash on Ultimate cast without a
    /// static singleton (CLAUDE.md §3.5) and without knowing about the UI implementation or other heroes.
    /// Implemented by the scene's <c>ScreenCastOverlayController</c>.
    /// </summary>
    public interface IScreenCastOverlay
    {
        /// <summary>Show one overlay described by <paramref name="config"/>. Safe to call with a null/inactive
        /// config (no-ops). Concurrent calls coexist (each fades independently) so overlapping hero casts stack
        /// without restarting one another.</summary>
        void Trigger(ScreenCastOverlayConfig config);
    }
}
