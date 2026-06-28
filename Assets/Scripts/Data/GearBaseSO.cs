using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Read-only template for a gear "base" — one archetype per equip slot (Task 67, gear redesign part 1).
    /// Defines the slot, the single LIVE stat the slot implicitly boosts (the same stat at every rarity), and
    /// how that implicit value scales per rarity tier. Rarity-driven random AFFIXES are layered on top at
    /// generation time (a later task); this template carries only the implicit. Never mutated at runtime
    /// (CLAUDE.md §3.5) — a per-item rolled instance lives in <c>GearInstance</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "GearBase", menuName = "Wavekeep/Gear/Gear Base")]
    public sealed class GearBaseSO : ScriptableObject
    {
        [Tooltip("Stable id used by saves to resolve this base. Never change once shipped.")]
        [SerializeField] private string _baseId;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private GearSlot _slot = GearSlot.Helmet;

        [Header("Implicit (slot-fixed, scales by rarity)")]
        [Tooltip("Which live stat this slot implicitly boosts (same type at every rarity).")]
        [SerializeField] private GearStatType _implicitStat = GearStatType.DamageMultiplier;
        [Tooltip("Implicit magnitude per rarity tier, indexed by Rarity (Common..Unique). For multiplier stats " +
                 "this is the multiplier (1.2 = +20%); for flat/Luck it is the added amount.")]
        [SerializeField] private float[] _implicitValueByRarity = new float[6];

        public string BaseId => _baseId;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public GearSlot Slot => _slot;
        public GearStatType ImplicitStat => _implicitStat;

        /// <summary>Implicit magnitude for a given rarity (clamped to the authored array; 0 if unauthored).</summary>
        public float ImplicitValue(Rarity rarity)
        {
            if (_implicitValueByRarity == null || _implicitValueByRarity.Length == 0) return 0f;
            int i = Mathf.Clamp((int)rarity, 0, _implicitValueByRarity.Length - 1);
            return _implicitValueByRarity[i];
        }
    }
}
