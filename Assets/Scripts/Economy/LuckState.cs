using System;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;

namespace Wavekeep.Economy
{
    /// <summary>
    /// Per-run Luck + run-progress service (Task 24). A non-static plain C# class owned by
    /// <c>GameSession</c> (no static <c>Instance</c>, CLAUDE.md §3.5) and reconstructed fresh each scene
    /// load — so the in-run potion bonus naturally RESETS to zero at run end, like other per-run state
    /// (CLAUDE.md §6), while the persistent gear-derived portion is re-fed by <c>HeroRuntime</c> on init.
    ///
    /// It is the single numeric source of truth for the live Luck total: <c>HeroRuntime</c> feeds it the
    /// hero base + summed equipped-gear Luck, the shop feeds it potion bonuses, and the stat panel reads
    /// it back through <c>HeroRuntime.CurrentLuck</c>. The total is clamped to 0..<see cref="ConfigMaxLuck"/>
    /// (0–100 by default). It tracks the current wave by subscribing to <see cref="WaveStartedEvent"/>, so
    /// both the shop and loot roll can ask for run progress without referencing the scene's WaveSpawner.
    ///
    /// The two <c>*TierMultiplier</c> helpers both route through the SAME <see cref="TierWeighting.Multiplier"/>
    /// step (shop vs loot differ only by strength) — there is no second, duplicated weighting path.
    /// </summary>
    public sealed class LuckState : IDisposable
    {
        private readonly EventBus _events;
        private readonly TierWeightingConfigSO _config;

        private float _heroBaseLuck;
        private float _gearLuck;
        private float _potionLuck;
        private int _currentWaveNumber; // 1-based; 0 before the first wave starts

        public LuckState(EventBus events, TierWeightingConfigSO config)
        {
            _events = events;
            _config = config;
            _events?.Subscribe<WaveStartedEvent>(OnWaveStarted);
        }

        // WaveStartedEvent carries a 0-based index (Task 02); store it 1-based to match WaveSpawner.CurrentWaveNumber.
        private void OnWaveStarted(WaveStartedEvent evt) => _currentWaveNumber = evt.WaveIndex + 1;

        /// <summary>Luck ceiling from config (defaults to 100 when no config is wired).</summary>
        public float ConfigMaxLuck => _config != null ? _config.MaxLuck : 100f;

        /// <summary>The live, clamped total Luck = hero base + equipped-gear Luck + in-run potion bonus.</summary>
        public float CurrentLuck => Mathf.Clamp(_heroBaseLuck + _gearLuck + _potionLuck, 0f, ConfigMaxLuck);

        /// <summary>1-based number of the current/last-started wave (0 before the run begins).</summary>
        public int CurrentWaveNumber => _currentWaveNumber;

        /// <summary>Current Luck normalised to 0..1 against the configured ceiling.</summary>
        public float Luck01 => Mathf.Clamp01(CurrentLuck / ConfigMaxLuck);

        /// <summary>Run progress normalised to 0..1, maxing out at the configured wave (then clamped).</summary>
        public float WaveProgress01
        {
            get
            {
                int maxWave = _config != null ? _config.WaveProgressMaxWave : 20;
                if (maxWave <= 0) return 0f;
                return Mathf.Clamp01((float)_currentWaveNumber / maxWave);
            }
        }

        /// <summary>Set the hero base + gear-derived Luck. Called by <c>HeroRuntime</c> on init (and on any
        /// equip-changed recompute) — NOT per frame. Potion bonus is left untouched.</summary>
        public void SetHeroLuck(float heroBaseLuck, float gearLuck)
        {
            _heroBaseLuck = Mathf.Max(0f, heroBaseLuck);
            _gearLuck = Mathf.Max(0f, gearLuck);
        }

        /// <summary>Add an in-run, non-persistent Luck bonus from a Luck Potion (Task 24). Routed here by
        /// <c>ShopController</c> through the normal purchase→effect flow.</summary>
        public void AddPotionBonus(float amount)
        {
            if (amount <= 0f) return;
            _potionLuck += amount;
        }

        // --- Shared weighting entry points (shop vs loot differ only by strength) -------------------

        /// <summary>Shop tier weight multiplier for a tier at <paramref name="normalizedTier01"/> (0..1),
        /// using current Luck + wave progress at full shop strength.</summary>
        public float ShopTierMultiplier(float normalizedTier01)
        {
            if (_config == null) return 1f;
            return TierWeighting.Multiplier(
                normalizedTier01, Luck01, WaveProgress01,
                _config.LuckWeight, _config.WaveWeight, _config.ShopStrength);
        }

        /// <summary>Loot tier weight multiplier — the SAME step as the shop, scaled by the loot strength
        /// multiplier (intentionally weaker). Only reweights eligible tiers; never affects the boss lock.</summary>
        public float LootTierMultiplier(float normalizedTier01)
        {
            if (_config == null) return 1f;
            return TierWeighting.Multiplier(
                normalizedTier01, Luck01, WaveProgress01,
                _config.LuckWeight, _config.WaveWeight, _config.ShopStrength * _config.LootStrengthMultiplier);
        }

        public void Dispose()
        {
            _events?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        }
    }
}
