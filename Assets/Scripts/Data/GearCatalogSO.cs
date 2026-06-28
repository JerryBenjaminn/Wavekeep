using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Master registry the gear persistence layer resolves saved ids against (Task 12, extended Task 67). Saves
    /// store stable string ids (not asset references), so on load each id is resolved back to its template here.
    /// A single authored asset wired into <c>GameSessionBootstrap</c>, so it works in builds without
    /// Resources/AssetDatabase lookups. Read-only at runtime.
    ///
    /// Task 67: now also registers the new templates a <c>GearInstance</c> resolves — <see cref="GearBaseSO"/>
    /// (by baseId / by slot) and <see cref="AffixDefinitionSO"/> (by affixId). The legacy <see cref="Items"/>
    /// list (finished <see cref="LootItemSO"/> assets) is RETAINED only so old authoring/loot tables still
    /// reference it; instance ownership no longer uses it. <b>DEAD as an ownership source — flagged for removal
    /// once drop generation (a later task) replaces the legacy drop path.</b>
    /// </summary>
    [CreateAssetMenu(fileName = "GearCatalog", menuName = "Wavekeep/Gear Catalog")]
    public sealed class GearCatalogSO : ScriptableObject
    {
        [Header("Task 67 — instance templates")]
        [Tooltip("Gear bases (one per slot archetype), resolved by a saved instance's baseId.")]
        [SerializeField] private List<GearBaseSO> _bases = new List<GearBaseSO>();
        [Tooltip("Affix definitions, resolved by a saved rolled affix's affixId.")]
        [SerializeField] private List<AffixDefinitionSO> _affixes = new List<AffixDefinitionSO>();

        [Header("Legacy (Task 12) — finished items (DEAD as an ownership source, see class summary)")]
        [SerializeField] private List<LootItemSO> _items = new List<LootItemSO>();

        public IReadOnlyList<GearBaseSO> Bases => _bases;
        public IReadOnlyList<AffixDefinitionSO> Affixes => _affixes;
        public IReadOnlyList<LootItemSO> Items => _items;

        private Dictionary<string, GearBaseSO> _basesById;
        private Dictionary<string, AffixDefinitionSO> _affixesById;
        private Dictionary<string, LootItemSO> _itemsById;

        /// <summary>Resolve a base id to its template, or null.</summary>
        public GearBaseSO FindBase(string baseId)
        {
            if (string.IsNullOrEmpty(baseId)) return null;
            EnsureBaseIndex();
            return _basesById.TryGetValue(baseId, out var b) ? b : null;
        }

        /// <summary>First registered base for a slot (used by the temporary legacy-drop bridge), or null.</summary>
        public GearBaseSO FindBaseForSlot(GearSlot slot)
        {
            for (int i = 0; i < _bases.Count; i++)
                if (_bases[i] != null && _bases[i].Slot == slot) return _bases[i];
            return null;
        }

        /// <summary>Resolve an affix id to its template, or null.</summary>
        public AffixDefinitionSO FindAffix(string affixId)
        {
            if (string.IsNullOrEmpty(affixId)) return null;
            EnsureAffixIndex();
            return _affixesById.TryGetValue(affixId, out var a) ? a : null;
        }

        /// <summary>Legacy resolve of a finished-item id to its SO, or null (DEAD path — see class summary).</summary>
        public LootItemSO Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_itemsById == null)
            {
                _itemsById = new Dictionary<string, LootItemSO>();
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                    _itemsById[item.ItemId] = item;
                }
            }
            return _itemsById.TryGetValue(itemId, out var found) ? found : null;
        }

        private void EnsureBaseIndex()
        {
            if (_basesById != null) return;
            _basesById = new Dictionary<string, GearBaseSO>();
            for (int i = 0; i < _bases.Count; i++)
            {
                var b = _bases[i];
                if (b == null || string.IsNullOrEmpty(b.BaseId)) continue;
                _basesById[b.BaseId] = b; // last wins on duplicate ids (authoring error)
            }
        }

        private void EnsureAffixIndex()
        {
            if (_affixesById != null) return;
            _affixesById = new Dictionary<string, AffixDefinitionSO>();
            for (int i = 0; i < _affixes.Count; i++)
            {
                var a = _affixes[i];
                if (a == null || string.IsNullOrEmpty(a.AffixId)) continue;
                _affixesById[a.AffixId] = a;
            }
        }
    }
}
