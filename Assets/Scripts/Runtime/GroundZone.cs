using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Abilities;
using Wavekeep.Data;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 31/33: a persistent, positional area effect on the ground — the runtime backing for Frost
    /// Warden's Frost Zone (ultimate) and Frozen Ground patches (basic). Each tick it acts on the enemies
    /// CURRENTLY inside its shape:
    /// <list type="bullet">
    /// <item>refreshes a Slow on everyone inside (so it lapses shortly after they leave — "while standing in it");</item>
    /// <item>optionally deals an AREA-TIED pulse: damage to whoever is inside at the moment of each pulse
    ///   (an enemy that exits before a pulse takes nothing — never a per-target DoT that follows them);</item>
    /// <item>optionally EXTENDS its remaining duration when an enemy dies inside it (Absolute Zero, Task 33),
    ///   clamped to a hard cap so uptime can't run away.</item>
    /// </list>
    /// Two shapes (Task 33): a <see cref="Circle"/> (Frozen Ground patches, caster/impact-centred) and a
    /// full-width <see cref="Box"/> Z-band (Frost Zone — spans the whole arena width across a fixed depth in
    /// front of the wall; X is unconstrained, so it is a true full-width rectangle, not a big circle).
    ///
    /// Pure CC + area DPS, no damage-over-time on the enemy. A plain C# object ticked by a
    /// <see cref="GroundZoneManager"/>; it never touches SOs. Death-inside detection is reliable because
    /// <c>WaveSpawner</c> creates a NEW <see cref="EnemyRuntime"/> per spawn (the GameObject is pooled, the
    /// runtime isn't), so a dead enemy's <c>IsAlive</c> stays false — a death is counted exactly once.
    /// </summary>
    public sealed class GroundZone
    {
        private enum Shape { Circle, Box }

        private readonly Shape _shape;
        private readonly Vector3 _center;   // Circle
        private readonly float _radiusSqr;  // Circle
        private readonly float _bandMinZ;   // Box (full-width Z-band)
        private readonly float _bandMaxZ;

        private float _remaining;
        private readonly float _maxDuration;            // hard cap for duration extensions
        private readonly float _slowMagnitude;
        private readonly float _slowRefresh;            // short; re-applied each tick so slow lapses after leaving
        private readonly float _pulseInterval;          // 0 = no pulse
        private readonly float _pulseFraction;          // damage = fraction × current basic damage
        private float _pulseTimer;
        private readonly float _durationExtendPerDeath; // Absolute Zero (Task 33); 0 = none

        // Task 45: optional persistent visual for this zone (Frost Zone band). The zone OWNS its handle so
        // the visual's pulse rhythm + teardown are driven by the SAME object that runs the gameplay — pulses
        // flash on the real damage ticks, and the band disappears exactly when the zone expires.
        private readonly IZoneVisual _visual;
        private bool _visualDisposed;

        private readonly HashSet<EnemyRuntime> _insideLastTick = new HashSet<EnemyRuntime>();
        private readonly List<EnemyRuntime> _inside = new List<EnemyRuntime>();

        public bool IsExpired => _remaining <= 0f;

        private GroundZone(
            Shape shape, Vector3 center, float radius, float bandMinZ, float bandMaxZ,
            float duration, float maxDuration, float slowMagnitude, float slowRefresh,
            float pulseInterval, float pulseFraction, float durationExtendPerDeath,
            IZoneVisual visual = null)
        {
            _visual = visual;
            _shape = shape;
            _center = center;
            float r = Mathf.Max(0f, radius);
            _radiusSqr = r * r;
            _bandMinZ = Mathf.Min(bandMinZ, bandMaxZ);
            _bandMaxZ = Mathf.Max(bandMinZ, bandMaxZ);
            _remaining = Mathf.Max(0f, duration);
            _maxDuration = maxDuration <= 0f ? _remaining : Mathf.Max(_remaining, maxDuration);
            _slowMagnitude = Mathf.Clamp01(slowMagnitude);
            _slowRefresh = Mathf.Max(0.05f, slowRefresh);
            _pulseInterval = Mathf.Max(0f, pulseInterval);
            _pulseFraction = Mathf.Max(0f, pulseFraction);
            _durationExtendPerDeath = Mathf.Max(0f, durationExtendPerDeath);
        }

        /// <summary>Caster/impact-centred circular zone (Frozen Ground patches).</summary>
        public static GroundZone Circle(
            Vector3 center, float radius, float duration, float slowMagnitude, float slowRefresh,
            float pulseInterval = 0f, float pulseFraction = 0f,
            float durationExtendPerDeath = 0f, float maxDuration = 0f)
        {
            return new GroundZone(Shape.Circle, center, radius, 0f, 0f, duration, maxDuration,
                slowMagnitude, slowRefresh, pulseInterval, pulseFraction, durationExtendPerDeath);
        }

        /// <summary>Full-width Z-band zone (Frost Zone): any X, Z within [<paramref name="bandMinZ"/>,
        /// <paramref name="bandMaxZ"/>]. Covers the whole arena width across a fixed depth in front of the wall.</summary>
        public static GroundZone Box(
            float bandMinZ, float bandMaxZ, float duration, float maxDuration, float slowMagnitude, float slowRefresh,
            float pulseInterval, float pulseFraction, float durationExtendPerDeath, IZoneVisual visual = null)
        {
            return new GroundZone(Shape.Box, Vector3.zero, 0f, bandMinZ, bandMaxZ, duration, maxDuration,
                slowMagnitude, slowRefresh, pulseInterval, pulseFraction, durationExtendPerDeath, visual);
        }

        private bool IsInside(Vector3 pos)
        {
            // Box: full arena width (X unconstrained) across the depth band on Z. Circle: radial distance.
            return _shape == Shape.Box
                ? pos.z >= _bandMinZ && pos.z <= _bandMaxZ
                : (pos - _center).sqrMagnitude <= _radiusSqr;
        }

        public void Tick(float deltaTime, IReadOnlyList<EnemyRuntime> enemies, float basicDamage)
        {
            _remaining -= deltaTime;

            // Snapshot the enemies inside right now (alive). All effects this tick use THIS set.
            _inside.Clear();
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e == null || !e.IsAlive) continue;
                    if (IsInside(e.Transform.position)) _inside.Add(e);
                }
            }

            // Slow everyone inside (refreshed; lapses shortly after they leave).
            if (_slowMagnitude > 0f)
            {
                for (int i = 0; i < _inside.Count; i++)
                    _inside[i].ApplyStatusEffect(StatusEffectType.Slow, _slowMagnitude, _slowRefresh);
            }

            // Area-tied pulse: damage whoever is inside at the moment the pulse fires.
            if (_pulseInterval > 0f && _pulseFraction > 0f && basicDamage > 0f)
            {
                _pulseTimer += deltaTime;
                while (_pulseTimer >= _pulseInterval)
                {
                    _pulseTimer -= _pulseInterval;
                    float dmg = _pulseFraction * basicDamage;
                    for (int i = 0; i < _inside.Count; i++)
                        if (_inside[i].IsAlive) _inside[i].TakeDamage(dmg);
                    _visual?.Pulse(); // Task 45: flash the band on the SAME tick damage actually lands
                }
            }

            // Absolute Zero (Task 33): each enemy that was inside last tick and is now dead extends the
            // zone's remaining duration by the per-death amount, clamped to the hard cap (no runaway uptime).
            if (_durationExtendPerDeath > 0f)
            {
                foreach (var e in _insideLastTick)
                {
                    if (e != null && !e.IsAlive)
                        _remaining = Mathf.Min(_remaining + _durationExtendPerDeath, _maxDuration);
                }
            }

            // Remember this tick's inside set for next-tick death detection.
            _insideLastTick.Clear();
            for (int i = 0; i < _inside.Count; i++) _insideLastTick.Add(_inside[i]);

            // Task 45: tear the visual down the moment the zone expires (the manager drops it this same frame).
            if (_remaining <= 0f) DisposeVisual();
        }

        /// <summary>Task 45: dispose the zone's visual handle exactly once (on natural expiry, or when the
        /// manager is cleared on run teardown) so the band never lingers past the zone's life.</summary>
        public void DisposeVisual()
        {
            if (_visualDisposed) return;
            _visualDisposed = true;
            _visual?.Dispose();
        }
    }
}
