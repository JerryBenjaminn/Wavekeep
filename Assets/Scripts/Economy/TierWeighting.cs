using UnityEngine;

namespace Wavekeep.Economy
{
    /// <summary>
    /// The single, shared tier-weighting step for Task 24 — used by BOTH the shop offer roll and the
    /// loot-table roll (a reviewer-blocking requirement: one approach, not two duplicated calculations).
    /// Pure, static, side-effect-free math so it is trivially testable and has no scene/SO dependency;
    /// callers supply the tunable inputs read from <c>TierWeightingConfigSO</c>.
    ///
    /// Given a tier's position within its range (0 = lowest, 1 = highest), the current Luck (normalised
    /// 0..1), and run progress (normalised 0..1), it returns a weight MULTIPLIER applied to that tier's
    /// base weight. Key properties:
    /// <list type="bullet">
    /// <item>The lowest tier (<paramref name="normalizedTier01"/> = 0) always returns 1 — its base weight
    ///   is never reduced, so its odds shrink relatively but can never reach zero.</item>
    /// <item>Higher tiers are boosted more as Luck and/or wave progress rise; Luck dominates because the
    ///   caller passes a larger <paramref name="luckWeight"/> than <paramref name="waveWeight"/>.</item>
    /// <item>Loot reuses this exact method with a smaller <paramref name="strength"/> (shop strength ×
    ///   the loot multiplier), which is the ONLY difference between the two reweightings.</item>
    /// </list>
    /// </summary>
    public static class TierWeighting
    {
        /// <summary>
        /// Multiplier (&gt;= 1) applied to a tier's base weight.
        /// </summary>
        /// <param name="normalizedTier01">Tier position within its range: 0 = lowest tier, 1 = highest.</param>
        /// <param name="luck01">Current total Luck normalised to 0..1 (luck / maxLuck).</param>
        /// <param name="waveProgress01">Run progress normalised to 0..1 (currentWave / maxWave).</param>
        /// <param name="luckWeight">Relative contribution of Luck (should exceed waveWeight).</param>
        /// <param name="waveWeight">Relative contribution of wave progress (the weaker factor).</param>
        /// <param name="strength">Overall strength scalar (shop strength, or shop × loot multiplier).</param>
        public static float Multiplier(
            float normalizedTier01, float luck01, float waveProgress01,
            float luckWeight, float waveWeight, float strength)
        {
            float tier = Mathf.Clamp01(normalizedTier01);
            float influence = luckWeight * Mathf.Clamp01(luck01) + waveWeight * Mathf.Clamp01(waveProgress01);
            // Lowest tier (tier == 0) → multiplier 1 regardless of influence: it is never penalised to zero.
            return 1f + Mathf.Max(0f, strength) * tier * influence;
        }
    }
}
