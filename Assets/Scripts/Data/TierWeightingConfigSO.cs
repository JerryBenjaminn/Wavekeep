using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored tuning for the Task 24 Luck-driven tier weighting. Read-only at runtime
    /// (CLAUDE.md §3.5) — the math lives in <c>Wavekeep.Economy.TierWeighting</c>, which reads these
    /// values. Keeps every magnitude out of code (CLAUDE.md §4 no-magic-numbers): how strongly Luck and
    /// wave-progress push offers toward higher tiers, the wave at which wave-progress maxes out, the
    /// base per-tier shop odds, the Luck display ceiling, and the weaker loot-table strength multiplier.
    ///
    /// ONE shared weighting approach drives both the shop offer roll and the loot-table roll — they
    /// differ only by <see cref="LootStrengthMultiplier"/> (loot is deliberately ~a quarter as strong),
    /// not by duplicated logic. The formula guarantees the lowest tier's weight is never reduced (its
    /// multiplier is always 1), so no tier's odds can reach zero.
    /// </summary>
    [CreateAssetMenu(fileName = "TierWeightingConfig", menuName = "Wavekeep/Tier Weighting Config")]
    public sealed class TierWeightingConfigSO : ScriptableObject
    {
        [Header("Luck")]
        [Tooltip("Display/calculation ceiling for total Luck (CLAUDE.md/Task 24: clamped 0–100). Luck is " +
                 "normalised against this when computing tier influence.")]
        [SerializeField, Min(1f)] private float _maxLuck = 100f;

        [Header("Influence weights (Luck should dominate wave progress)")]
        [Tooltip("How much current Luck contributes to pushing offers toward higher tiers. Should be the " +
                 "STRONGER of the two factors (Luck > Wave).")]
        [SerializeField, Min(0f)] private float _luckWeight = 0.75f;
        [Tooltip("How much run progress (current wave) contributes. The WEAKER factor — a late-run low-Luck " +
                 "player still sees some lift, but less than a high-Luck player.")]
        [SerializeField, Min(0f)] private float _waveWeight = 0.25f;
        [Tooltip("Wave number at which the wave-progress contribution reaches its maximum (1.0). Beyond this " +
                 "wave, wave progress is clamped — only more Luck improves odds further.")]
        [SerializeField, Min(1)] private int _waveProgressMaxWave = 20;

        [Header("Shop")]
        [Tooltip("Overall strength of the shop's tier shift. Higher = a fully Luck'd, late-run player sees " +
                 "dramatically more high-tier offers; 0 = flat odds (no weighting).")]
        [SerializeField, Min(0f)] private float _shopStrength = 4f;
        [Tooltip("Base relative odds per ConsumableTier (index 0 = Tier1 … ascending). These are the static " +
                 "odds at zero influence; weighting multiplies them. Lengths shorter than the tier count fall " +
                 "back to 1. Keep all entries > 0 so no tier is ever unreachable.")]
        [SerializeField] private List<float> _shopBaseTierWeights = new List<float> { 6f, 3f, 1f };

        [Header("Loot (secondary, intentionally weaker)")]
        [Tooltip("Loot uses the SAME weighting step as the shop, scaled by this multiplier (≈0.25 = roughly a " +
                 "quarter of the shop's strength). It only reweights tiers already eligible to drop — it never " +
                 "touches the boss-exclusive rarity lock (which is enforced by table contents, not here).")]
        [SerializeField, Range(0f, 1f)] private float _lootStrengthMultiplier = 0.25f;

        public float MaxLuck => _maxLuck;
        public float LuckWeight => _luckWeight;
        public float WaveWeight => _waveWeight;
        public int WaveProgressMaxWave => _waveProgressMaxWave;
        public float ShopStrength => _shopStrength;
        public float LootStrengthMultiplier => _lootStrengthMultiplier;

        /// <summary>Base relative odds for a shop tier (ascending ordinal). Falls back to 1 when not authored.</summary>
        public float ShopBaseTierWeight(int tierOrdinal)
        {
            if (_shopBaseTierWeights != null && tierOrdinal >= 0 && tierOrdinal < _shopBaseTierWeights.Count)
            {
                return Mathf.Max(0.0001f, _shopBaseTierWeights[tierOrdinal]);
            }
            return 1f;
        }
    }
}
