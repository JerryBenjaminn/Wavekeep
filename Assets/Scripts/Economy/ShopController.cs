using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Runtime;
using Wavekeep.Waves;

namespace Wavekeep.Economy
{
    /// <summary>
    /// Boss-reward utility shop (Task 80 redesign). The old between-wave currency shop with stat-boosting
    /// potions is gone: the shop now opens ONLY after a boss wave is cleared and offers a small OFFER of
    /// UTILITY items from which the player picks exactly ONE, for FREE (no Currency, no reroll, no stacking).
    ///
    /// All effects act on the WALL or the ARENA, never on hero stats:
    /// <list type="bullet">
    /// <item><see cref="ConsumableEffectType.HealWall"/> — instant <c>WallRuntime.Heal</c> on pick.</item>
    /// <item><see cref="ConsumableEffectType.WallDamageReduction"/> / <see cref="ConsumableEffectType.WallShield"/>
    ///   — armed on pick and applied at the START of the next wave (so their timers cover that wave rather than
    ///   draining during the intermission), via <c>WallRuntime.SetDamageReduction</c> / <c>AddShield</c>.</item>
    /// <item><see cref="ConsumableEffectType.ArenaSlowZone"/> / <see cref="ConsumableEffectType.ArenaFreezeZone"/> /
    ///   <see cref="ConsumableEffectType.FlashFreeze"/> — armed on pick and spawned at the next wave's start into
    ///   the WaveSpawner's <c>ArenaZones</c> (the EXISTING <c>GroundZoneManager</c>/<c>GroundZone</c> + status-effect
    ///   system — no parallel arena path).</item>
    /// </list>
    /// Arena/wall-buff picks are ARMED (applied on the next <see cref="WaveStartedEvent"/>) because the shop opens
    /// during the between-wave intermission, when no enemies exist and the sim is paused; arming makes "one wave"
    /// precise and avoids draining a timer or ticking a zone through the pause.
    ///
    /// A non-static plain C# class (CLAUDE.md §3.5) built by the shop UI from <see cref="GameSession"/> services +
    /// the scene <see cref="WallRuntime"/>/<see cref="WaveSpawner"/> — injected, never static.
    /// </summary>
    public sealed class ShopController
    {
        private readonly WallRuntime _wall;
        private readonly WaveSpawner _waveSpawner;
        private readonly EventBus _events;
        private readonly IReadOnlyList<ConsumableDefinitionSO> _pool;
        private readonly int _offerSize;

        // Optional Luck/wave tier weighting for the offer draw (same as the old shop). Null → uniform draw.
        private readonly LuckState _luck;
        private readonly TierWeightingConfigSO _weightingConfig;
        private static readonly int ConsumableTierCount = System.Enum.GetValues(typeof(ConsumableTier)).Length;

        private readonly List<ConsumableDefinitionSO> _offer = new List<ConsumableDefinitionSO>();
        private readonly List<int> _drawIndices = new List<int>();
        private readonly List<float> _drawWeights = new List<float>();

        // Task 80: at most one free pick per offer.
        private bool _picked;

        // Task 80: the pick whose effect is armed to apply at the START of the next wave (wall buff / arena zone).
        // Applied and cleared by the persistent WaveStarted handler; instant effects (HealWall) never sit here.
        private ConsumableDefinitionSO _pendingNextWave;

        // Zone geometry defaults (metres) — flagged for tuning. AreaExtent on the SO overrides band depth per item.
        private const float DefaultBandDepth = 12f;
        private const float ZoneStatusRefresh = 0.5f; // re-applied each tick so CC lapses shortly after leaving

        public ShopController(
            WallRuntime wall,
            WaveSpawner waveSpawner,
            EventBus events,
            IReadOnlyList<ConsumableDefinitionSO> pool,
            int offerSize,
            LuckState luck = null,
            TierWeightingConfigSO weightingConfig = null)
        {
            _wall = wall;
            _waveSpawner = waveSpawner;
            _events = events;
            _pool = pool;
            _offerSize = Mathf.Clamp(offerSize, 1, 4); // "3–4 items" per the design; hard-cap at 4
            _luck = luck;
            _weightingConfig = weightingConfig;

            // Persistent arming hook: a single subscription applies whatever pick was armed this intermission at
            // the next wave's start. Disposed by the UI's OnDestroy (and by EventBus.UnsubscribeAll on teardown).
            _events?.Subscribe<WaveStartedEvent>(OnWaveStarted);
        }

