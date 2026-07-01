using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Read-only template for one rollable AFFIX in the shared pool (Task 67). An affix is a "rolled modifier
    /// slot": an <see cref="GearEffect"/> (only the stat-modifier kind implemented now), a value RANGE the roll
    /// picks from, which slots it is eligible for, and a draw weight (used by generation in a later task — the
    /// field exists now). May optionally carry one existing <see cref="UpgradeTag"/> so a FUTURE task can let a
    /// hero's <c>TagInteractionRule</c> resolution trigger from equipped affixes (CLAUDE.md §3.8); that wiring is
    /// not done this task. Never mutated at runtime (CLAUDE.md §3.5) — a rolled instance lives in <c>RolledAffix</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Affix", menuName = "Wavekeep/Gear/Affix Definition")]
    public sealed class AffixDefinitionSO : ScriptableObject
    {
        [Tooltip("Stable id used by saves to resolve this affix. Never change once shipped.")]
        [SerializeField] private string _affixId;
        [SerializeField] private string _displayName;

        [Header("Effect (extensible; stat-modifier kind only for now)")]
        [SerializeField]
        private GearEffect _effect = new GearEffect(GearEffectKind.StatModifier, GearStatType.DamageFlatBonus);

        [Header("Per-rarity roll ranges (Task 76) — index 0=Common .. 4=Legendary")]
        [Tooltip("Inclusive value range a roll draws from, PER rarity tier. Adjacent tiers must NOT overlap so a " +
                 "higher rarity is always strictly better (validated by the Task 76 setup). Unique is EXEMPT — its " +
                 "affixes are hand-authored fixed values, never rolled from these ranges. Designer-tunable.")]
        [SerializeField] private AffixRarityRange[] _rarityRanges = new AffixRarityRange[5];

        [Header("Legacy flat range (Task 67 — kept only as a fallback if per-rarity ranges are unauthored)")]
        [Tooltip("Superseded by _rarityRanges in Task 76. Used only when a rarity's range is missing/zero, to avoid " +
                 "a hard break; author the per-rarity ranges via 'Setup Task 76'.")]
        [SerializeField] private float _minValue;
        [SerializeField] private float _maxValue;

        [Header("Generation (fields used by a later task)")]
        [Tooltip("Slots this affix can roll on. EMPTY = eligible for all slots.")]
        [SerializeField] private List<GearSlot> _eligibleSlots = new List<GearSlot>();
        [Tooltip("Relative draw weight in the shared pool.")]
        [SerializeField, Min(0)] private int _drawWeight = 1;

        [Header("Tag (optional — future TagInteractionRule hook)")]
        [Tooltip("If set, this affix carries the tag below (a future task can feed it into tag interactions).")]
        [SerializeField] private bool _hasTag;
        [SerializeField] private UpgradeTag _tag;

        public string AffixId => _affixId;
        public string DisplayName => _displayName;
        public GearEffect Effect => _effect;
        public IReadOnlyList<GearSlot> EligibleSlots => _eligibleSlots;
        public int DrawWeight => _drawWeight;
        public bool HasTag => _hasTag;
        public UpgradeTag Tag => _tag;

        /// <summary>True if this affix may roll on <paramref name="slot"/> (empty eligibility list = all slots).</summary>
        public bool IsEligibleFor(GearSlot slot) =>
            _eligibleSlots == null || _eligibleSlots.Count == 0 || _eligibleSlots.Contains(slot);

        // --- Task 76: per-rarity roll ranges --------------------------------------------------------

        /// <summary>Inclusive minimum this affix can roll at <paramref name="rarity"/> (falls back to the legacy flat
        /// range if the per-rarity range is unauthored). Unique never rolls from ranges (hand-authored).</summary>
        public float MinValueFor(Rarity rarity) => RangeFor(rarity, out float min, out _) ? min : _minValue;

        /// <summary>Inclusive maximum this affix can roll at <paramref name="rarity"/> (legacy-flat fallback).</summary>
        public float MaxValueFor(Rarity rarity) => RangeFor(rarity, out _, out float max) ? max : _maxValue;

        /// <summary>Midpoint of the rarity's range — used ONLY by the Task 67 debug spawn.</summary>
        public float MidValueFor(Rarity rarity) => (MinValueFor(rarity) + MaxValueFor(rarity)) * 0.5f;

        /// <summary>True if a designer-authored (non-zero) per-rarity range exists for <paramref name="rarity"/>.</summary>
        public bool HasRangeFor(Rarity rarity) => RangeFor(rarity, out _, out _);

        // Look up the authored range for a rarity. Returns false (so callers fall back) when no range array exists
        // or the entry is an unauthored (0,0) sentinel. Unique clamps to the last entry but callers never roll it.
        private bool RangeFor(Rarity rarity, out float min, out float max)
        {
            min = 0f; max = 0f;
            if (_rarityRanges == null || _rarityRanges.Length == 0) return false;
            int i = Mathf.Clamp((int)rarity, 0, _rarityRanges.Length - 1);
            var r = _rarityRanges[i];
            if (r.max == 0f && r.min == 0f) return false; // treat empty entry as "not set"
            min = r.min; max = r.max;
            return true;
        }
    }
}
