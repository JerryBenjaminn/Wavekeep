using Wavekeep.Data;

namespace Wavekeep.UI
{
    /// <summary>
    /// Plain-language labels + hover-tooltip text for gear/artifact stat rows (Task 25). A small STATIC
    /// lookup keyed by stat type (not per-item, per the task), so the detail panel renders a labeled row
    /// per <see cref="StatModifier"/> plus the Task 24 <c>luckBonus</c>. Display-only: it formats values
    /// the panel reads LIVE from the item's SO fields — it never stores or approximates item stats.
    /// </summary>
    public static class GearStatInfo
    {
        public const string LuckTooltip =
            "Luck improves the odds of higher-tier offers appearing in the shop, and has a smaller effect " +
            "on loot drop tiers. It does not affect combat.";

        /// <summary>Short label for one stat modifier, e.g. "+10 Damage", "-15% Cooldown".</summary>
        public static string Label(AbilityModifierType type, float value)
        {
            switch (type)
            {
                case AbilityModifierType.DamageFlatBonus: return $"{Signed(value)} Damage";
                case AbilityModifierType.DamageMultiplier: return $"{Percent(value)} Damage";
                case AbilityModifierType.CooldownMultiplier: return $"{Percent(value)} Cooldown";
                case AbilityModifierType.RangeMultiplier: return $"{Percent(value)} Range";
                case AbilityModifierType.SlowMagnitudeMultiplier: return $"{Percent(value)} Slow";
                default: return $"{type}: {value:0.##}";
            }
        }

        /// <summary>Plain-language description of what a stat does, shown on hover.</summary>
        public static string Tooltip(AbilityModifierType type)
        {
            switch (type)
            {
                case AbilityModifierType.DamageFlatBonus:
                case AbilityModifierType.DamageMultiplier:
                    return "Increases the damage your hero's abilities deal to enemies.";
                case AbilityModifierType.CooldownMultiplier:
                    return "Reduces your abilities' cooldowns, so they can be cast more often.";
                case AbilityModifierType.RangeMultiplier:
                    return "Increases the range or area your abilities reach.";
                case AbilityModifierType.SlowMagnitudeMultiplier:
                    return "Strengthens how much your slow effects reduce enemy movement speed.";
                default:
                    return "Modifies one of your hero's ability stats.";
            }
        }

        public static string LuckLabel(float value) => $"+{value:0.##} Luck";

        /// <summary>Task 26: signed delta for a stat-vs-equipped comparison. <paramref name="delta"/> is the
        /// raw difference of the two items' stored values (inspected − equipped). Multiplier stats render
        /// the difference as percentage points (e.g. 1.25 vs 1.20 → "+5%"); flat stats render as-is.</summary>
        public static string Delta(AbilityModifierType type, float delta)
        {
            switch (type)
            {
                case AbilityModifierType.DamageMultiplier:
                case AbilityModifierType.CooldownMultiplier:
                case AbilityModifierType.RangeMultiplier:
                case AbilityModifierType.SlowMagnitudeMultiplier:
                    return Signed(delta * 100f) + "%";
                default: // DamageFlatBonus (and any future flat stat)
                    return Signed(delta);
            }
        }

        /// <summary>Task 26: signed delta for the (flat) Luck stat.</summary>
        public static string LuckDelta(float delta) => Signed(delta);

        private static string Signed(float v) => (v >= 0f ? "+" : "") + v.ToString("0.##");

        // Modifier values are multipliers (1.2 = +20%, 0.85 = -15%); show the delta from 1.0 as a percent.
        private static string Percent(float multiplier)
        {
            float pct = (multiplier - 1f) * 100f;
            return (pct >= 0f ? "+" : "") + pct.ToString("0.#") + "%";
        }
    }
}
