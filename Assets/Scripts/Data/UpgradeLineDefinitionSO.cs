using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One tier of an <see cref="UpgradeLineDefinitionSO"/> (Task 29). Carries the player-facing
    /// description plus the tier's effect payload. The effect REUSES the existing
    /// <see cref="UpgradeDefinitionSO"/> (its numeric <c>UpgradeStatModifier</c>s + <c>StatusEffectType</c>
    /// + behaviour flags) — so a tier is just a pre-migration upgrade scoped into a line, and picking it
    /// feeds the run's <c>UpgradeInventory</c> exactly like an old upgrade pick (no new resolution path).
    /// </summary>
    [Serializable]
    public sealed class UpgradeLineTier
    {
        [SerializeField, TextArea] private string _description;
        [Tooltip("Effect applied when this tier is reached. Reuses the existing UpgradeDefinitionSO " +
                 "numeric-modifier + status pattern; added to UpgradeInventory on pick so AbilityRuntime " +
                 "resolves it unchanged.")]
        [SerializeField] private UpgradeDefinitionSO _effect;

        public string Description => _description;
        public UpgradeDefinitionSO Effect => _effect;
    }

    /// <summary>
    /// A single per-skill upgrade LINE (Task 29) — the structured replacement for the §3.8 tag-based
    /// shared/hero-exclusive upgrade pools. A line belongs to one hero and one of its skills
    /// (<see cref="AbilityRole.Basic"/>/<see cref="AbilityRole.Ultimate"/>) and holds up to three tiers of
    /// increasing effect. Lines progress independently and in parallel; <c>HeroRuntime</c> owns the live
    /// per-line tier (0 = not yet picked, 1–3 = current tier). When enough of a hero's lines reach their
    /// max tier, an <see cref="ApexTalentDefinitionSO"/> can unlock. Read-only at runtime (CLAUDE.md §3.5).
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeLine", menuName = "Wavekeep/Upgrade Line")]
    public sealed class UpgradeLineDefinitionSO : ScriptableObject
    {
        /// <summary>Design cap on tiers per line (Task 29: three tiers).</summary>
        public const int MaxTier = 3;

        [Tooltip("The hero this line belongs to.")]
        [SerializeField] private HeroDefinitionSO _hero;
        [Tooltip("Which of the hero's skills this line upgrades. The matching ability is the hero's " +
                 "Basic/Ultimate AbilityDefinitionSO; the per-tier effect targets it via the existing " +
                 "role-scoped UpgradeStatModifier targets.")]
        [SerializeField] private AbilityRole _skill = AbilityRole.Basic;
        [SerializeField] private string _lineName;
        [Tooltip("Ordered tiers (index 0 = Tier 1). Up to MaxTier are used.")]
        [SerializeField] private List<UpgradeLineTier> _tiers = new List<UpgradeLineTier>();

        public HeroDefinitionSO Hero => _hero;
        public AbilityRole Skill => _skill;
        public string LineName => _lineName;
        public IReadOnlyList<UpgradeLineTier> Tiers => _tiers;

        /// <summary>Number of usable tiers (authored count, capped at <see cref="MaxTier"/>).</summary>
        public int TierCount => Mathf.Min(MaxTier, _tiers != null ? _tiers.Count : 0);

        /// <summary>The tier entry for a 1-based tier number, or null if out of range.</summary>
        public UpgradeLineTier TierAt(int tier)
        {
            int index = tier - 1;
            if (_tiers == null || index < 0 || index >= _tiers.Count) return null;
            return _tiers[index];
        }
    }
}
