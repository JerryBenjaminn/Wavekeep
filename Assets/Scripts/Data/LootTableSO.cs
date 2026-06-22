using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored loot table (Task 13): an overall drop chance plus a list of weighted
    /// <see cref="LootEntry"/>. Read-only at runtime — the rolling behaviour lives in
    /// <c>LootService</c> (CLAUDE.md §3.1: SOs are data, code reads them).
    ///
    /// Rarity range is implicit in the authored entries: a regular-enemy table simply lists only
    /// Common/Uncommon/Rare items, while a boss table can include higher tiers. There is deliberately
    /// NO code-level rarity check (per the task) — restriction is data-driven and designer-tunable.
    /// Adding a new boss tier for a future wave is just a new asset + a WaveConfigSO reference, no code.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "Wavekeep/Loot Table")]
    public sealed class LootTableSO : ScriptableObject
    {
        [Tooltip("Probability (0..1) that killing this enemy drops anything at all. Bosses: ~1 (guaranteed).")]
        [SerializeField, Range(0f, 1f)] private float _dropChance = 0.1f;

        [SerializeField] private List<LootEntry> _entries = new List<LootEntry>();

        public float DropChance => _dropChance;
        public IReadOnlyList<LootEntry> Entries => _entries;

        /// <summary>Sum of all entry weights (for weighted selection). Pure read-only computation.</summary>
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
