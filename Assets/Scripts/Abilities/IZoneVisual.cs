namespace Wavekeep.Abilities
{
    /// <summary>
    /// Task 45: a lightweight handle to the persistent visual for a <c>GroundZone</c> (Frost Zone). Created
    /// by <see cref="IAbilityFeedback.BeginZone"/> at cast and held by the zone itself, so the visual's
    /// lifetime and pulse rhythm are driven by the SAME runtime object that applies the gameplay — never a
    /// parallel timer. The zone calls <see cref="Pulse"/> from inside its real pulse loop (so a pulse flash
    /// always lands on an actual damage tick) and <see cref="Dispose"/> exactly once when it expires.
    ///
    /// Intentionally Unity-free (no UnityEngine types) so <c>GroundZone</c> stays a pure-logic object; the
    /// concrete implementation lives in the MonoBehaviour presenter. A null handle is a valid no-op.
    /// </summary>
    public interface IZoneVisual
    {
        /// <summary>Flash the zone on a pulse tick (called from the zone's actual pulse cadence).</summary>
        void Pulse();

        /// <summary>Tear the zone visual down (called once when the zone expires).</summary>
        void Dispose();
    }
}