        /// <summary>Release the arming subscription (call from the owning UI's OnDestroy).</summary>
        public void Dispose()
        {
            _events?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        }

        /// <summary>The items offered this boss-reward visit (read-only).</summary>
        public IReadOnlyList<ConsumableDefinitionSO> CurrentOffer => _offer;

        /// <summary>True once the player has taken their single free pick this offer.</summary>
        public bool HasPicked => _picked;

        /// <summary>Draw a fresh offer of up to <see cref="_offerSize"/> distinct utility items (tier-weighted by
        /// Luck/wave when a config is wired, else uniform). Resets the single-pick gate.</summary>
        public void GenerateOffer()
        {
            _offer.Clear();
            _picked = false;
            if (_pool == null) return;

            _drawIndices.Clear();
            _drawWeights.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] == null) continue;
                _drawIndices.Add(i);
                _drawWeights.Add(OfferWeight(_pool[i]));
            }

            int want = Mathf.Min(_offerSize, _drawIndices.Count);
            for (int k = 0; k < want; k++)
            {
                int picked = PickWeighted(k);
                (_drawIndices[k], _drawIndices[picked]) = (_drawIndices[picked], _drawIndices[k]);
                (_drawWeights[k], _drawWeights[picked]) = (_drawWeights[picked], _drawWeights[k]);
                _offer.Add(_pool[_drawIndices[k]]);
            }
        }

        /// <summary>True while <paramref name="item"/> is a valid, not-yet-taken pick from the current offer.</summary>
        public bool CanPick(ConsumableDefinitionSO item) =>
            item != null && !_picked && _offer.Contains(item);

        /// <summary>Take the single free pick: apply its effect and lock further picks this offer. Returns false
        /// (no change) if a pick was already taken or the item isn't in the offer.</summary>
        public bool Pick(ConsumableDefinitionSO item)
        {
            if (!CanPick(item)) return false;
            _picked = true;
            ApplyPick(item);
            Debug.Log($"[ShopController] Picked utility reward '{item.DisplayName}'.");
            return true;
        }

        private void ApplyPick(ConsumableDefinitionSO item)
        {
            switch (item.EffectType)
            {
                case ConsumableEffectType.HealWall:
                    // Repair the wall NOW — the boss wave just damaged it.
                    if (_wall != null) _wall.Heal(item.EffectValue);
                    else Debug.LogWarning("[ShopController] HealWall picked but no WallRuntime wired.");
                    break;

                // Wall buffs + arena zones protect/affect the NEXT wave → arm them for its start (§ arming note).
                case ConsumableEffectType.WallDamageReduction:
                case ConsumableEffectType.WallShield:
                case ConsumableEffectType.ArenaSlowZone:
                case ConsumableEffectType.ArenaFreezeZone:
                case ConsumableEffectType.FlashFreeze:
                    _pendingNextWave = item;
                    break;

                default:
                    Debug.LogWarning($"[ShopController] '{item.DisplayName}' has non-utility effect " +
                                     $"{item.EffectType}; ignored (utility-only shop, Task 80).");
                    break;
            }
        }

        private void OnWaveStarted(WaveStartedEvent _)
        {
            var item = _pendingNextWave;
            if (item == null) return;
            _pendingNextWave = null;
            ApplyNextWaveEffect(item);
        }

        private void ApplyNextWaveEffect(ConsumableDefinitionSO item)
        {
            switch (item.EffectType)
            {
                case ConsumableEffectType.WallDamageReduction:
                    _wall?.SetDamageReduction(item.EffectValue, item.Duration);
                    break;

                case ConsumableEffectType.WallShield:
                    _wall?.AddShield(item.EffectValue, item.Duration);
                    break;

                case ConsumableEffectType.ArenaSlowZone:
                    SpawnLaneZone(StatusEffectType.Slow, item.EffectValue, item.Duration, BandDepth(item), centerLane: true);
                    break;

                case ConsumableEffectType.ArenaFreezeZone:
                    SpawnLaneZone(StatusEffectType.Freeze, 0f, item.Duration, BandDepth(item), centerLane: false);
                    break;

                case ConsumableEffectType.FlashFreeze:
                    SpawnFullArenaFreeze(item.Duration);
                    break;
            }
        }

        // --- Arena geometry (reuses the WaveSpawner's wall/spawn Z, like Frost Zone / Firewall) ---------

        private float BandDepth(ConsumableDefinitionSO item) =>
            item.AreaExtent > 0f ? item.AreaExtent : DefaultBandDepth;

        // A full-width Z-band. centerLane=true → centred in the middle of the approach (Tar Field, wide slow);
        // false → hugging the wall (Glacial Choke freeze — enemies are frozen as they reach the choke).
        private void SpawnLaneZone(StatusEffectType status, float slowMagnitude, float duration, float depth, bool centerLane)
        {
            if (_waveSpawner == null || duration <= 0f) return;
            float wallZ = _waveSpawner.DefendedLineZ;
            float spawnZ = _waveSpawner.SpawnLineZ;
            float dir = Mathf.Sign(spawnZ - wallZ == 0f ? 1f : spawnZ - wallZ);

            float minZ, maxZ;
            if (centerLane)
            {
                float center = (wallZ + spawnZ) * 0.5f;
                minZ = center - depth * 0.5f;
                maxZ = center + depth * 0.5f;
            }
            else
            {
                // Near-wall band: from the wall outward toward the spawn edge by `depth`.
                minZ = wallZ;
                maxZ = wallZ + dir * depth;
            }

            _waveSpawner.ArenaZones.Spawn(
                GroundZone.ControlBox(minZ, maxZ, duration, status, slowMagnitude, ZoneStatusRefresh));
            Debug.Log($"[ShopController] Arena {status} zone z=[{Mathf.Min(minZ, maxZ):0.#},{Mathf.Max(minZ, maxZ):0.#}] " +
                      $"for {duration:0.#}s.");
        }

        // Flash Freeze: a short-lived FULL-ARENA freeze band at the wave's start (covers wall→spawn depth).
        private void SpawnFullArenaFreeze(float duration)
        {
            if (_waveSpawner == null || duration <= 0f) return;
            float wallZ = _waveSpawner.DefendedLineZ;
            float spawnZ = _waveSpawner.SpawnLineZ;
            _waveSpawner.ArenaZones.Spawn(
                GroundZone.ControlBox(wallZ, spawnZ, duration, StatusEffectType.Freeze, 0f, ZoneStatusRefresh));
            Debug.Log($"[ShopController] Flash Freeze (full arena) for {duration:0.#}s.");
        }

        // --- Offer draw weighting (unchanged from the previous shop) ------------------------------------

        private float OfferWeight(ConsumableDefinitionSO item)
        {
            if (_weightingConfig == null) return 1f;
            int ordinal = (int)item.Tier;
            float baseWeight = _weightingConfig.ShopBaseTierWeight(ordinal);
            float normTier = ConsumableTierCount > 1 ? (float)ordinal / (ConsumableTierCount - 1) : 0f;
            float multiplier = _luck != null ? _luck.ShopTierMultiplier(normTier) : 1f;
            return baseWeight * multiplier;
        }

        private int PickWeighted(int start)
        {
            float total = 0f;
            for (int i = start; i < _drawWeights.Count; i++) total += Mathf.Max(0f, _drawWeights[i]);
            if (total <= 0f) return Random.Range(start, _drawIndices.Count);

            float roll = Random.value * total;
            for (int i = start; i < _drawWeights.Count; i++)
            {
                roll -= Mathf.Max(0f, _drawWeights[i]);
                if (roll < 0f) return i;
            }
            return _drawIndices.Count - 1;
        }
    }
}
