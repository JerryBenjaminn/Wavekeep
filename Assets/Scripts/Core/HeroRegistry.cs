using System.Collections.Generic;
using Wavekeep.Runtime;

namespace Wavekeep.Core
{
    /// <summary>
    /// Task 36: the run's set of active <see cref="HeroRuntime"/> instances, owned by
    /// <see cref="GameSession"/> (CLAUDE.md §3.5 — NOT a static singleton). Heroes self-register here as
    /// they are spawned/initialised, so any system that needs to act on "all active heroes" (the level-up
    /// card pool, the team ultimate input, the cooldown HUD) reaches them through the session rather than a
    /// global lookup. Registration order is spawn order, which the team input uses to bind per-hero keys.
    /// </summary>
    public sealed class HeroRegistry
    {
        private readonly List<HeroRuntime> _heroes = new List<HeroRuntime>();

        /// <summary>The active heroes, in spawn/registration order.</summary>
        public IReadOnlyList<HeroRuntime> Heroes => _heroes;

        /// <summary>Add a hero (idempotent — a re-registered instance is ignored).</summary>
        public void Register(HeroRuntime hero)
        {
            if (hero != null && !_heroes.Contains(hero)) _heroes.Add(hero);
        }

        public void Clear() => _heroes.Clear();
    }
}
