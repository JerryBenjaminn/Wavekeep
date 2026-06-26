namespace Wavekeep.Abilities
{
    /// <summary>
    /// Task 51: the persistent visual handle for a Firewall <c>GroundZone</c> — the fire counterpart to the
    /// frost <see cref="IZoneVisual"/>, extending it so the zone can also request a Wildfire-Spread cooling
    /// ember patch at a death position. Created by <see cref="IAbilityFeedback.BeginFireWall"/> at cast and held
    /// by the zone, so the wall's pulse rhythm (Inferno Surge flares via <see cref="IZoneVisual.Pulse"/>) and
    /// teardown (<see cref="IZoneVisual.Dispose"/>) ride the SAME runtime object that runs the gameplay.
    ///
    /// Intentionally Unity-free so <c>GroundZone</c> stays pure-logic; the concrete implementation lives in the
    /// MonoBehaviour presenter. A null handle is a valid no-op.
    /// </summary>
    public interface IFireZoneVisual : IZoneVisual
    {
        /// <summary>Spawn a dim, cooling-ember after-patch (no full flame) at world (<paramref name="x"/>,
        /// <paramref name="z"/>) of <paramref name="radius"/>, living <paramref name="life"/> seconds. Called by
        /// the Firewall zone exactly when it spawns a Wildfire-Spread death-patch, with the SAME position/radius/
        /// lifetime the gameplay patch uses, so the visual never diverges. The patch is independent of this
        /// handle's lifetime (it persists after the wall expires).</summary>
        void SpawnCoolingPatch(float x, float z, float radius, float life);
    }
}
