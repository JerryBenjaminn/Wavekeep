using System.Collections.Generic;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 31 (Pass 2): owns and ticks the active <see cref="GroundZone"/>s (Frost Zone + Frozen Ground
    /// patches). A plain C# object owned per-run by <see cref="HeroRuntime"/> (no static singleton); abilities
    /// spawn zones into it via the execution context, and HeroRuntime advances it each frame (frozen with the
    /// rest of gameplay while paused) with the live active-enemy list and the caster's current basic damage.
    /// Expired zones are dropped automatically.
    /// </summary>
    public sealed class GroundZoneManager
    {
        private readonly List<GroundZone> _zones = new List<GroundZone>();

        public void Spawn(GroundZone zone)
        {
            if (zone != null) _zones.Add(zone);
        }

        public void Tick(float deltaTime, IReadOnlyList<EnemyRuntime> enemies, float basicDamage)
        {
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                _zones[i].Tick(deltaTime, enemies, basicDamage);
                if (_zones[i].IsExpired) _zones.RemoveAt(i);
            }
        }

        public void Clear() => _zones.Clear();
    }
}
