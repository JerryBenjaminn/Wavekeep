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
    /// The frost variants are pure CC + area DPS with no following damage-over-time. The Firewall fire variant
    /// (<see cref="FireBox"/>) additionally applies a lingering Burn DoT the moment an enemy ENTERS the band
    /// (Task 53), on top of its area-tied per-tick DoT. A plain C# object ticked by a
    /// <see cref="GroundZoneManager"/>; it never touches SOs. Death-inside detection is reliable because
    /// <c>WaveSpawner</c> creates a NEW <see cref="EnemyRuntime"/> per spawn (the GameObject is pooled, the
    /// runtime isn't), so a dead enemy's <c>IsAlive</c> stays false — a death is counted exactly once.
    /// </summary>
    public sealed class GroundZone
    {
        private enum Shape { Circle, Box }

        // Task 48: placeholder radius of a Wildfire-Spread smoldering patch left by a death inside the Firewall.
        private const float WildfirePatchRadius = 1.5f;

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

        // Task 48 (Firewall): an optional FIRE layer on top of the same shape. Sustained DoT (absolute per-tick
        // damage), an independent Inferno-Surge burst (fraction of basic), and Wildfire-Spread death-patches
        // (a smaller fire patch spawned when an enemy dies inside — a SEPARATE zone, so it outlives this one).
        private readonly float _fireTickInterval;       // 0 = no fire DoT
        private readonly float _fireTickDamage;         // absolute damage per fire tick
        private float _fireTickTimer;
        private readonly float _burstInterval;          // 0 = no Inferno-Surge burst
        private readonly float _burstBasicFraction;     // burst damage = fraction × current basic damage
        private float _burstTimer;
        private readonly float _deathPatchDuration;     // 0 = no Wildfire-Spread patch
        private readonly float _deathPatchTickDamage;   // absolute per-tick damage of the spawned patch
        private readonly System.Action<GroundZone> _spawnSibling; // how a patch is added to the manager

        // Task 53 (Firewall): a one-shot Burn DoT applied the moment an enemy first ENTERS the band (a strong
        // Fireball-tier lingering Burn for "walking through and getting burned for it"), separate from the
        // per-tick band DoT above. 0 = none. Applied non-stacking, refreshed on each fresh entry.
        private readonly float _entryBurnPerTick;
        private readonly float _entryBurnDuration;

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
            IZoneVisual visual = null,
            float fireTickInterval = 0f, float fireTickDamage = 0f,
            float burstInterval = 0f, float burstBasicFraction = 0f,
            float deathPatchDuration = 0f, float deathPatchTickDamage = 0f,
            System.Action<GroundZone> spawnSibling = null,
            float entryBurnPerTick = 0f, float entryBurnDuration = 0f)
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
            _fireTickInterval = Mathf.Max(0f, fireTickInterval);
            _fireTickDamage = Mathf.Max(0f, fireTickDamage);
            _burstInterval = Mathf.Max(0f, burstInterval);
            _burstBasicFraction = Mathf.Max(0f, burstBasicFraction);
            _deathPatchDuration = Mathf.Max(0f, deathPatchDuration);
            _deathPatchTickDamage = Mathf.Max(0f, deathPatchTickDamage);
            _spawnSibling = spawnSibling;
            _entryBurnPerTick = Mathf.Max(0f, entryBurnPerTick);
            _entryBurnDuration = Mathf.Max(0f, entryBurnDuration);
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

        /// <summary>Task 48: full-width FIRE band (Firewall): sustained DoT (<paramref name="tickDamage"/> every
        /// <paramref name="tickInterval"/>), an Inferno-Surge burst (<paramref name="burstBasicFraction"/> of basic
        /// every <paramref name="burstInterval"/>), and Wildfire-Spread death-patches (when an enemy dies inside,
        /// spawn a fire patch lasting <paramref name="patchDuration"/> dealing <paramref name="patchTickDamage"/>
        /// per tick — added via <paramref name="spawnSibling"/> so it is a separate zone that OUTLIVES this band).
        /// No slow (fire only CCs through Burn elsewhere); the depth band is on Z, X is full arena width.
        /// Task 53: <paramref name="entryBurnPerTick"/>/<paramref name="entryBurnDuration"/> apply a lingering Burn
        /// the moment an enemy first enters the band (in addition to the per-tick DoT).</summary>
        public static GroundZone FireBox(
            float bandMinZ, float bandMaxZ, float duration,
            float tickInterval, float tickDamage,
            float burstInterval, float burstBasicFraction,
            float patchDuration, float patchTickDamage, System.Action<GroundZone> spawnSibling,
            IZoneVisual visual = null,
            float entryBurnPerTick = 0f, float entryBurnDuration = 0f)
        {
            return new GroundZone(Shape.Box, Vector3.zero, 0f, bandMinZ, bandMaxZ, duration, 0f,
                0f, 0.5f, 0f, 0f, 0f, visual,
                tickInterval, tickDamage, burstInterval, burstBasicFraction,
                patchDuration, patchTickDamage, spawnSibling,
                entryBurnPerTick, entryBurnDuration);
        }

        /// <summary>Task 48: a small circular FIRE patch (Wildfire-Spread leftover): sustained DoT only, no burst,
        /// no further patches. Spawned at a dead enemy's position; persists independently of the Firewall band.</summary>
        public static GroundZone FirePatch(Vector3 center, float radius, float duration,
            float tickInterval, float tickDamage)
        {
            return new GroundZone(Shape.Circle, center, radius, 0f, 0f, duration, 0f,
                0f, 0.5f, 0f, 0f, 0f, null,
                tickInterval, tickDamage, 0f, 0f, 0f, 0f, null);
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

            // Task 53 (Firewall on-entry Burn): an enemy that is inside NOW but was not last tick just crossed
            // into the band — apply a lingering Burn once on entry (non-stacking; refreshed on a later re-entry),
            // on top of the per-tick band DoT below. Runs while _insideLastTick still holds the previous set.
            if (_entryBurnPerTick > 0f && _entryBurnDuration > 0f)
            {
                for (int i = 0; i < _inside.Count; i++)
                {
                    var e = _inside[i];
                    if (!_insideLastTick.Contains(e))
                        e.ApplyBurn(_entryBurnPerTick, _entryBurnDuration, maxStacks: 0, perStackBonus: 0f);
                }
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

            // Task 48 (Firewall DoT): sustained per-tick damage to whoever is inside at each fire tick.
            if (_fireTickInterval > 0f && _fireTickDamage > 0f)
            {
                _fireTickTimer += deltaTime;
                while (_fireTickTimer >= _fireTickInterval)
                {
                    _fireTickTimer -= _fireTickInterval;
                    for (int i = 0; i < _inside.Count; i++)
                        if (_inside[i].IsAlive) _inside[i].TakeDamage(_fireTickDamage);
                }
            }

            // Task 48 (Inferno Surge): an independent periodic burst on top of the regular DoT — damage to
            // whoever is inside at the moment the burst fires (fraction of the caster's current basic damage).
            if (_burstInterval > 0f && _burstBasicFraction > 0f && basicDamage > 0f)
            {
                _burstTimer += deltaTime;
                while (_burstTimer >= _burstInterval)
                {
                    _burstTimer -= _burstInterval;
                    float dmg = _burstBasicFraction * basicDamage;
                    for (int i = 0; i < _inside.Count; i++)
                        if (_inside[i].IsAlive) _inside[i].TakeDamage(dmg);
                    _visual?.Pulse(); // Task 51: Inferno Surge flare on the SAME tick the burst damage lands
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

            // Task 48 (Wildfire Spread): each enemy that was inside last tick and is now dead leaves a small
            // smoldering fire patch at its position — spawned as a SEPARATE zone, so it persists after THIS
            // Firewall band expires (a reviewer-blocking requirement). The patch lifetime is the band's
            // REMAINING duration plus the patch duration, so the spec's "lasts Ns AFTER Firewall ends" holds
            // for every patch regardless of when the death occurred (not just deaths near the band's end).
            if (_deathPatchDuration > 0f && _deathPatchTickDamage > 0f && _spawnSibling != null)
            {
                float patchLife = Mathf.Max(0f, _remaining) + _deathPatchDuration;
                float patchTick = _fireTickInterval > 0f ? _fireTickInterval : 0.5f;
                foreach (var e in _insideLastTick)
                {
                    if (e == null || e.IsAlive) continue;
                    var pos = e.Transform.position;
                    _spawnSibling(FirePatch(pos, WildfirePatchRadius,
                        patchLife, patchTick, _deathPatchTickDamage));
                    // Task 51: a distinct cooling-ember after-patch visual at the SAME position/radius/lifetime as
                    // the gameplay patch (dim embers, no full flame). Independent of the wall band's own lifetime.
                    (_visual as IFireZoneVisual)?.SpawnCoolingPatch(pos.x, pos.z, WildfirePatchRadius, patchLife);
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
