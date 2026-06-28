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
        [Tooltip("Inclusive value range a roll draws from (generation is a later task).")]
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
        public float MinValue => _minValue;
        public float MaxValue => _maxValue;
        public IReadOnlyList<GearSlot> EligibleSlots => _eligibleSlots;
        public int DrawWeight => _drawWeight;
        public bool HasTag => _hasTag;
        public UpgradeTag Tag => _tag;

        /// <summary>True if this affix may roll on <paramref name="slot"/> (empty eligibility list = all slots).</summary>
        public bool IsEligibleFor(GearSlot slot) =>
            _eligibleSlots == null || _eligibleSlots.Count == 0 || _eligibleSlots.Contains(slot);

        /// <summary>Midpoint of the roll range — used ONLY by the Task 67 debug spawn (real rolling is a later task).</summary>
        public float MidValue => (_minValue + _maxValue) * 0.5f;
    }
}
