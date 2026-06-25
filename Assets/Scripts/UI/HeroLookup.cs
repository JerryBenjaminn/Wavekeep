using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Runtime;

namespace Wavekeep.UI
{
    /// <summary>
    /// Task 36: tiny helper that resolves the run's active heroes for HUD components. Prefers the session's
    /// <see cref="GameSession.Heroes"/> registry (the designed source of truth); if no
    /// <see cref="GameSessionBootstrap"/> is wired, it falls back to a cached scene scan so older scenes that
    /// haven't been re-run through the Task 36 setup still render. NOT a static singleton — each HUD owns its
    /// own instance and the registry lives on <see cref="GameSession"/> (CLAUDE.md §3.5).
    /// </summary>
    internal sealed class HeroLookup
    {
        private HeroRuntime[] _fallback;

        public IReadOnlyList<HeroRuntime> Get(GameSessionBootstrap bootstrap)
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session != null && session.Heroes != null && session.Heroes.Heroes.Count > 0)
                return session.Heroes.Heroes;

            // Fallback: scan the scene (stable InstanceID order) when no registry is available, re-scanning
            // only while empty or after a hero is destroyed — not every frame.
            if (_fallback == null || _fallback.Length == 0 || HasNull(_fallback))
                _fallback = Object.FindObjectsByType<HeroRuntime>(FindObjectsSortMode.InstanceID);
            return _fallback;
        }

        private static bool HasNull(HeroRuntime[] heroes)
        {
            for (int i = 0; i < heroes.Length; i++) if (heroes[i] == null) return true;
            return false;
        }
    }
}
