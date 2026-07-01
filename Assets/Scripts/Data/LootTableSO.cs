using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored loot table (Task 13; reshaped Task 68): an overall drop chance plus, in the new model,
    /// two INDEPENDENT weighted pools — which <see cref="SlotEntries">slot/base</see> drops and which
    /// <see cref="RarityWeights">rarity</see> it rolls. Read-only at runtime — the generation behaviour lives in
    /// <c>GearGenerator</c>/<c>LootService</c> (CLAUDE.md §3.1: SOs are data, code reads them).
    ///
    /// Task 68 pivot: an entry no longer IS a finished item. A drop now picks a slot (weighted) and rolls a rarity
    /// as a SEPARATE Luck-weighted step, then generates a fresh <c>GearInstance</c> (implicit + rolled affixes).
    /// The boss-exclusive rarity lock is still data-driven — a regular table simply omits Legendary/Unique from
    /// its <see cref="RarityWeights"/>; there is no code-level rarity check.
    ///
    /// The legacy <see cref="Entries"/> list (finished <see cref="LootEntry"/> items) is RETAINED only so existing
    /// authoring tools (Task 13/27/63 setups, the Enemy authoring window) still compile and so the Task 68 setup
    /// can DERIVE the new slot/rarity weights from the already-tuned legacy weights. <b>It is DEAD as a runtime
    /// drop source — generation no longer reads it; flagged for removal once the authoring tools migrate.</b>
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "Wavekeep/Loot Table")]
    public sealed class LootTableSO : ScriptableObject
    {
        [Tooltip("Probability (0..1) that killing this enemy drops anything at all. Bosses: ~1 (guaranteed).")]
        [SerializeField, Range(0f, 1f)] private float _dropChance = 0.1f;

        [Header("Task 68 — generation pools (slot pick + separate Luck-weighted rarity roll)")]
        [Tooltip("Which slots/bases this table can drop, with relative weights (the slot pick). Rarity is rolled " +
                 "separately from RarityWeights below.")]
        [SerializeField] private List<LootSlotEntry> _slotEntries = new List<LootSlotEntry>();
        [Tooltip("Which rarities this table can roll, with relative base weights. Luck reshuffles odds within the " +
                 "listed span. Omitting a tier (e.g. Legendary/Unique) is how the boss-exclusive lock is enforced.")]
        [SerializeField] private List<LootRarityWeight> _rarityWeights = new List<LootRarityWeight>();

        [Header("Legacy (Task 13) — finished-item entries (DEAD as a runtime source, see class summary)")]
        [SerializeField] private List<LootEntry> _entries = new List<LootEntry>();

        public float DropChance => _dropChance;

        /// <summary>Task 68: weighted slot/base pool a drop picks its slot from.</summary>
        public IReadOnlyList<LootSlotEntry> SlotEntries => _slotEntries;

        /// <summary>Task 68: weighted rarity pool a drop rolls its rarity from (Luck-reshuffled by the generator).</summary>
        public IReadOnlyList<LootRarityWeight> RarityWeights => _rarityWeights;

        /// <summary>Legacy finished-item entries (DEAD as a runtime drop source — see class summary).</summary>
        public IReadOnlyList<LootEntry> Entries => _entries;

        /// <summary>Sum of all legacy entry weights (for weighted selection). Pure read-only computation. DEAD.</summary>
        public int TotalWeight
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i] != null && _entries[i].Item != null) total += Mathf.Max(0, _entries[i].Weight);
                }
                return total;
            }
        }
    }
}
